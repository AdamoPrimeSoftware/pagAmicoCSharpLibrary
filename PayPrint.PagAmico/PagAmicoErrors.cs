using System;
using System.Collections.Generic;
using System.Linq;

namespace PayPrint.PagAmico;

/// <summary>Codici di errore restituiti nel campo "errorCode" (manuale cap. 4).</summary>
public static class PagAmicoErrorCodes
{
    public const string CommandNotExecutable = "E100"; // vedi errorType per lo stato macchina
    public const string AcceptorsEmpty = "E200";
    public const string BanknoteStackerEmpty = "E201";
    public const string CoinHopperEmpty = "E202";
    public const string AmountOverDispenseLimit = "E300";
    public const string AmountOverAvailability = "E301";
    public const string NotEnoughBanknotes = "E302";
    public const string NotEnoughCoins = "E303";
    public const string PosNotResponding = "E500";

    // codici testuali (comandi di erogazione / setup)
    public const string DispenseDisabled = "DISPAG";
    public const string WrongPassword = "ERRPWDPAG";
    public const string WrongPasswordAlt = "ERPWDPAG";   // variante presente sui comandi P2 / M2
    public const string WrongLength = "ERRLUNGHEZZA";
    public const string BanknoteQty = "QTABANCONOTE";
    public const string BanknoteQtyDispenser1 = "QTABANCONOTE1";
    public const string BanknoteQtyDispenser2 = "QTABANCONOTE2";
    public const string CoinQty = "QTAMONETE";
    public const string MaxDispensable = "MASSIMOEROGABILE";
    public const string NotAvailable = "ERRNONDISP";
    public const string MaxStockError = "ERRSCOMAX";
    public const string MinOverMaxStock = "ERRSCOMIN>SCOMAX";

    // FW 8.71+: modalita' EMERGENZA (solo POS)
    public const string AcceptorsNotStarted = "NOT STARTED";
    public const string PosOnly = "SOLO POS";

    // esito comando IM (incasso automatico contanti/POS)
    public const string PaidWithCash = "CONT";
    public const string PaidWithPos = "POS";
    public const string PosTransactionRefused = "POS ERROR";

    private static readonly Dictionary<string, string> Descriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        [CommandNotExecutable] = "Comando non eseguibile (vedi errorType per lo stato macchina)",
        [AcceptorsEmpty] = "Accettatori vuoti",
        [BanknoteStackerEmpty] = "Stacker banconote vuoto",
        [CoinHopperEmpty] = "Hopper monete vuoto",
        [AmountOverDispenseLimit] = "Importo superiore al limite erogabile",
        [AmountOverAvailability] = "Importo superiore alla disponibilita'",
        [NotEnoughBanknotes] = "Banconote insufficienti per effettuare il pagamento",
        [NotEnoughCoins] = "Monete insufficienti per effettuare il pagamento",
        [PosNotResponding] = "POS non risponde",
        [DispenseDisabled] = "Erogazione disabilitata nel setup",
        [WrongPassword] = "Password di erogazione errata",
        [WrongPasswordAlt] = "Password di erogazione errata",
        [WrongLength] = "Formato/lunghezza del comando errata",
        [BanknoteQty] = "Quantita' banconote errata o superiore alla disponibilita'",
        [BanknoteQtyDispenser1] = "Banconote insufficienti nel primo erogatore",
        [BanknoteQtyDispenser2] = "Banconote insufficienti nel secondo erogatore",
        [CoinQty] = "Quantita' monete errata",
        [MaxDispensable] = "Importo superiore al massimo erogabile",
        [NotAvailable] = "pagAmico non disponibile",
        [MaxStockError] = "Scorta massima errata",
        [MinOverMaxStock] = "Scorta minima superiore alla scorta massima",
        [AcceptorsNotStarted] = "Accettatori non operativi: macchina in modalita' EMERGENZA (solo POS)",
        [PosOnly] = "Macchina operativa solo con POS"
    };

    public static string Describe(string? code)
    {
        // variabile locale non nullable: sui framework legacy il compilatore non conosce
        // l'annotazione [NotNullWhen] di string.IsNullOrWhiteSpace
        var value = code ?? string.Empty;
        if (value.Trim().Length == 0) return string.Empty;

        if (Descriptions.TryGetValue(value, out var d)) return d;
        if (value.StartsWith("QTAMONETE", StringComparison.OrdinalIgnoreCase))
            return $"Quantita' monete insufficiente per il taglio {value.Substring("QTAMONETE".Length)}";
        return value;
    }

    /// <summary>Interpreta errorType quando errorCode = "E100".</summary>
    public static MachineState ParseState(string? errorType) =>
        int.TryParse(errorType, out var n) && Enum.IsDefined(typeof(MachineState), n)
            ? (MachineState)n
            : MachineState.Unknown;
}

