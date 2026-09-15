using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using PayPrint.PagAmico;

namespace PayPrint.PagAmico.Tests;

/// <summary>
/// Registro su file (righe intatte con due scrittori, cambio di giorno, ripulitura) e vocabolario degli
/// errori. Gemello di LoggerAndErrorTest.kt: stessi casi, stessi nomi.
/// </summary>
internal static class LoggerAndErrorTests
{
    public static void Run()
    {
        Program.Section("Registro su file");
        LoggerTests();

        Program.Section("Errori e display");
        ErrorTests();
    }

    // ---------------------------------------------------------------- registro su file

    private static void LoggerTests()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pagamico-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            ConcurrentWriters(dir);
            CrossProcessWriters(dir);
            DayChange(dir);
            WritersStartedOnDifferentDays(dir);
            Sanitize(dir);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignorato */ }
        }
    }

    /// <summary>
    /// Due logger distinti sullo stesso file, come due processi: senza esclusione, con l'append di .NET, meta'
    /// delle righe si sovrascrive. Dentro un solo processo il mutex con nome si comporta come fra processi.
    /// </summary>
    private static void ConcurrentWriters(string dir)
    {
        using var a = new PagAmicoFileLogger(dir, "concorrenza");
        using var b = new PagAmicoFileLogger(dir, "concorrenza");
        const int perWriter = 500;

        var ta = new Thread(() => { for (var i = 0; i < perWriter; i++) a.Write("TX", $"A-{i}"); });
        var tb = new Thread(() => { for (var i = 0; i < perWriter; i++) b.Write("TX", $"B-{i}"); });
        ta.Start(); tb.Start();
        ta.Join(); tb.Join();

        var lines = ReadShared(a.CurrentFilePath);
        var wellFormed = new Regex(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}  TX  [AB]-\d+$");
        Program.Check(lines.Length == 2 * perWriter && lines.All(l => wellFormed.IsMatch(l)),
            "due logger sullo stesso file da due thread: 1000 righe intatte");
    }

    /// <summary>
    /// Tre processi veri sullo stesso file: questo e due figli, lanciati con lo stesso assembly di test. Il mutex
    /// con nome deve tenere intere tutte le righe, come il lock sul file in Kotlin.
    /// </summary>
    private static void CrossProcessWriters(string dir)
    {
        const int perWriter = 500;
        // sotto "dotnet test" il processo corrente e' testhost: il figlio si avvia sempre con l'host dotnet
        var dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host ? host : "dotnet";
        var assembly = typeof(LoggerWriterProcess).Assembly.Location;

        var children = new[] { "C", "D" }.Select(label =>
        {
            var start = new ProcessStartInfo(dotnet) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { assembly, LoggerWriterProcess.Argument, dir, "processi", label, perWriter.ToString() })
                start.ArgumentList.Add(arg);
            return Process.Start(start)!;
        }).ToArray();

        using (var log = new PagAmicoFileLogger(dir, "processi"))
            for (var i = 0; i < perWriter; i++) log.Write("TX", $"A-{i}");

        var exited = children.All(p =>
        {
            // letti fino in fondo, cosi' un figlio che scrive molto non si blocca sul buffer pieno
            p.StandardOutput.ReadToEnd();
            p.StandardError.ReadToEnd();
            var done = p.WaitForExit(60_000) && p.ExitCode == 0;
            p.Dispose();
            return done;
        });

        var lines = ReadShared(Path.Combine(dir, $"processi-{DateTime.Now:yyyy-MM-dd}.log"));
        var wellFormed = new Regex(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}  TX  [ACD]-\d+$");
        Program.Check(exited && lines.Length == 3 * perWriter && lines.All(l => wellFormed.IsMatch(l)),
            "tre processi sullo stesso file: 1500 righe intatte");
    }

    private static void DayChange(string dir)
    {
        var now = new DateTime(2030, 1, 1, 23, 59, 59, 900);
        using var log = new PagAmicoFileLogger(dir, "giorno") { Clock = () => now };

        log.Write("--", "prima di mezzanotte");
        var yesterday = log.CurrentFilePath;
        now = new DateTime(2030, 1, 2, 0, 0, 0, 100);
        log.Write("--", "dopo mezzanotte");
        var today = log.CurrentFilePath;

        Program.Check(yesterday != today && ReadShared(today).Length == 1 && ReadShared(today)[0].EndsWith("dopo mezzanotte"),
            "cambio di giorno: la riga dopo mezzanotte va nel file nuovo");
        Program.Check(ReadShared(yesterday).Length == 1 && ReadShared(yesterday)[0].EndsWith("prima di mezzanotte"),
            "cambio di giorno: il file di ieri resta com'era");
    }

    /// <summary>
    /// Un logger che ha scritto la prima riga ieri e uno partito oggi scrivono insieme il file di oggi: devono
    /// usare lo stesso mutex. Fino all'11 settembre il nome del mutex restava quello del primo giorno.
    /// </summary>
    private static void WritersStartedOnDifferentDays(string dir)
    {
        var yesterday = new DateTime(2030, 3, 1, 23, 0, 0);
        var today = new DateTime(2030, 3, 2, 9, 0, 0);
        var clockA = yesterday;
        using var a = new PagAmicoFileLogger(dir, "mutex") { Clock = () => clockA };
        using var b = new PagAmicoFileLogger(dir, "mutex") { Clock = () => today };
        a.Write("--", "ieri");
        clockA = today;
        const int perWriter = 500;

        var ta = new Thread(() => { for (var i = 0; i < perWriter; i++) a.Write("TX", $"A-{i}"); });
        var tb = new Thread(() => { for (var i = 0; i < perWriter; i++) b.Write("TX", $"B-{i}"); });
        ta.Start(); tb.Start();
        ta.Join(); tb.Join();

        var lines = ReadShared(b.CurrentFilePath);
        var wellFormed = new Regex(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}  TX  [AB]-\d+$");
        Program.Check(lines.Length == 2 * perWriter && lines.All(l => wellFormed.IsMatch(l)),
            "logger avviati in giorni diversi: sul file di oggi si escludono ancora");
    }

    private static void Sanitize(string dir)
    {
        using var log = new PagAmicoFileLogger(dir, "pulizia");
        string? last = null;
        log.LineWritten += (_, line) => last = line;

        log.Write("RX", "a\r\nb\u0002c");
        Program.Check(last is not null && last.EndsWith("a  b<02>c"), "CR e LF diventano spazi, i caratteri di controllo <XX>");

        log.MaxLineLength = 5;
        log.Write("RX", "1234567890");
        Program.Check(last is not null && last.EndsWith("12345... (+5 caratteri)"), "riga oltre il massimo troncata con il conto");

        log.MaxLineLength = 0;
        log.Enabled = false;
        last = null;
        log.Write("RX", "non registrata");
        Program.Check(last is null && ReadShared(log.CurrentFilePath).Length == 2, "logger disabilitato: nessuna riga");
    }

    /// <summary>Legge il file mentre il logger lo tiene aperto in scrittura.</summary>
    private static string[] ReadShared(string path)
    {
        if (!File.Exists(path)) return Array.Empty<string>();
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fs);
        return reader.ReadToEnd().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
    }

    // ---------------------------------------------------------------- errori e display

    private static void ErrorTests()
    {
        Program.Eq("DI|29|0|0|NOME|MARIO|2",
            PagAmicoDisplay.InputBox("NOME", "MARIO", KeyboardLayout.NumericNoDecimals),
            "DI finestra di input, forma a sette campi");
        Program.Eq("DI|29|0|0|A/B||1", PagAmicoDisplay.InputBox("A|B"), "DI: la barra verticale nel titolo diventa /");

        Program.Eq("Comando non eseguibile (vedi errorType per lo stato macchina)", PagAmicoErrorCodes.Describe("E100"), "descrizione E100");
        Program.Eq("Erogazione disabilitata nel setup", PagAmicoErrorCodes.Describe("DISPAG"), "descrizione DISPAG");
        Program.Eq("Quantita' monete insufficiente per il taglio 50", PagAmicoErrorCodes.Describe("QTAMONETE50"), "descrizione QTAMONETE per taglio");
        Program.Eq("XYZ", PagAmicoErrorCodes.Describe("XYZ"), "codice sconosciuto restituito com'e'");
        Program.Check(PagAmicoErrorCodes.ParseState("5") == MachineState.Rebooting && PagAmicoErrorCodes.ParseState("abc") == MachineState.Unknown,
            "errorType 5 = riavvio, non numerico = sconosciuto");

        var list = PagAmicoErrorList.Parse("E1291");
        Program.Check(list.PaymentDigit == 1 && list.CoinsDigit == 2 && list.BanknotesDigit == 9 && list.BtaDigit == 1 &&
                      list.Warnings().Count == 4 && list.RequiresOperatorAttention,
            "errorList E1291: quattro anomalie");
        Program.Eq("Date non valide nel comando MV", PagAmicoTextResponses.Describe("CMD ERROR, DATE INVALID"), "descrizione CMD ERROR di MV");

        Program.Check(Text("BUSY").IsBusy && Text("BUSY").IsError, "testo BUSY: occupato ed errore");
        Program.Check(Text("ER BUSY").IsBusy, "testo ER BUSY: occupato");
        Program.Check(!Text("CMD ERROR").IsBusy && Text("CMD ERROR").IsError, "CMD ERROR: errore ma non occupato");
        Program.Check(!Json("{\"response\":\"ER\",\"errorCode\":\"E100\",\"errorType\":\"5\"}").IsBusy,
            "ER E100 con errorType 5 (riavvio): non occupato");
    }

    private static PagAmicoFrame Text(string raw) => new(FrameKind.Text, raw, null);

    private static PagAmicoFrame Json(string raw) => new(FrameKind.Json, raw, PagAmicoResponse.TryParse(raw));
}
