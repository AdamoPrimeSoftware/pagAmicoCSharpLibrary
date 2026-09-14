using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PayPrint.PagAmico;

namespace PayPrint.PagAmico.Tests;

/// <summary>
/// Test offline delle sequenze di incasso: il client vero parla con un finto pagAmico su 127.0.0.1,
/// che riceve i comandi e risponde con le sequenze dell'esito della risposta PayPrint (cap. 3).
/// Nessuna macchina, nessun simulatore. Gemello di SequenceTest.kt: stessi casi, stessi nomi.
/// </summary>
internal static class SequenceTests
{
    // frame usati dai casi: le sequenze sono quelle del log del simulatore del 2/9 e del manuale 2.33
    private const string Ok = "{\"response\":\"OK\"}";
    private const string InFinal = "{\"response\":\"IN\",\"amountRequested\":80,\"collectedAmount\":80,\"changeCoins\":0,\"changeBanknotes\":0,\"amountUnpaid\":0}";
    private const string CmAccepted = "{\"response\":\"CM\",\"errorCode\":\"OK\",\"collectedAmount\":470,\"committedAmout\":0.0}";
    private const string CmFinal = "{\"response\":\"CM\",\"errorCode\":\"\",\"collectedAmount\":470,\"committedAmout\":470.0}";
    private const string CmRefused = "{\"response\":\"CM\",\"errorCode\":\"NO\"}";
    private const string AnFinal = "{\"response\":\"AN\",\"changeReturn\":30,\"halted\":\"FALSE\"}";
    private const string ErBusy = "{\"response\":\"ER\",\"errorCode\":\"E100\",\"errorType\":\"99\"}";
    private const string AnHalted = "{\"response\":\"AN\",\"changeReturn\":20,\"halted\":\"TRUE\"}";
    private const string PoFinal = "{\"response\":\"PO\",\"amountRequested\":3.2,\"collectedAmount\":3.2}";
    private const string ImFinal = "{\"response\":\"IM\",\"errorCode\":\"CONT\",\"collectedAmount\":1}";
    private const string StFinal = "{\"response\":\"ST\",\"errorList\":\"E0000\"}";

    private static string P(decimal collected) =>
        "{\"response\":\"p\",\"collectedAmount\":" + collected.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";

    public static void Run()
    {
        Program.Section("Sequenze di incasso");
        RunAsync().GetAwaiter().GetResult();

        Program.Section("Comandi semplici e invio");
        RunSimpleAndSendAsync().GetAwaiter().GetResult();
    }

    private static async Task RunSimpleAndSendAsync()
    {
        await LateCommitBeforeStatus();
        await OkClosesSimpleCommand();
        await TextErrorClosesSimpleCommand();
        await LastJsonAcceptsAnything();
        await DefaultPauseBetweenCommands();
        await TerminatorAppended();
        await ImagePackets();
    }

    private static async Task RunAsync()
    {
        await BusyAfterOk();
        await CmdErrorAfterOk();
        await BusyBeforeOk();
        await CmdErrorBeforeOk();
        await ErBusyBeforeOk();
        await CommitDuringCollection();
        await CommitRefused();
        await CommitBeforeOk();
        await SingleClosing();
        await CancelWithToken();
        await BlockedSends();
        await OrphanWithoutWaiter();
        await ConnectionLostWhileCollecting();
        await CommitOutsideCollection();
        await CancelDuringCommit();
        await CancelBeforeOk();
        await OtherCollections();
        await ForcedCloseFromPanel();
        await DropWhileCommitWaitsForOk();
        await DisconnectWhileCollecting();
        await CollectionTimeout();
        await DropAfterCallerCancel();
    }

    // ---------------------------------------------------------------- D3: predicato in due fasi

    private static async Task BusyAfterOk()
    {
        using var s = await Session.OpenAsync();
        var partials = new List<decimal?>();
        var progress = new SyncProgress(r => partials.Add(r.CollectedAmount));

        var t = s.Client.CollectCashAsync(80m, progress);
        s.Fake.Expect("IN008000");
        s.Fake.Json(Ok);
        s.Fake.Json(P(10));
        s.Fake.Text("BUSY");
        s.Fake.Json(P(30));
        s.Fake.Json(InFinal);
        var r = await Within(t);

        Program.Check(r.Response == "IN", "OK p BUSY p IN: chiude IN, non il BUSY");
        Program.Check(partials.Count == 2 && partials[0] == 10m && partials[1] == 30m, "parziali: solo i frame p, in ordine");
        Program.Check(s.Orphans.Exists(f => f.IsBusy), "il BUSY dopo l'OK va all'evento dei frame orfani");
        Program.Check(!s.Client.IsCollecting, "IsCollecting falso a incasso chiuso");
    }

