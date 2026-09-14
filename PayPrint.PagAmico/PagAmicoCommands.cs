using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PayPrint.PagAmico;

/// <summary>Fondo cassa da aggiornare con il comando [AF].</summary>
public enum CashFloatTarget
{
    Coins = 1,
    Banknotes = 2,
    Both = 9
}

/// <summary>Soglia di scorta minima/massima per un taglio ([SM] / [SB]).</summary>
public readonly struct StockThreshold
{
    public StockThreshold(int min, int max)
    {
        if (min is < 0 or > 999) throw new ArgumentOutOfRangeException(nameof(min), "0..999");
        if (max is < 0 or > 999) throw new ArgumentOutOfRangeException(nameof(max), "0..999");
        if (min > max) throw new ArgumentException("La scorta minima non puo' superare la massima (ERRSCOMIN>SCOMAX)");
        Min = min;
        Max = max;
    }

    public int Min { get; }
    public int Max { get; }

    public override string ToString() => Min.ToString("D3") + Max.ToString("D3");
}

/// <summary>Abilitazione di un taglio in incasso ed erogazione ([EM] / [EB]).</summary>
public readonly struct DenominationToggle
{
    public DenominationToggle(bool acceptOnCollect, bool dispenseAsChange)
    {
        AcceptOnCollect = acceptOnCollect;
        DispenseAsChange = dispenseAsChange;
    }

    /// <summary>Prima cifra: il taglio e' accettato in incasso.</summary>
    public bool AcceptOnCollect { get; }

    /// <summary>Seconda cifra: il taglio e' erogabile come resto.</summary>
    public bool DispenseAsChange { get; }

    public static DenominationToggle All => new(true, true);
    public static DenominationToggle None => new(false, false);

    public override string ToString() => (AcceptOnCollect ? "1" : "0") + (DispenseAsChange ? "1" : "0");
}

/// <summary>Causali dei movimenti per il comando [MV].</summary>
public static class MovementCause
{
    public const string All = "000";
    public const string Collection = "001";           // Incasso
    public const string Payment = "010";              // Pagamento
    public const string CoinUnload = "011";           // Scarico Monete
    public const string CoinReload = "021";           // Ricarica Monete
    public const string BanknoteReload = "031";       // Ricarica Banconote
    public const string BanknoteUnload = "041";       // Scarico Banconote
    public const string BanknoteUnloadBta = "051";    // Scarico Banconote in BTA
    public const string BanknoteUnloadAlt = "061";    // Scarico Banconote (2a codifica presente a manuale)
    public const string CoinUnloadAlt = "071";        // Scarico Monete (2a codifica presente a manuale)
    public const string PosCollection = "090";        // Incasso POS
}

/// <summary>
/// Costruttori delle stringhe di comando del protocollo TCP-IP pagAmico.
/// <para>ATTENZIONE: nei comandi gli importi sono in CENTESIMI a lunghezza fissa; nelle risposte JSON sono in EURO.</para>
/// </summary>
public static class PagAmicoCommands
{
    /// <summary>Converte euro in centesimi con arrotondamento commerciale.</summary>
    public static long ToCents(decimal euro) => (long)Math.Round(euro * 100m, MidpointRounding.AwayFromZero);

