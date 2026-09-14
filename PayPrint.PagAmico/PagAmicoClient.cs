using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PayPrint.PagAmico;

/// <summary>Layout del pacchetto binario per l'invio di immagini (SF / SI).</summary>
public enum ImagePacketLayout
{
    /// <summary>Formato descritto a testo nel manuale: "SF" + FF FF + png + FE FE + "||".</summary>
    Documented,

    /// <summary>Formato dell'esempio Python del manuale: "SF" + FF FF + "||" + png + "||" + FE FE.</summary>
    PythonSample
}

/// <summary>
/// Client TCP-IP per cassa rendiresto PayPrint pagAmico (protocollo rev. 2.33, FW 8.72).
/// <para>
/// Il pagAmico e' il server: aprire UNA connessione e tenerla aperta.
/// Il protocollo e' asincrono e non e' request/response puro: ad un comando possono seguire
/// piu' messaggi ({"response":"OK"} di accettazione, {"response":"p"} parziali, quindi il messaggio finale).
/// </para>
/// </summary>
public sealed class PagAmicoClient : IDisposable
{
    /// <summary>Porta di default. Il manuale consiglia di cambiarla (es. 43775) perche' la 9100 e' molto usata.</summary>
    public const int DefaultPort = 9100;

    private static readonly Regex ButtonRegex = new(@"^BT([123])$", RegexOptions.Compiled);

    private readonly PagAmicoFrameParser _parser = new();
    private readonly List<Waiter> _waiters = new();
    private readonly object _gate = new();
    private readonly SemaphoreSlim _commandLock = new(1, 1);
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private DateTime _lastSendUtc = DateTime.MinValue;
    private Collection? _collection;   // incasso aperto, protetto da _gate
    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private Task? _receiveLoop;

    public PagAmicoClient(string host, int port = DefaultPort)
    {
        Host = host ?? throw new ArgumentNullException(nameof(host));
        Port = port;
    }

    public string Host { get; }

    public int Port { get; }

    /// <summary>Codifica usata per comandi e risposte. Il FW 8.72 gestisce accentate ed euro.</summary>
    public Encoding Encoding { get; set; } = new UTF8Encoding(false);

    /// <summary>
    /// Terminatore accodato ad ogni comando. Il manuale 2.33 non lo documenta, ma PayPrint
    /// (risposta dell'11 settembre 2026, domande 1.1 e 1.2) indica CR o CR+LF per la macchina.
    /// Il simulatore del Dev Kit lo accetta: collaudo completo 64/64 con CR (14 settembre 2026).
    /// Vuoto = nessun terminatore, come Hercules.
    /// </summary>
    public string CommandTerminator { get; set; } = "\r";

    /// <summary>Timeout di default per i comandi "brevi".</summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Timeout di default per incasso / erogazione (operazioni con intervento utente).</summary>
    public TimeSpan TransactionTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Intervallo di polling del loop di ricezione, determina la latenza di chiusura dei frame testuali.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Distanza minima fra due invii consecutivi.
    /// <para>
    /// Nata perche' una versione precedente del simulatore leggeva il buffer del socket come UN
    /// solo comando: due comandi senza terminatore nello stesso segmento TCP, il secondo si perdeva.
    /// Il simulatore attuale regge le raffiche con e senza terminatore (collaudo 64/64 a 0 ms e
    /// prova con piu' comandi in un segmento, 14 settembre 2026), e PayPrint dice che con CR la
    /// pausa non serve. Il default resta 80 ms come rete di sicurezza finche' non e' verificato
    /// sulla macchina reale.
    /// </para>
    /// </summary>
    public TimeSpan MinimumCommandInterval { get; set; } = TimeSpan.FromMilliseconds(80);

    /// <summary>
    /// Silenzio dopo il quale parte la prima sonda keepalive TCP. Durante un incasso non si puo'
    /// mandare [ST]: il keepalive e' l'unico modo di accorgersi che la macchina non e' piu' raggiungibile.
    /// Il default di sistema e' 2 ore. Si applica alla connessione successiva.
    /// </summary>
    public TimeSpan KeepAliveTime { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Distanza fra due sonde keepalive senza risposta. Si applica alla connessione successiva.</summary>
    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Sonde senza risposta prima di dichiarare caduta la connessione. Applicato solo su net8.0:
    /// con netstandard2.0 / net47 Windows ne usa sempre 10. Si applica alla connessione successiva.
    /// </summary>
    public int KeepAliveRetryCount { get; set; } = 5;

    public ImagePacketLayout ImageLayout { get; set; } = ImagePacketLayout.Documented;

    public bool IsConnected => _tcp?.Connected == true && _stream is not null;

    /// <summary>
    /// Vero dall'invio di un incasso ([IN], [PO], [IM], [I2]) al suo esito. Finche' e' vero la macchina accetta
    /// solo [AN] e [CM]: ogni altro invio lancia <see cref="PagAmicoCollectionOpenException"/> senza trasmettere nulla.
    /// </summary>
    public bool IsCollecting
    {
        get { lock (_gate) return _collection is not null; }
    }

    /// <summary>Ogni messaggio ricevuto, anche quello gia' consegnato ad un'attesa.</summary>
    public event EventHandler<PagAmicoFrame>? FrameReceived;

    /// <summary>
    /// Messaggio che nessuna attesa riconosce come proprio: arrivato dopo un timeout o una caduta, la risposta a
    /// qualcos'altro durante un incasso (un BUSY), l'[AN] di una chiusura forzata dal pannello.
    /// <b>Puo' portare importi</b>: chi integra lo deve ascoltare e salvare.
    /// Arriva dal thread di ricezione.
    /// </summary>
    public event EventHandler<PagAmicoFrame>? OrphanFrame;

    /// <summary>Ogni comando trasmesso (utile per log e pannelli di traffico).</summary>
    public event EventHandler<string>? CommandSent;

    /// <summary>
    /// Diagnostica interna della libreria: connessione, pause imposte fra un invio e l'altro,
    /// separazione dei messaggi, attese soddisfatte o scadute. Serve per capire <i>perche'</i>
    /// un comando non ha avuto risposta, cosa che il solo traffico TX/RX non mostra.
    /// </summary>
    public Action<string>? Trace { get; set; }

    private void T(string message) => Trace?.Invoke(message);

    /// <summary>Connessione caduta o loop di ricezione terminato.</summary>
    public event EventHandler<Exception?>? Disconnected;

    // ------------------------------------------------------------------ connessione

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        Disconnect();

        var tcp = new TcpClient { NoDelay = true };
        try
        {
            // l'overload con CancellationToken esiste solo da .NET 5: chiudere il socket
            // e' il modo portabile di interrompere una connessione in corso
            var connect = tcp.ConnectAsync(Host, Port);
            using (ct.Register(() => tcp.Close()))
                await connect.ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
        }
        catch
        {
            tcp.Dispose();
            throw;
        }

        ConfigureKeepAlive(tcp.Client);
        _tcp = tcp;
        _stream = tcp.GetStream();
        _parser.Clear();
        _cts = new CancellationTokenSource();
        _receiveLoop = Task.Run(() => ReceiveLoopAsync(_cts.Token));

        T($"connesso a {Host}:{Port} (pausa minima fra invii {MinimumCommandInterval.TotalMilliseconds:0} ms, " +
          $"terminatore {(CommandTerminator.Length == 0 ? "nessuno" : "presente")})");
    }

