using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using PayPrint.PagAmico;

namespace PayPrint.PagAmico.Tests;

/// <summary>
/// Test di autoverifica senza dipendenze esterne: confronta i comandi generati con gli
/// esempi letterali del manuale, verifica il framer sulle risposte documentate e, con un finto
/// pagAmico su 127.0.0.1, le sequenze di incasso (SequenceTests.cs).
/// Uso: dotnet run --project PayPrint.PagAmico.Tests
/// </summary>
internal static class Program
{
    private static int _failed;
    private static int _passed;
    private static readonly List<string> Failures = new();

    private static int Main()
    {
        var (passed, failures) = RunAll();

        Console.WriteLine();
        Console.WriteLine($"{passed} test superati, {failures.Count} falliti");
        return failures.Count == 0 ? 0 : 1;
    }

    /// <summary>Esegue tutti i test offline. Usato da Main (dotnet run) e da SuiteTests (dotnet test).</summary>
    internal static (int Passed, IReadOnlyList<string> Failures) RunAll()
    {
        _passed = 0;
        _failed = 0;
        Failures.Clear();

        CommandTests();
        ParserTests();
        ResponseTests();
        PrinterTests();
        DisplayTests();
        SequenceTests.Run();
        LoggerAndErrorTests.Run();

        return (_passed, Failures.ToArray());
    }

    // ---------------------------------------------------------------- comandi (esempi letterali del manuale)

    private static void CommandTests()
    {
        Section("Comandi");

        Eq("IN001050", PagAmicoCommands.Collect(10.50m), "IN incasso 10,50 EUR");
        Eq("IN000150", PagAmicoCommands.Collect(1.50m), "IN incasso 1,50 EUR");
        Eq("PO001050", PagAmicoCommands.CollectPos(10.50m), "PO incasso POS 10,50 EUR");
        Eq("IM001050", PagAmicoCommands.CollectAuto(10.50m), "IM incasso automatico 10,50 EUR");
        Eq("I2020000100", PagAmicoCommands.CollectWithTimeout(20, 1.00m), "I2 incasso 1 EUR timeout 20s");
        Eq("PA0000001050", PagAmicoCommands.Dispense(10.50m), "PA erogazione 10,50 EUR");
        Eq("PA0000001050mypassword", PagAmicoCommands.Dispense(10.50m, "mypassword"), "PA con password");
        Eq("PM001001000000000000", PagAmicoCommands.DispenseCoins(1, 1, 0, 0, 0, 0), "PM 1x0,05 + 1x0,10");
        Eq("P2001000000000000000myPassword", PagAmicoCommands.DispenseBanknotes(1, 0, 0, 0, 0, 0, "myPassword"), "P2 1 banconota da 5");
        Eq("M2001000000000000000myPassword", PagAmicoCommands.MoveBanknotesToBta(1, 0, 0, 0, 0, 0, "myPassword"), "M2 1 banconota in BTA");
        Eq("MF001000000000000000myPassword", PagAmicoCommands.MoveCoinsToCashbox(1, 0, 0, 0, 0, 0, "myPassword"), "MF 1 moneta da 0,05");
        Eq("AF1myPassword", PagAmicoCommands.UpdateCashFloat(CashFloatTarget.Coins, "myPassword"), "AF fondo cassa monete");
        Eq("AF9", PagAmicoCommands.UpdateCashFloat(CashFloatTarget.Both), "AF fondo cassa monete + banconote");
        Eq("AZ2myPassword", PagAmicoCommands.ResetBanknotes("myPassword"), "AZ azzera banconote");
        Eq("BTmyPassword", PagAmicoCommands.ResetBta("myPassword"), "BT azzera BTA");

        var stock = Enumerable.Repeat(new StockThreshold(5, 100), 6);
        Eq("SM005100005100005100005100005100005100", PagAmicoCommands.SetCoinStock(stock), "SM scorte monete 5/100");
        Eq("SB005100005100005100005100005100005100", PagAmicoCommands.SetBanknoteStock(stock), "SB scorte banconote 5/100");

        var toggles = new List<DenominationToggle>
        {
            new(false, true), new(false, true), new(false, true), new(false, true),
            new(true, true), new(true, true)
        };
        Eq("EM010101011111", PagAmicoCommands.EnableCoins(toggles), "EM abilitazione tagli monete");
        Eq("EB010101011111", PagAmicoCommands.EnableBanknotes(toggles), "EB abilitazione tagli banconote");

        Eq("MV2025/01/01 10:002025/04/22 18:59000",
            PagAmicoCommands.Movements(new DateTime(2025, 1, 1, 10, 0, 0), new DateTime(2025, 4, 22, 18, 59, 0)),
            "MV elenco movimenti");
        Eq("MI000007222", PagAmicoCommands.MovementById(7222), "MI movimento per Id");

        Eq("PLT", PagAmicoCommands.PosLastTransaction(true), "PL ultima transazione con ristampa");
        Eq("PLF", PagAmicoCommands.PosLastTransaction(false), "PL ultima transazione senza ristampa");
        Eq("RS", PagAmicoCommands.ReloadMixed(true), "RS ricarica mista con fondo cassa");
        Eq("VC", PagAmicoCommands.ReloadMixed(false, sendPartials: true), "VC ricarica mista con parziali");

        Throws(() => PagAmicoCommands.Collect(10000m), "IN oltre 6 cifre deve fallire");
        Throws(() => PagAmicoCommands.Dispense(1m, new string('x', 20)), "password oltre 19 caratteri deve fallire");
        Throws(() => new StockThreshold(50, 10), "scorta minima > massima deve fallire");
    }

