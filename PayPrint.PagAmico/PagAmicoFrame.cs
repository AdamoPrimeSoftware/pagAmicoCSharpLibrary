using System;

namespace PayPrint.PagAmico;

/// <summary>Tipo di frame ricevuto dal pagAmico.</summary>
public enum FrameKind
{
    /// <summary>Oggetto JSON completo (risposta standard del protocollo).</summary>
    Json,

    /// <summary>Risposta testuale non JSON: "CMD ERROR", "OK comando", "ER BUSY", "BT1", "AN", "EX", barcode letto, ...</summary>
    Text
}

/// <summary>
/// Un singolo messaggio ricevuto dal pagAmico. Il protocollo e' asincrono: ad un
/// comando possono seguire piu' frame (es. IN -> {"response":"OK"} -> {"response":"p"} ... -> {"response":"IN"}).
/// </summary>
public sealed class PagAmicoFrame
{
    public PagAmicoFrame(FrameKind kind, string raw, PagAmicoResponse? json)
    {
        Kind = kind;
        Raw = raw ?? string.Empty;
        Json = json;
        ReceivedAt = DateTimeOffset.Now;
    }

    public FrameKind Kind { get; }

    /// <summary>Payload grezzo cosi' come arrivato dal socket (JSON o testo).</summary>
    public string Raw { get; }

    /// <summary>Risposta deserializzata, valorizzata solo se <see cref="Kind"/> == Json.</summary>
    public PagAmicoResponse? Json { get; }

    public DateTimeOffset ReceivedAt { get; }

    public bool IsJson => Kind == FrameKind.Json;

    public bool IsText => Kind == FrameKind.Text;

    /// <summary>Valore del campo "response" se JSON, altrimenti null.</summary>
    public string? Response => Json?.Response;

    /// <summary>Comando accettato dal pagAmico ({"response":"OK"}).</summary>
    public bool IsAck => Response == "OK";

    /// <summary>Incasso/ricarica parziale in corso ({"response":"p"}).</summary>
    public bool IsPartial => Response == "p";

    /// <summary>Errore, sia in forma JSON ({"response":"ER"}) sia testuale ("CMD ERROR", "ER BUSY", "BUSY", ...).</summary>
    public bool IsError =>
        Response == "ER" || IsBusy ||
        (IsText && (Raw.StartsWith("ER", StringComparison.OrdinalIgnoreCase) ||
                    Raw.StartsWith("CMD ", StringComparison.OrdinalIgnoreCase) ||
                    Raw.Contains("CMD ERROR", StringComparison.OrdinalIgnoreCase) ||
                    Raw.Contains("POS DISABLED", StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// Macchina impegnata: il testo "BUSY" (anche "ER BUSY"), oppure un ER con E100 ed errorType 99 (manuale p. 61).
    /// </summary>
    public bool IsBusy =>
        (IsText && Raw.IndexOf(PagAmicoTextResponses.Busy, StringComparison.OrdinalIgnoreCase) >= 0) ||
        (Response == "ER" &&
         string.Equals(Json?.ErrorCode, PagAmicoErrorCodes.CommandNotExecutable, StringComparison.OrdinalIgnoreCase) &&
         PagAmicoErrorCodes.ParseState(Json?.ErrorType) == MachineState.Busy);

    public override string ToString() => $"[{Kind}] {Raw}";
}