    private static async Task CmdErrorAfterOk()
    {
        using var s = await Session.OpenAsync();
        var t = s.Client.CollectCashAsync(80m);
        s.Fake.Expect("IN008000");
        s.Fake.Json(Ok);
        s.Fake.Text("CMD ERROR");
        s.Fake.Json(InFinal);
        var r = await Within(t);

        Program.Check(r.Response == "IN", "OK CMD ERROR IN: chiude IN");
        Program.Check(s.Orphans.Exists(f => f.Raw == "CMD ERROR"), "CMD ERROR dopo l'OK e' un frame orfano");
    }

    private static async Task BusyBeforeOk()
    {
        using var s = await Session.OpenAsync();
        var t = s.Client.CollectCashAsync(80m);
        s.Fake.Expect("IN008000");
        s.Fake.Text("BUSY");

        var ex = await Catch(t);
        Program.Check(ex is PagAmicoBusyException, "BUSY prima dell'OK: PagAmicoBusyException");
        Program.Check(!s.Client.IsCollecting, "dopo il rifiuto nessun incasso aperto");
    }

    private static async Task CmdErrorBeforeOk()
    {
        using var s = await Session.OpenAsync();
        var t = s.Client.CollectCashAsync(80m);
        s.Fake.Expect("IN008000");
        s.Fake.Text("CMD ERROR");

        var ex = await Catch(t);
        Program.Check(ex is PagAmicoRejectedException && ex is not PagAmicoBusyException,
            "CMD ERROR prima dell'OK: rifiutato, non occupato");
    }

    private static async Task ErBusyBeforeOk()
    {
        using var s = await Session.OpenAsync();
        var t = s.Client.CollectCashAsync(80m);
        s.Fake.Expect("IN008000");
        s.Fake.Json(ErBusy);

        var ex = await Catch(t);
        Program.Check(ex is PagAmicoBusyException, "ER E100/99 prima dell'OK: occupato");
    }

    // ---------------------------------------------------------------- D1 e CM durante l'incasso

    private static async Task CommitDuringCollection()
    {
        using var s = await Session.OpenAsync();
        var collect = s.Client.CollectCashAsync(80m);
        s.Fake.Expect("IN008000");
        s.Fake.Json(Ok);
        await WaitUntil(() => s.Client.IsCollecting);

        var commit = s.Client.CommitAsync();
        Program.Check(s.Fake.TryExpect("CM"), "CM durante l'incasso parte senza attendere la fine dell'IN");
        s.Fake.Json(CmAccepted);
        s.Fake.Json(CmFinal);
        var r = await Within(commit);

        Program.Check(r.ErrorCode != "OK", "CM/OK poi CM finale: CommitAsync restituisce l'esito, non l'accettazione");
        Program.Check(r.CollectedAmount == 470m, "trattenuto letto da collectedAmount: 470,00");
        var c = await Within(collect);
        Program.Check(c.Response == "CM" && c.ErrorCode != "OK", "l'incasso si chiude sul CM finale");
    }

    private static async Task CommitRefused()
    {
        using var s = await Session.OpenAsync();
        var collect = s.Client.CollectCashAsync(80m);
        s.Fake.Expect("IN008000");
        s.Fake.Json(Ok);
        await WaitUntil(() => s.Client.IsCollecting);

        var commit = s.Client.CommitAsync();
        s.Fake.Expect("CM");
        s.Fake.Json(CmRefused);
        var r = await Within(commit);

        Program.Check(r.ErrorCode == "NO", "CM/NO: commit rifiutato");
        Program.Check(s.Client.IsCollecting && !collect.IsCompleted, "dopo CM/NO l'incasso resta aperto");

        var cancel = s.Client.CancelAsync();
        Program.Check(s.Fake.TryExpect("AN"), "dopo CM/NO si puo' chiudere con AN");
        s.Fake.Json(AnFinal);
        var a = await Within(cancel);
        var c = await Within(collect);
        Program.Check(a.Response == "AN" && c.Response == "AN", "l'esito dell'AN arriva sia all'annullo sia all'incasso");
    }