    // ---------------------------------------------------------------- framer

    private static void ParserTests()
    {
        Section("Framer");

        // 1. JSON spezzato su piu' segmenti TCP
        var p = new PagAmicoFrameParser();
        p.Append("{\"response\":\"I");
        Check(!p.TryReadFrame(false, out _), "JSON incompleto non deve produrre frame");
        p.Append("N\",\"collectedAmount\":1.5}");
        Check(p.TryReadFrame(false, out var f1) && f1.Response == "IN", "JSON riassemblato su due segmenti");

        // 2. due JSON nello stesso segmento
        p = new PagAmicoFrameParser();
        p.Append("{\"response\":\"OK\"}{\"response\":\"p\"}");
        Check(p.TryReadFrame(false, out var f2) && f2.Response == "OK", "primo JSON di un segmento doppio");
        Check(p.TryReadFrame(false, out var f3) && f3.Response == "p", "secondo JSON di un segmento doppio");

        // 3. terminatore |\ dei comandi MV / MI
        p = new PagAmicoFrameParser();
        p.Append("{\"root\":[{\"dataora\":\"2025-07-01 11:30:15\",\"codice_operazione\":\"090\",\"importo_pos\":101.55}]}|\\");
        Check(p.TryReadFrame(false, out var f4) && f4.Json!.Movements.Count == 1, "risposta MV con terminatore |\\");
        Check(p.BufferedLength == 0, "il terminatore |\\ viene consumato");
        Check(f4!.Json!.Movements[0].PosAmount == 101.55m, "importo_pos del movimento");

        // 4. graffe e virgolette dentro il messaggio POS non devono chiudere il frame
        p = new PagAmicoFrameParser();
        p.Append("{\"response\":\"PO\",\"posFinancialTransactionEndResponseMessage\":\"\\u0002{ciao} \\\"x\\\" }\\u0003\"}");
        Check(p.TryReadFrame(false, out var f5) && f5.Response == "PO", "graffe dentro stringa JSON");

        // 5. risposta testuale chiusa dal silenzio
        p = new PagAmicoFrameParser { TextIdle = TimeSpan.FromMilliseconds(1) };
        p.Append("CMD ERROR");
        Check(!p.TryReadFrame(false, out _), "testo senza silenzio resta in buffer");
        Thread.Sleep(10);
        Check(p.TryReadFrame(true, out var f6) && f6.IsText && f6.Raw == "CMD ERROR" && f6.IsError, "testo chiuso dal silenzio");

        // 6. testo seguito da JSON
        p = new PagAmicoFrameParser();
        p.Append("BT3{\"response\":\"OK\"}");
        Check(p.TryReadFrame(false, out var f7) && f7.Raw == "BT3", "testo chiuso dalla graffa successiva");
        Check(p.TryReadFrame(false, out var f8) && f8.Response == "OK", "JSON dopo il testo");
    }

    // ---------------------------------------------------------------- risposta

