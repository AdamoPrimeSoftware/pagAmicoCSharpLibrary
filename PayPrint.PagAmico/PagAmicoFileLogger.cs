using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace PayPrint.PagAmico;

/// <summary>
/// Registra su file tutto il traffico con il pagAmico: comandi trasmessi, risposte ricevute
/// ed eventi di connessione. Un file al giorno, in append, apribile mentre viene scritto.
/// <para>
/// Serve sia in sviluppo (rileggere una sessione, allegare un tracciato a una segnalazione)
/// sia in esercizio: quando una cassa contesta un incasso, il tracciato e' l'unica prova
/// di che cosa e' stato chiesto alla macchina e che cosa ha risposto.
/// </para>
/// </summary>
public sealed class PagAmicoFileLogger : IDisposable
{
    private readonly object _gate = new();

    /// <summary>
    /// Serializza la scrittura fra processi diversi che puntano allo stesso file: due banchi di
    /// prova, un banco e il collaudo, l'applicazione e uno strumento di servizio. Senza, meta'
    /// delle righe sparisce senza errori, perche' con <see cref="FileMode.Append"/> la posizione
    /// di scrittura viene fissata all'apertura e ogni processo sovrascrive quanto ha appena
    /// aggiunto l'altro. Per un file che deve valere come prova di un incasso e' inaccettabile.
    /// </summary>
    private Mutex? _crossProcess;
    private string? _crossProcessPath;   // file a cui si riferisce il nome del mutex
    private readonly string _directory;
    private readonly string _prefix;

    private FileStream? _stream;
    private StreamWriter? _writer;
    private DateTime _openDate = DateTime.MinValue;
    private PagAmicoClient? _client;
    private bool _disposed;

    /// <summary>Cartella di default: %LOCALAPPDATA%\PayPrint.PagAmico\logs</summary>
    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PayPrint.PagAmico", "logs");

    public PagAmicoFileLogger(string? directory = null, string prefix = "pagamico")
    {
        _directory = string.IsNullOrWhiteSpace(directory) ? DefaultDirectory : directory!;
        _prefix = string.IsNullOrWhiteSpace(prefix) ? "pagamico" : prefix;
        Directory.CreateDirectory(_directory);
    }

    /// <summary>Cartella in cui vengono scritti i file di log.</summary>
    public string LogDirectory => _directory;

    /// <summary>File attualmente in scrittura (cambia a mezzanotte).</summary>
    public string CurrentFilePath => Path.Combine(_directory, $"{_prefix}-{Clock():yyyy-MM-dd}.log");

    /// <summary>Orologio del logger: sostituibile solo dai test, per provare il cambio di giorno.</summary>
    internal Func<DateTime> Clock { get; set; } = () => DateTime.Now;

    /// <summary>Se false le righe vengono scartate senza toccare il file.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Massima lunghezza di una riga registrata; oltre viene troncata. 0 = nessun limite.</summary>
    public int MaxLineLength { get; set; } = 0;

    /// <summary>Invocata dopo ogni riga scritta, con la riga formattata (utile per una finestra di log).</summary>
    public event EventHandler<string>? LineWritten;

    /// <summary>Aggancia il logger al client: da qui in poi registra TX, RX e disconnessioni.</summary>
    public void Attach(PagAmicoClient client)
    {
        Detach();
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _client.CommandSent += OnCommandSent;
        _client.FrameReceived += OnFrameReceived;
        _client.Disconnected += OnDisconnected;
        Write("--", $"logger agganciato a {client.Host}:{client.Port}");
    }

    public void Detach()
    {
        if (_client is null) return;
        _client.CommandSent -= OnCommandSent;
        _client.FrameReceived -= OnFrameReceived;
        _client.Disconnected -= OnDisconnected;
        _client = null;
    }

    private void OnCommandSent(object? sender, string command) => Write("TX", command);

    private void OnFrameReceived(object? sender, PagAmicoFrame frame) => Write("RX", frame.Raw);

    private void OnDisconnected(object? sender, Exception? ex) =>
        Write("--", ex is null ? "disconnesso (chiusura richiesta)" : $"disconnesso: {ex.Message}");

