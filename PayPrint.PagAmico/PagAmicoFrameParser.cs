using System;
using System.Text;

namespace PayPrint.PagAmico;

/// <summary>
/// Estrae i singoli messaggi dallo stream TCP del pagAmico.
/// <para>
/// Il protocollo NON ha un terminatore unico:
/// <list type="bullet">
///   <item>le risposte standard sono oggetti JSON (dal FW 8.71 inviati in un unico blocco, ma il TCP puo' comunque frammentarli o accorparli);</item>
///   <item>le risposte MV / MI terminano con il marcatore <c>|\</c> perche' possono superare il buffer del socket;</item>
///   <item>alcune risposte sono testo puro senza terminatore: "CMD ERROR", "OK comando", "ER BUSY", "BT1", "AN", "EX", il contenuto di un barcode letto.</item>
/// </list>
/// Per questo il parser conta le graffe (ignorando quelle dentro le stringhe JSON) e,
/// per il testo, chiude il frame su CR/LF oppure dopo un periodo di silenzio (<see cref="TextIdle"/>).
/// </para>
/// </summary>
public sealed class PagAmicoFrameParser
{
    /// <summary>Marcatore di fine risposta usato dai comandi MV / MI.</summary>
    public const string LongResponseTerminator = "|\\";

    private readonly StringBuilder _buffer = new();
    private DateTime _lastAppendUtc = DateTime.UtcNow;

    /// <summary>Silenzio dopo il quale un frame testuale incompleto viene comunque emesso.</summary>
    public TimeSpan TextIdle { get; set; } = TimeSpan.FromMilliseconds(150);

    /// <summary>Silenzio dopo il quale un JSON incompleto viene emesso come testo grezzo (protezione anti-stallo).</summary>
    public TimeSpan JsonIdle { get; set; } = TimeSpan.FromSeconds(10);

    public int BufferedLength => _buffer.Length;

    public string Buffered => _buffer.ToString();

    public void Append(string chunk)
    {
        if (string.IsNullOrEmpty(chunk)) return;
        _buffer.Append(chunk);
        _lastAppendUtc = DateTime.UtcNow;
    }

    public void Clear() => _buffer.Clear();

    /// <summary>
    /// Prova ad estrarre un frame completo. Chiamare in ciclo finche' ritorna false.
    /// </summary>
    /// <param name="idle">true se non arrivano byte da almeno un ciclo di polling (abilita la chiusura dei frame testuali).</param>
    /// <param name="frame">Frame estratto, valorizzato solo quando il metodo restituisce true.</param>
    public bool TryReadFrame(bool idle, out PagAmicoFrame frame)
    {
        frame = null!;
        TrimLeadingSeparators();
        if (_buffer.Length == 0) return false;

        return _buffer[0] == '{'
            ? TryReadJsonFrame(idle, out frame)
            : TryReadTextFrame(idle, out frame);
    }

    private bool TryReadJsonFrame(bool idle, out PagAmicoFrame frame)
    {
        frame = null!;
        var text = _buffer.ToString();
        var end = FindJsonEnd(text);

        if (end < 0)
        {
            // JSON ancora incompleto: se il silenzio si prolunga troppo lo emetto grezzo
            if (idle && DateTime.UtcNow - _lastAppendUtc > JsonIdle)
            {
                frame = new PagAmicoFrame(FrameKind.Text, text.Trim(), null);
                _buffer.Clear();
                return true;
            }
            return false;
        }

        var json = text.Substring(0, end + 1);
        var consumed = end + 1;

        // MV / MI: rimuovo il marcatore di fine risposta se presente
        if (text.Length >= consumed + LongResponseTerminator.Length &&
            string.CompareOrdinal(text, consumed, LongResponseTerminator, 0, LongResponseTerminator.Length) == 0)
        {
            consumed += LongResponseTerminator.Length;
        }

        _buffer.Remove(0, consumed);

        var parsed = PagAmicoResponse.TryParse(json);
        frame = parsed is null
            ? new PagAmicoFrame(FrameKind.Text, json, null)
            : new PagAmicoFrame(FrameKind.Json, json, parsed);
        return true;
    }

    private bool TryReadTextFrame(bool idle, out PagAmicoFrame frame)
    {
        frame = null!;
        var text = _buffer.ToString();

        // 1) testo seguito da un JSON: chiudo il frame testuale sulla graffa
        var brace = text.IndexOf('{');
        if (brace > 0)
        {
            frame = Emit(text.Substring(0, brace), brace);
            return true;
        }

        // 2) testo terminato da CR/LF
        var nl = text.IndexOfAny(new[] { '\r', '\n' });
        if (nl >= 0)
        {
            var consumed = nl + 1;
            if (nl + 1 < text.Length && text[nl] == '\r' && text[nl + 1] == '\n') consumed++;
            frame = Emit(text.Substring(0, nl), consumed);
            return true;
        }

        // 3) silenzio: il messaggio testuale e' finito
        if (idle && DateTime.UtcNow - _lastAppendUtc > TextIdle)
        {
            frame = Emit(text, text.Length);
            return true;
        }

        return false;
    }

    private PagAmicoFrame Emit(string payload, int consumed)
    {
        _buffer.Remove(0, consumed);
        return new PagAmicoFrame(FrameKind.Text, payload.Trim(), null);
    }

    private void TrimLeadingSeparators()
    {
        var i = 0;
        while (i < _buffer.Length && (_buffer[i] == '\r' || _buffer[i] == '\n' || _buffer[i] == '\0'))
            i++;
        if (i > 0) _buffer.Remove(0, i);
    }

    /// <summary>
    /// Indice della graffa che chiude l'oggetto JSON che inizia a 0, oppure -1 se incompleto.
    /// Gestisce stringhe ed escape (il messaggio POS contiene graffe e backslash).
    /// </summary>
    internal static int FindJsonEnd(string text)
    {
        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (inString)
            {
                if (escaped) escaped = false;
                else if (c == '\\') escaped = true;
                else if (c == '"') inString = false;
                continue;
            }

            switch (c)
            {
                case '"': inString = true; break;
                case '{': depth++; break;
                case '}':
                    depth--;
                    if (depth == 0) return i;
                    break;
            }
        }
        return -1;
    }
}