    private static void ResponseTests()
    {
        Section("Risposta JSON");

        const string json = "{\"response\":\"PO\",\"amountRequested\":0.1,\"collectedAmount\":0.1," +
                            "\"errorList\":\"E0090\",\"coins\":[0,0,0,41,57,71,29,47,26,0]," +
                            "\"coinsLimits\":[0,0,0,10,10,10,10,10,10,0]," +
                            "\"bankNotes\":[[5,2,0,0,1,5,30,0,0,0],[10,0,0,0,2,1,30,0,0,0]]," +
                            "\"bankNotes_BTA\":[0,0,15,6,0,0,0,0,0,0,0],\"bankNotesInStock\":[0,2,0,1,0,0,0]," +
                            "\"coinsInStock\":[0,0,0,82,117,142,58,96,57,0],\"firmwareVers\":8.7,\"sN\":\"0000\"," +
                            "\"typePagAmico\":\"4B\",\"committedAmout\":0,\"AmountBanknotesInBTA\":0,\"Id\":7365," +
                            "\"PosTot_1\":12.5,\"PosTot_2\":0}";

        var r = PagAmicoResponse.TryParse(json)!;
        Check(r is not null, "parsing della risposta di esempio del manuale");
        Eq("PO", r!.Response, "campo response");
        Eq("0000", r.SerialNumber, "sN mappato su SerialNumber");
        Check(r.CommittedAmount == 0m, "committedAmout (refuso del firmware) mappato");
        Check(r.Id == 7365, "Id del movimento");
        Check(r.PosTot1 == 12.5m, "PosTot_1");
        Check(r.FirmwareVersion == 8.7, "firmwareVers");

        Check(r.CoinsByDenomination[5] == 41 && r.CoinsByDenomination[200] == 26, "monete per taglio");
        Check(r.CoinsInStockByDenomination[10] == 117, "fondo cassa monete per taglio");
        Check(r.BankNotesInStockByDenomination[5] == 2 && r.BankNotesInStockByDenomination[20] == 1, "fondo cassa banconote per taglio");
        Check(r.BankNotesBtaByDenomination[5] == 15 && r.BankNotesBtaByDenomination[10] == 6, "banconote in BTA per taglio");
        Check(r.Drawers.Count == 2 && r.Drawers[0].Value == 5 && r.Drawers[0].Quantity == 2, "cassetti banconote");

        var status = r.Status;
        Check(status.BanknotesEmpty, "errorList E0090: taglio banconote esaurito");
        Check(!status.CoinsBelowMinimum, "errorList E0090: monete ok");
        Check(status.Warnings().Count == 1, "una sola anomalia segnalata");

        // varianti di nome del firmware precedente
        var legacy = PagAmicoResponse.TryParse("{\"response\":\"IN\",\"serialNumber\":\"33004724002\",\"committedAmount\":3.5,\" AmountResettedBanknotesInBTA\":7}")!;
        Eq("33004724002", legacy.SerialNumber, "serialNumber (FW < 8.71) mappato");
        Check(legacy.CommittedAmount == 3.5m, "committedAmount corretto");
        Check(legacy.AmountResettedBanknotesInBta == 7m, "chiave con spazio iniziale gestita");

        Check(PagAmicoResponse.TryParse("CMD ERROR") is null, "testo non JSON restituisce null");

        Eq("Monete insufficienti per effettuare il pagamento", PagAmicoErrorCodes.Describe("E303"), "descrizione E303");
        Check(PagAmicoErrorCodes.ParseState("99") == MachineState.Busy, "errorType 99 = occupato");
    }

    // ---------------------------------------------------------------- stampa

    private static void PrinterTests()
    {
        Section("Stampa");

        Eq("PTSTST", PagAmicoPrint.BeginPrint(), "inizio stampa");
        Eq("PTSTEN", PagAmicoPrint.EndPrint(), "fine stampa");
        Eq("PTBDON", PagAmicoPrint.Bold(true), "grassetto on");
        Eq("PTJTCE", PagAmicoPrint.Align(PrintAlignment.Center), "allineamento centro");
        Eq("PTFAHW", PagAmicoPrint.Font(PrinterFont.A, PrinterFontMode.DoubleHeightWidth), "font A doppia altezza/larghezza");
        Eq("PTLFNR03", PagAmicoPrint.LineFeed(3), "avanzamento 3 righe");
        Eq("PTUN01", PagAmicoPrint.SetUnderline(Underline.Single), "sottolineato 1");
        Eq("PTPRWLTotale 10,50", PagAmicoPrint.WriteLine("Totale 10,50"), "stampa riga");
        Eq("PTPRRL32-", PagAmicoPrint.RepeatCharLine('-', 32), "riga di separazione");
        Eq("PTQRQR42www.pagamico.it", PagAmicoPrint.QrCode("www.pagamico.it", 4, 2), "QR code (esempio del manuale)");
        Eq("PTBCBC210029876543210123", PagAmicoPrint.Barcode(BarcodeType.Ean13, "876543210123", 100, BarcodeTextPosition.Below, 9),
            "barcode EAN13 (esempio del manuale)");

        var job = new PagAmicoPrintJob().Align(PrintAlignment.Center).Bold().Line("SCONTRINO").Cut();
        var cmds = job.Build();
        Check(cmds.First() == "PTSTST" && cmds.Last() == "PTSTEN", "il job e' delimitato da PTSTST/PTSTEN");
        Check(cmds.Count == 6, "numero comandi del job");

        var ok = PrinterStatus.Parse(PagAmicoResponse.TryParse("{\"response\":\"OK\",\"errorType\":\"00000000\"}")!);
        Check(ok.Installed && ok.CanPrint && !ok.PaperEmpty, "stato stampante OK");

        var ko = PrinterStatus.Parse(PagAmicoResponse.TryParse("{\"response\":\"OK\",\"errorType\":\"10100010\"}")!);
        Check(ko.Printing && ko.CoverOpen && !ko.PaperEmpty && !ko.PaperLow, "stato stampante 10100010: b7 e b1 attivi");

        var none = PrinterStatus.Parse(PagAmicoResponse.TryParse("{\"response\":\"ER\",\"errorType\":\"NO PRINTER\"}")!);
        Check(!none.Installed, "stampante non installata");
    }