/// <summary>
/// Risposte che il pagAmico invia in chiaro, non incapsulate in un JSON
/// (guida pagAmico Dev Kit 1.0, cap. 10.4 e manuale protocollo di stampa).
/// </summary>
public static class PagAmicoTextResponses
{
    public const string CommandError = "CMD ERROR";
    public const string MvDateInvalid = "CMD ERROR, DATE INVALID";
    public const string MvCodeOperationInvalid = "CMD ERROR, CODE OPERATION INVALID";
    public const string MvLengthError = "CMD LENGHT ERROR";
    public const string MiFormatError = "CMD ERROR, FORMAT ERROR";
    public const string PosDisabled = "POS DISABLED";
    public const string PrinterBusy = "ER BUSY";
    public const string PrinterNothingToCancel = "ER NO-PRINT";
    public const string PrinterCommandError = "ER CMD-ERROR";

    /// <summary>Macchina impegnata: risposta a un comando che non puo' eseguire (per esempio durante un incasso).</summary>
    public const string Busy = "BUSY";

    /// <summary>Risposta del display al MessageBox [DM]: bottone premuto.</summary>
    public const string ButtonPrefix = "BT";

    /// <summary>Risposta di annullo di [DM], [DI] e [QR].</summary>
    public const string Cancelled = "AN";

    /// <summary>Uscita dalla lista [ID].</summary>
    public const string ListExit = "EX";

    private static readonly Dictionary<string, string> Descriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        [CommandError] = "Comando inesistente o formato non valido",
        [MvDateInvalid] = "Date non valide nel comando MV",
        [MvCodeOperationInvalid] = "Causale non valida nel comando MV",
        [MvLengthError] = "Lunghezza del comando MV non corretta",
        [MiFormatError] = "Formato dell'Id non valido nel comando MI",
        [PosDisabled] = "POS disabilitato nel setup del pagAmico",
        [PrinterBusy] = "Stampa gia' in corso: riprovare",
        [PrinterNothingToCancel] = "Ricevuto PTSTAN ma nessuna stampa era in corso",
        [PrinterCommandError] = "Comando di stampa non riconosciuto",
        [Busy] = "Macchina impegnata: comando non eseguito"
    };

    public static string Describe(string? raw)
    {
        var t = (raw ?? string.Empty).Trim();
        if (t.Length == 0) return string.Empty;
        if (Descriptions.TryGetValue(t, out var d)) return d;
        var match = Descriptions.Keys.FirstOrDefault(k => t.StartsWith(k, StringComparison.OrdinalIgnoreCase));
        return match is null ? t : Descriptions[match];
    }
}

/// <summary>Comandi che non producono alcuna risposta (guida Dev Kit, cap. 12).</summary>
public static class PagAmicoSilentCommands
{
    private static readonly string[] Prefixes = { "CL", "DS", "DC", "QA", "CO", "DT", "DG", "TS" };