    private static async Task CommitBeforeOk()
    {
        using var s = await Session.OpenAsync();
        var collect = s.Client.CollectCashAsync(80m);
        s.Fake.Expect("IN008000");

        var commit = s.Client.CommitAsync();
        Program.Check(s.Fake.Silent(400), "CM prima dell'OK aspetta l'accettazione");
        s.Fake.Json(Ok);
        Program.Check(s.Fake.TryExpect("CM"), "dopo l'OK parte il CM");
        s.Fake.Json(CmAccepted);
        s.Fake.Json(CmFinal);
        await Within(commit);
        await Within(collect);
    }

    private static async Task SingleClosing()
    {
        using var s = await Session.OpenAsync();
        var collect = s.Client.CollectCashAsync(80m);
        s.Fake.Expect("IN008000");
        s.Fake.Json(Ok);
        await WaitUntil(() => s.Client.IsCollecting);

        var first = s.Client.CommitAsync();
        s.Fake.Expect("CM");
        var second = await Catch(s.Client.CommitAsync());
        var third = await Catch(s.Client.CancelAsync());
        Program.Check(second is PagAmicoException && second is not PagAmicoCollectionOpenException &&
                      third is PagAmicoException, "seconda chiusura rifiutata subito");
        Program.Check(s.Fake.Silent(300), "alla macchina arriva un solo comando di chiusura");

        s.Fake.Json(CmAccepted);
        s.Fake.Json(CmFinal);
        await Within(first);
        await Within(collect);
    }

    private static async Task CancelWithToken()
    {
        using var s = await Session.OpenAsync();
        using var cts = new CancellationTokenSource();
        var collect = s.Client.CollectCashAsync(80m, null, null, cts.Token);
        s.Fake.Expect("IN008000");
        s.Fake.Json(Ok);
        await WaitUntil(() => s.Client.IsCollecting);

        cts.Cancel();
        s.Fake.Expect("AN");
        Program.Check(s.Fake.Silent(300), "annullo del chiamante: un solo AN");
        s.Fake.Json(AnFinal);
        var r = await Within(collect);
        Program.Check(r.Response == "AN" && r.ChangeReturn == 30m, "annullo del chiamante: esito AN consegnato");
    }

    // ---------------------------------------------------------------- invii bloccati, orfani, caduta

    private static async Task BlockedSends()
    {
        using var s = await Session.OpenAsync();
        var collect = s.Client.CollectCashAsync(80m);
        s.Fake.Expect("IN008000");
        s.Fake.Json(Ok);
        await WaitUntil(() => s.Client.IsCollecting);
        Program.Check(s.Client.IsCollecting, "IsCollecting vero durante l'incasso");

        var st = await Catch(s.Client.GetStatusAsync());
        var dt = await Catch(s.Client.ShowTextAsync("ciao"));
        var raw = await Catch(s.Client.SendRawAsync("ST"));
        Program.Check(st is PagAmicoCollectionOpenException, "ST a incasso aperto: eccezione immediata");
        Program.Check(dt is PagAmicoCollectionOpenException, "display a incasso aperto: eccezione immediata");
        Program.Check(raw is PagAmicoCollectionOpenException, "invio grezzo a incasso aperto: eccezione immediata");
        Program.Check(s.Fake.Silent(300), "a incasso aperto nulla e' trasmesso alla macchina");

        s.Fake.Json(InFinal);
        await Within(collect);
    }

    private static async Task OrphanWithoutWaiter()
    {
        using var s = await Session.OpenAsync();
        s.Fake.Json(AnFinal);
        await WaitUntil(() => s.Orphans.Count > 0);
        Program.Check(s.Orphans.Exists(f => f.Response == "AN"), "frame senza nessuna attesa: evento dei frame orfani");
    }

    private static async Task ConnectionLostWhileCollecting()
    {
        using var s = await Session.OpenAsync();
        var collect = s.Client.CollectCashAsync(80m);
        s.Fake.Expect("IN008000");
        s.Fake.Json(Ok);
        s.Fake.Json(P(20));
        await WaitUntil(() => s.Client.IsCollecting);
        s.Fake.Drop();

        var ex = await Catch(collect);
        Program.Check(ex is PagAmicoConnectionLostException { MayBeCollecting: true },
            "caduta durante l'incasso: connessione persa, forse sta incassando");
    }