    // ---------------------------------------------------------------- display

    private static void DisplayTests()
    {
        Section("Display");

        Eq("DT|ESEMPIO FINESTRA CON TESTO|38|1|0|",
            PagAmicoDisplay.ShowText("ESEMPIO FINESTRA CON TESTO", DisplayPosition.Top, 38, FontStyle.Bold, FontColor.Blue),
            "DT finestra in alto (esempio del manuale)");

        Eq("DM|29|0|2|Selezionare la forma di pagamento|Contanti|POS|ANNULLA",
            PagAmicoDisplay.MessageBox("Selezionare la forma di pagamento", "Contanti", "POS", "ANNULLA", 29, FontStyle.Normal, FontColor.Green),
            "DM messagebox (esempio del manuale)");

        Eq("DM|29|0|2|Confermi?|SI|NO|[]",
            PagAmicoDisplay.MessageBox("Confermi?", "SI", "NO", "", 29, FontStyle.Normal, FontColor.Green),
            "DM terzo bottone nascosto");

        Eq("QR|Avvicinare il QR code al lettore ottico|16|1|2|0",
            PagAmicoDisplay.ReadCode("Avvicinare il QR code al lettore ottico", KeyboardMode.ShowKeyboard, 16, FontStyle.Bold, FontColor.Green),
            "QR lettura codice (esempio del manuale)");

        var layout = new ListLayout
        {
            Title = ListTextProperties.Create("ELENCO PRODOTTI ACQUISTATI", 28, FontStyle.Bold, TextAlignment.Center, "FF2E86C1", "FFFFFFFF"),
            Body = { ["a"] = ListCell.Create("Descrizione"), ["b"] = ListCell.Create("Quantita") }
        };
        var json = layout.ToCompactJson();
        Check(json.StartsWith("{\"listView\":{\"titleProperties\":{\"tX\":\"ELENCO PRODOTTI ACQUISTATI\""), "JSON struttura lista compatto");
        Check(!json.Contains(" \"") && !json.Contains("\n"), "JSON senza spazi ne' a capo");
        Check(PagAmicoDisplay.ListLayout(json).StartsWith("TS{"), "comando TS");

        var data = new ListData
        {
            Rows = { new ListRow { A = "01/01/2025", B = "Acquisto merci", C = 120.50m, D = 0.00m } },
            Footer = new ListFooter { Description = "Totale", Total1 = 200.50m, Total2 = 200.00m, Total3 = 0.50m }
        };
        Check(PagAmicoDisplay.ListData(data.ToCompactJson()).StartsWith("ID{\"movimenti\":["), "comando ID");

        Throws(() => PagAmicoDisplay.ListLayout(new string('x', 4000)), "JSON oltre 3999 caratteri deve fallire");
    }

    // ---------------------------------------------------------------- infrastruttura

    internal static void Section(string name) => Console.WriteLine($"\n--- {name} ---");

    internal static void Eq(string expected, string? actual, string what)
    {
        if (string.Equals(expected, actual, StringComparison.Ordinal)) Pass(what);
        else Fail($"{what}\n      atteso : '{expected}'\n      ottenuto: '{actual}'");
    }

    internal static void Check(bool condition, string what)
    {
        if (condition) Pass(what);
        else Fail(what);
    }

    private static void Throws(Action action, string what)
    {
        try
        {
            action();
            Fail($"{what} (nessuna eccezione)");
        }
        catch (Exception)
        {
            Pass(what);
        }
    }

    private static void Pass(string what)
    {
        _passed++;
        Console.WriteLine($"  OK   {what}");
    }

    private static void Fail(string what)
    {
        _failed++;
        Failures.Add(what);
        Console.WriteLine($"  FAIL {what}");
    }
}