    private static string Fixed(long value, int digits)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Importo/quantita' negativa non ammessa");
        var s = value.ToString(CultureInfo.InvariantCulture);
        if (s.Length > digits)
            throw new ArgumentOutOfRangeException(nameof(value), $"Valore {value} eccede le {digits} cifre previste dal protocollo");
        return s.PadLeft(digits, '0');
    }

    private static string Password(string? password)
    {
        if (string.IsNullOrEmpty(password)) return string.Empty;
        if (password!.Length > 19) throw new ArgumentException("La password di autorizzazione ha massimo 19 caratteri", nameof(password));
        return password;
    }

    // ---- incasso ----------------------------------------------------------

    /// <summary>[IN] Incasso contanti. INxxxxxx (6 cifre, centesimi).</summary>
    public static string Collect(decimal amountEuro) => "IN" + Fixed(ToCents(amountEuro), 6);

    /// <summary>[I2] Incasso con timeout. I2tttxxxxxx.</summary>
    public static string CollectWithTimeout(int seconds, decimal amountEuro)
    {
        if (seconds is < 0 or > 999) throw new ArgumentOutOfRangeException(nameof(seconds), "0..999 secondi");
        return "I2" + Fixed(seconds, 3) + Fixed(ToCents(amountEuro), 6);
    }

    /// <summary>[PO] Incasso tramite POS integrato. POxxxxxx.</summary>
    public static string CollectPos(decimal amountEuro) => "PO" + Fixed(ToCents(amountEuro), 6);

    /// <summary>[IM] Incasso automatico contanti/POS (FW &gt;= 8.71). IMxxxxxx.</summary>
    public static string CollectAuto(decimal amountEuro) => "IM" + Fixed(ToCents(amountEuro), 6);

    /// <summary>[AN] Annulla l'operazione in corso.</summary>
    public static string Cancel() => "AN";

    /// <summary>[CM] Chiude l'incasso trattenendo il parziale.</summary>
    public static string Commit() => "CM";

    // ---- erogazione -------------------------------------------------------

    /// <summary>[PA] Eroga un importo. PAxxxxxxxxxx + password (10 cifre, centesimi).</summary>
    public static string Dispense(decimal amountEuro, string password = "") =>
        "PA" + Fixed(ToCents(amountEuro), 10) + Password(password);

    /// <summary>[P2] Eroga banconote per taglio. P2 aaa bbb ccc ddd eee fff + password.</summary>
    public static string DispenseBanknotes(int n5, int n10, int n20, int n50, int n100, int n200, string password = "") =>
        "P2" + Counts3(n5, n10, n20, n50, n100, n200) + Password(password);

    /// <summary>[PM] Eroga monete per taglio (0,05 / 0,10 / 0,20 / 0,50 / 1,00 / 2,00). PM aaa bbb ccc ddd eee fff + password.</summary>
    public static string DispenseCoins(int c05, int c10, int c20, int c50, int c100, int c200, string password = "") =>
        "PM" + Counts3(c05, c10, c20, c50, c100, c200) + Password(password);

    /// <summary>[M2] Sposta banconote nel cassetto BTA. M2 aaa bbb ccc ddd eee fff + password.</summary>
    public static string MoveBanknotesToBta(int n5, int n10, int n20, int n50, int n100, int n200, string password = "") =>
        "M2" + Counts3(n5, n10, n20, n50, n100, n200) + Password(password);

    /// <summary>[MF] Sposta monete nel cassetto di recupero. MF aaa bbb ccc ddd eee fff + password.</summary>
    public static string MoveCoinsToCashbox(int c05, int c10, int c20, int c50, int c100, int c200, string password = "") =>
        "MF" + Counts3(c05, c10, c20, c50, c100, c200) + Password(password);

    private static string Counts3(params int[] counts) =>
        string.Concat(counts.Select(c => Fixed(c, 3)));

    // ---- fondo cassa e configurazione ------------------------------------

    /// <summary>[AF] Aggiorna fondo cassa. AFn + password.</summary>
    public static string UpdateCashFloat(CashFloatTarget target, string password = "") =>
        "AF" + (int)target + Password(password);

    /// <summary>[BT] Azzera cassetto BTA. BT + password.</summary>
    public static string ResetBta(string password = "") => "BT" + Password(password);

    /// <summary>[AZ] Azzera banconote presenti (solo modelli a due cassetti). AZ2 + password.</summary>
    public static string ResetBanknotes(string password = "") => "AZ2" + Password(password);

    /// <summary>[SM] Scorta minima/massima monete: 6 tagli nell'ordine 0,05 0,10 0,20 0,50 1,00 2,00.</summary>
    public static string SetCoinStock(IEnumerable<StockThreshold> thresholds) => "SM" + Thresholds(thresholds);

    /// <summary>[SB] Scorta minima/massima banconote: 6 tagli nell'ordine 5 10 20 50 100 200.</summary>
    public static string SetBanknoteStock(IEnumerable<StockThreshold> thresholds) => "SB" + Thresholds(thresholds);

    private static string Thresholds(IEnumerable<StockThreshold> thresholds)
    {
        var list = thresholds?.ToList() ?? throw new ArgumentNullException(nameof(thresholds));
        if (list.Count != 6) throw new ArgumentException("Servono esattamente 6 soglie (una per taglio)", nameof(thresholds));
        return string.Concat(list.Select(t => t.ToString()));
    }

    /// <summary>[EM] Abilita/disabilita tagli monete: 6 tagli 0,05 0,10 0,20 0,50 1,00 2,00.</summary>
    public static string EnableCoins(IEnumerable<DenominationToggle> toggles) => "EM" + Toggles(toggles);

    /// <summary>[EB] Abilita/disabilita tagli banconote: 6 tagli 5 10 20 50 100 200.</summary>
    public static string EnableBanknotes(IEnumerable<DenominationToggle> toggles) => "EB" + Toggles(toggles);

    private static string Toggles(IEnumerable<DenominationToggle> toggles)
    {
        var list = toggles?.ToList() ?? throw new ArgumentNullException(nameof(toggles));
        if (list.Count != 6) throw new ArgumentException("Servono esattamente 6 abilitazioni (una per taglio)", nameof(toggles));
        return string.Concat(list.Select(t => t.ToString()));
    }

    // ---- ricariche --------------------------------------------------------

    /// <summary>[RM] senza aggiornamento fondo cassa, [R3] con aggiornamento.</summary>
    public static string ReloadCoins(bool updateCashFloat) => updateCashFloat ? "R3" : "RM";

    /// <summary>[RB] senza aggiornamento fondo cassa, [R2] con aggiornamento.</summary>
    public static string ReloadBanknotes(bool updateCashFloat) => updateCashFloat ? "R2" : "RB";

    /// <summary>[RC]/[RS] ricarica mista; [VC]/[VS] la variante che invia i parziali al client.</summary>
    public static string ReloadMixed(bool updateCashFloat, bool sendPartials = false) =>
        sendPartials
            ? (updateCashFloat ? "VS" : "VC")
            : (updateCashFloat ? "RS" : "RC");

    /// <summary>[FR] Fine ricarica.</summary>
    public static string ReloadEnd() => "FR";

    // ---- stato / servizio -------------------------------------------------

    /// <summary>[ST] Richiesta situazione.</summary>
    public static string Status() => "ST";

    /// <summary>[CL] Pulisce il display.</summary>
    public static string ClearDisplay() => "CL";

    /// <summary>[RI] Riavvia il pagAmico.</summary>
    public static string Reboot() => "RI";

    /// <summary>[LO] Rinvio dell'ultimo JSON trasmesso.</summary>
    public static string LastJson() => "LO";

    /// <summary>[SR] Rimuove l'immagine temporanea.</summary>
    public static string RemoveTempImage() => "SR";

    // ---- POS --------------------------------------------------------------

    /// <summary>[PL] Ultima transazione POS: PLT ristampa lo scontrino, PLF no.</summary>
    public static string PosLastTransaction(bool reprintReceipt) => reprintReceipt ? "PLT" : "PLF";

    /// <summary>[PR] Totali POS.</summary>
    public static string PosTotals() => "PR";

    /// <summary>[PS] Chiusura giornaliera POS.</summary>
    public static string PosDailyClose() => "PS";

    /// <summary>
    /// [PZ] Riavvio POS. NB: il manuale rev. 2.33 elenca PZ nella tabella comandi ma nel dettaglio
    /// del paragrafo 2.30 riporta "PR": PZ e' il codice corretto secondo la tabella riepilogativa.
    /// </summary>
    public static string PosReboot() => "PZ";

    /// <summary>[PP] Primo DLL POS.</summary>
    public static string PosFirstDll() => "PP";

    // ---- movimenti --------------------------------------------------------

    /// <summary>
    /// [MV] Elenco movimenti: MV + "yyyy/MM/dd HH:mm" iniziale + "yyyy/MM/dd HH:mm" finale + causale (3 cifre).
    /// </summary>
    public static string Movements(DateTime from, DateTime to, string causale = MovementCause.All)
    {
        if (causale is null || causale.Length != 3 || !causale.All(char.IsDigit))
            throw new ArgumentException("La causale deve essere di 3 cifre (es. \"000\")", nameof(causale));

        var sb = new StringBuilder("MV");
        sb.Append(from.ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture));
        sb.Append(to.ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture));
        sb.Append(causale);
        return sb.ToString();
    }

    /// <summary>[MI] Movimento per Id: MI + 9 cifre.</summary>
    public static string MovementById(long id) => "MI" + Fixed(id, 9);
}