    public static bool IsSilent(string? command) =>
        !string.IsNullOrEmpty(command) &&
        Prefixes.Any(p => command!.StartsWith(p, StringComparison.Ordinal));
}

/// <summary>Stato macchina restituito nel campo errorType quando errorCode = "E100".</summary>
public enum MachineState
{
    Unknown = -1,
    OutOfService = 0,
    Ok = 1,
    Starting = 2,
    SetupOrMaintenance = 3,
    Rebooting = 5,
    Busy = 99
}

/// <summary>
/// Parser della stringa "errorList" (formato E + 4 cifre, es. "E0090").
/// Cifra 1 = esito pagamento, 2 = scorta monete, 3 = scorta banconote, 4 = cassetto BTA.
/// </summary>
public readonly struct PagAmicoErrorList
{
    public static readonly PagAmicoErrorList Empty = new(null, 0, 0, 0, 0);

    private PagAmicoErrorList(string? raw, int payment, int coins, int banknotes, int bta)
    {
        Raw = raw;
        PaymentDigit = payment;
        CoinsDigit = coins;
        BanknotesDigit = banknotes;
        BtaDigit = bta;
    }

    public string? Raw { get; }

    /// <summary>0 = ok; 1 = importo superiore alla disponibilita' di monete; 2 = importo non erogabile; ...</summary>
    public int PaymentDigit { get; }

    /// <summary>0 = ok; 1 = monete sottoscorta; 2 = troppe monete; 9 = monete esaurite.</summary>
    public int CoinsDigit { get; }

    /// <summary>0 = ok; 1 = sottoscorta; 2 = troppe banconote; 5 = entrambi i cassetti vuoti; 6 = un cassetto vuoto; 9 = un taglio esaurito.</summary>
    public int BanknotesDigit { get; }

    /// <summary>0 = ok; 1 = BTA prossimo al riempimento; 2 = BTA pieno.</summary>
    public int BtaDigit { get; }

    public bool CoinsBelowMinimum => CoinsDigit == 1;
    public bool TooManyCoins => CoinsDigit == 2;      // richiede scarico monete
    public bool CoinsEmpty => CoinsDigit == 9;
    public bool BanknotesBelowMinimum => BanknotesDigit == 1;
    public bool TooManyBanknotes => BanknotesDigit == 2;
    public bool BanknotesEmpty => BanknotesDigit == 9;
    public bool BtaAlmostFull => BtaDigit == 1;
    public bool BtaFull => BtaDigit == 2;

    public bool RequiresOperatorAttention =>
        PaymentDigit != 0 || CoinsDigit != 0 || BanknotesDigit != 0 || BtaDigit != 0;

    public static PagAmicoErrorList Parse(string? errorList)
    {
        var s = (errorList ?? string.Empty).Trim();
        if (s.Length == 0) return Empty;
        if (s.Length < 5 || (s[0] != 'E' && s[0] != 'e')) return new PagAmicoErrorList(errorList, 0, 0, 0, 0);

        int D(int i) => i < s.Length && char.IsDigit(s[i]) ? s[i] - '0' : 0;
        return new PagAmicoErrorList(errorList, D(1), D(2), D(3), D(4));
    }

    /// <summary>Descrizioni leggibili delle sole condizioni anomale.</summary>
    public IReadOnlyList<string> Warnings()
    {
        var list = new List<string>();
        switch (PaymentDigit)
        {
            case 1: list.Add("Importo superiore alla disponibilita' di monete"); break;
            case 2: list.Add("Importo non erogabile"); break;
            case 0: break;
            default: list.Add($"Errore pagamento (codice {PaymentDigit})"); break;
        }
        switch (CoinsDigit)
        {
            case 1: list.Add("Monete sottoscorta"); break;
            case 2: list.Add("Troppe monete: eseguire scarico monete"); break;
            case 9: list.Add("Monete esaurite"); break;
        }
        switch (BanknotesDigit)
        {
            case 1: list.Add("Banconote sottoscorta"); break;
            case 2: list.Add("Troppe banconote: eseguire scarico banconote"); break;
            case 5: list.Add("Entrambi i cassetti vuoti ma numero banconote > 0"); break;
            case 6: list.Add("Uno dei cassetti vuoto ma numero banconote > 0"); break;
            case 9: list.Add("Uno dei tagli di banconote esaurito"); break;
        }
        switch (BtaDigit)
        {
            case 1: list.Add("Cassetto BTA prossimo al riempimento"); break;
            case 2: list.Add("Cassetto BTA pieno"); break;
        }
        return list;
    }

    public override string ToString() => Raw ?? "E0000";
}

