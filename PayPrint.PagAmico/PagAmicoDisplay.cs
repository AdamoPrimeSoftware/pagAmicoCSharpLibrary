using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PayPrint.PagAmico;

/// <summary>Posizione della finestra di testo sul display: [DT] in alto, [DG] in basso.</summary>
public enum DisplayPosition
{
    Top,
    Bottom
}

/// <summary>Stile font (manuale INTEGRAZIONE 1.2, cap. 3).</summary>
public enum FontStyle
{
    Normal = 0,
    Bold = 1,
    Italic = 2,
    BoldItalic = 3
}

/// <summary>Colore font (manuale INTEGRAZIONE 1.2, cap. 3).</summary>
public enum FontColor
{
    Blue = 0,
    Black = 1,
    Green = 2,
    Red = 3,
    Magenta = 4,
    Cyan = 5,
    White = 6,
    Gray = 7,
    DarkGray = 8,
    Black9 = 9
}

/// <summary>Modalita' della dialog [QR].</summary>
public enum KeyboardMode
{
    /// <summary>0 = mostra la tastiera.</summary>
    ShowKeyboard = 0,

    /// <summary>1 = non mostra la tastiera (lettura da lettore ottico/USB).</summary>
    NoKeyboard = 1,

    /// <summary>2 = legge la Tessera Sanitaria dal POS e trasferisce il solo codice fiscale.</summary>
    HealthCardFromPos = 2,

    /// <summary>3 = inserimento tessera.</summary>
    CardInsert = 3,

    /// <summary>4 = mostra tastiera numerica.</summary>
    NumericKeyboard = 4
}

/// <summary>Allineamento del testo nelle liste.</summary>
public enum TextAlignment
{
    Left = 0,
    Center = 1,
    Right = 2
}

/// <summary>
/// Layout della tastiera on-screen della finestra di input [DI]
/// (manuale INTEGRAZIONE 1.2, cap. 3 "Layout Tastiera").
/// </summary>
public enum KeyboardLayout
{
    None = 0,
    Standard = 1,
    NumericNoDecimals = 2,
    NumericWithDecimals = 3
}

/// <summary>
/// Comandi di interazione con il display (manuale supplementare INTEGRAZIONE rev. 1.2).
/// Il separatore dei parametri e' la pipe "|".
/// </summary>
public static class PagAmicoDisplay
{
    /// <summary>Lunghezza massima del JSON dei comandi [TS] / [ID].</summary>
    public const int MaxListJsonLength = 3999;

    /// <summary>Un bottone assente si invia come "[]".</summary>
    public const string HiddenButton = "[]";

    /// <summary>[DT] / [DG] finestra di testo. Formato: DT|{testo}|a|b|c|</summary>
    public static string ShowText(string text, DisplayPosition position = DisplayPosition.Top,
        int fontSize = 38, FontStyle style = FontStyle.Bold, FontColor color = FontColor.Blue)
    {
        var prefix = position == DisplayPosition.Top ? "DT" : "DG";
        return $"{prefix}|{Sanitize(text)}|{fontSize}|{(int)style}|{(int)color}|";
    }

    /// <summary>[DS] chiude la finestra di testo.</summary>
    public static string CloseText() => "DS";

    /// <summary>
    /// [DM] MessageBox. Formato: DM|a|b|c|{testo}|{bottone1}|{bottone2}|{bottone3}
    /// I bottoni vuoti vengono inviati come "[]" (nascosti). La risposta e' "BT1" | "BT2" | "BT3".
    /// </summary>
    public static string MessageBox(string text, string button1, string button2 = "", string button3 = "",
        int fontSize = 29, FontStyle style = FontStyle.Normal, FontColor color = FontColor.Green)
    {
        string B(string b) => string.IsNullOrWhiteSpace(b) ? HiddenButton : Sanitize(b);
        return $"DM|{fontSize}|{(int)style}|{(int)color}|{Sanitize(text)}|{B(button1)}|{B(button2)}|{B(button3)}";
    }

