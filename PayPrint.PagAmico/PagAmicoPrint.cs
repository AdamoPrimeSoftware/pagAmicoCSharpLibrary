using System;
using System.Collections.Generic;
using System.Globalization;

namespace PayPrint.PagAmico;

/// <summary>Allineamento di stampa ([PTJT]).</summary>
public enum PrintAlignment
{
    Left,
    Center,
    Right
}

/// <summary>Font della stampante ([PTFA] / [PTFB]).</summary>
public enum PrinterFont
{
    A,
    B
}

/// <summary>Modificatore del font di stampa.</summary>
public enum PrinterFontMode
{
    Normal,
    Bold,
    DoubleHeight,
    DoubleWidth,
    DoubleHeightWidth
}

/// <summary>Sottolineatura ([PTUN]).</summary>
public enum Underline
{
    None = 0,
    Single = 1,
    Double = 2
}

/// <summary>
/// Tipo di barcode ([PTBCBC]).
/// <para>
/// Sui codici 7 e 8 il manuale Protocollo di stampa 2.00 si contraddice da solo: la tabella
/// del comando a pagina 3 dice 7 = CODE128 e 8 = CODE93, la nota di aggiornamento a pagina 5
/// dello stesso documento dice 7 = CODE93 e 8 = CODE1128 (refuso per CODE128). Vale la
/// seconda: il catalogo comandi del pagAmico Dev Kit, che e' anche il programma che emula il
/// dispositivo, riporta 7 = CODE93 e 8 = CODE128.
/// </para>
/// </summary>
public enum BarcodeType
{
    UpcA = 0,
    UpcE = 1,
    Ean13 = 2,
    Ean8 = 3,
    Code39 = 4,
    Itf = 5,
    Codabar = 6,
    Code93 = 7,
    Code128 = 8
}

/// <summary>Posizione del testo rispetto al barcode.</summary>
public enum BarcodeTextPosition
{
    None = 0,
    Above = 1,
    Below = 2,
    Both = 3
}

/// <summary>
/// Comandi del protocollo di stampa pagAmico (manuale "Protocollo di Stampa" rev. 2.00).
/// Ogni comando inizia con l'header "PT" seguito da comando e parametri, senza spazi.
/// </summary>
public static class PagAmicoPrint
{
    public static string BeginPrint() => "PTSTST";
    public static string EndPrint() => "PTSTEN";
    public static string CancelPrint() => "PTSTAN";
    public static string Reset() => "PTRSET";

    public static string Bold(bool on) => on ? "PTBDON" : "PTBDOF";
    public static string Italic(bool on) => on ? "PTITON" : "PTITOF";
    public static string DoubleSize(bool on) => on ? "PTDBON" : "PTDBOF";

    public static string CodePage(int codePage)
    {
        if (codePage is < 0 or > 99) throw new ArgumentOutOfRangeException(nameof(codePage), "Code page a 2 cifre (00..99)");
        return "PTCP" + codePage.ToString("D2", CultureInfo.InvariantCulture);
    }

    public static string Cut(bool full) => full ? "PTCUTL" : "PTCUPT";

    public static string Align(PrintAlignment alignment) => alignment switch
    {
        PrintAlignment.Center => "PTJTCE",
        PrintAlignment.Right => "PTJTRI",
        _ => "PTJTLE"
    };

    public static string Font(PrinterFont font, PrinterFontMode mode)
    {
        var f = font == PrinterFont.A ? "PTFA" : "PTFB";
        var m = mode switch
        {
            PrinterFontMode.Bold => "BD",
            PrinterFontMode.DoubleHeight => "H2",
            PrinterFontMode.DoubleWidth => "W2",
            PrinterFontMode.DoubleHeightWidth => "HW",
            _ => "NO"
        };
        return f + m;
    }

    public static string SetUnderline(Underline underline) => "PTUN" + ((int)underline).ToString("D2", CultureInfo.InvariantCulture);