    private static async Task CommitOutsideCollection()
    {
        using var s = await Session.OpenAsync();
        var commit = s.Client.CommitAsync();
        s.Fake.Expect("CM");
        s.Fake.Json(CmAccepted);
        s.Fake.Json(CmFinal);
        var r = await Within(commit);
        Program.Check(r.ErrorCode != "OK" && r.CollectedAmount == 470m, "CM fuori incasso: esito dal CM finale, non 0,00");
    }

    private static async Task CancelDuringCommit()
    {
        using var s = await Session.OpenAsync();
        using var cts = new CancellationTokenSource();
        var collect = s.Client.CollectCashAsync(80m, null, null, cts.Token);
        s.Fake.Expect("IN008000");
        s.Fake.Json(Ok);
        await WaitUntil(() => s.Client.IsCollecting);

        var commit = s.Client.CommitAsync();
        s.Fake.Expect("CM");
        cts.Cancel();
        Program.Check(s.Fake.Silent(300), "annullo del chiamante durante un CM: l'AN aspetta l'esito del CM");
        s.Fake.Json(CmRefused);
        await Within(commit);
        Program.Check(s.Fake.TryExpect("AN"), "dopo il CM/NO parte l'AN chiesto dal chiamante");
        s.Fake.Json(AnFinal);
        var r = await Within(collect);
        Program.Check(r.Response == "AN", "incasso chiuso dall'AN");
    }

    // ---------------------------------------------------------------- casi che toccano i soldi

    private static async Task CancelBeforeOk()
    {
        using var s = await Session.OpenAsync();
        using var cts = new CancellationTokenSource();
        var collect = s.Client.CollectCashAsync(80m, null, null, cts.Token);
        s.Fake.Expect("IN008000");

        cts.Cancel();
        Program.Check(s.Fake.Silent(400), "annullo del chiamante prima dell'OK: l'AN aspetta l'accettazione");
        s.Fake.Json(Ok);
        Program.Check(s.Fake.TryExpect("AN"), "dopo l'OK parte l'AN chiesto prima");
        s.Fake.Json(AnFinal);
        var r = await Within(collect);
        Program.Check(r.Response == "AN", "annullo prima dell'OK: esito AN consegnato");
    }

    private static async Task OtherCollections()
    {
        using (var s = await Session.OpenAsync())
        {
            var t = s.Client.CollectPosAsync(3.20m);
            s.Fake.Expect("PO000320");
            s.Fake.Json(Ok);
            s.Fake.Text("BUSY");
            s.Fake.Json(PoFinal);
            var r = await Within(t);
            Program.Check(r.Response == "PO" && r.CollectedAmount == 3.2m, "PO: chiude sull'esito PO, non sul BUSY");
        }

        using (var s = await Session.OpenAsync())
        {
            var t = s.Client.CollectAutoAsync(1.00m);
            s.Fake.Expect("IM000100");
            s.Fake.Json(Ok);
            s.Fake.Json(P(1));
            s.Fake.Json(ImFinal);
            var r = await Within(t);
            Program.Check(r.Response == "IM" && r.ErrorCode == "CONT", "IM: chiude sull'esito IM (CONT)");
        }

        using (var s = await Session.OpenAsync())
        {
            var t = s.Client.CollectCashWithTimeoutAsync(1.00m, 20);
            s.Fake.Expect("I2020000100");
            s.Fake.Json(Ok);
            s.Fake.Json(InFinal);
            var r = await Within(t);
            Program.Check(r.Response == "IN", "I2: chiude sull'esito IN");
        }
    }

    private static async Task ForcedCloseFromPanel()
    {
        using var s = await Session.OpenAsync();
        var collect = s.Client.CollectCashAsync(80m);
        s.Fake.Expect("IN008000");
        s.Fake.Json(Ok);
        s.Fake.Json(P(20));
        s.Fake.Json(AnHalted);
        var r = await Within(collect);

        Program.Check(r.Response == "AN" && r.IsHalted, "chiusura forzata dal pannello: esito AN con halted TRUE");
        Program.Check(s.Fake.Silent(200), "chiusura forzata: il client non ha mandato nessun AN");
    }