/// <summary>Eccezione sollevata quando il pagAmico risponde con un errore o il comando non e' valido.</summary>
public class PagAmicoException : Exception
{
    public PagAmicoException(string message, PagAmicoFrame? frame = null) : base(message)
    {
        Frame = frame;
    }

    public PagAmicoFrame? Frame { get; }

    public string? ErrorCode => Frame?.Json?.ErrorCode;

    public string? ErrorType => Frame?.Json?.ErrorType;

    public static PagAmicoException FromFrame(PagAmicoFrame frame)
    {
        if (frame.IsJson && frame.Json is not null)
        {
            var code = frame.Json.ErrorCode ?? string.Empty;
            var desc = PagAmicoErrorCodes.Describe(code);
            var type = frame.Json.ErrorType;
            var msg = string.IsNullOrEmpty(code)
                ? $"pagAmico ha risposto ER (errorType={type})"
                : $"pagAmico ha risposto ER: {code} - {desc}" + (string.IsNullOrEmpty(type) ? "" : $" (errorType={type})");
            return new PagAmicoException(msg, frame);
        }
        return new PagAmicoException($"pagAmico ha risposto: {frame.Raw}", frame);
    }
}

/// <summary>Timeout in attesa della risposta del pagAmico.</summary>
public sealed class PagAmicoTimeoutException : PagAmicoException
{
    public PagAmicoTimeoutException(string message) : base(message) { }
}

/// <summary>
/// La macchina ha rifiutato l'incasso prima di accettarlo (testo o ER prima dell'OK):
/// <b>non ha incassato nulla</b>.
/// </summary>
public class PagAmicoRejectedException : PagAmicoException
{
    public PagAmicoRejectedException(string message, PagAmicoFrame? frame = null) : base(message, frame) { }
}

/// <summary>
/// La macchina ha rifiutato l'incasso perche' impegnata (BUSY, oppure ER con E100 / errorType 99):
/// <b>non ha incassato nulla</b>.
/// </summary>
public sealed class PagAmicoBusyException : PagAmicoRejectedException
{
    public PagAmicoBusyException(string message, PagAmicoFrame? frame = null) : base(message, frame) { }
}

/// <summary>
/// Connessione caduta mentre si attendeva una risposta. Se <see cref="MayBeCollecting"/> e' vero
/// l'incasso era gia' stato accettato: <b>la macchina potrebbe stare ancora incassando</b>.
/// </summary>
public sealed class PagAmicoConnectionLostException : PagAmicoException
{
    public PagAmicoConnectionLostException(string message, bool mayBeCollecting) : base(message)
    {
        MayBeCollecting = mayBeCollecting;
    }

    public bool MayBeCollecting { get; }
}

/// <summary>
/// Comando non inviato perche' c'e' un incasso aperto: durante [IN] la macchina accetta solo [AN] e [CM]
/// (risposta PayPrint dell'11/09/2026). Nulla e' stato trasmesso.
/// </summary>
public sealed class PagAmicoCollectionOpenException : PagAmicoException
{
    public PagAmicoCollectionOpenException(string command)
        : base($"Incasso aperto: '{command}' non inviato, durante l'incasso la macchina accetta solo AN e CM")
    {
        Command = command;
    }

    public string Command { get; }
}