    /// <summary>[PTLFNRxx] avanzamento di xx righe.</summary>
    public static string LineFeed(int lines)
    {
        if (lines is < 0 or > 99) throw new ArgumentOutOfRangeException(nameof(lines), "0..99 righe");
        return "PTLFNR" + lines.ToString("D2", CultureInfo.InvariantCulture);
    }

    /// <summary>[PTPRWL] stampa testo seguito da CR LF.</summary>
    public static string WriteLine(string text) => "PTPRWL" + (text ?? string.Empty);

    /// <summary>[PTPRWR] stampa testo senza avanzamento riga.</summary>
    public static string Write(string text) => "PTPRWR" + (text ?? string.Empty);

    /// <summary>[PTPRRExx] stampa un carattere ripetuto xx volte.</summary>
    public static string RepeatChar(char c, int times) =>
        "PTPRRE" + Times(times) + c;

    /// <summary>[PTPRRLxx] stampa un carattere ripetuto xx volte seguito da CR LF.</summary>
    public static string RepeatCharLine(char c, int times) =>
        "PTPRRL" + Times(times) + c;

    private static string Times(int times)
    {
        if (times is < 0 or > 99) throw new ArgumentOutOfRangeException(nameof(times), "0..99 ripetizioni");
        return times.ToString("D2", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// [PTPRDT_ASCII:] invio diretto di sequenze ESC/POS.
    /// NB: il manuale usa in punti diversi "PTPRDT_ASCII:", "PTSTDT_ASCII:" e "PTSTAT_ASCII:": da confermare con PayPrint.
    /// </summary>
    public static string RawEscPos(string asciiPayload) => "PTPRDT_ASCII:" + (asciiPayload ?? string.Empty);

    /// <summary>
    /// [PTQRQRsl...] stampa un QR code.
    /// s = dimensione modulo in dot (1 cifra), l = livello di correzione errore (1 cifra).
    /// </summary>
    public static string QrCode(string content, int moduleSize = 4, int errorCorrection = 2)
    {
        if (moduleSize is < 1 or > 9) throw new ArgumentOutOfRangeException(nameof(moduleSize), "1..9");
        if (errorCorrection is < 0 or > 9) throw new ArgumentOutOfRangeException(nameof(errorCorrection), "0..9");
        return $"PTQRQR{moduleSize}{errorCorrection}{content}";
    }

    /// <summary>
    /// [PTBCBCthhhps...] stampa un barcode.
    /// t = tipo, hhh = altezza in dot, p = posizione testo, s = spaziatura (1 cifra).
    /// </summary>
    public static string Barcode(BarcodeType type, string value, int heightDots = 100,
        BarcodeTextPosition textPosition = BarcodeTextPosition.Below, int spacing = 9)
    {
        if (heightDots is < 0 or > 999) throw new ArgumentOutOfRangeException(nameof(heightDots), "0..999 dot");
        if (spacing is < 0 or > 9) throw new ArgumentOutOfRangeException(nameof(spacing), "0..9");
        return $"PTBCBC{(int)type}{heightDots:D3}{(int)textPosition}{spacing}{value}";
    }

    /// <summary>[PTSTAT] lettura stato stampante.</summary>
    public static string Status() => "PTSTAT";
}

/// <summary>Stato stampante restituito da [PTSTAT].</summary>
public readonly struct PrinterStatus
{
    private PrinterStatus(bool installed, string? raw, bool printing, bool paperEmpty, bool coverOpen, bool paperLow)
    {
        Installed = installed;
        Raw = raw;
        Printing = printing;
        PaperEmpty = paperEmpty;
        CoverOpen = coverOpen;
        PaperLow = paperLow;
    }

    /// <summary>false quando errorType = "NO PRINTER".</summary>
    public bool Installed { get; }

    public string? Raw { get; }

    /// <summary>b7 - stampa avviata.</summary>
    public bool Printing { get; }

    /// <summary>b2 - carta esaurita.</summary>
    public bool PaperEmpty { get; }

    /// <summary>b1 - coperchio aperto.</summary>
    public bool CoverOpen { get; }

    /// <summary>b0 - carta quasi finita.</summary>
    public bool PaperLow { get; }

    public bool CanPrint => Installed && !PaperEmpty && !CoverOpen;

    public static PrinterStatus Parse(PagAmicoResponse response)
    {
        var raw = response.ErrorType?.Trim();

        if (string.Equals(raw, "NO PRINTER", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(response.Response, "ER", StringComparison.OrdinalIgnoreCase))
            return new PrinterStatus(false, raw, false, false, false, false);

        if (raw is null || raw.Length != 8)
            return new PrinterStatus(true, raw, false, false, false, false);

        // stringa "b7 b6 b5 b4 b3 b2 b1 b0": indice 0 = b7
        bool Bit(int b) => raw[7 - b] == '1';
        return new PrinterStatus(true, raw, Bit(7), Bit(2), Bit(1), Bit(0));
    }

    public override string ToString() =>
        Installed
            ? $"stampante ok={CanPrint} raw={Raw} inStampa={Printing} cartaEsaurita={PaperEmpty} coperchioAperto={CoverOpen} cartaQuasiFinita={PaperLow}"
            : "stampante non installata";
}

/// <summary>
/// Costruttore fluente di uno scontrino. Produce la sequenza PTSTST ... PTSTEN.
/// </summary>
public sealed class PagAmicoPrintJob
{
    private readonly List<string> _commands = new();

    public PagAmicoPrintJob Reset() => Add(PagAmicoPrint.Reset());
    public PagAmicoPrintJob Bold(bool on = true) => Add(PagAmicoPrint.Bold(on));
    public PagAmicoPrintJob Italic(bool on = true) => Add(PagAmicoPrint.Italic(on));
    public PagAmicoPrintJob DoubleSize(bool on = true) => Add(PagAmicoPrint.DoubleSize(on));
    public PagAmicoPrintJob CodePage(int codePage) => Add(PagAmicoPrint.CodePage(codePage));
    public PagAmicoPrintJob Align(PrintAlignment alignment) => Add(PagAmicoPrint.Align(alignment));
    public PagAmicoPrintJob Font(PrinterFont font, PrinterFontMode mode) => Add(PagAmicoPrint.Font(font, mode));
    public PagAmicoPrintJob SetUnderline(Underline underline) => Add(PagAmicoPrint.SetUnderline(underline));
    public PagAmicoPrintJob Line(string text) => Add(PagAmicoPrint.WriteLine(text));
    public PagAmicoPrintJob Text(string text) => Add(PagAmicoPrint.Write(text));
    public PagAmicoPrintJob Feed(int lines = 1) => Add(PagAmicoPrint.LineFeed(lines));
    public PagAmicoPrintJob Separator(char c = '-', int times = 32) => Add(PagAmicoPrint.RepeatCharLine(c, times));
    public PagAmicoPrintJob QrCode(string content, int moduleSize = 4, int errorCorrection = 2) =>
        Add(PagAmicoPrint.QrCode(content, moduleSize, errorCorrection));
    public PagAmicoPrintJob Barcode(BarcodeType type, string value, int heightDots = 100,
        BarcodeTextPosition textPosition = BarcodeTextPosition.Below, int spacing = 9) =>
        Add(PagAmicoPrint.Barcode(type, value, heightDots, textPosition, spacing));
    public PagAmicoPrintJob RawEscPos(string payload) => Add(PagAmicoPrint.RawEscPos(payload));
    public PagAmicoPrintJob Cut(bool full = true) => Add(PagAmicoPrint.Cut(full));

    private PagAmicoPrintJob Add(string command)
    {
        _commands.Add(command);
        return this;
    }

    /// <summary>Sequenza completa dei comandi, delimitata da PTSTST / PTSTEN.</summary>
    public IReadOnlyList<string> Build()
    {
        var list = new List<string>(_commands.Count + 2) { PagAmicoPrint.BeginPrint() };
        list.AddRange(_commands);
        list.Add(PagAmicoPrint.EndPrint());
        return list;
    }
}