    private static async Task DropWhileCommitWaitsForOk()
    {
        using var s = await Session.OpenAsync();
        var collect = s.Client.CollectCashAsync(80m);
        s.Fake.Expect("IN008000");
        var commit = s.Client.CommitAsync();
        await Task.Delay(200);
        s.Fake.Drop();

        var commitEx = await Catch(commit);
        var collectEx = await Catch(collect);
        Program.Check(commitEx is PagAmicoException, "caduta mentre il CM aspetta l'OK: il commit fallisce");
        Program.Check(collectEx is PagAmicoConnectionLostException { MayBeCollecting: false },
            "caduta prima dell'OK: connessione persa, l'incasso non era accettato");
        Program.Check(s.Fake.Silent(200), "caduta prima dell'OK: il CM non e' mai partito");
    }

    private static async Task DisconnectWhileCollecting()
    {
        using var s = await Session.OpenAsync();
        var collect = s.Client.CollectCashAsync(80m);
        s.Fake.Expect("IN008000");
        s.Fake.Json(Ok);
        await Task.Delay(200);
        s.Client.Disconnect();

        var ex = await Catch(collect);
        Program.Check(ex is PagAmicoConnectionLostException { MayBeCollecting: true },
            "Disconnect() a incasso accettato: connessione persa, forse sta incassando");
        await WaitUntil(() => !s.Client.IsCollecting);
        Program.Check(!s.Client.IsCollecting, "dopo Disconnect() nessun incasso aperto");
    }

    private static async Task CollectionTimeout()
    {
        using var s = await Session.OpenAsync();
        var collect = s.Client.CollectCashAsync(80m, null, TimeSpan.FromMilliseconds(600));
        s.Fake.Expect("IN008000");
        s.Fake.Json(Ok);

        var ex = await Catch(collect);
        Program.Check(ex is PagAmicoTimeoutException, "timeout dell'incasso: PagAmicoTimeoutException");
        Program.Check(!s.Client.IsCollecting, "dopo il timeout lo stato dell'incasso si libera (la macchina no: D2)");
        s.Fake.Json(P(30));
        await WaitUntil(() => s.Orphans.Exists(f => f.IsPartial));
        Program.Check(s.Orphans.Exists(f => f.IsPartial), "dopo il timeout i frame dell'incasso vanno ai frame orfani");
    }

    private static async Task DropAfterCallerCancel()
    {
        using var s = await Session.OpenAsync();
        using var cts = new CancellationTokenSource();
        var collect = s.Client.CollectCashAsync(80m, null, null, cts.Token);
        s.Fake.Expect("IN008000");
        s.Fake.Json(Ok);
        await WaitUntil(() => s.Client.IsCollecting);
        cts.Cancel();
        s.Fake.Expect("AN");
        s.Fake.Drop();

        await Catch(collect);
        await WaitUntil(() => !s.Client.IsCollecting);
        Program.Check(!s.Client.IsCollecting, "caduta dopo l'annullo del chiamante: nessun incasso resta aperto");
    }

    // ---------------------------------------------------------------- comandi semplici e invio

    private static async Task LateCommitBeforeStatus()
    {
        using var s = await Session.OpenAsync();
        var t = s.Client.GetStatusAsync();
        s.Fake.Expect("ST");
        s.Fake.Json(CmFinal);
        s.Fake.Json(StFinal);
        var r = await Within(t);

        Program.Check(r.Response == "ST", "CM in ritardo prima della risposta a ST: ST riceve la sua");
        Program.Check(s.Orphans.Exists(f => f.Response == "CM"), "il CM in ritardo va ai frame orfani");
    }

    private static async Task OkClosesSimpleCommand()
    {
        using var s = await Session.OpenAsync();
        var t = s.Client.UpdateCashFloatAsync(CashFloatTarget.Both);
        s.Fake.Expect("AF9");
        s.Fake.Json(Ok);
        var r = await Within(t);
        Program.Check(r.Response == "OK", "comando semplice chiuso da OK");
    }

    private static async Task TextErrorClosesSimpleCommand()
    {
        using var s = await Session.OpenAsync();
        var t = s.Client.GetStatusAsync();
        s.Fake.Expect("ST");
        s.Fake.Text("CMD ERROR");
        var ex = await Catch(t);
        Program.Check(ex is PagAmicoException && ex is not PagAmicoTimeoutException, "comando semplice chiuso da un testo di errore");
    }

    private static async Task LastJsonAcceptsAnything()
    {
        using var s = await Session.OpenAsync();
        var t = s.Client.GetLastJsonAsync();
        s.Fake.Expect("LO");
        s.Fake.Json(CmFinal);
        var r = await Within(t);
        Program.Check(r.Response == "CM", "LO accetta l'ultimo JSON, qualunque sia");
    }