    private void ConfigureKeepAlive(Socket socket)
    {
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        var time = Math.Max(1, (int)KeepAliveTime.TotalSeconds);
        var interval = Math.Max(1, (int)KeepAliveInterval.TotalSeconds);
        try
        {
#if NET8_0_OR_GREATER
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, time);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, interval);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, Math.Max(1, KeepAliveRetryCount));
            T($"keepalive TCP: prima sonda dopo {time} s, poi ogni {interval} s, caduta dopo {KeepAliveRetryCount} sonde senza risposta");
#else
            // SIO_KEEPALIVE_VALS: acceso, tempo e intervallo in millisecondi. Solo Windows.
            var values = new byte[12];
            BitConverter.GetBytes(1u).CopyTo(values, 0);
            BitConverter.GetBytes((uint)time * 1000).CopyTo(values, 4);
            BitConverter.GetBytes((uint)interval * 1000).CopyTo(values, 8);
            socket.IOControl(IOControlCode.KeepAliveValues, values, null);
            T($"keepalive TCP: prima sonda dopo {time} s, poi ogni {interval} s (sonde prima della caduta: default di Windows)");
#endif
        }
        catch (Exception ex) when (ex is SocketException or PlatformNotSupportedException or NotSupportedException)
        {
            T($"keepalive TCP con i valori di sistema: regolazione non supportata ({ex.Message})");
        }
    }

    public void Disconnect()
    {
        try { _cts?.Cancel(); } catch { /* ignorato */ }

        try { _stream?.Dispose(); } catch { /* ignorato */ }
        try { _tcp?.Close(); } catch { /* ignorato */ }

        _stream = null;
        _tcp = null;

        FailWaiters("Connessione chiusa");
    }

    /// <summary>Fa fallire tutte le attese in corso: la connessione non c'e' piu'.</summary>
    private void FailWaiters(string reason)
    {
        lock (_gate)
        {
            var mayBeCollecting = _collection?.Accepted == true;
            foreach (var w in _waiters)
                w.Completion.TrySetException(new PagAmicoConnectionLostException(
                    mayBeCollecting ? reason + ": l'incasso era aperto, la macchina potrebbe stare ancora incassando" : reason,
                    mayBeCollecting));
            _waiters.Clear();
        }
    }

    public void Dispose()
    {
        Disconnect();
        _cts?.Dispose();
        _commandLock.Dispose();
        _writeLock.Dispose();
    }

    // ------------------------------------------------------------------ IO di basso livello

    /// <summary>
    /// Invia un comando testuale senza attendere risposta. A incasso aperto lancia
    /// <see cref="PagAmicoCollectionOpenException"/>: per chiudere un incasso usare <see cref="CancelAsync"/> o <see cref="CommitAsync"/>.
    /// </summary>
    public Task SendRawAsync(string command, CancellationToken ct = default) =>
        SendCommandAsync(command, duringCollection: false, ct);

    /// <summary>Invia byte grezzi (usato per l'invio delle immagini SF / SI). A incasso aperto lancia.</summary>
    public Task SendRawAsync(byte[] payload, CancellationToken ct = default) =>
        WriteAsync(payload, "(invio binario)", duringCollection: false, ct);

    private async Task SendCommandAsync(string command, bool duringCollection, CancellationToken ct)
    {
        var payload = Encoding.GetBytes(command + CommandTerminator);
        await WriteAsync(payload, command, duringCollection, ct).ConfigureAwait(false);
        CommandSent?.Invoke(this, command);
    }

    // duringCollection: vero solo per l'[IN] stesso e per l'[AN] / [CM] che lo chiudono
    private async Task WriteAsync(byte[] payload, string what, bool duringCollection, CancellationToken ct)
    {
        var stream = _stream ?? throw new PagAmicoException("Client non connesso: chiamare ConnectAsync()");
        if (!duringCollection) ThrowIfCollecting(what);
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // ricontrollo sotto il lock di scrittura: l'incasso si apre prima che l'[IN] sia scritto,
            // quindi nessun invio laterale puo' finire dopo l'[IN]
            if (!duringCollection) ThrowIfCollecting(what);

            var elapsed = DateTime.UtcNow - _lastSendUtc;
            if (elapsed < MinimumCommandInterval)
            {
                var wait = MinimumCommandInterval - elapsed;
                T($"attesa di {wait.TotalMilliseconds:0} ms prima dell'invio: il pagAmico ignora i comandi troppo ravvicinati");
                await Task.Delay(wait, ct).ConfigureAwait(false);
            }

            await stream.WriteAsync(payload, 0, payload.Length, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
            _lastSendUtc = DateTime.UtcNow;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Invia un comando e attende il messaggio finale identificato da <paramref name="isFinal"/>.
    /// I messaggi intermedi (accettazione, parziali) vengono inoltrati a <paramref name="progress"/>.
    /// </summary>
    public Task<PagAmicoFrame> SendAndWaitAsync(
        string command,
        Func<PagAmicoFrame, bool> isFinal,
        TimeSpan? timeout = null,
        IProgress<PagAmicoFrame>? progress = null,
        CancellationToken ct = default) =>
        SendAndWaitCoreAsync(command, Verdicts(isFinal), progress is null ? null : progress.Report, timeout, ct);

    private async Task<PagAmicoFrame> SendAndWaitCoreAsync(
        string command,
        Func<PagAmicoFrame, Verdict> classify,
        Action<PagAmicoFrame>? onProgress,
        TimeSpan? timeout,
        CancellationToken ct)
    {
        // prima del lock: un comando accodato dietro un incasso aperto aspetterebbe per minuti
        // e partirebbe comunque durante l'incasso, provocando un BUSY
        ThrowIfCollecting(command);
        await _commandLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var waiter = new Waiter(classify, onProgress);
            lock (_gate) _waiters.Add(waiter);
            try
            {
                await SendCommandAsync(command, duringCollection: false, ct).ConfigureAwait(false);
                return await AwaitWaiterTracedAsync(waiter, command, timeout ?? DefaultTimeout, ct).ConfigureAwait(false);
            }
            finally
            {
                lock (_gate) _waiters.Remove(waiter);
            }
        }
        finally
        {
            _commandLock.Release();
        }
    }

    private void ThrowIfCollecting(string command)
    {
        if (!IsCollecting) return;
        T($"'{Shorten(command)}' NON inviato: incasso aperto, la macchina accetta solo AN e CM");
        throw new PagAmicoCollectionOpenException(command);
    }

    /// <summary>Attende un messaggio senza inviare nulla (es. esito di una dialog gia' aperta sul display).</summary>
    public async Task<PagAmicoFrame> WaitForAsync(
        Func<PagAmicoFrame, bool> isFinal,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        var waiter = new Waiter(Verdicts(isFinal), null);
        lock (_gate) _waiters.Add(waiter);
        try
        {
            return await AwaitWaiterTracedAsync(waiter, "(attesa senza invio)", timeout ?? DefaultTimeout, ct).ConfigureAwait(false);
        }
        finally
        {
            lock (_gate) _waiters.Remove(waiter);
        }
    }

    private static async Task<PagAmicoFrame> AwaitWaiterAsync(Waiter waiter, string command, TimeSpan timeout, CancellationToken ct)
    {
        using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var completed = await Task.WhenAny(waiter.Completion.Task, Task.Delay(timeout, delayCts.Token)).ConfigureAwait(false);
        delayCts.Cancel();

        if (completed != waiter.Completion.Task)
        {
            ct.ThrowIfCancellationRequested();
            throw new PagAmicoTimeoutException($"Nessuna risposta dal pagAmico entro {timeout.TotalSeconds:0.#}s per il comando '{command}'");
        }
        return await waiter.Completion.Task.ConfigureAwait(false);
    }

    private async Task<PagAmicoFrame> AwaitWaiterTracedAsync(Waiter waiter, string command, TimeSpan timeout, CancellationToken ct)
    {
        T($"in attesa dell'esito di '{command}' (timeout {timeout.TotalSeconds:0.#}s)");
        try
        {
            var frame = await AwaitWaiterAsync(waiter, command, timeout, ct).ConfigureAwait(false);
            T($"attesa di '{command}' soddisfatta da: {Shorten(frame.Raw)}");
            return frame;
        }
        catch (PagAmicoTimeoutException)
        {
            T($"attesa di '{command}' SCADUTA dopo {timeout.TotalSeconds:0.#}s senza alcun messaggio utile");
            throw;
        }
    }

    private static string Shorten(string text, int max = 90) =>
        text.Length <= max ? text : text.Substring(0, max) + "...";

    // ------------------------------------------------------------------ loop di ricezione

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var stream = _stream!;
        var decoder = Encoding.GetDecoder();
        var bytes = new byte[16 * 1024];
        var chars = new char[16 * 1024];
        Exception? fault = null;
        Task<int>? pendingRead = null;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                pendingRead ??= stream.ReadAsync(bytes, 0, bytes.Length, ct);
                var finished = await Task.WhenAny(pendingRead, Task.Delay(PollInterval, ct)).ConfigureAwait(false);

                if (finished == pendingRead)
                {
                    var read = await pendingRead.ConfigureAwait(false);
                    pendingRead = null;
                    if (read <= 0) throw new IOException("Connessione chiusa dal pagAmico");

                    var count = decoder.GetChars(bytes, 0, read, chars, 0);
                    T($"letti {read} byte dal socket");
                    _parser.Append(new string(chars, 0, count));
                    DrainFrames(idle: false);
                    if (_parser.BufferedLength > 0)
                        T($"nel buffer restano {_parser.BufferedLength} caratteri in attesa del resto del messaggio");
                }
                else
                {
                    DrainFrames(idle: true);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // chiusura richiesta
        }
        catch (Exception ex)
        {
            fault = ex;
        }
        finally
        {
            FailWaiters(fault?.Message ?? "Connessione chiusa");
            Disconnected?.Invoke(this, fault);
        }
    }

    private void DrainFrames(bool idle)
    {
        while (_parser.TryReadFrame(idle, out var frame))
        {
            if (frame.Raw.Length == 0) continue;
            T($"messaggio separato: {(frame.IsJson ? "JSON" : "testo")}, {frame.Raw.Length} caratteri" +
              (idle ? " (chiuso dal silenzio)" : ""));
            Dispatch(frame);
        }
    }

    private void Dispatch(PagAmicoFrame frame)
    {
        Waiter[] snapshot;
        lock (_gate) snapshot = _waiters.ToArray();

        var claimed = false;
        foreach (var w in snapshot)
        {
            Verdict verdict;
            try { verdict = w.Classify(frame); }
            catch { verdict = Verdict.Foreign; }

            if (verdict == Verdict.Final)
            {
                claimed = true;
                lock (_gate) _waiters.Remove(w);
                w.Completion.TrySetResult(frame);
            }
            else if (verdict == Verdict.Progress)
            {
                claimed = true;
                // chiamata sincrona dal thread di ricezione: i parziali arrivano nell'ordine in cui la macchina li manda
                try { w.OnProgress?.Invoke(frame); }
                catch (Exception ex) { T($"eccezione nel gestore di avanzamento, ignorata: {ex.Message}"); }
            }
        }

        FrameReceived?.Invoke(this, frame);

        if (!claimed)
        {
            T(snapshot.Length == 0
                ? $"messaggio non atteso da nessuno (nessun comando in corso): {Shorten(frame.Raw)}"
                : $"messaggio estraneo alle attese in corso: {Shorten(frame.Raw)}");
            OrphanFrame?.Invoke(this, frame);
        }
    }

    // ------------------------------------------------------------------ predicati

    /// <summary>Come un'attesa giudica un messaggio.</summary>
    private enum Verdict
    {
        /// <summary>Non e' suo: se nessun'altra attesa lo riconosce, e' un frame orfano.</summary>
        Foreign,

        /// <summary>Avanzamento (accettazione, parziale): l'attesa continua.</summary>
        Progress,

        /// <summary>Esito: chiude l'attesa.</summary>
        Final
    }

    /// <summary>Predicato booleano delle API pubbliche: vero chiude, falso e' avanzamento.</summary>
    private static Func<PagAmicoFrame, Verdict> Verdicts(Func<PagAmicoFrame, bool> isFinal) =>
        f => isFinal(f) ? Verdict.Final : Verdict.Progress;

    /// <summary>
    /// Predicato dei comandi semplici. Sul simulatore ogni comando riceve il proprio codice (ST, SM, SB, PL...)
    /// oppure OK, e un errore come testo o ER (collaudo dell'11/09/2026). Il resto non e' suo: sul simulatore un CM
    /// arrivato in ritardo ha fatto da risposta a un ST. Unica eccezione [LO], che rimanda l'ultimo JSON qualunque sia.
    /// </summary>
    private static Func<PagAmicoFrame, Verdict> OwnResponse(string command)
    {
        if (command == PagAmicoCommands.LastJson()) return _ => Verdict.Final;
        var code = command.Length >= 2 ? command.Substring(0, 2) : command;
        return f =>
        {
            if (f.IsText) return Verdict.Final;
            var r = f.Response;
            if (r is null) return Verdict.Foreign;
            return r == "OK" ||
                   r.Equals("ER", StringComparison.OrdinalIgnoreCase) ||
                   r.Equals(code, StringComparison.OrdinalIgnoreCase)
                ? Verdict.Final
                : Verdict.Foreign;
        };
    }

    private static Func<PagAmicoFrame, bool> Terminal(params string[] finalResponses) => f =>
    {
        if (f.IsText) return true;                      // "CMD ERROR", "ER ...", ecc.
        var r = f.Response;
        if (r is null) return false;
        if (r == "OK" || r == "p") return false;        // accettazione / parziale
        return finalResponses.Length == 0 || finalResponses.Contains(r, StringComparer.OrdinalIgnoreCase) ||
               r.Equals("ER", StringComparison.OrdinalIgnoreCase) ||
               r.Equals("AN", StringComparison.OrdinalIgnoreCase);
    };

    /// <summary>
    /// Predicato dell'incasso, in due fasi (esito della risposta PayPrint, D3).
    /// <para>Prima dell'OK di accettazione un testo o un ER vuol dire incasso rifiutato ("BUSY", "CMD ERROR") e chiude.</para>
    /// <para>
    /// Dopo l'OK chiudono solo la risposta finale del comando, [AN] e il [CM] finale. Un testo o un ER sono la risposta a
    /// qualcos'altro: non toccano l'incasso e finiscono all'evento dei frame orfani.
    /// </para>
    /// </summary>
    private Verdict ClassifyCollection(Collection c, string finalResponse, PagAmicoFrame f)
    {
        var r = f.Response;
        bool accepted;
        lock (_gate) accepted = c.Accepted;

        if (!accepted)
        {
            if (f.IsText) return Verdict.Final;
            if (r is null) return Verdict.Foreign;
            if (r == "OK")
            {
                lock (_gate) c.Accepted = true;
                T($"incasso '{c.Command}' accettato: da qui chiudono solo {finalResponse}, AN e il CM finale");
                c.Acceptance.TrySetResult(true);
                return Verdict.Progress;
            }
            if (r == "p") return Verdict.Progress;
            if (r.Equals("ER", StringComparison.OrdinalIgnoreCase)) return Verdict.Final;
            return IsCollectionOutcome(f, finalResponse) ? Verdict.Final : Verdict.Foreign;
        }

        if (f.IsText || r is null) return Verdict.Foreign;
        if (r == "p") return Verdict.Progress;
        return IsCollectionOutcome(f, finalResponse) ? Verdict.Final : Verdict.Foreign;
    }

    private static bool IsCollectionOutcome(PagAmicoFrame f, string finalResponse)
    {
        var r = f.Response;
        return string.Equals(r, finalResponse, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(r, "AN", StringComparison.OrdinalIgnoreCase) ||
               IsCommitOutcome(f);
    }

    /// <summary>
    /// [CM] finale: quello con errorCode diverso da "OK" e da "NO" (D1). L'accettazione porta errorCode "OK" e
    /// committedAmount a zero, il rifiuto errorCode "NO"; il manuale non documenta la differenza, il log del
    /// simulatore del 2/9 si'.
    /// </summary>
    private static bool IsCommitOutcome(PagAmicoFrame f) =>
        f.Response == "CM" && !IsCommitAck(f) && !IsCommitRefused(f);

    private static bool IsCommitAck(PagAmicoFrame f) =>
        f.Response == "CM" && string.Equals(f.Json?.ErrorCode, "OK", StringComparison.OrdinalIgnoreCase);

    private static bool IsCommitRefused(PagAmicoFrame f) =>
        f.Response == "CM" && string.Equals(f.Json?.ErrorCode, "NO", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Predicato di [CM]: chiude sul rifiuto, sull'esito, o sulla seconda accettazione (se la macchina ne mandasse due).
    /// Dentro un incasso un testo non e' suo; fuori, come per ogni comando, chiude con errore.
    /// </summary>
    private static Func<PagAmicoFrame, Verdict> CommitVerdicts(bool insideCollection)
    {
        var acks = 0;
        return f =>
        {
            if (f.IsText) return insideCollection ? Verdict.Foreign : Verdict.Final;
            if (f.Response != "CM") return insideCollection ? Verdict.Foreign : Verdict.Progress;
            if (!IsCommitAck(f)) return Verdict.Final;
            return ++acks >= 2 ? Verdict.Final : Verdict.Progress;
        };
    }

    // ------------------------------------------------------------------ comandi generici

    /// <summary>
    /// Invia un comando e restituisce la sua risposta (per i comandi che rispondono una sola volta): quella col
    /// codice del comando (ST su ST), un OK, oppure un errore. Un altro messaggio arrivato nel frattempo - un CM in
    /// ritardo, un parziale - non la sostituisce: va ai frame orfani.
    /// </summary>
    public async Task<PagAmicoResponse> SendSimpleAsync(string command, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var frame = await SendAndWaitCoreAsync(command, OwnResponse(command), null, timeout, ct).ConfigureAwait(false);
        return RequireJson(frame, command);
    }

    private static PagAmicoResponse RequireJson(PagAmicoFrame frame, string command)
    {
        if (frame.IsJson && frame.Json is not null)
        {
            if (frame.Json.IsError) throw PagAmicoException.FromFrame(frame);
            return frame.Json;
        }
        throw new PagAmicoException($"Risposta non JSON al comando '{command}': {frame.Raw}", frame);
    }

    // ------------------------------------------------------------------ stato

    /// <summary>[ST] Situazione completa: fondi cassa, scorte, stato accettatori.</summary>
    public Task<PagAmicoResponse> GetStatusAsync(CancellationToken ct = default) =>
        SendSimpleAsync(PagAmicoCommands.Status(), DefaultTimeout, ct);

    /// <summary>[CL] Pulisce il display. Nessuna risposta prevista.</summary>
    public Task ClearDisplayAsync(CancellationToken ct = default) =>
        SendRawAsync(PagAmicoCommands.ClearDisplay(), ct);

    /// <summary>[LO] Rinvia l'ultimo JSON trasmesso dal pagAmico (utile dopo una disconnessione).</summary>
    public Task<PagAmicoResponse> GetLastJsonAsync(CancellationToken ct = default) =>
        SendSimpleAsync(PagAmicoCommands.LastJson(), DefaultTimeout, ct);

    // ------------------------------------------------------------------ incasso

    /// <summary>
    /// [IN] Incasso in contanti. Restituisce il messaggio finale ("IN", "AN" se annullato, oppure lancia su "ER").
    /// La cancellazione di <paramref name="ct"/> invia [AN] al pagAmico e attende l'esito.
    /// </summary>
    public Task<PagAmicoResponse> CollectCashAsync(
        decimal amountEuro,
        IProgress<PagAmicoResponse>? partials = null,
        TimeSpan? timeout = null,
        CancellationToken ct = default) =>
        RunTransactionAsync(PagAmicoCommands.Collect(amountEuro), "IN", partials, timeout, ct);

    /// <summary>[PO] Incasso tramite POS integrato.</summary>
    public Task<PagAmicoResponse> CollectPosAsync(
        decimal amountEuro,
        IProgress<PagAmicoResponse>? partials = null,
        TimeSpan? timeout = null,
        CancellationToken ct = default) =>
        RunTransactionAsync(PagAmicoCommands.CollectPos(amountEuro), "PO", partials, timeout, ct);

    /// <summary>
    /// [IM] Incasso automatico: contanti oppure POS, decide il cliente (FW &gt;= 8.71).
    /// A fine incasso errorCode vale "CONT" o "POS".
    /// </summary>
    public Task<PagAmicoResponse> CollectAutoAsync(
        decimal amountEuro,
        IProgress<PagAmicoResponse>? partials = null,
        TimeSpan? timeout = null,
        CancellationToken ct = default) =>
        RunTransactionAsync(PagAmicoCommands.CollectAuto(amountEuro), "IM", partials, timeout, ct);

    /// <summary>[I2] Incasso con timeout in secondi (sconsigliato dal manuale).</summary>
    public Task<PagAmicoResponse> CollectCashWithTimeoutAsync(
        decimal amountEuro,
        int seconds,
        IProgress<PagAmicoResponse>? partials = null,
        CancellationToken ct = default) =>
        RunTransactionAsync(PagAmicoCommands.CollectWithTimeout(seconds, amountEuro), "IN", partials,
            TimeSpan.FromSeconds(seconds + 30), ct);

    private async Task<PagAmicoResponse> RunTransactionAsync(
        string command,
        string finalResponse,
        IProgress<PagAmicoResponse>? partials,
        TimeSpan? timeout,
        CancellationToken ct)
    {
        ThrowIfCollecting(command);
        await _commandLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        var c = new Collection(command);
        PagAmicoFrame? outcome = null;
        try
        {
            // ai parziali arrivano solo i frame [p], chiamati in modo sincrono e quindi in ordine:
            // niente Progress<T> interno, che li posterebbe sul pool senza garanzia d'ordine
            var waiter = new Waiter(
                f => ClassifyCollection(c, finalResponse, f),
                f => { if (f.IsPartial && f.Json is not null) partials?.Report(f.Json); });

            lock (_gate)
            {
                _collection = c;
                _waiters.Add(waiter);
            }

            // la cancellazione non abbandona l'attesa: invia [AN] (dopo l'OK, se l'incasso non e' ancora
            // accettato) e resta in attesa dell'esito
            using var registration = ct.Register(() => _ = CancelFromTokenAsync(c));
            try
            {
                await SendCommandAsync(command, duringCollection: true, CancellationToken.None).ConfigureAwait(false);
                outcome = await AwaitWaiterTracedAsync(waiter, command, timeout ?? TransactionTimeout, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            finally
            {
                lock (_gate) _waiters.Remove(waiter);
            }
        }
        finally
        {
            EndCollection(c, outcome);
            _commandLock.Release();
        }

        if (outcome is null) throw new PagAmicoException($"Incasso '{command}' terminato senza esito");
        if (!c.Accepted && (outcome.IsText || outcome.IsError))
        {
            var reason = outcome.IsJson ? PagAmicoException.FromFrame(outcome).Message : outcome.Raw;
            if (outcome.IsBusy)
                throw new PagAmicoBusyException($"Incasso '{command}' rifiutato, macchina impegnata: {reason}", outcome);
            throw new PagAmicoRejectedException($"Incasso '{command}' rifiutato: {reason}", outcome);
        }
        return RequireJson(outcome, command);
    }

    /// <summary>Chiude lo stato dell'incasso e sblocca chi aspettava l'accettazione o l'esito.</summary>
    private void EndCollection(Collection c, PagAmicoFrame? outcome)
    {
        Waiter? commitWaiter;
        lock (_gate)
        {
            if (_collection == c) _collection = null;
            commitWaiter = c.CommitWaiter;
        }

        var ended = new PagAmicoException(outcome is null
            ? $"Incasso '{c.Command}' terminato senza esito"
            : $"Incasso '{c.Command}' chiuso da '{Shorten(outcome.Raw)}'");
        c.Acceptance.TrySetException(ended);

        if (outcome is null) c.Outcome.TrySetException(ended);
        else c.Outcome.TrySetResult(outcome);

        // un [CM] ancora in attesa: se l'incasso si e' chiuso sul CM finale e' il suo esito, altrimenti
        // la macchina ha chiuso prima (IN, AN) e l'eventuale risposta al CM arrivera' come frame orfano
        if (commitWaiter is not null)
        {
            if (outcome is not null && IsCommitOutcome(outcome)) commitWaiter.Completion.TrySetResult(outcome);
            else commitWaiter.Completion.TrySetException(new PagAmicoException(
                $"{ended.Message} prima dell'esito di CM"));
        }
        T(outcome is null ? $"incasso '{c.Command}' chiuso senza esito" : $"incasso '{c.Command}' chiuso");
    }

    /// <summary>
    /// Prenota il solo comando di chiusura dell'incasso e lo invia, dopo l'OK se l'incasso non e' ancora accettato.
    /// Vince il primo: una seconda chiusura lancia subito.
    /// </summary>
    private async Task SendClosingAsync(Collection c, string command, Waiter? commitWaiter = null)
    {
        lock (_gate)
        {
            if (_collection != c) throw new PagAmicoException($"Nessun incasso aperto: '{command}' non inviato");
            if (c.Closing is not null)
                throw new PagAmicoException($"Chiusura dell'incasso gia' richiesta con {c.Closing}: '{command}' non inviato");
            c.Closing = command;
            if (commitWaiter is not null) c.CommitWaiter = commitWaiter;
        }

        try
        {
            if (!c.Acceptance.Task.IsCompleted)
                T($"'{command}' in attesa dell'OK dell'incasso prima di partire");
            await c.Acceptance.Task.ConfigureAwait(false);
            await SendCommandAsync(command, duringCollection: true, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            lock (_gate) if (c.Closing == command) c.Closing = null;
            throw;
        }
    }

    private async Task CancelFromTokenAsync(Collection c)
    {
        lock (_gate)
        {
            c.CancelRequested = true;
            if (c.Closing is not null)
            {
                T($"annullo richiesto col token mentre e' in corso {c.Closing}: l'AN partira' solo se il {c.Closing} viene rifiutato");
                return;
            }
        }

        try
        {
            await SendClosingAsync(c, PagAmicoCommands.Cancel()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            T($"annullo col token non inviato: {ex.Message}");
        }
    }

    /// <summary>
    /// [AN] Annulla l'operazione in corso; l'importo restituito e' in changeReturn.
    /// <para>
    /// A incasso aperto passa per la via laterale, senza accodarsi dietro l'incasso: parte dopo l'OK di
    /// accettazione e restituisce l'esito dell'incasso (di norma [AN], ma [IN] se il cliente ha finito prima).
    /// </para>
    /// </summary>
    public async Task<PagAmicoResponse> CancelAsync(CancellationToken ct = default)
    {
        Collection? c;
        lock (_gate) c = _collection;

        if (c is null)
        {
            var frame = await SendAndWaitAsync(PagAmicoCommands.Cancel(), f => f.IsText || f.Response == "AN",
                DefaultTimeout, null, ct).ConfigureAwait(false);
            return RequireJson(frame, "AN");
        }

        await SendClosingAsync(c, PagAmicoCommands.Cancel()).ConfigureAwait(false);
        var outcome = await AwaitTaskAsync(c.Outcome.Task, "AN", TransactionTimeout, ct).ConfigureAwait(false);
        return RequireJson(outcome, "AN");
    }

    /// <summary>
    /// [CM] Chiude l'incasso in corso trattenendo quanto gia' incassato, e restituisce l'<b>esito</b>:
    /// il [CM] con errorCode diverso da "OK" (il primo [CM], con errorCode "OK" e committedAmount a zero,
    /// e' solo l'accettazione). <b>L'importo trattenuto e' in CollectedAmount</b>; CommittedAmount serve da controllo.
    /// Un [CM] rifiutato torna con errorCode "NO" e lascia l'incasso aperto.
    /// <para>
    /// A incasso aperto non si accoda dietro l'incasso: parte subito, o all'OK se l'incasso non e' ancora accettato.
    /// Un solo comando di chiusura per incasso: se e' gia' partito un [AN] o un [CM] lancia.
    /// </para>
    /// NB: dopo il commit possono ancora arrivare parziali [p] o [IN]: l'hopper legge molto velocemente.
    /// </summary>
    public async Task<PagAmicoResponse> CommitAsync(TimeSpan? timeout = null, CancellationToken ct = default)
    {
        Collection? c;
        lock (_gate) c = _collection;

        PagAmicoFrame frame;
        if (c is null)
        {
            frame = await SendAndWaitCoreAsync(PagAmicoCommands.Commit(), CommitVerdicts(insideCollection: false), null,
                timeout ?? TransactionTimeout, ct).ConfigureAwait(false);
        }
        else
        {
            // l'attesa si registra prima dell'invio, altrimenti una risposta immediata andrebbe persa
            var waiter = new Waiter(CommitVerdicts(insideCollection: true), null);
            lock (_gate) _waiters.Add(waiter);

            try
            {
                await SendClosingAsync(c, PagAmicoCommands.Commit(), waiter).ConfigureAwait(false);
                frame = await AwaitWaiterTracedAsync(waiter, "CM", timeout ?? TransactionTimeout, ct).ConfigureAwait(false);
            }
            finally
            {
                lock (_gate)
                {
                    _waiters.Remove(waiter);
                    if (c.CommitWaiter == waiter) c.CommitWaiter = null;
                }
            }

            if (IsCommitRefused(frame))
            {
                // CM rifiutato: il posto di chiusura si libera, e un annullo chiesto col token nel frattempo parte ora
                bool cancel;
                lock (_gate)
                {
                    if (c.Closing == PagAmicoCommands.Commit()) c.Closing = null;
                    cancel = c.CancelRequested;
                }
                T("CM rifiutato dalla macchina (errorCode NO): l'incasso resta aperto");
                if (cancel) _ = CancelFromTokenAsync(c);
            }
        }

        var response = RequireJson(frame, "CM");
        if (response.CollectedAmount is { } collected && response.CommittedAmount is { } committed && collected != committed)
            T($"controllo CM: collectedAmount {collected:0.00} diverso da committedAmount {committed:0.00}; vale collectedAmount");
        return response;
    }

    private static async Task<PagAmicoFrame> AwaitTaskAsync(Task<PagAmicoFrame> task, string what, TimeSpan timeout, CancellationToken ct)
    {
        using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var completed = await Task.WhenAny(task, Task.Delay(timeout, delayCts.Token)).ConfigureAwait(false);
        delayCts.Cancel();
        if (completed != task)
        {
            ct.ThrowIfCancellationRequested();
            throw new PagAmicoTimeoutException($"Nessun esito dal pagAmico entro {timeout.TotalSeconds:0.#}s per '{what}'");
        }
        return await task.ConfigureAwait(false);
    }

    // ------------------------------------------------------------------ erogazione

    /// <summary>[PA] Eroga un importo (resto/rimborso). Password di erogazione se impostata nel setup.</summary>
    public async Task<PagAmicoResponse> DispenseAsync(decimal amountEuro, string password = "", TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var cmd = PagAmicoCommands.Dispense(amountEuro, password);
        var frame = await SendAndWaitAsync(cmd, Terminal("PA"), timeout ?? TransactionTimeout, null, ct).ConfigureAwait(false);
        return RequireJson(frame, cmd);
    }

    /// <summary>[P2] Eroga un numero preciso di banconote per taglio.</summary>
    public async Task<PagAmicoResponse> DispenseBanknotesAsync(
        int n5, int n10, int n20, int n50, int n100, int n200, string password = "",
        TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var cmd = PagAmicoCommands.DispenseBanknotes(n5, n10, n20, n50, n100, n200, password);
        var frame = await SendAndWaitAsync(cmd, Terminal("PB", "P2"), timeout ?? TransactionTimeout, null, ct).ConfigureAwait(false);
        return RequireJson(frame, cmd);
    }

    /// <summary>[PM] Eroga un numero preciso di monete per taglio.</summary>
    public async Task<PagAmicoResponse> DispenseCoinsAsync(
        int c05, int c10, int c20, int c50, int c100, int c200, string password = "",
        TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var cmd = PagAmicoCommands.DispenseCoins(c05, c10, c20, c50, c100, c200, password);
        var frame = await SendAndWaitAsync(cmd, Terminal("PM"), timeout ?? TransactionTimeout, null, ct).ConfigureAwait(false);
        return RequireJson(frame, cmd);
    }

    // ------------------------------------------------------------------ manutenzione / fondo cassa

    /// <summary>[M2] Sposta banconote nel cassetto BTA.</summary>
    public async Task<PagAmicoResponse> MoveBanknotesToBtaAsync(
        int n5, int n10, int n20, int n50, int n100, int n200, string password = "",
        TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var cmd = PagAmicoCommands.MoveBanknotesToBta(n5, n10, n20, n50, n100, n200, password);
        var frame = await SendAndWaitAsync(cmd, Terminal("M2"), timeout ?? TransactionTimeout, null, ct).ConfigureAwait(false);
        return RequireJson(frame, cmd);
    }

    /// <summary>[MF] Sposta monete nel cassetto di recupero (solo modelli con cassetto monete sul fondo).</summary>
    public async Task<PagAmicoResponse> MoveCoinsToCashboxAsync(
        int c05, int c10, int c20, int c50, int c100, int c200, string password = "",
        TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var cmd = PagAmicoCommands.MoveCoinsToCashbox(c05, c10, c20, c50, c100, c200, password);
        var frame = await SendAndWaitAsync(cmd, Terminal("MF"), timeout ?? TransactionTimeout, null, ct).ConfigureAwait(false);
        return RequireJson(frame, cmd);
    }

    /// <summary>[BT] Azzera il cassetto BTA; l'importo presente e' in AmountResettedBanknotesInBta.</summary>
    public Task<PagAmicoResponse> ResetBtaAsync(string password = "", CancellationToken ct = default) =>
        SendSimpleAsync(PagAmicoCommands.ResetBta(password), DefaultTimeout, ct);

    /// <summary>[AF] Aggiorna il fondo cassa (monete, banconote o entrambi).</summary>
    public Task<PagAmicoResponse> UpdateCashFloatAsync(CashFloatTarget target, string password = "", CancellationToken ct = default) =>
        SendSimpleAsync(PagAmicoCommands.UpdateCashFloat(target, password), DefaultTimeout, ct);

    /// <summary>[AZ] Azzera le banconote presenti (solo modelli a due cassetti).</summary>
    public Task<PagAmicoResponse> ResetBanknotesAsync(string password = "", CancellationToken ct = default) =>
        SendSimpleAsync(PagAmicoCommands.ResetBanknotes(password), DefaultTimeout, ct);

    /// <summary>[SM] Imposta scorta minima/massima monete.</summary>
    public Task<PagAmicoResponse> SetCoinStockAsync(IEnumerable<StockThreshold> thresholds, CancellationToken ct = default) =>
        SendSimpleAsync(PagAmicoCommands.SetCoinStock(thresholds), DefaultTimeout, ct);

    /// <summary>[SB] Imposta scorta minima/massima banconote.</summary>
    public Task<PagAmicoResponse> SetBanknoteStockAsync(IEnumerable<StockThreshold> thresholds, CancellationToken ct = default) =>
        SendSimpleAsync(PagAmicoCommands.SetBanknoteStock(thresholds), DefaultTimeout, ct);

    /// <summary>[EM] Abilita/disabilita i tagli monete in incasso e come resto.</summary>
    public Task<PagAmicoResponse> SetCoinAcceptanceAsync(IEnumerable<DenominationToggle> toggles, CancellationToken ct = default) =>
        SendSimpleAsync(PagAmicoCommands.EnableCoins(toggles), DefaultTimeout, ct);

    /// <summary>[EB] Abilita/disabilita i tagli banconote in incasso e come resto.</summary>
    public Task<PagAmicoResponse> SetBanknoteAcceptanceAsync(IEnumerable<DenominationToggle> toggles, CancellationToken ct = default) =>
        SendSimpleAsync(PagAmicoCommands.EnableBanknotes(toggles), DefaultTimeout, ct);

    /// <summary>[RI] Riavvia il pagAmico.</summary>
    public Task RebootAsync(CancellationToken ct = default) =>
        SendRawAsync(PagAmicoCommands.Reboot(), ct);

    // ------------------------------------------------------------------ ricariche

    /// <summary>[RC]/[RS]/[VC]/[VS] Avvia una ricarica mista monete + banconote. Chiudere con <see cref="EndReloadAsync"/>.</summary>
    public Task<PagAmicoResponse> StartMixedReloadAsync(bool updateCashFloat, bool sendPartials = false, CancellationToken ct = default) =>
        SendSimpleAsync(PagAmicoCommands.ReloadMixed(updateCashFloat, sendPartials), DefaultTimeout, ct);

    /// <summary>[RM]/[R3] Ricarica monete.</summary>
    public Task<PagAmicoResponse> StartCoinReloadAsync(bool updateCashFloat, CancellationToken ct = default) =>
        SendSimpleAsync(PagAmicoCommands.ReloadCoins(updateCashFloat), DefaultTimeout, ct);

    /// <summary>[RB]/[R2] Ricarica banconote.</summary>
    public Task<PagAmicoResponse> StartBanknoteReloadAsync(bool updateCashFloat, CancellationToken ct = default) =>
        SendSimpleAsync(PagAmicoCommands.ReloadBanknotes(updateCashFloat), DefaultTimeout, ct);

    /// <summary>[FR] Fine ricarica.</summary>
    public Task<PagAmicoResponse> EndReloadAsync(CancellationToken ct = default) =>
        SendSimpleAsync(PagAmicoCommands.ReloadEnd(), DefaultTimeout, ct);

    // ------------------------------------------------------------------ POS

    /// <summary>[PL] Rilegge l'ultima transazione POS, con o senza ristampa scontrino.</summary>
    public Task<PagAmicoResponse> ReadLastPosTransactionAsync(bool reprintReceipt, CancellationToken ct = default) =>
        SendSimpleAsync(PagAmicoCommands.PosLastTransaction(reprintReceipt), DefaultTimeout, ct);

    /// <summary>[PR] Totali POS (campi PosTot1 / PosTot2).</summary>
    public Task<PagAmicoResponse> ReadPosTotalsAsync(CancellationToken ct = default) =>
        SendSimpleAsync(PagAmicoCommands.PosTotals(), DefaultTimeout, ct);

    /// <summary>[PS] Chiusura giornaliera POS.</summary>
    public Task<PagAmicoResponse> ClosePosDayAsync(TimeSpan? timeout = null, CancellationToken ct = default) =>
        SendSimpleAsync(PagAmicoCommands.PosDailyClose(), timeout ?? TimeSpan.FromMinutes(2), ct);

    /// <summary>[PZ] Riavvio POS.</summary>
    public Task<PagAmicoResponse> RebootPosAsync(TimeSpan? timeout = null, CancellationToken ct = default) =>
        SendSimpleAsync(PagAmicoCommands.PosReboot(), timeout ?? TimeSpan.FromMinutes(2), ct);

    /// <summary>[PP] Primo DLL POS (ricarica certificati).</summary>
    public Task<PagAmicoResponse> PosFirstDllAsync(TimeSpan? timeout = null, CancellationToken ct = default) =>
        SendSimpleAsync(PagAmicoCommands.PosFirstDll(), timeout ?? TimeSpan.FromMinutes(5), ct);

    // ------------------------------------------------------------------ movimenti

    /// <summary>[MV] Elenco movimenti in un intervallo. La risposta termina con il marcatore <c>|\</c>.</summary>
    public async Task<IReadOnlyList<PagAmicoMovement>> GetMovementsAsync(
        DateTime from, DateTime to, string causale = MovementCause.All,
        TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var cmd = PagAmicoCommands.Movements(from, to, causale);
        var frame = await SendAndWaitAsync(cmd, f => f.IsText || f.Json is not null, timeout ?? TimeSpan.FromSeconds(60), null, ct)
            .ConfigureAwait(false);

        if (frame.IsText) throw new PagAmicoException($"Errore comando MV: {frame.Raw}", frame);
        return frame.Json!.Movements;
    }

    /// <summary>[MI] Singolo movimento per Id.</summary>
    public async Task<PagAmicoMovement?> GetMovementAsync(long id, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var cmd = PagAmicoCommands.MovementById(id);
        var frame = await SendAndWaitAsync(cmd, f => f.IsText || f.Json is not null, timeout ?? DefaultTimeout, null, ct)
            .ConfigureAwait(false);

        if (frame.IsText) throw new PagAmicoException($"Errore comando MI: {frame.Raw}", frame);
        return frame.Json!.Movements.FirstOrDefault();
    }

    // ------------------------------------------------------------------ display (manuale INTEGRAZIONE 1.2)

    /// <summary>[DT]/[DG] Mostra una finestra di testo in alto o in basso. Nessuna risposta prevista.</summary>
    public Task ShowTextAsync(string text, DisplayPosition position = DisplayPosition.Top,
        int fontSize = 38, FontStyle style = FontStyle.Bold, FontColor color = FontColor.Blue,
        CancellationToken ct = default) =>
        SendRawAsync(PagAmicoDisplay.ShowText(text, position, fontSize, style, color), ct);

    /// <summary>[DS] Chiude la finestra di testo.</summary>
    public Task CloseTextAsync(CancellationToken ct = default) =>
        SendRawAsync(PagAmicoDisplay.CloseText(), ct);

    /// <summary>
    /// [DM] MessageBox con 1..3 bottoni. Restituisce 1, 2 o 3 in base al bottone premuto.
    /// Passare stringa vuota per nascondere un bottone.
    /// </summary>
    public async Task<int> ShowMessageBoxAsync(
        string text, string button1, string button2 = "", string button3 = "",
        int fontSize = 29, FontStyle style = FontStyle.Normal, FontColor color = FontColor.Green,
        TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var cmd = PagAmicoDisplay.MessageBox(text, button1, button2, button3, fontSize, style, color);
        var frame = await SendAndWaitAsync(cmd, f => f.IsText && ButtonRegex.IsMatch(f.Raw.Trim()),
            timeout ?? TimeSpan.FromMinutes(2), null, ct).ConfigureAwait(false);
        return int.Parse(ButtonRegex.Match(frame.Raw.Trim()).Groups[1].Value);
    }

    /// <summary>[DC] Chiude il MessageBox o la finestra di input.</summary>
    public Task CloseMessageBoxAsync(CancellationToken ct = default) =>
        SendRawAsync(PagAmicoDisplay.CloseMessageBox(), ct);

    /// <summary>
    /// [DI] Finestra di input con tastiera on-screen: restituisce il testo digitato,
    /// oppure null se l'utente ha annullato ("AN").
    /// </summary>
    public async Task<string?> ReadInputAsync(
        string title, string initialText = "", KeyboardLayout keyboard = KeyboardLayout.Standard,
        int fontSize = 29, FontStyle style = FontStyle.Normal, FontColor color = FontColor.Blue,
        TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var cmd = PagAmicoDisplay.InputBox(title, initialText, keyboard, fontSize, style, color);
        var frame = await SendAndWaitAsync(cmd, f => f.IsText && f.Raw.Trim().Length > 0,
            timeout ?? TimeSpan.FromMinutes(2), null, ct).ConfigureAwait(false);

        var value = frame.Raw.Trim();
        return value == PagAmicoTextResponses.Cancelled ? null : value;
    }

    /// <summary>
    /// [QR] Attiva la lettura di barcode / QrCode / Tessera Sanitaria / input da tastiera.
    /// Restituisce il codice letto, oppure null se l'utente ha premuto Annulla ("AN").
    /// </summary>
    public async Task<string?> ReadCodeAsync(
        string prompt, KeyboardMode mode = KeyboardMode.NoKeyboard,
        int fontSize = 16, FontStyle style = FontStyle.Bold, FontColor color = FontColor.Green,
        TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var cmd = PagAmicoDisplay.ReadCode(prompt, mode, fontSize, style, color);
        var frame = await SendAndWaitAsync(cmd, f => f.IsText && f.Raw.Trim().Length > 0,
            timeout ?? TimeSpan.FromMinutes(2), null, ct).ConfigureAwait(false);

        var value = frame.Raw.Trim();
        return value == "AN" ? null : value;
    }

    /// <summary>[QA] Chiude la richiesta di lettura codice.</summary>
    public Task CloseCodeReaderAsync(CancellationToken ct = default) =>
        SendRawAsync(PagAmicoDisplay.CloseCodeReader(), ct);

    /// <summary>[TS] Definisce la struttura della lista (JSON compatto, max 3999 caratteri).</summary>
    public Task ShowListLayoutAsync(string compactJson, CancellationToken ct = default) =>
        SendRawAsync(PagAmicoDisplay.ListLayout(compactJson), ct);

    /// <summary>[ID] Popola la lista. Alla pressione del bottone di uscita il pagAmico risponde "EX".</summary>
    public Task ShowListDataAsync(string compactJson, CancellationToken ct = default) =>
        SendRawAsync(PagAmicoDisplay.ListData(compactJson), ct);

    /// <summary>Attende la chiusura della lista da parte dell'utente ("EX").</summary>
    public async Task WaitListExitAsync(TimeSpan? timeout = null, CancellationToken ct = default) =>
        await WaitForAsync(f => f.IsText && f.Raw.Trim() == "EX", timeout ?? TimeSpan.FromMinutes(5), ct).ConfigureAwait(false);

    /// <summary>[CO] Chiude la lista.</summary>
    public Task CloseListAsync(CancellationToken ct = default) =>
        SendRawAsync(PagAmicoDisplay.CloseList(), ct);

    // ------------------------------------------------------------------ immagini

    /// <summary>[SF] Invia il logo permanente (PNG, area 571x520 dip).</summary>
    public async Task SendLogoAsync(byte[] pngBytes, CancellationToken ct = default)
    {
        await SendRawAsync(BuildImagePacket("SF", pngBytes), ct).ConfigureAwait(false);
        NotifyBinarySent("SF", pngBytes.Length);
    }

    /// <summary>[SI] Invia un'immagine temporanea (PNG/JPG/BMP) che sostituisce il logo fino alla rimozione.</summary>
    public async Task SendTemporaryImageAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        await SendRawAsync(BuildImagePacket("SI", imageBytes), ct).ConfigureAwait(false);
        NotifyBinarySent("SI", imageBytes.Length);
    }

    /// <summary>[SR] Rimuove l'immagine temporanea e ripristina il logo.</summary>
    public Task RemoveTemporaryImageAsync(CancellationToken ct = default) =>
        SendRawAsync(PagAmicoCommands.RemoveTempImage(), ct);

    /// <summary>Notifica manualmente il traffico in uscita per gli invii binari (immagini).</summary>
    private void NotifyBinarySent(string prefix, int length) =>
        CommandSent?.Invoke(this, $"[{prefix}] payload binario di {length} byte");

    private byte[] BuildImagePacket(string prefix, byte[] image)
    {
        using var ms = new MemoryStream();
        var head = Encoding.ASCII.GetBytes(prefix);
        ms.Write(head, 0, head.Length);

        if (ImageLayout == ImagePacketLayout.Documented)
        {
            // "SF" + CHR(255) + CHR(255) + immagine + CHR(254) + CHR(254) + "||"
            ms.WriteByte(0xFF); ms.WriteByte(0xFF);
            ms.Write(image, 0, image.Length);
            ms.WriteByte(0xFE); ms.WriteByte(0xFE);
            ms.WriteByte((byte)'|'); ms.WriteByte((byte)'|');
        }
        else
        {
            // esempio Python del manuale: b"SF\xFF\xFF||" + immagine + b"||\xFE\xFE"
            ms.WriteByte(0xFF); ms.WriteByte(0xFF);
            ms.WriteByte((byte)'|'); ms.WriteByte((byte)'|');
            ms.Write(image, 0, image.Length);
            ms.WriteByte((byte)'|'); ms.WriteByte((byte)'|');
            ms.WriteByte(0xFE); ms.WriteByte(0xFE);
        }
        return ms.ToArray();
    }

    // ------------------------------------------------------------------ stampa

    /// <summary>
    /// Invia un job di stampa: PTSTST ... PTSTEN. La stampa parte dopo PTSTEN.
    /// Gli ack testuali ("OK comando") vengono attesi ma un timeout non blocca il job.
    /// </summary>
    public async Task PrintAsync(PagAmicoPrintJob job, bool waitAck = true, CancellationToken ct = default)
    {
        foreach (var cmd in job.Build())
        {
            if (!waitAck)
            {
                await SendRawAsync(cmd, ct).ConfigureAwait(false);
                continue;
            }

            try
            {
                await SendAndWaitCoreAsync(cmd, OwnResponse(cmd), null, TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
            }
            catch (PagAmicoTimeoutException)
            {
                // alcuni firmware non confermano ogni singolo comando di stampa
            }
        }
    }

    /// <summary>[PTSTAT] Stato stampante: {"response":"OK","errorType":"00000000"}.</summary>
    public async Task<PrinterStatus> GetPrinterStatusAsync(CancellationToken ct = default)
    {
        var frame = await SendAndWaitCoreAsync(PagAmicoPrint.Status(), OwnResponse(PagAmicoPrint.Status()), null, DefaultTimeout, ct)
            .ConfigureAwait(false);
        if (frame.Json is null) throw new PagAmicoException($"Risposta inattesa a PTSTAT: {frame.Raw}", frame);
        return PrinterStatus.Parse(frame.Json);
    }

    /// <summary>[PTSTAN] Annulla la stampa inviata ma non ancora eseguita.</summary>
    public Task CancelPrintAsync(CancellationToken ct = default) =>
        SendRawAsync(PagAmicoPrint.CancelPrint(), ct);

    // ------------------------------------------------------------------

    private sealed class Waiter
    {
        public Waiter(Func<PagAmicoFrame, Verdict> classify, Action<PagAmicoFrame>? onProgress)
        {
            Classify = classify;
            OnProgress = onProgress;
        }

        public Func<PagAmicoFrame, Verdict> Classify { get; }
        public Action<PagAmicoFrame>? OnProgress { get; }
        public TaskCompletionSource<PagAmicoFrame> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>Un incasso aperto. I campi mutabili si toccano sotto _gate.</summary>
    private sealed class Collection
    {
        public Collection(string command) => Command = command;

        public string Command { get; }

        /// <summary>OK di accettazione ricevuto: da qui la macchina sta incassando.</summary>
        public bool Accepted;

        /// <summary>Comando di chiusura in corso ("AN" o "CM"), null se il posto e' libero.</summary>
        public string? Closing;

        /// <summary>Annullo chiesto col token mentre il posto era occupato da un CM.</summary>
        public bool CancelRequested;

        /// <summary>Attesa di un [CM] mandato dentro l'incasso.</summary>
        public Waiter? CommitWaiter;

        /// <summary>Si completa all'OK; fallisce se l'incasso si chiude prima (rifiuto, caduta, timeout).</summary>
        public TaskCompletionSource<bool> Acceptance { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Messaggio che ha chiuso l'incasso.</summary>
        public TaskCompletionSource<PagAmicoFrame> Outcome { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
