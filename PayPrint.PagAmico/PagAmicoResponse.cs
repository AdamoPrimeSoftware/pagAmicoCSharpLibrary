using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace PayPrint.PagAmico;

/// <summary>
/// Risposta JSON del pagAmico (protocollo TCP-IP rev. 2.33 / FW 8.72).
/// <para>
/// Il parsing e' volutamente "difensivo": le chiavi vengono normalizzate
/// (case-insensitive, senza underscore e spazi) perche' la documentazione e i firmware
/// usano nomi diversi per lo stesso campo:
/// serialNumber/sN, committedAmount/committedAmout, PosFinancial.../posFinancial...,
/// " AmountResettedBanknotesInBTA" con spazio iniziale.
/// </para>
/// <para>ATTENZIONE: gli importi in RISPOSTA sono in EURO (es. 101.55), i comandi in RICHIESTA sono in CENTESIMI.</para>
/// </summary>
public sealed class PagAmicoResponse
{
    private static readonly int[] CoinCentsByIndex = { 0, 1, 2, 5, 10, 20, 50, 100, 200, 0 };
    private static readonly int[] NoteEuroByStockIndex = { 0, 5, 10, 20, 50, 100, 200 };
    private static readonly int[] NoteEuroByBtaIndex = { 0, 0, 5, 10, 20, 50, 100, 200 };

    private PagAmicoResponse(string rawJson, IReadOnlyDictionary<string, JsonElement> fields)
    {
        RawJson = rawJson;
        Fields = fields;
    }

    /// <summary>JSON originale, utile per log e per campi non mappati.</summary>
    public string RawJson { get; private init; } = string.Empty;

    /// <summary>Tutti i campi, con chiave normalizzata (minuscolo, senza spazi/underscore).</summary>
    public IReadOnlyDictionary<string, JsonElement> Fields { get; private init; }

    // ---- campi principali -------------------------------------------------

    public string? Response { get; private set; }
    public decimal? AmountRequested { get; private set; }
    public decimal? AmountToCollect { get; private set; }
    public decimal? CollectedAmount { get; private set; }
    public decimal? CollectedCoins { get; private set; }
    public decimal? CollectedBanknotes { get; private set; }

    /// <summary>Importo NON erogato come resto (mancanza tagli). Va segnalato all'operatore.</summary>
    public decimal? AmountUnpaid { get; private set; }

    public decimal? ChangeCoins { get; private set; }
    public decimal? ChangeBanknotes { get; private set; }
    public decimal? AmountPaid { get; private set; }

    public string? ErrorCode { get; private set; }
    public string? ErrorType { get; private set; }

    /// <summary>Stringa stato macchina, formato E + 4 cifre (vedi <see cref="PagAmicoErrorList"/>).</summary>
    public string? ErrorList { get; private set; }

    public int[] Coins { get; private set; } = Array.Empty<int>();
    public int[] CoinsLimits { get; private set; } = Array.Empty<int>();
    public int[][] BankNotes { get; private set; } = Array.Empty<int[]>();
    public int[] BankNotesBta { get; private set; } = Array.Empty<int>();
    public int[] BankNotesInStock { get; private set; } = Array.Empty<int>();
    public int[] CoinsInStock { get; private set; } = Array.Empty<int>();

    public double? FirmwareVersion { get; private set; }

    /// <summary>Matricola. FW &gt;= 8.71 la espone come "sN", prima come "serialNumber".</summary>
    public string? SerialNumber { get; private set; }

    public decimal? ChangeReturn { get; private set; }

    /// <summary>"TRUE" se l'incasso e' stato interrotto dal pannello del pagAmico.</summary>
    public string? Halted { get; private set; }