    private static async Task DefaultPauseBetweenCommands()
    {
        // configurazione di default: nessun terminatore, 80 ms fra un invio e l'altro
        using var s = await Session.OpenAsync(defaults: true);
        await s.Client.ShowTextAsync("A");
        await s.Client.CloseTextAsync();
        var segments = s.Fake.Segments(2);

        Program.Check(segments.Count == 2 && (segments[1].At - segments[0].At).TotalMilliseconds >= 70,
            "pausa di default: due invii consecutivi arrivano ad almeno 70 ms");
        Program.Check(segments.Count == 2 && Encoding.UTF8.GetString(segments[1].Data) == "DS\r",
            "default: ogni comando arriva nel suo segmento, chiuso da CR");
    }

    private static async Task TerminatorAppended()
    {
        using var s = await Session.OpenAsync(defaults: true);
        s.Client.CommandTerminator = "\r\n";
        await s.Client.ClearDisplayAsync();
        Program.Check(Encoding.ASCII.GetString(s.Fake.TakeBytes(4)) == "CL\r\n", "terminatore CR+LF accodato al comando");
    }

    private static async Task ImagePackets()
    {
        using var s = await Session.OpenAsync(defaults: true);
        var image = new byte[] { 1, 2, 3 };

        await s.Client.SendLogoAsync(image);
        var documented = s.Fake.TakeBytes(11);
        Program.Check(documented.AsSpan().SequenceEqual(new byte[] { (byte)'S', (byte)'F', 0xFF, 0xFF, 1, 2, 3, 0xFE, 0xFE, (byte)'|', (byte)'|' }),
            "immagine SF, incapsulamento del manuale: SF FF FF png FE FE ||");

        s.Client.ImageLayout = ImagePacketLayout.PythonSample;
        await s.Client.SendTemporaryImageAsync(image);
        var python = s.Fake.TakeBytes(13);
        Program.Check(python.AsSpan().SequenceEqual(new byte[] { (byte)'S', (byte)'I', 0xFF, 0xFF, (byte)'|', (byte)'|', 1, 2, 3, (byte)'|', (byte)'|', 0xFE, 0xFE }),
            "immagine SI, incapsulamento dell'esempio Python: SI FF FF || png || FE FE");
    }

    // ---------------------------------------------------------------- infrastruttura

    private static async Task<T> Within<T>(Task<T> task, int ms = 5000)
    {
        if (await Task.WhenAny(task, Task.Delay(ms)) != task)
            throw new TimeoutException("il test non si e' chiuso entro " + ms + " ms");
        return await task;
    }

    private static async Task<Exception?> Catch(Task task, int ms = 5000)
    {
        if (await Task.WhenAny(task, Task.Delay(ms)) != task) return new TimeoutException("nessun esito entro " + ms + " ms");
        try { await task; return null; }
        catch (Exception ex) { return ex; }
    }

    private static async Task WaitUntil(Func<bool> condition, int ms = 3000)
    {
        var end = DateTime.UtcNow.AddMilliseconds(ms);
        while (!condition() && DateTime.UtcNow < end) await Task.Delay(10);
    }

    /// <summary>IProgress sincrono: Progress&lt;T&gt; posterebbe i parziali sul pool, senza ordine.</summary>
    private sealed class SyncProgress : IProgress<PagAmicoResponse>
    {
        private readonly Action<PagAmicoResponse> _action;
        public SyncProgress(Action<PagAmicoResponse> action) => _action = action;
        public void Report(PagAmicoResponse value) => _action(value);
    }

    private sealed class Session : IDisposable
    {
        private Session(FakePagAmico fake, PagAmicoClient client)
        {
            Fake = fake;
            Client = client;
            client.OrphanFrame += (_, f) => { lock (Orphans) Orphans.Add(f); };
        }

        public FakePagAmico Fake { get; }
        public PagAmicoClient Client { get; }
        public List<PagAmicoFrame> Orphans { get; } = new();

        /// <param name="defaults">true: configurazione di default della libreria (terminatore CR, pausa di 80 ms).</param>
        public static async Task<Session> OpenAsync(bool defaults = false)
        {
            var fake = new FakePagAmico();
            // il terminatore serve solo al finto pagAmico per separare i comandi
            var client = defaults
                ? new PagAmicoClient("127.0.0.1", fake.Port)
                : new PagAmicoClient("127.0.0.1", fake.Port) { CommandTerminator = "\r", MinimumCommandInterval = TimeSpan.Zero };
            await client.ConnectAsync();
            fake.WaitConnected();
            return new Session(fake, client);
        }