    /// <summary>[DC] chiude il MessageBox o la finestra di input.</summary>
    public static string CloseMessageBox() => "DC";

    /// <summary>
    /// [DI] finestra di input da tastiera on-screen. Risponde con il testo digitato, oppure "AN" se annullato.
    /// <para>
    /// ATTENZIONE: il comando non e' documentato nei manuali TCP-IP 2.33 / INTEGRAZIONE 1.2; la guida
    /// pagAmico Dev Kit riporta il formato <c>DI|dim|stile|colore|titolo|...|tastiera</c> lasciando
    /// indeterminato il campo intermedio. Qui e' implementato come testo predefinito della casella:
    /// <c>DI|a|b|c|{titolo}|{testo iniziale}|{tastiera}</c>. Da verificare sul simulatore/macchina.
    /// </para>
    /// </summary>
    public static string InputBox(string title, string initialText = "",
        KeyboardLayout keyboard = KeyboardLayout.Standard,
        int fontSize = 29, FontStyle style = FontStyle.Normal, FontColor color = FontColor.Blue) =>
        $"DI|{fontSize}|{(int)style}|{(int)color}|{Sanitize(title)}|{Sanitize(initialText)}|{(int)keyboard}";

    /// <summary>[DC] chiude la finestra di input (stesso comando del MessageBox).</summary>
    public static string CloseInputBox() => "DC";

    /// <summary>
    /// [QR] lettura barcode / QrCode / Tessera Sanitaria / input da tastiera.
    /// Formato: QR|{testo}|a|b|c|d  (a=font size, b=stile, c=colore, d=modalita' tastiera).
    /// NB: il manuale riporta "QR|{testo}|a|b|c|" ma l'esempio ufficiale ha 4 parametri numerici.
    /// </summary>
    public static string ReadCode(string prompt, KeyboardMode mode = KeyboardMode.NoKeyboard,
        int fontSize = 16, FontStyle style = FontStyle.Bold, FontColor color = FontColor.Green) =>
        $"QR|{Sanitize(prompt)}|{fontSize}|{(int)style}|{(int)color}|{(int)mode}";

    /// <summary>[QA] chiude la richiesta di lettura codice.</summary>
    public static string CloseCodeReader() => "QA";

    /// <summary>[TS] struttura della lista (JSON compatto).</summary>
    public static string ListLayout(string compactJson)
    {
        EnsureJsonSize(compactJson);
        return "TS" + compactJson;
    }

    /// <summary>[ID] dati della lista (JSON compatto).</summary>
    public static string ListData(string compactJson)
    {
        EnsureJsonSize(compactJson);
        return "ID" + compactJson;
    }

    /// <summary>[CO] chiude la lista.</summary>
    public static string CloseList() => "CO";

    private static void EnsureJsonSize(string json)
    {
        if (json is null) throw new ArgumentNullException(nameof(json));
        if (json.Length > MaxListJsonLength)
            throw new ArgumentException($"Il JSON supera i {MaxListJsonLength} caratteri ammessi dal protocollo", nameof(json));
    }

    /// <summary>La pipe e' il separatore del protocollo: va rimossa dai testi utente.</summary>
    private static string Sanitize(string? text) =>
        (text ?? string.Empty).Replace('|', '/').Replace("\r", " ").Replace("\n", " ");
}

/// <summary>Proprieta' grafiche di un elemento della lista ([TS]).</summary>
public sealed class ListTextProperties
{
    [JsonPropertyName("tX")] public string Text { get; set; } = string.Empty;
    [JsonPropertyName("bC")] public string BackgroundColor { get; set; } = "FFFFFFFF";
    [JsonPropertyName("tC")] public string TextColor { get; set; } = "FF000000";
    [JsonPropertyName("fZ")] public int FontSize { get; set; } = 14;
    [JsonPropertyName("fS")] public int FontStyle { get; set; } = 0;
    [JsonPropertyName("aL")] public int Alignment { get; set; } = 0;