    public bool IsHalted => string.Equals(Halted, "TRUE", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Scontrino / messaggio di fine transazione POS. Contiene caratteri di controllo ASCII
    /// codificati come sequenze uXXXX: STX (u0002) apre il blocco, ETX (u0003) lo chiude,
    /// ETB (u0017) marca la fine del blocco di trasmissione.
    /// </summary>
    public string? PosFinancialTransactionEndResponseMessage { get; private set; }

    /// <summary>0 = nessun problema, 9 = probabile banconota doppia, n = numero banconote spostate in BTA.</summary>
    public int? NoteCollectedBta { get; private set; }

    /// <summary>Modello: 2B, 3S, 4B, 3C.</summary>
    public string? TypePagAmico { get; private set; }

    public decimal? CommittedAmount { get; private set; }
    public decimal? AmountResettedBanknotesInBta { get; private set; }
    public decimal? AmountBanknotesInBta { get; private set; }

    /// <summary>Id univoco del movimento nel database SQLite del pagAmico.</summary>
    public long? Id { get; private set; }

    public decimal? PosTot1 { get; private set; }
    public decimal? PosTot2 { get; private set; }

    /// <summary>Movimenti restituiti dai comandi MV / MI (chiave "root").</summary>
    public IReadOnlyList<PagAmicoMovement> Movements { get; private set; } = Array.Empty<PagAmicoMovement>();

    // ---- helper -----------------------------------------------------------

    public bool IsAck => Response == "OK";
    public bool IsPartial => Response == "p";
    public bool IsError => Response == "ER";

    public PagAmicoErrorList Status => PagAmicoErrorList.Parse(ErrorList);

    /// <summary>Monete disponibili per taglio (chiave = centesimi: 5, 10, 20, 50, 100, 200).</summary>
    public IReadOnlyDictionary<int, int> CoinsByDenomination => MapCoins(Coins);

    /// <summary>Fondo cassa monete per taglio (chiave = centesimi).</summary>
    public IReadOnlyDictionary<int, int> CoinsInStockByDenomination => MapCoins(CoinsInStock);

    /// <summary>Soglie minime monete per taglio (chiave = centesimi).</summary>
    public IReadOnlyDictionary<int, int> CoinLimitsByDenomination => MapCoins(CoinsLimits);

    /// <summary>Fondo cassa banconote per taglio (chiave = euro: 5, 10, 20, 50, 100, 200).</summary>
    public IReadOnlyDictionary<int, int> BankNotesInStockByDenomination
    {
        get
        {
            var map = new Dictionary<int, int>();
            for (var i = 1; i < NoteEuroByStockIndex.Length && i < BankNotesInStock.Length; i++)
                map[NoteEuroByStockIndex[i]] = BankNotesInStock[i];
            return map;
        }
    }

    /// <summary>Banconote presenti nel cassetto BTA per taglio (chiave = euro).</summary>
    public IReadOnlyDictionary<int, int> BankNotesBtaByDenomination
    {
        get
        {
            var map = new Dictionary<int, int>();
            for (var i = 2; i < NoteEuroByBtaIndex.Length && i < BankNotesBta.Length; i++)
                map[NoteEuroByBtaIndex[i]] = BankNotesBta[i];
            return map;
        }
    }

    /// <summary>
    /// Cassetti banconote. NB: i tagli possono essere in ordine non crescente e duplicati,
    /// quindi il taglio va SEMPRE letto da <see cref="BanknoteDrawer.Value"/> e mai dedotto dalla posizione.
    /// </summary>
    public IReadOnlyList<BanknoteDrawer> Drawers =>
        BankNotes.Select((row, i) => new BanknoteDrawer(i, row)).ToList();

    /// <summary>
    /// Banconote effettivamente disponibili nei riciclatori, sommate per taglio.
    /// E' l'unico modo corretto di leggerle: i cassetti non sono in ordine e lo stesso taglio
    /// puo' comparire in piu' cassetti (guida Dev Kit, cap. 9.3).
    /// </summary>
    public IReadOnlyDictionary<int, int> BanknotesAvailableByDenomination
    {
        get
        {
            var map = new Dictionary<int, int>();
            foreach (var d in Drawers)
            {
                if (!d.IsConfigured) continue;
                map[d.Value] = map.TryGetValue(d.Value, out var q) ? q + d.Quantity : d.Quantity;
            }
            return map;
        }
    }

    private static IReadOnlyDictionary<int, int> MapCoins(int[] arr)
    {
        var map = new Dictionary<int, int>();
        for (var i = 1; i < CoinCentsByIndex.Length - 1 && i < arr.Length; i++)
        {
            var cents = CoinCentsByIndex[i];
            if (cents > 0) map[cents] = arr[i];
        }
        return map;
    }

    // ---- parsing ----------------------------------------------------------

    /// <summary>Restituisce null se la stringa non e' un oggetto JSON valido.</summary>
    public static PagAmicoResponse? TryParse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            return FromRoot(json, doc.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static PagAmicoResponse FromRoot(string rawJson, JsonElement root)
    {
        var fields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var p in root.EnumerateObject())
            fields[Normalize(p.Name)] = p.Value.Clone();

        var r = new PagAmicoResponse(rawJson, fields)
        {
            Response = Str(fields, "response"),
            AmountRequested = Dec(fields, "amountrequested"),
            AmountToCollect = Dec(fields, "amounttocollect"),
            CollectedAmount = Dec(fields, "collectedamount"),
            CollectedCoins = Dec(fields, "collectedcoins"),
            CollectedBanknotes = Dec(fields, "collectedbanknotes"),
            AmountUnpaid = Dec(fields, "amountunpaid"),
            ChangeCoins = Dec(fields, "changecoins"),
            ChangeBanknotes = Dec(fields, "changebanknotes"),
            AmountPaid = Dec(fields, "amountpaid"),
            ErrorCode = Str(fields, "errorcode"),
            ErrorType = Str(fields, "errortype"),
            ErrorList = Str(fields, "errorlist"),
            Coins = IntArray(fields, "coins"),
            CoinsLimits = IntArray(fields, "coinslimits"),
            BankNotes = IntMatrix(fields, "banknotes"),
            BankNotesBta = IntArray(fields, "banknotesbta"),
            BankNotesInStock = IntArray(fields, "banknotesinstock"),
            CoinsInStock = IntArray(fields, "coinsinstock"),
            FirmwareVersion = Dbl(fields, "firmwarevers"),
            SerialNumber = Str(fields, "sn", "serialnumber"),
            ChangeReturn = Dec(fields, "changereturn"),
            Halted = Str(fields, "halted"),
            PosFinancialTransactionEndResponseMessage = Str(fields, "posfinancialtransactionendresponsemessage"),
            NoteCollectedBta = Int(fields, "notecollectedbta"),
            TypePagAmico = Str(fields, "typepagamico"),
            CommittedAmount = Dec(fields, "committedamount", "committedamout"),
            AmountResettedBanknotesInBta = Dec(fields, "amountresettedbanknotesinbta"),
            AmountBanknotesInBta = Dec(fields, "amountbanknotesinbta"),
            Id = Lng(fields, "id"),
            PosTot1 = Dec(fields, "postot1"),
            PosTot2 = Dec(fields, "postot2")
        };

        if (fields.TryGetValue("root", out var rootArray) && rootArray.ValueKind == JsonValueKind.Array)
            r.Movements = rootArray.EnumerateArray().Select(PagAmicoMovement.FromElement).ToList();

        return r;
    }

    /// <summary>Normalizza la chiave: trim, rimozione underscore, minuscolo.</summary>
    private static string Normalize(string key) =>
        key.Trim().Replace("_", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();

    private static string? Str(IReadOnlyDictionary<string, JsonElement> f, params string[] keys)
    {
        foreach (var k in keys)
            if (f.TryGetValue(k, out var e))
                return e.ValueKind switch
                {
                    JsonValueKind.String => e.GetString(),
                    JsonValueKind.Null or JsonValueKind.Undefined => null,
                    _ => e.ToString()
                };
        return null;
    }

    private static decimal? Dec(IReadOnlyDictionary<string, JsonElement> f, params string[] keys)
    {
        foreach (var k in keys)
        {
            if (!f.TryGetValue(k, out var e)) continue;
            if (e.ValueKind == JsonValueKind.Number && e.TryGetDecimal(out var d)) return d;
            if (e.ValueKind == JsonValueKind.String &&
                decimal.TryParse(e.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var ds)) return ds;
        }
        return null;
    }

    private static double? Dbl(IReadOnlyDictionary<string, JsonElement> f, params string[] keys)
    {
        var d = Dec(f, keys);
        return d.HasValue ? (double)d.Value : null;
    }

    private static int? Int(IReadOnlyDictionary<string, JsonElement> f, params string[] keys)
    {
        var d = Dec(f, keys);
        return d.HasValue ? (int)Math.Round(d.Value) : null;
    }

    private static long? Lng(IReadOnlyDictionary<string, JsonElement> f, params string[] keys)
    {
        var d = Dec(f, keys);
        return d.HasValue ? (long)Math.Round(d.Value) : null;
    }

    private static int[] IntArray(IReadOnlyDictionary<string, JsonElement> f, string key)
    {
        if (!f.TryGetValue(key, out var e) || e.ValueKind != JsonValueKind.Array) return Array.Empty<int>();
        return e.EnumerateArray()
                .Select(x => x.ValueKind == JsonValueKind.Number && x.TryGetDouble(out var v) ? (int)Math.Round(v) : 0)
                .ToArray();
    }

    private static int[][] IntMatrix(IReadOnlyDictionary<string, JsonElement> f, string key)
    {
        if (!f.TryGetValue(key, out var e) || e.ValueKind != JsonValueKind.Array) return Array.Empty<int[]>();
        return e.EnumerateArray()
                .Where(row => row.ValueKind == JsonValueKind.Array)
                .Select(row => row.EnumerateArray()
                                  .Select(x => x.ValueKind == JsonValueKind.Number && x.TryGetDouble(out var v) ? (int)Math.Round(v) : 0)
                                  .ToArray())
                .ToArray();
    }

    public override string ToString() =>
        $"response={Response} errorCode={ErrorCode} errorType={ErrorType} errorList={ErrorList} collected={CollectedAmount} unpaid={AmountUnpaid}";
}

/// <summary>
/// Riga dell'array "bankNotes": un cassetto/riciclatore banconote.
/// Layout (guida pagAmico Dev Kit 1.0, cap. 9.2):
/// [0] valore del taglio | [1] quantita' in riciclo | [2]-[4] uso interno |
/// [5] soglia minima | [6] soglia massima | [7]-[9] uso interno.
/// </summary>
public sealed class BanknoteDrawer
{
    public BanknoteDrawer(int index, int[] row)
    {
        Index = index;
        Raw = row ?? Array.Empty<int>();
    }

    public int Index { get; }
    public int[] Raw { get; }

    private int At(int i) => i < Raw.Length ? Raw[i] : 0;

    /// <summary>Taglio della banconota in euro contenuto nel cassetto (0 = cassetto non configurato).</summary>
    public int Value => At(0);

    /// <summary>Quantita' presente nel riciclatore.</summary>
    public int Quantity => At(1);

    /// <summary>
    /// Posizione [4]. Negli esempi del manuale contiene il numero progressivo del cassetto, ma la guida
    /// Dev Kit la classifica come "uso interno": non farci affidamento, usare <see cref="Index"/>.
    /// </summary>
    public int Raw4 => At(4);

    public int MinStock => At(5);

    public int MaxStock => At(6);

    public bool IsConfigured => Value > 0;

    public override string ToString() => $"cassetto#{Index} taglio={Value}EUR qta={Quantity} min={MinStock} max={MaxStock}";
}

/// <summary>Movimento contabile restituito dai comandi MV / MI.</summary>
public sealed class PagAmicoMovement
{
    public DateTime? DateTimeValue { get; private set; }
    public string? RawDateTime { get; private set; }

    /// <summary>Causale: 000 tutte, 001 Incasso, 010 Pagamento, 011/071 Scarico Monete, 021 Ricarica Monete,
    /// 031 Ricarica Banconote, 041/061 Scarico Banconote, 051 Scarico Banconote in BTA, 090 Incasso POS.</summary>
    public string? OperationCode { get; private set; }

    public decimal Debit { get; private set; }
    public decimal Credit { get; private set; }
    public decimal Change { get; private set; }
    public decimal CoinsAmount { get; private set; }
    public decimal BanknotesAmount { get; private set; }
    public decimal PosAmount { get; private set; }
    public decimal NotDispensedAmount { get; private set; }
    public bool Cancelled { get; private set; }

    internal static PagAmicoMovement FromElement(JsonElement e)
    {
        string? S(string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        decimal D(string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d) ? d : 0m;

        var raw = S("dataora");
        DateTime.TryParseExact(raw, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var parsed);

        return new PagAmicoMovement
        {
            RawDateTime = raw,
            DateTimeValue = parsed == default ? null : parsed,
            OperationCode = S("codice_operazione"),
            Debit = D("importo_dare"),
            Credit = D("importo_avere"),
            Change = D("resto"),
            CoinsAmount = D("importo_monete"),
            BanknotesAmount = D("importo_banconote"),
            PosAmount = D("importo_pos"),
            NotDispensedAmount = D("importo_non_erogato"),
            Cancelled = D("flg_annullato") != 0
        };
    }

    public override string ToString() =>
        $"{RawDateTime} cod={OperationCode} dare={Debit} avere={Credit} pos={PosAmount} annullato={Cancelled}";
}