        public void Dispose()
        {
            Client.Dispose();
            Fake.Dispose();
        }
    }

    /// <summary>Finto pagAmico: un solo client, comandi separati dal CR, risposte scritte dal test.</summary>
    private sealed class FakePagAmico : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly BlockingCollection<string> _commands = new();
        private readonly ManualResetEventSlim _connected = new();
        private readonly List<(DateTime At, byte[] Data)> _segments = new();
        private readonly List<byte> _raw = new();
        private TcpClient? _peer;

        public FakePagAmico()
        {
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _ = Task.Run(AcceptAsync);
        }

        public int Port { get; }

        public void WaitConnected()
        {
            if (!_connected.Wait(3000)) throw new TimeoutException("il client non si e' connesso al finto pagAmico");
        }

        private async Task AcceptAsync()
        {
            try
            {
                _peer = await _listener.AcceptTcpClientAsync();
                _connected.Set();
                var stream = _peer.GetStream();
                var buffer = new byte[4096];
                var pending = new StringBuilder();
                int n;
                while ((n = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    lock (_segments)
                    {
                        var data = new byte[n];
                        Array.Copy(buffer, data, n);
                        _segments.Add((DateTime.UtcNow, data));
                        _raw.AddRange(data);
                    }
                    pending.Append(Encoding.UTF8.GetString(buffer, 0, n));
                    int cr;
                    while ((cr = pending.ToString().IndexOf('\r')) >= 0)
                    {
                        _commands.Add(pending.ToString(0, cr));
                        pending.Remove(0, cr + 1);
                    }
                }
            }
            catch
            {
                // chiusura
            }
        }

        /// <summary>Attende il prossimo comando e verifica che sia quello atteso.</summary>
        public void Expect(string command)
        {
            if (!_commands.TryTake(out var received, 3000))
                throw new TimeoutException($"il finto pagAmico non ha ricevuto '{command}'");
            if (received != command)
                throw new InvalidOperationException($"il finto pagAmico attendeva '{command}', ha ricevuto '{received}'");
        }

        public bool TryExpect(string command) =>
            _commands.TryTake(out var received, 3000) && received == command;

        /// <summary>I primi <paramref name="count"/> segmenti TCP ricevuti, con l'istante d'arrivo.</summary>
        public List<(DateTime At, byte[] Data)> Segments(int count, int ms = 3000)
        {
            var end = DateTime.UtcNow.AddMilliseconds(ms);
            while (DateTime.UtcNow < end)
            {
                lock (_segments) if (_segments.Count >= count) return _segments.GetRange(0, count);
                Thread.Sleep(10);
            }
            lock (_segments) return new List<(DateTime, byte[])>(_segments);
        }

        /// <summary>Attende <paramref name="count"/> byte grezzi, li restituisce e li toglie dal buffer.</summary>
        public byte[] TakeBytes(int count, int ms = 3000)
        {
            var end = DateTime.UtcNow.AddMilliseconds(ms);
            while (DateTime.UtcNow < end)
            {
                lock (_segments)
                {
                    if (_raw.Count >= count)
                    {
                        var bytes = _raw.GetRange(0, count).ToArray();
                        _raw.RemoveRange(0, count);
                        return bytes;
                    }
                }
                Thread.Sleep(10);
            }
            lock (_segments) return _raw.ToArray();
        }

        /// <summary>Vero se per <paramref name="ms"/> millisecondi non arriva nessun comando.</summary>
        public bool Silent(int ms) => !_commands.TryTake(out _, ms);

        public void Json(string json) => Write(json);

        /// <summary>Testo nudo, senza terminatore, come lo manda la macchina: il client lo chiude dopo 150 ms di silenzio.</summary>
        public void Text(string text)
        {
            Write(text);
            Thread.Sleep(300);
        }

        public void Drop() => _peer?.Close();

        private void Write(string raw)
        {
            var bytes = Encoding.UTF8.GetBytes(raw);
            _peer!.GetStream().Write(bytes, 0, bytes.Length);
            Thread.Sleep(20);
        }

        public void Dispose()
        {
            try { _peer?.Close(); } catch { /* ignorato */ }
            _listener.Stop();
        }
    }
}