    public static ListTextProperties Create(string text, int fontSize = 14,
        FontStyle style = PayPrint.PagAmico.FontStyle.Normal, TextAlignment alignment = TextAlignment.Left,
        string background = "FFFFFFFF", string foreground = "FF000000") => new()
        {
            Text = text,
            FontSize = fontSize,
            FontStyle = (int)style,
            Alignment = (int)alignment,
            BackgroundColor = background,
            TextColor = foreground
        };
}

/// <summary>Coppia etichetta/valore di una colonna o di un totale.</summary>
public sealed class ListCell
{
    [JsonPropertyName("label")] public ListTextProperties Label { get; set; } = new();
    [JsonPropertyName("value")] public ListTextProperties Value { get; set; } = new();

    public static ListCell Create(string label, int fontSize = 14, TextAlignment alignment = TextAlignment.Left) => new()
    {
        Label = ListTextProperties.Create(label, fontSize, PayPrint.PagAmico.FontStyle.Normal, alignment, "FFF5F5F5", "FF333333"),
        Value = ListTextProperties.Create(string.Empty, fontSize, PayPrint.PagAmico.FontStyle.Normal, alignment)
    };
}

/// <summary>Struttura della lista da inviare con [TS].</summary>
public sealed class ListLayout
{
    [JsonPropertyName("titleProperties")] public ListTextProperties Title { get; set; } = new();
    [JsonPropertyName("bodyProperties")] public Dictionary<string, ListCell> Body { get; set; } = new();
    [JsonPropertyName("footerProperties")] public Dictionary<string, object> Footer { get; set; } = new();
    [JsonPropertyName("exitButton")] public ListTextProperties ExitButton { get; set; } = new();
    [JsonPropertyName("printButton")] public ListTextProperties PrintButton { get; set; } = new();
    [JsonPropertyName("rowHeight")] public int RowHeight { get; set; } = 20;

    /// <summary>0 = corpo con 4 colonne (a=data, b=descrizione, c=dare, d=avere); 1 = colonne aggiuntive.</summary>
    [JsonPropertyName("listType")] public int ListType { get; set; } = 0;

    private static readonly JsonSerializerOptions CompactOptions = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    /// <summary>Serializza in JSON compatto nella forma attesa: {"listView": { ... }}.</summary>
    public string ToCompactJson() =>
        JsonSerializer.Serialize(new Dictionary<string, object> { ["listView"] = this }, CompactOptions);
}

/// <summary>Riga dati della lista da inviare con [ID].</summary>
public sealed class ListRow
{
    [JsonPropertyName("a")] public object? A { get; set; }
    [JsonPropertyName("b")] public object? B { get; set; }
    [JsonPropertyName("c")] public object? C { get; set; }
    [JsonPropertyName("d")] public object? D { get; set; }
    [JsonPropertyName("e")] public object? E { get; set; }
}

/// <summary>Piede della lista dati.</summary>
public sealed class ListFooter
{
    [JsonPropertyName("desc")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("tot1")] public decimal Total1 { get; set; }
    [JsonPropertyName("tot2")] public decimal Total2 { get; set; }
    [JsonPropertyName("tot3")] public decimal Total3 { get; set; }
}

/// <summary>Payload dati della lista ([ID]).</summary>
public sealed class ListData
{
    [JsonPropertyName("movimenti")] public List<ListRow> Rows { get; set; } = new();
    [JsonPropertyName("footer")] public ListFooter Footer { get; set; } = new();

    // Le colonne non usate si omettono invece di spedirle come "e":null. Lo scarto era
    // visibile nei log: la libreria Kotlin le salta gia', questa le scriveva. Il layout
    // ([TS]) resta a JsonIgnoreCondition.Never, li' i campi devono esserci tutti.
    private static readonly JsonSerializerOptions CompactOptions = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string ToCompactJson() => JsonSerializer.Serialize(this, CompactOptions);
}