    /// <summary>Registra una riga arbitraria (esito di un comando, nota applicativa, errore).</summary>
    public void Write(string kind, string? text)
    {
        if (!Enabled || _disposed) return;

        var payload = Sanitize(text);
        var line = FormattableString.Invariant(
            $"{Clock():yyyy-MM-dd HH:mm:ss.fff}  {kind,-2}  {payload}");

        lock (_gate)
        {
            var held = Acquire();
            try
            {
                EnsureWriter();
                // La posizione del nostro stream e' ferma a dove eravamo arrivati noi: se nel
                // frattempo ha scritto un altro processo, il file e' piu' lungo.
                _stream!.Seek(0, SeekOrigin.End);
                _writer!.WriteLine(line);
            }
            catch (IOException)
            {
                // un log che non riesce a scrivere non deve mai fermare l'incasso in corso
            }
            finally
            {
                if (held) Release();
            }
        }

        LineWritten?.Invoke(this, line);
    }

    private string Sanitize(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var sb = new StringBuilder(text!.Length);
        foreach (var c in text)
        {
            // i caratteri di controllo del messaggio POS renderebbero illeggibile il file
            if (c == '\r' || c == '\n') { sb.Append(' '); continue; }
            if (char.IsControl(c)) { sb.Append($"<{(int)c:X2}>"); continue; }
            sb.Append(c);
        }

        var line = sb.ToString();
        if (MaxLineLength > 0 && line.Length > MaxLineLength)
            line = line.Substring(0, MaxLineLength) + $"... (+{line.Length - MaxLineLength} caratteri)";
        return line;
    }

    /// <summary>
    /// Prende il mutex del file, creandolo alla prima riga. Se il sistema non supporta i mutex
    /// con nome si scrive lo stesso: meglio un log con qualche riga a rischio che nessun log.
    /// </summary>
    private bool Acquire()
    {
        // il mutex prende il nome dal file del giorno, e il file cambia a mezzanotte: un nome calcolato una volta
        // sola lascerebbe un processo avviato ieri e uno avviato oggi con due mutex diversi sullo stesso file
        var path = CurrentFilePath;
        if (_crossProcess is null || _crossProcessPath != path)
        {
            try
            {
                _crossProcess?.Dispose();
                _crossProcess = null;
                // il nome non puo' contenere separatori di percorso: si usa un'impronta del path.
                // Non string.GetHashCode(): in .NET Core e' randomizzato a ogni avvio, quindi
                // due processi otterrebbero due mutex diversi e non si escluderebbero affatto.
                _crossProcess = new Mutex(false, @"Local\PayPrint.PagAmico.log." + Fingerprint(path));
                _crossProcessPath = path;
            }
            catch (Exception)
            {
                return false;
            }
        }

        try
        {
            // se un processo muore tenendo il mutex, WaitOne lancia AbandonedMutexException
            // ma il mutex risulta comunque acquisito: va rilasciato lo stesso.
            return _crossProcess.WaitOne(2000);
        }
        catch (AbandonedMutexException)
        {
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Impronta stabile fra processi e fra avvii (FNV-1a a 32 bit), per dare al mutex un nome
    /// che dipenda solo dal percorso del file.
    /// </summary>
    private static string Fingerprint(string path)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var c in path.ToUpperInvariant())
            {
                hash ^= c;
                hash *= 16777619u;
            }
            return hash.ToString("X8", CultureInfo.InvariantCulture);
        }
    }

    private void Release()
    {
        try { _crossProcess?.ReleaseMutex(); }
        catch (ApplicationException) { /* non lo tenevamo noi */ }
    }

    private void EnsureWriter()
    {
        var today = Clock().Date;
        if (_writer is not null && _openDate == today) return;

        _writer?.Flush();
        _writer?.Dispose();

        Directory.CreateDirectory(_directory);
        _stream = new FileStream(CurrentFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        _writer = new StreamWriter(_stream, new UTF8Encoding(false)) { AutoFlush = true };
        _openDate = today;
    }

    public void Dispose()
    {
        if (_disposed) return;
        Detach();
        lock (_gate)
        {
            _disposed = true;
            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;
            _stream = null;
            _crossProcess?.Dispose();
            _crossProcess = null;
        }
    }
}
