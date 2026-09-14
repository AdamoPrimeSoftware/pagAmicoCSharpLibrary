# -*- coding: utf-8 -*-
"""
Genera il documento PDF che spiega le librerie pagAmico e la comunicazione con il dispositivo.

    python genera_documentazione.py

Produce: Integrazione-pagAmico.pdf nella stessa cartella.
Richiede reportlab (pip install reportlab).
"""

import os

from reportlab.lib import colors
from reportlab.lib.enums import TA_JUSTIFY
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.graphics.shapes import Drawing, Line, Polygon, Rect, String
from reportlab.platypus import (
    BaseDocTemplate, Frame, KeepTogether, PageBreak, PageTemplate,
    Paragraph, Spacer, Table, TableStyle,
)

# --------------------------------------------------------------------------- stile

INK = colors.HexColor("#1a1a1a")
MUTED = colors.HexColor("#5f6b76")
ACCENT = colors.HexColor("#1565c0")
RULE = colors.HexColor("#c9d1d9")
BOX_BG = colors.HexColor("#f4f6f8")
CODE_BG = colors.HexColor("#f0f2f4")
WARN = colors.HexColor("#b3261e")
GOOD = colors.HexColor("#2e7d32")

PAGE_W, PAGE_H = A4
MARGIN = 22 * mm
CONTENT_W = PAGE_W - 2 * MARGIN

styles = getSampleStyleSheet()

S_TITLE = ParagraphStyle("t", parent=styles["Title"], fontName="Helvetica-Bold",
                         fontSize=24, leading=28, textColor=INK, spaceAfter=4)
S_SUBTITLE = ParagraphStyle("st", parent=styles["Normal"], fontName="Helvetica",
                            fontSize=12, leading=16, textColor=MUTED, alignment=1)
S_H1 = ParagraphStyle("h1", parent=styles["Heading1"], fontName="Helvetica-Bold",
                      fontSize=15, leading=19, textColor=INK, spaceBefore=16, spaceAfter=7)
S_H2 = ParagraphStyle("h2", parent=styles["Heading2"], fontName="Helvetica-Bold",
                      fontSize=11.5, leading=15, textColor=ACCENT, spaceBefore=11, spaceAfter=4)
S_BODY = ParagraphStyle("b", parent=styles["Normal"], fontName="Helvetica",
                        fontSize=9.7, leading=14, textColor=INK, alignment=TA_JUSTIFY, spaceAfter=6)
S_SMALL = ParagraphStyle("s", parent=S_BODY, fontSize=8.6, leading=12, textColor=MUTED)
S_CODE = ParagraphStyle("c", parent=styles["Normal"], fontName="Courier",
                        fontSize=8.1, leading=11, textColor=INK,
                        backColor=CODE_BG, borderPadding=6, spaceBefore=3, spaceAfter=7)
S_CELL = ParagraphStyle("cell", parent=styles["Normal"], fontName="Helvetica",
                        fontSize=8.6, leading=11.5, textColor=INK)
S_CELL_B = ParagraphStyle("cellb", parent=S_CELL, fontName="Helvetica-Bold")
S_CELL_C = ParagraphStyle("cellc", parent=S_CELL, fontName="Courier", fontSize=8.1)
S_CAPTION = ParagraphStyle("cap", parent=S_SMALL, alignment=1, spaceBefore=2, spaceAfter=10)


def P(text, style=S_BODY):
    return Paragraph(text, style)


def code(text):
    lines = text.strip("\n").split("\n")
    escaped = [l.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;").replace(" ", "&nbsp;")
               for l in lines]
    return Paragraph("<br/>".join(escaped), S_CODE)


def table(rows, widths, header=True, code_cols=()):
    data = []
    for r, row in enumerate(rows):
        cells = []
        for c, cell in enumerate(row):
            if r == 0 and header:
                style = S_CELL_B
            elif c in code_cols:
                style = S_CELL_C
            else:
                style = S_CELL
            cells.append(Paragraph(str(cell), style))
        data.append(cells)

    t = Table(data, colWidths=widths, hAlign="LEFT", repeatRows=1 if header else 0)
    commands = [
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("TOPPADDING", (0, 0), (-1, -1), 4),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
        ("LEFTPADDING", (0, 0), (-1, -1), 6),
        ("RIGHTPADDING", (0, 0), (-1, -1), 6),
        ("LINEBELOW", (0, 0), (-1, -2), 0.4, RULE),
    ]
    if header:
        commands += [("BACKGROUND", (0, 0), (-1, 0), BOX_BG),
                     ("LINEBELOW", (0, 0), (-1, 0), 0.8, MUTED)]
    t.setStyle(TableStyle(commands))
    return t


def callout(title, text, color=ACCENT):
    inner = [[Paragraph(f"<b>{title}</b><br/>{text}", S_CELL)]]
    t = Table(inner, colWidths=[CONTENT_W], hAlign="LEFT")
    t.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, -1), BOX_BG),
        ("LINEBEFORE", (0, 0), (0, -1), 2.5, color),
        ("TOPPADDING", (0, 0), (-1, -1), 7),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 7),
        ("LEFTPADDING", (0, 0), (-1, -1), 9),
        ("RIGHTPADDING", (0, 0), (-1, -1), 9),
    ]))
    return t


def caption(text):
    return Paragraph(text, S_CAPTION)


def figure(drawing, text):
    """Tiene insieme il disegno e la sua didascalia, evitando che li separi un salto pagina."""
    return KeepTogether([drawing, caption(text)])


# --------------------------------------------------------------------------- diagrammi

def sequence_diagram(actors, messages, width=CONTENT_W, top=26, row=21, bottom=14):
    """
    actors:   ["Gestionale", "Libreria", "pagAmico"]
    messages: (da, a, testo, tipo) con tipo in {"call", "reply", "self", "note"}
    """
    height = top + row * len(messages) + bottom
    d = Drawing(width, height)

    n = len(actors)
    slot = width / n
    xs = [slot * (i + 0.5) for i in range(n)]
    box_w = min(slot - 12, 118)
    box_h = 17
    y_top = height - box_h - 2

    for i, name in enumerate(actors):
        d.add(Rect(xs[i] - box_w / 2, y_top, box_w, box_h, fillColor=BOX_BG,
                   strokeColor=MUTED, strokeWidth=0.6))
        d.add(String(xs[i], y_top + 5.4, name, fontName="Helvetica-Bold", fontSize=8,
                     fillColor=INK, textAnchor="middle"))
        # lifeline
        y = y_top
        while y > bottom - 4:
            d.add(Line(xs[i], y, xs[i], max(y - 4, bottom - 4), strokeColor=RULE, strokeWidth=0.6))
            y -= 7

    y = y_top - top + 6
    for msg in messages:
        src, dst, text, kind = msg
        x1, x2 = xs[src], xs[dst]

        if kind == "note":
            w = width - 24
            d.add(Rect(12, y - 5, w, 15, fillColor=colors.HexColor("#fff4e5"),
                       strokeColor=colors.HexColor("#e0a458"), strokeWidth=0.6))
            d.add(String(width / 2, y - 0.5, text, fontName="Helvetica-Oblique", fontSize=7.6,
                         fillColor=INK, textAnchor="middle"))
            y -= row
            continue

        if kind == "self":
            d.add(Line(x1, y + 6, x1 + 26, y + 6, strokeColor=ACCENT, strokeWidth=0.9))
            d.add(Line(x1 + 26, y + 6, x1 + 26, y - 4, strokeColor=ACCENT, strokeWidth=0.9))
            d.add(Line(x1 + 26, y - 4, x1 + 3, y - 4, strokeColor=ACCENT, strokeWidth=0.9))
            d.add(Polygon([x1 + 3, y - 4, x1 + 10, y - 1.6, x1 + 10, y - 6.4],
                          fillColor=ACCENT, strokeColor=ACCENT))
            d.add(String(x1 + 32, y + 1, text, fontName="Helvetica", fontSize=7.6, fillColor=INK))
            y -= row
            continue

        colour = ACCENT if kind == "call" else colors.HexColor("#37474f")
        dash = None if kind == "call" else (2.4, 2.0)
        d.add(Line(x1, y, x2, y, strokeColor=colour, strokeWidth=1.0, strokeDashArray=dash))

        head = 7 if x2 > x1 else -7
        d.add(Polygon([x2, y, x2 - head, y + 2.7, x2 - head, y - 2.7],
                      fillColor=colour, strokeColor=colour))

        d.add(String((x1 + x2) / 2, y + 4.2, text, fontName="Helvetica", fontSize=7.6,
                     fillColor=INK, textAnchor="middle"))
        y -= row

    return d


def architecture_diagram(width=CONTENT_W):
    d = Drawing(width, 232)

    def box(x, y, w, h, title, subtitle=None, fill=BOX_BG, stroke=MUTED, bold=True):
        d.add(Rect(x, y, w, h, fillColor=fill, strokeColor=stroke, strokeWidth=0.7))
        font = "Helvetica-Bold" if bold else "Helvetica"
        ty = y + h - 12 if subtitle else y + h / 2 - 3
        d.add(String(x + w / 2, ty, title, fontName=font, fontSize=8.2,
                     fillColor=INK, textAnchor="middle"))
        if subtitle:
            for i, line in enumerate(subtitle):
                d.add(String(x + w / 2, ty - 10 - i * 9, line, fontName="Helvetica", fontSize=7.2,
                             fillColor=MUTED, textAnchor="middle"))

    def arrow(x1, y1, x2, y2, label=None, dashed=False):
        d.add(Line(x1, y1, x2, y2, strokeColor=ACCENT, strokeWidth=1.0,
                   strokeDashArray=(2.4, 2.0) if dashed else None))
        if x2 != x1:
            head = 6 if x2 > x1 else -6
            d.add(Polygon([x2, y2, x2 - head, y2 + 2.6, x2 - head, y2 - 2.6],
                          fillColor=ACCENT, strokeColor=ACCENT))
        else:
            head = 6 if y2 > y1 else -6
            d.add(Polygon([x2, y2, x2 + 2.6, y2 - head, x2 - 2.6, y2 - head],
                          fillColor=ACCENT, strokeColor=ACCENT))
        if label:
            d.add(String((x1 + x2) / 2 + 4, (y1 + y2) / 2 + 3, label, fontName="Helvetica",
                         fontSize=7, fillColor=MUTED))

    col = (width - 24) / 3

    box(0, 186, width, 34, "Applicazione (gestionale, banco di prova WinForms / Compose Desktop)")

    arrow(width / 2, 186, width / 2, 168)

    box(0, 96, col, 66, "PagAmicoClient",
        ["connessione e riconnessione", "attese e timeout", "annullo -> AN", "distanza minima fra invii"])
    box(col + 12, 96, col, 66, "PagAmicoFrameParser",
        ["separa i messaggi", "conta le graffe", "marcatore |\\ di MV/MI", "chiude il testo sul silenzio"])
    box(2 * (col + 12), 96, col, 66, "PagAmicoResponse",
        ["normalizza le chiavi", "importi in euro", "giacenze per taglio", "errorList decodificato"])

    box(0, 44, col, 40, "PagAmicoCommands", ["stringhe di comando"])
    box(col + 12, 44, col, 40, "PagAmicoDisplay / Print", ["display e scontrini"])
    box(2 * (col + 12), 44, col, 40, "PagAmicoFileLogger", ["tracciato su file"])

    arrow(width / 2, 96, width / 2, 30)
    box(0, 0, width, 28, "pagAmico  -  socket TCP, porta 9100", fill=colors.HexColor("#e8f0fe"),
        stroke=ACCENT)

    return d


def framing_diagram(width=CONTENT_W):
    """Mostra come il flusso di byte viene diviso in messaggi."""
    d = Drawing(width, 132)

    d.add(String(0, 120, "Byte in arrivo dal socket (tre letture successive):",
                 fontName="Helvetica-Bold", fontSize=8, fillColor=INK))

    chunks = [
        ('{"response":"OK"}{"resp', 0.34),
        ('onse":"p","collected', 0.30),
        ('Amount":1.0}CMD ERROR', 0.36),
    ]
    x = 0
    for text, frac in chunks:
        w = (width - 12) * frac
        d.add(Rect(x, 88, w, 22, fillColor=CODE_BG, strokeColor=MUTED, strokeWidth=0.6))
        d.add(String(x + 5, 96, text, fontName="Courier", fontSize=7, fillColor=INK))
        x += w + 6

    for cx in (width * 0.24, width * 0.55, width * 0.85):
        d.add(Line(cx, 86, cx, 62, strokeColor=RULE, strokeWidth=0.6, strokeDashArray=(2, 2)))

    d.add(String(0, 50, "Messaggi consegnati all'applicazione:",
                 fontName="Helvetica-Bold", fontSize=8, fillColor=INK))

    frames = [
        ('{"response":"OK"}', GOOD, 0.30),
        ('{"response":"p",...}', GOOD, 0.38),
        ('CMD ERROR', WARN, 0.28),
    ]
    x = 0
    for text, colr, frac in frames:
        w = (width - 12) * frac
        d.add(Rect(x, 16, w, 22, fillColor=colors.HexColor("#ffffff"), strokeColor=colr, strokeWidth=1.0))
        d.add(String(x + 5, 24, text, fontName="Courier", fontSize=7, fillColor=INK))
        x += w + 6

    d.add(String(0, 3, "i primi due riconosciuti contando le graffe, il terzo (testo puro) chiuso dopo 150 ms di silenzio",
                 fontName="Helvetica-Oblique", fontSize=7, fillColor=MUTED))
    return d


# --------------------------------------------------------------------------- documento

def build(path):
    doc = BaseDocTemplate(path, pagesize=A4,
                          leftMargin=MARGIN, rightMargin=MARGIN,
                          topMargin=MARGIN, bottomMargin=MARGIN + 6 * mm,
                          title="Integrazione pagAmico - librerie C# e Kotlin",
                          author="Prime Software S.r.l.")

    frame = Frame(MARGIN, MARGIN + 6 * mm, CONTENT_W, PAGE_H - 2 * MARGIN - 6 * mm, id="main")

    def decorate(canvas, document):
        canvas.saveState()
        canvas.setFont("Helvetica", 7.5)
        canvas.setFillColor(MUTED)
        if document.page > 1:
            canvas.drawString(MARGIN, MARGIN + 2 * mm,
                              "Integrazione pagAmico - librerie C# e Kotlin")
            canvas.drawRightString(PAGE_W - MARGIN, MARGIN + 2 * mm, str(document.page))
            canvas.setStrokeColor(RULE)
            canvas.setLineWidth(0.5)
            canvas.line(MARGIN, MARGIN + 5.4 * mm, PAGE_W - MARGIN, MARGIN + 5.4 * mm)
        canvas.restoreState()

    doc.addPageTemplates([PageTemplate(id="all", frames=[frame], onPage=decorate)])
    doc.build(story())


def story():
    s = []

    # ---------------------------------------------------------------- copertina
    s.append(Spacer(1, 34 * mm))
    s.append(P("Integrazione pagAmico", S_TITLE))
    s.append(P("Librerie C# e Kotlin per la cassa rendiresto PayPrint<br/>"
               "e comunicazione con il dispositivo e con il simulatore", S_SUBTITLE))
    s.append(Spacer(1, 14 * mm))

    s.append(table([
        ["Contenuto", "Libreria C# (net8.0, netstandard2.0, net47), libreria Kotlin (JVM/Android), "
                      "banchi di prova, collaudo automatico, proxy di analisi, riempimento del simulatore"],
        ["Protocollo", "PayPrint pagAmico, TCP-IP rev. 2.33 - firmware 8.72"],
        ["Documenti di base", "Manuale Lista comandi TCP-IP 2.33; Integrazione display 1.2; "
                              "Protocollo di stampa 2.00; Note di rilascio FW 8.72; Guida pagAmico Dev Kit 1.0"],
        ["Stato della verifica", "test offline superati: 169 in C#, 171 in Kotlin; 64 passi di collaudo "
                                 "sul simulatore in entrambi i linguaggi (14 settembre 2026)"],
    ], [95, CONTENT_W - 95], header=False))

    s.append(Spacer(1, 10 * mm))
    s.append(callout("A cosa serve questo documento",
                     "Spiega come sono fatte le due librerie, che cosa succede sul filo quando "
                     "l'applicazione chiede un incasso, e come si collauda tutto contro il simulatore "
                     "fornito da PayPrint senza avere la macchina in ufficio. Il capitolo 9 aggiunge "
                     "il passo successivo: che cosa comporta innestare la libreria in un gestionale "
                     "che pilota gia' una cassa automatica di un'altra marca. "
                     "Le parti segnalate come <i>da confermare</i> sono punti in cui la documentazione "
                     "del costruttore si contraddice o tace."))

    s.append(PageBreak())

    # ---------------------------------------------------------------- 1
    s.append(P("1. Che cosa contiene il pacchetto", S_H1))
    s.append(P(
        "Il pacchetto e' diviso in due alberi indipendenti, uno per linguaggio. Le due librerie "
        "espongono le stesse funzionalita' con gli stessi nomi, adattati alle convenzioni di "
        "ciascun linguaggio: chi conosce una conosce l'altra."))

    s.append(P("Lato C#", S_H2))
    s.append(table([
        ["Progetto", "Ruolo"],
        ["PayPrint.PagAmico", "la libreria: nessuna dipendenza esterna, solo <font face='Courier'>System.Text.Json</font>. "
                              "Compila per net8.0, netstandard2.0 e net47"],
        ["PayPrint.PagAmico.WinForms", "banco di prova con tutti i comandi, pannello del traffico e finestra di log"],
        ["PayPrint.PagAmico.Tests", "169 test offline: 86 confrontano le stringhe generate con gli esempi dei "
                                    "manuali, 63 fanno parlare il client con un finto pagAmico locale, 20 provano "
                                    "il registro su file e gli errori"],
        ["PayPrint.PagAmico.LiveTest", "collaudo end-to-end: 64 passi in 13 gruppi di default (71 in 15, accendendo la sonda DI e i riavvii), contro simulatore o macchina reale"],
        ["PayPrint.PagAmico.Tap", "proxy TCP che registra il traffico fra un client qualsiasi e la macchina"],
        ["PayPrint.PagAmico.Fill", "riempie le giacenze del simulatore prima di una sessione di prove"],
        ["PayPrint.PagAmico.Demo", "esempio minimo da riga di comando"],
    ], [150, CONTENT_W - 150]))

    s.append(Spacer(1, 6))
    s.append(P("Lato Kotlin", S_H2))
    s.append(table([
        ["Modulo", "Ruolo"],
        ["pagamico-lib", "la libreria (coroutine + kotlinx-serialization), gli stessi test offline (171: "
                         "due in piu', annullo del chiamante e tre processi sullo stesso file) e "
                         "lo stesso collaudo a 64 passi"],
        ["pagamico-desktop", "banco di prova Compose for Desktop, gemello di quello WinForms"],
    ], [150, CONTENT_W - 150]))

    s.append(Spacer(1, 8))
    s.append(callout("Portabilita' su Android",
                     "La libreria Kotlin usa solo <font face='Courier'>java.net.Socket</font>, "
                     "<font face='Courier'>java.math.BigDecimal</font> e "
                     "<font face='Courier'>java.time</font>: funziona su Android con "
                     "<font face='Courier'>minSdk 26</font> (oppure con il core library desugaring), "
                     "il permesso <font face='Courier'>INTERNET</font> e "
                     "<font face='Courier'>kotlinx-coroutines-android</font>. "
                     "Non serve il plugin di serializzazione, perche' si usa solo l'API "
                     "<font face='Courier'>JsonElement</font> a runtime."))

    # ---------------------------------------------------------------- 2
    s.append(P("2. Il protocollo in una pagina", S_H1))
    s.append(P(
        "Il pagAmico e' un server TCP. Il client apre <b>una sola connessione</b> e la tiene aperta: "
        "porta di fabbrica 9100, che il manuale consiglia di cambiare perche' e' la porta di stampa "
        "RAW standard e spesso e' gia' occupata."))

    s.append(table([
        ["Aspetto", "Come funziona"],
        ["Comando", "stringa ASCII a lunghezza fissa, chiusa da CR o CR+LF secondo PayPrint (il manuale "
                    "non lo dice e i suoi esempi ne sono privi; le librerie mandano CR per default): "
                    "<font face='Courier'>IN001050</font> chiede un incasso di 10,50 euro"],
        ["Importi in richiesta", "<b>centesimi</b>, con un numero di cifre fisso per comando "
                                 "(6 per <font face='Courier'>IN</font>, 10 per <font face='Courier'>PA</font>)"],
        ["Importi in risposta", "<b>euro</b> come numero JSON: <font face='Courier'>\"collectedAmount\": 10.5</font>"],
        ["Risposta", "oggetto JSON con sempre gli stessi campi; cambia il valore di "
                     "<font face='Courier'>response</font>"],
        ["Asincronia", "a un comando seguono piu' messaggi: accettazione, parziali, esito finale"],
        ["Risposte non JSON", "testo puro: <font face='Courier'>CMD ERROR</font>, "
                              "<font face='Courier'>OK&nbsp;&nbsp;&nbsp;&nbsp;STST</font>, "
                              "<font face='Courier'>BT1</font>, <font face='Courier'>AN</font>, "
                              "<font face='Courier'>EX</font>, il contenuto di un barcode letto"],
        ["Risposte lunghe", "<font face='Courier'>MV</font> e <font face='Courier'>MI</font> possono "
                            "superare il buffer del socket e terminano con il marcatore "
                            "<font face='Courier'>|\\</font>"],
        ["Comandi muti", "<font face='Courier'>CL, DS, DC, QA, CO, DT, DG, TS</font> non rispondono nulla: "
                         "attendere una risposta li' significa restare appesi"],
    ], [110, CONTENT_W - 110], code_cols=()))

    s.append(Spacer(1, 6))
    s.append(callout("La regola che il manuale non scrive",
                     "Ogni comando va chiuso da <b>CR</b> o <b>CR+LF</b>: lo dice PayPrint, il manuale no. "
                     "Sul simulatore, senza terminatore, il buffer del socket viene interpretato come "
                     "<b>un solo comando</b>: due comandi trasmessi a distanza quasi nulla vengono letti "
                     "insieme e il secondo viene ignorato, senza alcuna risposta e senza errore. "
                     "Misurato sul simulatore: a 0 ms il comando si perde, a 30 ms passa. "
                     "Le librerie chiudono ogni comando con <b>CR</b> per default (dal 14 settembre) e "
                     "impongono ancora una distanza minima fra gli invii, 80 ms per default. PayPrint "
                     "dice la pausa non necessaria con il terminatore, e sul simulatore attuale la raffica "
                     "regge: resta come rete di sicurezza finche' la stessa prova non regge sulla macchina "
                     "(<font face='Courier'>docs/prova-terminatore-cr-2026-09-14.md</font>).", WARN))

    s.append(PageBreak())

    # ---------------------------------------------------------------- 3
    s.append(P("3. Architettura delle librerie", S_H1))
    s.append(P(
        "La stessa struttura in C# e in Kotlin. L'applicazione parla solo con "
        "<font face='Courier'>PagAmicoClient</font>; tutto il resto e' interno o di supporto."))
    s.append(figure(architecture_diagram(), "I blocchi della libreria e il loro rapporto con l'applicazione e con la macchina."))

    s.append(P("3.1 Il separatore dei messaggi", S_H2))
    s.append(P(
        "E' la parte piu' delicata, perche' il protocollo non ha un terminatore unico. Il TCP puo' "
        "spezzare un JSON su piu' letture oppure accorparne due nella stessa, e in mezzo possono "
        "arrivare risposte testuali senza alcun delimitatore. Il separatore lavora cosi':"))
    s.append(figure(framing_diagram(), "Dal flusso di byte ai messaggi: graffe contate per il JSON, silenzio per il testo."))

    s.append(table([
        ["Situazione", "Come viene risolta"],
        ["messaggio JSON", "conteggio delle graffe, ignorando quelle dentro le stringhe: lo scontrino "
                           "POS contiene graffe, virgolette e backslash"],
        ["JSON incompleto", "resta nel buffer finche' non arriva il resto"],
        ["due JSON insieme", "vengono separati e consegnati uno alla volta"],
        ["risposta MV / MI", "il marcatore <font face='Courier'>|\\</font> viene riconosciuto e rimosso"],
        ["testo puro", "chiuso su CR/LF, oppure dopo 150 ms di silenzio, oppure quando inizia un JSON"],
        ["JSON mai completato", "dopo 10 secondi viene emesso come testo grezzo, per non restare bloccati"],
    ], [120, CONTENT_W - 120]))

    s.append(P("3.2 Attese, parziali e annullo", S_H2))
    s.append(P(
        "Ogni comando registra un'attesa con un predicato che riconosce il messaggio finale. I messaggi "
        "intermedi non vengono buttati: sono consegnati all'applicazione come avanzamento, cosi' la cassa "
        "puo' mostrare l'importo che sale mentre il cliente inserisce le monete. "
        "Se l'applicazione annulla l'operazione, la libreria non abbandona l'attesa: manda "
        "<font face='Courier'>AN</font> alla macchina e resta in ascolto dell'esito, perche' l'annullo "
        "comporta la restituzione del denaro gia' inserito e quel dato serve."))

    s.append(P("3.3 Nomi dei campi instabili fra firmware", S_H2))
    s.append(P(
        "Il firmware 8.71 ha accorciato alcuni nomi per far stare il JSON in un unico blocco. "
        "Le chiavi vengono percio' normalizzate (minuscolo, senza underscore e senza spazi) e "
        "mappate su piu' alias, cosi' lo stesso codice funziona con le macchine vecchie e nuove."))
    s.append(table([
        ["Campo", "Varianti gestite"],
        ["numero di serie", "<font face='Courier'>sN</font> (da 8.71) e <font face='Courier'>serialNumber</font>"],
        ["id del movimento", "<font face='Courier'>Id</font> (da 8.71) e <font face='Courier'>OperationId</font>"],
        ["importo trattenuto (controllo)", "<font face='Courier'>committedAmount</font> e il refuso "
                                           "<font face='Courier'>committedAmout</font>; l'importo si legge "
                                           "in <font face='Courier'>collectedAmount</font>"],
        ["scontrino POS", "<font face='Courier'>posFinancial...</font> e <font face='Courier'>PosFinancial...</font>"],
        ["azzeramento BTA", "<font face='Courier'>\"&nbsp;AmountResettedBanknotesInBTA\"</font>, "
                            "con lo spazio iniziale presente nel firmware"],
    ], [120, CONTENT_W - 120]))

    s.append(P("3.4 Un comando alla volta", S_H2))
    s.append(P(
        "I comandi sono <b>serializzati su un mutex</b>: la libreria ne tiene in volo uno solo per "
        "connessione. Serve a non mescolare le risposte, dato che il protocollo non le correla alla "
        "richiesta in nessun modo. La pausa minima fra un invio e il successivo (sezione 8.1) e' "
        "invece imposta da un <b>secondo</b> semaforo, quello di scrittura, che avvolge ogni singolo "
        "invio: vale anche per i comandi che il mutex non lo prendono mai. Sono due meccanismi "
        "distinti, e conviene non confonderli: togliendo il mutex la pausa resterebbe."))
    s.append(callout("La conseguenza da conoscere prima di progettarci sopra",
                     "L'incasso <b>tiene il mutex per tutta la sua durata</b>, fino al timeout di "
                     "transazione. Durante <font face='Courier'>IN</font> la macchina accetta solo "
                     "<font face='Courier'>AN</font> e <font face='Courier'>CM</font>, e agli altri "
                     "risponde <font face='Courier'>BUSY</font>. Dall'11 settembre la libreria lo fa "
                     "rispettare: finche' <font face='Courier'>IsCollecting</font> e' vero, ogni altro "
                     "invio - stato, display, immagini, stampa - <b>lancia subito</b> "
                     "<font face='Courier'>PagAmicoCollectionOpenException</font> senza trasmettere "
                     "nulla. Commit e annullo passano per una <b>via laterale</b> fuori dal mutex: "
                     "partono dopo l'<font face='Courier'>OK</font> di accettazione, e ne parte uno solo "
                     "per incasso. Fino al 10 settembre si accodavano dietro l'incasso, e un display "
                     "partiva davvero provocando un <font face='Courier'>BUSY</font> che chiudeva l'attesa "
                     "(difetti D1 e D3, <font face='Courier'>docs/esito-risposta-payprint.md</font>).",
                     WARN))
    s.append(P(
        "Chi integra deve quindi decidere <b>chi possiede la connessione</b> e che cosa succede se un "
        "secondo interessato - un controllo periodico di presenza, una pagina di diagnostica, il "
        "display - prova a parlare con lo stesso box mentre un incasso e' aperto. Su una API senza "
        "stato la domanda non si pone; qui si'.", S_SMALL))

    s.append(PageBreak())

    # ---------------------------------------------------------------- 4
    s.append(P("4. Che cosa succede sul filo", S_H1))

    s.append(P("4.1 Incasso in contanti", S_H2))
    s.append(P(
        "Il caso piu' importante. L'applicazione chiede 1,50 euro; il cliente inserisce due monete da "
        "1 euro; la macchina eroga 0,50 di resto e chiude."))
    s.append(figure(sequence_diagram(
        ["Applicazione", "Libreria", "pagAmico"],
        [
            (0, 1, "collectCash(1,50)", "call"),
            (1, 2, "IN000150", "call"),
            (2, 1, '{"response":"OK"}  comando accettato', "reply"),
            (2, 1, '{"response":"p"}  incassato 1,00', "reply"),
            (1, 0, "avanzamento: 1,00 di 1,50", "reply"),
            (2, 1, '{"response":"p"}  incassato 2,00', "reply"),
            (1, 0, "avanzamento: 2,00 di 1,50", "reply"),
            (2, 1, '{"response":"IN"}  resto 0,50 erogato', "reply"),
            (1, 0, "esito finale con Id del movimento", "reply"),
        ]), "Un comando, quattro risposte. La libreria consegna i parziali e restituisce solo l'esito."))

    s.append(callout("Il campo da non ignorare mai",
                     "Se la macchina non riesce a comporre il resto per mancanza di un taglio, l'incasso "
                     "si chiude comunque e la differenza finisce in "
                     "<font face='Courier'>amountUnpaid</font>. Il denaro resta dovuto al cliente: "
                     "va segnalato all'operatore, non trattato come una transazione riuscita."))

    s.append(P("4.2 Annullo dell'incasso", S_H2))
    s.append(figure(sequence_diagram(
        ["Applicazione", "Libreria", "pagAmico"],
        [
            (0, 1, "annulla (token o coroutine)", "call"),
            (1, 2, "AN", "call"),
            (1, 1, "resta in attesa dell'esito", "self"),
            (2, 1, '{"response":"AN", "changeReturn": 5.0}', "reply"),
            (1, 0, "annullato, restituiti 5,00", "reply"),
        ]), "La cancellazione non interrompe l'attesa: serve sapere quanto e' stato restituito."))

    s.append(P("4.3 Il difetto scoperto in collaudo", S_H2))
    s.append(P(
        "Sequenza usata nel banco di prova: chiudere la finestra di testo e subito dopo chiedere lo stato "
        "della stampante. Nella prima versione, sul simulatore e con i comandi inviati senza terminatore, "
        "l'attesa scadeva dopo 15 secondi senza che arrivasse nulla."))
    s.append(figure(sequence_diagram(
        ["Libreria", "pagAmico"],
        [
            (0, 1, "DS", "call"),
            (0, 1, "PTSTAT   (1 ms dopo)", "call"),
            (1, 1, "una sola lettura: esegue DS, scarta il resto", "self"),
            (0, 0, "nessuna risposta, timeout dopo 15 s", "note"),
        ], row=20), "Prima: i due comandi arrivano insieme e il secondo sparisce."))
    s.append(figure(sequence_diagram(
        ["Libreria", "pagAmico"],
        [
            (0, 1, "DS", "call"),
            (0, 0, "attesa di 80 ms imposta dalla libreria", "note"),
            (0, 1, "PTSTAT", "call"),
            (1, 0, '{"response":"OK","errorType":"00000000"}', "reply"),
        ], row=20), "Dopo: con la distanza minima fra invii il simulatore risponde regolarmente. Il rimedio "
                    "indicato da PayPrint e' il terminatore CR o CR+LF, default CR dal 14 settembre: la "
                    "pausa resta come rete di sicurezza finche' non e' provato sulla macchina."))

    s.append(PageBreak())

    # ---------------------------------------------------------------- 5
    s.append(P("5. Come si usa", S_H1))

    s.append(P("C#", S_H2))
    s.append(code("""
using var client = new PagAmicoClient("192.168.1.231", 9100);
await client.ConnectAsync();

var stato = await client.GetStatusAsync();                  // [ST]
Console.WriteLine(stato.BanknotesAvailableByDenomination[20]);

var esito = await client.CollectCashAsync(                  // [IN]
    10.50m,
    new Progress<PagAmicoResponse>(p =>
        Console.WriteLine($"incassato {p.CollectedAmount}")));

if (esito.AmountUnpaid > 0)
    Console.WriteLine($"resto non erogato: {esito.AmountUnpaid} EUR");
"""))

    s.append(P("Kotlin", S_H2))
    s.append(code("""
val client = PagAmicoClient("192.168.1.231", 9100)
client.connect()

val stato = client.status()                                 // [ST]
println(stato.banknotesAvailableByDenomination[20])

val esito = client.collectCash(BigDecimal("10.50")) { p ->  // [IN]
    println("incassato ${p.collectedAmount}")
}

esito.amountUnpaid?.takeIf { it.signum() > 0 }?.let {
    println("resto non erogato: $it EUR")
}
"""))

    s.append(P("5.1 I comandi coperti", S_H2))
    s.append(table([
        ["Area", "Comandi"],
        ["Incasso", "IN, I2, PO, IM, AN, CM"],
        ["Erogazione", "PA, P2, PM"],
        ["Contanti e scorte", "ST, M2, MF, BT, AZ, AF, SM, SB, EM, EB"],
        ["Ricariche", "RC, RS, VC, VS, RM, R3, RB, R2, FR"],
        ["POS", "PL, PR, PS, PZ, PP"],
        ["Sistema e movimenti", "CL, RI, LO, MV, MI"],
        ["Display", "DT, DG, DS, DM, DC, DI, QR, QA, TS, ID, CO"],
        ["Immagini", "SF, SI, SR"],
        ["Stampa", "tutti i comandi PT del manuale 2.00, piu' un compositore di scontrini"],
    ], [110, CONTENT_W - 110], code_cols=(1,)))
    s.append(P(
        "Il manuale indica come minimo indispensabile i comandi <font face='Courier'>IN, PO, AN, PA, "
        "RI, ST, CL, RS, RC</font>, il controllo delle soglie minime e la gestione di "
        "<font face='Courier'>errorList</font> ed <font face='Courier'>errorCode</font>: tutto coperto.",
        S_SMALL))

    s.append(P("5.2 Le due giacenze da non confondere", S_H2))
    s.append(P(
        "La macchina espone due grandezze diverse che durante l'esercizio si scollano. La "
        "<b>giacenza reale</b> e' quello che c'e' fisicamente dentro e decide se un resto e' erogabile. "
        "Il <b>fondo cassa</b> e' un valore contabile che cambia solo quando si manda "
        "<font face='Courier'>AF</font>, e serve per la quadratura. La libreria le tiene separate."))
    s.append(table([
        ["", "giacenza reale", "fondo cassa"],
        ["monete", "<font face='Courier'>CoinsByDenomination</font>", "<font face='Courier'>CoinsInStockByDenomination</font>"],
        ["banconote", "<font face='Courier'>BanknotesAvailableByDenomination</font>", "<font face='Courier'>BankNotesInStockByDenomination</font>"],
    ], [70, (CONTENT_W - 70) / 2, (CONTENT_W - 70) / 2]))

    s.append(callout("La trappola dei cassetti",
                     "Le banconote possono essere caricate in ordine qualsiasi e lo stesso taglio puo' "
                     "stare in piu' cassetti. Il taglio si legge dalla posizione 0 di ogni cassetto, "
                     "mai dedotto dalla posizione del cassetto nell'array. "
                     "<font face='Courier'>BanknotesAvailableByDenomination</font> fa la somma nel modo "
                     "corretto: usare quella.", WARN))

    s.append(P("5.3 Log e diagnostica", S_H2))
    s.append(P(
        "Quando un comando non risponde, il traffico grezzo non basta a capire perche': mostra i byte "
        "usciti, non se il client li ha trattenuti, se il messaggio ricevuto era completo o se "
        "l'attesa e' semplicemente scaduta. Per questo la libreria espone tre livelli di traccia "
        "indipendenti, attivabili uno per volta."))

    s.append(table([
        ["Livello", "C#", "Kotlin", "Che cosa registra"],
        ["traffico", "<font face='Courier'>CommandSent</font>, <font face='Courier'>MessageReceived</font>",
         "<font face='Courier'>messages</font>",
         "le stringhe inviate e i messaggi ricevuti, cosi' come sono"],
        ["diagnostica", "<font face='Courier'>Trace</font>", "<font face='Courier'>onTrace</font>",
         "connessione, pause imposte fra un invio e l'altro, byte letti dal socket, separazione dei "
         "messaggi, resto rimasto nel buffer, attese soddisfatte o scadute, messaggi non sollecitati"],
        ["su file", "<font face='Courier'>PagAmicoFileLogger</font>", "<font face='Courier'>PagAmicoFileLogger</font>",
         "un file al giorno con tutto quanto sopra, con orario al millisecondo"],
    ], [55, 105, 70, CONTENT_W - 230]))

    s.append(Spacer(1, 4))
    s.append(P("Una sessione vista dalla diagnostica:", S_SMALL))
    s.append(code("""
..  keepalive TCP: prima sonda dopo 10 s, poi ogni 2 s, caduta dopo 5 sonde senza risposta
..  connesso a 127.0.0.1:9100 (pausa minima fra invii 80 ms, terminatore presente)
..  attesa di 80 ms prima dell'invio: il pagAmico ignora i comandi troppo ravvicinati
..  in attesa dell'esito di 'ST' (timeout 15s)
..  letti 1076 byte dal socket
..  messaggio separato: JSON, 1076 caratteri
..  attesa di 'ST' soddisfatta da: {"response":"ST", ...
"""))
    s.append(P(
        "Le righe che contano davvero sono le ultime due quando <b>non</b> compaiono: se la traccia si "
        "ferma su <i>in attesa</i> e poi dice <i>attesa scaduta</i> senza aver letto un solo byte, il "
        "comando non e' mai arrivato alla macchina. E' esattamente il sintomo del difetto descritto al "
        "capitolo 8.", S_SMALL))

    s.append(P("I due banchi di prova hanno le stesse caselle: <i>registra su file</i>, "
               "<i>diagnostica libreria</i> e una <i>finestra traffico</i> separata, con filtri per "
               "direzione, ricerca testuale, scorrimento automatico ed esportazione."))

    s.append(callout("Uso su .NET Framework",
                     "La libreria C# compila anche per <font face='Courier'>netstandard2.0</font> e "
                     "<font face='Courier'>net47</font>, quindi entra in un progetto legacy senza "
                     "riscritture: il codice moderno resta dentro la libreria e verso l'esterno espone "
                     "solo tipi disponibili ovunque. Su un progetto con "
                     "<font face='Courier'>packages.config</font> vanno pero' aggiunte a mano le "
                     "dipendenze transitive di <font face='Courier'>System.Text.Json</font>, "
                     "che in quel formato non arrivano da sole."))

    s.append(PageBreak())

    # ---------------------------------------------------------------- 6
    s.append(P("6. Errori e stato macchina", S_H1))
    s.append(P(
        "Le anomalie viaggiano su tre campi distinti, che vanno letti insieme."))

    s.append(table([
        ["Campo", "Quando", "Significato"],
        ["errorList", "sempre", "fotografia dello stato della macchina, anche a comando riuscito"],
        ["errorCode", "con <font face='Courier'>response = ER</font>", "perche' il comando non e' andato a buon fine"],
        ["errorType", "insieme a errorCode", "la circostanza: stato del dispositivo, esito, codice del POS"],
    ], [70, 110, CONTENT_W - 180]))

    s.append(P("6.1 errorList: quattro cifre dopo la E", S_H2))
    s.append(table([
        ["Posizione", "Valori"],
        ["1 - pagamento", "0 ok - 1 importo superiore alla disponibilita' di monete - 2 importo non erogabile"],
        ["2 - monete", "0 ok - 1 sottoscorta - 2 troppe monete - 9 esaurite"],
        ["3 - banconote", "0 ok - 1 sottoscorta - 2 troppe - 5 entrambi i cassetti vuoti - "
                          "6 un cassetto vuoto - 9 un taglio esaurito"],
        ["4 - cassetto BTA", "0 ok - 1 prossimo al riempimento - 2 pieno"],
    ], [85, CONTENT_W - 85]))
    s.append(P(
        "Le librerie lo decodificano in avvisi leggibili. Il manuale indica come imprescindibili il "
        "controllo delle soglie minime e la gestione della condizione <i>troppe monete</i>: senza quelli "
        "la cassa arriva a non erogare piu' il resto.", S_SMALL))

    s.append(P("6.2 I codici piu' frequenti", S_H2))
    s.append(table([
        ["Codice", "Significato"],
        ["E100", "comando non eseguibile ora: <font face='Courier'>errorType</font> dice perche' "
                 "(0 fuori servizio, 1 ok, 2 avvio, 3 setup, 5 reboot, 99 occupato)"],
        ["E200 / E201 / E202", "accettatori vuoti, stacker banconote vuoto, hopper monete vuoto"],
        ["E300 / E301", "importo oltre il limite erogabile, oppure oltre la disponibilita'"],
        ["E302 / E303", "banconote o monete insufficienti per comporre il pagamento"],
        ["E500", "il POS non risponde o ha rifiutato: <font face='Courier'>errorType</font> "
                 "contiene il codice Ingenico"],
        ["NOT STARTED", "accettatori guasti: dal FW 8.71 la macchina riparte in modalita' emergenza, "
                        "solo POS"],
    ], [110, CONTENT_W - 110], code_cols=(0,)))

    # ---------------------------------------------------------------- 7
    s.append(P("7. Il simulatore e gli strumenti di prova", S_H1))
    s.append(P(
        "PayPrint distribuisce <b>pagAmico Dev Kit</b>, un'applicazione Windows che contiene un pagAmico "
        "emulato: apre un socket TCP, accetta i comandi documentati e risponde con gli stessi JSON del "
        "dispositivo reale, comprese le giacenze che si aggiornano a ogni operazione. Si avvia dalla "
        "sezione <i>Simulatore</i>, poi ci si collega come a una macchina vera."))

    s.append(figure(sequence_diagram(
        ["Banco di prova", "Tap (9200)", "Simulatore (9100)"],
        [
            (0, 1, "IN000150", "call"),
            (1, 2, "IN000150  (inoltrato)", "call"),
            (2, 1, '{"response":"OK"}', "reply"),
            (1, 0, '{"response":"OK"}', "reply"),
            (1, 1, "annota orario, direzione e distanza", "self"),
        ], row=20), "Il proxy di analisi si inserisce fra client e macchina senza modificare nulla."))

    s.append(table([
        ["Strumento", "A che serve"],
        ["Collaudo automatico", "64 passi in sequenza, divisi in gruppi selezionabili, con esito "
                                "per ciascuno. Esercita tutti i comandi implementati"],
        ["Proxy di analisi", "registra il traffico fra un client e la macchina, segmento per segmento. "
                             "Abbina ogni risposta al comando che l'ha chiesta, misura le pause del "
                             "client e i tempi di risposta della macchina, segnala gli invii troppo "
                             "ravvicinati. Puntandoci il Dev Kit di PayPrint si vede cosa manda la loro "
                             "applicazione: e' il modo per chiudere alcuni dei punti ancora aperti "
                             "(sezione 8.2)"],
        ["Riempimento", "il simulatore si svuota dopo qualche prova. Le sessioni di ricarica non "
                        "aggiungono nulla, perche' sulla macchina vera il contante lo mette l'operatore: "
                        "lo strumento riempie eseguendo incassi, alternando i tagli"],
        ["Log su file", "un file al giorno con tutto il traffico. Il Dev Kit mostra il traffico a video "
                        "ma non lo salva"],
    ], [110, CONTENT_W - 110]))

    s.append(P("7.1 I gruppi del collaudo", S_H2))
    s.append(P(
        "Senza argomenti il collaudo esegue tutti i gruppi tranne <font face='Courier'>riavvii</font>. "
        "Indicandone uno si prova solo quello, utile quando si sta lavorando su un'area sola."))
    s.append(table([
        ["Gruppo", "Copre"],
        ["base", "ST e decodifica delle giacenze, CL, LO"],
        ["cash", "SM, SB, EM, EB, AF, BT"],
        ["collect", "IN con parziali, annullo dal client, CM, PA"],
        ["erogazione", "P2, PM, M2, MF, AZ, I2, IM"],
        ["ricariche", "RM, R3, RB, R2, RS, VC, VS, ciascuna chiusa con FR"],
        ["display", "DT/DS, DM/DC, QR/QA, TS/ID/CO"],
        ["display2", "DG, DI con tastiera numerica"],
        ["print", "PTSTAT e uno scontrino completo da PTSTST a PTSTEN"],
        ["print2", "PTITON/OF, PTDBON/OF, PTUN, PTFB, PTCP, PTPRWR, PTPRRE, PTBCBC, PTPRDT, PTSTAN"],
        ["system", "PR, PL, PO, RC/FR, e un comando inesistente per verificare CMD ERROR"],
        ["pos2", "PLT con ristampa, PS, PP"],
        ["immagini", "SI, SR, SF, e il confronto fra i due incapsulamenti binari"],
        ["movimenti", "MV, MI"],
        ["riavvii", "PZ e RI: esclusi di default, riavviano POS e macchina"],
    ], [70, CONTENT_W - 70], code_cols=(0,)))
    s.append(P(
        "I passi che erogano o spostano banconote leggono prima <font face='Courier'>ST</font> e "
        "scelgono un taglio effettivamente presente: dopo un'erogazione quel taglio potrebbe non "
        "esserci piu', e un collaudo scritto su valori fissi fallirebbe per colpa propria.", S_SMALL))

    s.append(callout("Che cosa il simulatore non riproduce",
                     "Il cliente virtuale si attiva solo sugli incassi: durante una sessione di ricarica "
                     "(<font face='Courier'>RC</font>, <font face='Courier'>RS</font>, "
                     "<font face='Courier'>RM</font>, <font face='Courier'>RB</font>) non entra denaro, "
                     "perche' li' il contante lo inserisce fisicamente l'operatore. Verificato: "
                     "<font face='Courier'>RS</font> seguito da <font face='Courier'>FR</font> lascia le "
                     "giacenze identiche. Nella sezione <i>Simulatore</i> va acceso l'interruttore "
                     "<i>Il cliente inserisce l'importo esatto</i>: senza, il cliente arrotonda per "
                     "eccesso e la macchina restituisce come resto quasi tutto quello che incassa."))

    s.append(PageBreak())

    s.append(P("7.2 Le tabelle del catalogo comandi", S_H2))
    s.append(P(
        "Il Dev Kit e' un'applicazione Flutter e la sua logica Dart e' compilata in "
        "<font face='Courier'>data/app.so</font>. Il file non e' decompilabile, ma conserva i "
        "letterali di stringa, e fra questi c'e' il catalogo comandi che l'applicazione mostra a "
        "video: descrizioni, esempi e tabelle scritti da PayPrint. Le tabelle che seguono vengono "
        "da li' e dalla <i>Guida all'uso</i> che accompagna il Dev Kit, un documento a se' che "
        "riassume i quattro manuali PayPrint e in piu' punti li disambigua. Coprono punti che i "
        "manuali da soli lasciavano aperti."))

    s.append(P("<b>Tipi di codice a barre</b> (terzo carattere di "
               "<font face='Courier'>PTBCBC</font>)"))
    s.append(table([
        ["Cod.", "Tipo", "Cod.", "Tipo"],
        ["0", "UPC-A, 12 caratteri", "5", "ITF"],
        ["1", "UPC-E, 8 caratteri", "6", "CODEBAR"],
        ["2", "EAN13, 13 caratteri", "7", "CODE93"],
        ["3", "EAN8, 8 caratteri", "8", "CODE128"],
        ["4", "CODE39", "", ""],
    ], [34, CONTENT_W / 2 - 34, 34, CONTENT_W / 2 - 34], code_cols=(0, 2)))
    s.append(P(
        "Chiude la contraddizione fra tabella comandi e note di rilascio sui codici 7 e 8. I tipi a "
        "lunghezza fissa rifiutano stringhe di lunghezza diversa.", S_SMALL))

    s.append(P("<b>Causali dei movimenti</b> (ultime tre cifre di "
               "<font face='Courier'>MV</font>, e campo "
               "<font face='Courier'>codice_operazione</font> nella risposta)"))
    s.append(table([
        ["Cod.", "Significato", "Cod.", "Significato"],
        ["000", "tutte le causali", "041", "scarico banconote"],
        ["001", "incasso", "051", "scarico banconote in BTA"],
        ["010", "pagamento", "061", "scarico banconote"],
        ["011", "scarico monete", "071", "scarico monete"],
        ["021", "ricarica monete", "090", "incasso POS"],
        ["031", "ricarica banconote", "", ""],
    ], [34, CONTENT_W / 2 - 34, 34, CONTENT_W / 2 - 34], code_cols=(0, 2)))
    s.append(P(
        "I doppioni sono del produttore, non nostri: nel suo catalogo 011 e 071 portano la stessa "
        "etichetta, e cosi' 041 e 061. Resta da farsi dire che differenza c'e'.", S_SMALL))

    s.append(P("<b>Attributi del display</b> (validi per "
               "<font face='Courier'>DT</font>, <font face='Courier'>DG</font>, "
               "<font face='Courier'>DM</font>, <font face='Courier'>DI</font>, "
               "<font face='Courier'>QR</font>)"))
    s.append(table([
        ["Campo", "Valori"],
        ["colore font", "0 blu, 1 nero, 2 verde, 3 rosso, 4 magenta, 5 ciano, 6 bianco, "
                        "7 grigio, 8 grigio scuro, 9 nero"],
        ["stile font", "0 normale, 1 grassetto, 2 corsivo, 3 grassetto corsivo"],
        ["tastiera (DI)", "0 nascosta, 1 alfanumerica, 2 numerica senza decimali, "
                          "3 numerica con decimali"],
        ["modo lettura (QR)", "0 con tastiera, 1 senza tastiera, 2 tessera sanitaria dal POS, "
                              "3 inserimento tessera, 4 tastiera numerica"],
        ["allineamento", "0 sinistra, 1 centro, 2 destra"],
        ["etichette dei bottoni", "un'etichetta vuota nasconde il pulsante"],
        ["sottolineatura (stampa)", "00 nessuna, 01 livello 1, 02 livello 2"],
        ["testo del barcode", "0 nessuno, 1 sopra, 2 sotto, 3 entrambi"],
    ], [95, CONTENT_W - 95], code_cols=(0,)))
    s.append(P(
        "Le due tastiere non vanno confuse: quella di <font face='Courier'>DI</font> parte da "
        "0 = nascosta, quella di <font face='Courier'>QR</font> da 0 = con tastiera. Le librerie le "
        "tengono in due enumerativi distinti, <font face='Courier'>KeyboardLayout</font> e "
        "<font face='Courier'>KeyboardMode</font>.", S_SMALL))

    s.append(callout(
        "Il formato di DI, finalmente",
        "L'esempio del produttore ha dodici campi:<br/>"
        "<font face='Courier'>DI|16|0|1|SCRIVI NOME|18|1|0|OK|ANNULLA|ESCI|3</font><br/>"
        "dimensione, stile e colore del titolo; il titolo; gli stessi tre attributi per il campo "
        "di input; le tre etichette dei bottoni; il tipo di tastiera. Le librerie ne inviano sette "
        "e il simulatore accetta lo stesso, ma cosi' non si vedono ne' i bottoni ne' la "
        "formattazione del campo. La variante a dodici campi e' stata aggiunta al gruppo "
        "<font face='Courier'>di</font> del collaudo: va confermata sulla macchina vera prima di "
        "cambiare il metodo pubblico.", WARN))

    s.append(P("<b>Altre conferme</b>"))
    s.append(table([
        ["Punto", "Esito"],
        ["<font face='Courier'>PTPRDT_ASCII:</font>",
         "il manuale alternava tre grafie, il Dev Kit usa questa: e' quella che inviamo"],
        ["<font face='Courier'>DT</font>, <font face='Courier'>DG</font>, "
         "<font face='Courier'>DM</font>, <font face='Courier'>QR</font>",
         "gli esempi del produttore coincidono con le nostre stringhe carattere per carattere"],
        ["<font face='Courier'>EM</font> / <font face='Courier'>EB</font>",
         "sigla piu' dodici cifre, due per taglio: formato confermato"],
        ["codici di errore",
         "il Dev Kit conosce DISPAG, ERPWDPAG, ERRPWDPAG, ERRLUNGHEZZA, ERRNONDISP, ERRSCOMAX, "
         "QTAMONETE, QTABANCONOTE: la nostra tabella li ha gia' tutti"],
        ["immagini",
         "rettangolo consigliato 571x520 dip. I delimitatori del payload binario restano non "
         "documentati"],
        ["marcatore <font face='Courier'>|\\</font>",
         "le risposte di <font face='Courier'>MV</font> e <font face='Courier'>MI</font> possono "
         "superare il buffer del socket e finiscono con questo marcatore: il parser lo riconosce e "
         "lo consuma"],
        ["risposte di stampa",
         "in chiaro, non JSON: <font face='Courier'>OK &lt;comando&gt;</font>, "
         "<font face='Courier'>ER BUSY</font>, <font face='Courier'>ER NO-PRINT</font>, "
         "<font face='Courier'>ER CMD-ERROR</font>. Gestite tutte"],
        ["porta 9100",
         "e' quella di fabbrica ma e' la porta RAW di stampa, molto usata: PayPrint consiglia di "
         "cambiarla nel Setup del pagAmico, ad esempio 43775"],
        ["<font face='Courier'>P2</font> risponde <font face='Courier'>PB</font>",
         "non e' una stranezza del firmware: il manuale 2.33 lo scrive a pagina 37, la risposta "
         "positiva di <font face='Courier'>P2</font> porta il tag del comando deprecato. Le "
         "librerie accettano entrambi"],
        ["tipi di barcode",
         "il manuale Protocollo di stampa 2.00 si contraddice da solo: pagina 3 dice 7 = CODE128 "
         "e 8 = CODE93, la sua nota di aggiornamento a pagina 5 dice l'opposto. Il Dev Kit "
         "conferma la seconda, ed e' quella implementata"],
    ], [110, CONTENT_W - 110]))

    s.append(PageBreak())

    # ---------------------------------------------------------------- 8
    s.append(P("8. Esito delle verifiche", S_H1))
    s.append(P(
        "I test offline confrontano le stringhe generate con gli esempi <b>letterali</b> dei manuali "
        "(<font face='Courier'>IN001050</font>, <font face='Courier'>PA0000001050mypassword</font>, "
        "<font face='Courier'>PM001001000000000000</font>, "
        "<font face='Courier'>SM005100005100005100005100005100005100</font>, "
        "<font face='Courier'>EM010101011111</font>, "
        "<font face='Courier'>MV2025/01/01 10:002025/04/22 18:59000</font>, "
        "<font face='Courier'>PTQRQR42www.pagamico.it</font>, "
        "<font face='Courier'>PTBCBC210029876543210123</font>, "
        "<font face='Courier'>QR|...|16|1|2|0</font>). "
        "Il collaudo invece parla con una macchina."))

    s.append(table([
        ["Verifica", "C#", "Kotlin"],
        ["test offline sugli esempi dei manuali", "86 su 86", "86 su 86"],
        ["test offline contro un finto pagAmico (incasso, comandi semplici, invio)", "62 su 62", "62 su 62"],
        ["test offline sul registro su file e sugli errori", "20 su 20", "20 su 20"],
        ["collaudo sul simulatore", "64 su 64", "64 su 64"],
    ], [CONTENT_W - 150, 75, 75]))
    s.append(P(
        "I 64 sono i passi dei <b>gruppi di default</b>. Non coincidono con le 49 chiamate presenti "
        "nel sorgente, perche' tre gruppi girano in ciclo: le sette sessioni di ricarica partono da "
        "un solo punto, i comandi di stampa singoli da un altro, e altrettanto la sonda del comando "
        "non documentato. Accendendo anche quella sonda e i riavvii i passi diventano <b>71</b>.", S_SMALL))
    s.append(P(
        "Il collaudo esercita <b>tutti</b> i comandi implementati: restano fuori i soli "
        "<font face='Courier'>PB</font> e <font face='Courier'>MB</font>, che il manuale marca come "
        "deprecati e sostituisce con <font face='Courier'>P2</font> e "
        "<font face='Courier'>M2</font>.", S_SMALL))

    s.append(P("8.1 Difetti trovati grazie al collaudo", S_H2))
    s.append(table([
        ["Difetto", "Effetto", "Rimedio"],
        ["comandi inviati troppo ravvicinati",
         "sul simulatore, senza terminatore, il secondo veniva ignorato senza risposta; l'attesa "
         "scadeva dopo 15 o 45 secondi",
         "distanza minima fra invii, 80 ms per default, e dal 14 settembre terminatore CR come indica "
         "PayPrint: la pausa resta come rete di sicurezza finche' non e' provata sulla macchina"],
        ["Kotlin: cancellazione confusa con timeout",
         "annullando un incasso il comando <font face='Courier'>AN</font> non partiva e la macchina "
         "restava occupata per i comandi successivi",
         "la cancellazione del chiamante viene propagata come tale"],
    ], [110, 210, CONTENT_W - 320]))

    s.append(P("8.2 Punti da confermare con PayPrint", S_H2))
    s.append(P(
        "L'elenco si e' accorciato da una parte e allungato dall'altra. Il catalogo comandi estratto "
        "dal Dev Kit (sezione 7.2) ha chiuso i tipi di barcode, la grafia di "
        "<font face='Courier'>PTPRDT_ASCII:</font> e le tabelle degli attributi del display. In "
        "compenso l'analisi dell'innesto nel gestionale (capitolo 9) ne ha aperti sei nuovi, di "
        "natura diversa: non sono ambiguita' dei manuali, sono <b>comportamenti della macchina che "
        "il simulatore non permette di osservare</b>. La risposta di PayPrint dell'11 settembre 2026 "
        "ha chiuso il terminatore, <font face='Courier'>CM</font> durante l'incasso e i parziali, e "
        "chiarito solo in parte pausa, messaggi spontanei e incasso abbandonato; "
        "<font face='Courier'>amountPaid</font> lo definiva gia' il manuale. Lo stato di ciascun punto "
        "e' nella tabella; l'analisi, i difetti emersi e le domande nuove stanno in "
        "<font face='Courier'>docs/esito-risposta-payprint.md</font>.", S_SMALL))
    s.append(table([
        ["Punto", "Situazione"],
        ["distanza minima fra comandi", "chiarita in parte: per PayPrint nessuna pausa e' necessaria se i "
                                        "comandi terminano con CR o CR+LF, e il simulatore <i>probabilmente "
                                        "ha qualche difficolta'</i>. Sul simulatore attuale, il 14 settembre, "
                                        "la raffica regge con e senza CR. Gli 80 ms restano come rete di "
                                        "sicurezza finche' una raffica con CR non regge sulla macchina"],
        ["terminatore dei comandi", "chiuso per la macchina: CR o CR+LF, dice PayPrint. Il manuale non lo "
                                    "nomina e i suoi esempi ne sono privi (pp. 12, 15, 43, 66). Provato "
                                    "sul simulatore, dal 14 settembre il client chiude ogni comando con "
                                    "CR per default. Resta da chiedere se il CR serve dopo i pacchetti "
                                    "immagine e con <font face='Courier'>PTPRDT</font>"],
        ["comando DI", "assente dai manuali. Il Dev Kit ne mostra un esempio a dodici campi, molto piu' "
                       "ricco dei sette che inviamo: da confermare sulla macchina vera, il simulatore "
                       "accetta entrambi"],
        ["comando ID", "il manuale scrive <font face='Courier'>TD&lt;JSON&gt;</font> come formato completo "
                       "mentre il comando e' <font face='Courier'>ID</font>"],
        ["invio immagini", "il manuale e il suo esempio Python indicano due incapsulamenti diversi, il Dev "
                           "Kit dice solo <i>fra i delimitatori documentati</i>: sono implementati "
                           "entrambi"],
        ["causali dei movimenti", "confermato che 041 e 061 valgono entrambe <i>scarico banconote</i> e "
                                  "011 e 071 <i>scarico monete</i>: resta da capire che differenza c'e'"],
        ["stampa immagine IM", "presente nel riepilogo dei comandi ma senza sintassi: non implementato"],
        ["<b>CM durante un incasso</b>", "chiuso: PayPrint conferma che durante <font face='Courier'>IN</font> "
                                         "la macchina accetta solo <font face='Courier'>AN</font> e "
                                         "<font face='Courier'>CM</font>; <font face='Courier'>ST</font> va "
                                         "mandato solo a transazione chiusa, prima verrebbe ignorato. Il limite "
                                         "nella libreria e' tolto l'11 settembre: il <font face='Courier'>CM</font> "
                                         "a incasso aperto passa per una via laterale e il collaudo usa l'incasso "
                                         "di libreria. Da chiedere quanti frame seguono "
                                         "<font face='Courier'>CM</font>: sul simulatore due, e la libreria ora "
                                         "chiude sul secondo (difetto D1, corretto)"],
        ["<b>campi della risposta AN</b>", "ancora aperto. Arrivano <font face='Courier'>collectedAmount</font> e "
                                           "<font face='Courier'>amountUnpaid</font>, o solo "
                                           "<font face='Courier'>changeReturn</font>? Senza il primo non si sa se "
                                           "la restituzione e' stata <b>incompleta</b>, cioe' se e' rimasto in "
                                           "macchina denaro del cliente. PayPrint ha spiegato "
                                           "<font face='Courier'>amountUnpaid</font> (resto non erogato per "
                                           "mancanza di monete), ma la frase riguarda il resto di un "
                                           "<font face='Courier'>IN</font> (manuale p. 16), non il rimborso"],
        ["<b>contenuto del parziale p</b>", "chiuso: PayPrint dice che i parziali arrivano a ogni aggiunta di "
                                            "contante e sono <b>cumulativi</b>, quindi uno perso non costa "
                                            "nulla: il successivo porta il totale"],
        ["<b>semantica di amountPaid</b>", "chiuso dal manuale, che lo definisce <i>Importo erogato totale</i> "
                                           "(pp. 9, 59). Nel simulatore vale l'erogato di "
                                           "<font face='Courier'>PA</font> ed e' 0 sul resto di un "
                                           "<font face='Courier'>IN</font>: da confermare se comprende il resto, "
                                           "che oggi va sommato da <font face='Courier'>changeCoins</font> + "
                                           "<font face='Courier'>changeBanknotes</font>"],
        ["<b>messaggi spontanei</b>", "chiarito in parte: <font face='Courier'>CMD ERROR</font> risponde a un "
                                      "comando sconosciuto, <font face='Courier'>BUSY</font> a un comando che la "
                                      "macchina impegnata non puo' eseguire. Sono risposte a comandi nostri, non "
                                      "messaggi spontanei; su barcode, <font face='Courier'>BT1</font> ed "
                                      "<font face='Courier'>EX</font> PayPrint non dice nulla. Il rischio era "
                                      "concreto - un <font face='Courier'>BUSY</font> provocato da un nostro invio "
                                      "a incasso aperto chiudeva l'attesa (difetto D3) - ed e' chiuso l'11 "
                                      "settembre: gli invii laterali sono bloccati, e dopo l'accettazione un "
                                      "testo non tocca l'incasso"],
        ["<b>incasso abbandonato</b>", "chiarito in parte. Dopo <font face='Courier'>IN</font> la macchina non ha "
                                       "timeout e l'incasso resta aperto; PayPrint dice di non metterne uno "
                                       "(<font face='Courier'>I2</font> c'e' ma lo sconsiglia), mentre la libreria "
                                       "allo scadere dei suoi 5 minuti abbandona l'incasso senza mandare "
                                       "<font face='Courier'>AN</font> (difetto D2). Dopo una caduta di rete la "
                                       "macchina accetta la riconnessione dallo stesso IP; dal pannello si chiude "
                                       "con una chiusura forzata (RESTO + password) che restituisce "
                                       "<font face='Courier'>AN</font>. Resta da sapere se i frame arrivano sul "
                                       "nuovo socket e come si scopre quanto e' entrato: "
                                       "<font face='Courier'>LO</font> da' l'<i>ultimo</i> JSON trasmesso, non lo "
                                       "stato corrente, e durante <font face='Courier'>IN</font> e' escluso"],
    ], [130, CONTENT_W - 130]))

    s.append(Spacer(1, 8))
    s.append(callout("Come chiuderli senza aspettare",
                     "Il proxy di analisi risponde da solo ai primi sette: basta puntarci il Dev Kit di "
                     "PayPrint e osservare che cosa manda la loro applicazione, comprese le pause che "
                     "tiene fra un comando e l'altro - non tutti pero': le causali dei movimenti e la "
                     "stampa di immagini restano aperte anche dopo, mentre terminatore e pause li ha "
                     "chiariti PayPrint e sul simulatore sono provati; sulla macchina no. Degli ultimi sei, quattro "
                     "hanno ora una risposta scritta del fornitore e <font face='Courier'>amountPaid</font> "
                     "lo definisce il manuale; quello che resta richiede una macchina vera, "
                     "perche' riguarda che cosa fa il dispositivo in condizioni che il simulatore non "
                     "sa riprodurre - resto insufficiente, incasso interrotto a meta', messaggio "
                     "spontaneo. PayPrint ha offerto il collegamento alla sua macchina.", GOOD))

    s.append(PageBreak())

    # ---------------------------------------------------------------- 9
    s.append(P("9. Innesto in un gestionale esistente", S_H1))
    s.append(P(
        "Questo capitolo riassume l'analisi svolta su <b>Giano</b> (soluzione "
        "<font face='Courier'>Neo.sln</font>), il nostro gestionale per la ristorazione. Giano pilota "
        "gia' una cassa automatica di marca <b>VNE</b>: il pagAmico va <i>accanto</i> a quella, non al "
        "suo posto. L'analisi completa, con i riferimenti riga per riga, sta in "
        "<font face='Courier'>docs/analisi-giano-vne-vs-pagamico.md</font>; qui c'e' quello che serve "
        "a chi lavora sulla libreria."))

    s.append(P("9.1 La superficie da coprire e' piccola", S_H2))
    s.append(P(
        "Il livello VNE di Giano implementa <b>29 comandi</b>. Nel flusso di vendita ne girano "
        "<b>cinque</b>: apertura dell'incasso, interrogazione, annullo, elenco dei pagamenti pendenti, "
        "chiusura sessione. Un sesto - la versione macchina, usata come <i>ping</i> - vive fuori dalla "
        "vendita, nell'avvio hardware e nelle pagine di stato. Tre stanno in rami irraggiungibili "
        "della finestra di pagamento, uno solo e' invocato dalla finestra di prova hardware, e i "
        "restanti diciannove - prelievi, svuotamenti, chiusura cassa, storici, configurazione - non "
        "hanno un solo chiamante: e' copertura di protocollo scritta e mai usata."))
    s.append(P(
        "Non manca quasi niente, quindi. Il lavoro non e' aggiungere comandi: e' <b>tradurre un "
        "modello in un altro</b>."))

    s.append(P("9.2 I due modelli a confronto", S_H2))
    s.append(table([
        ["", "VNE (oggi)", "pagAmico"],
        ["trasporto", "REST/JSON, un POST per operazione", "socket TCP persistente"],
        ["stato della transazione", "sulla <b>macchina</b>, con un identificativo interrogabile",
         "nel <b>client</b>, in memoria"],
        ["avanzamento", "interrogazione ogni 500 ms", "notifiche spinte durante l'incasso"],
        ["concorrenza", "libera: piu' thread, piu' richieste", "un comando alla volta, serializzati"],
        ["transazioni aperte", "una coda, elencabile per identificativo", "una sola, della connessione"],
        ["dopo un riavvio", "si enumerano i pendenti e si annullano alla cieca",
         "resta solo <font face='Courier'>LO</font>, l'ultimo JSON trasmesso"],
    ], [92, 150, CONTENT_W - 242]))
    s.append(P(
        "Tre membri dell'interfaccia hardware di Giano - interrogazione, elenco pendenti, chiusura "
        "sessione - <b>non hanno alcun corrispondente</b>; contando anche le due modalita' di "
        "rifornimento e lo stato cassa completo, i membri da <b>eliminare</b> sono sei. Il thread di "
        "interrogazione non si adatta: si riscrive.", S_SMALL))

    s.append(P("9.3 Il punto che fa piu' male: l'annullo cambia segno contabile", S_H2))
    s.append(P(
        "Giano annulla i pagamenti chiedendo alla macchina di <i>trattenere</i> quanto gia' incassato, "
        "e registra l'operazione come <b>annullata</b>: nessuna vendita, il denaro resta nel "
        "dispositivo. Lo fa in tre chiamanti su quattro."))
    s.append(P(
        "Il comando pagAmico che compie lo stesso gesto fisico e' <font face='Courier'>CM</font>. Ma "
        "<font face='Courier'>CM</font> significa <b>incasso accettato e chiuso</b>, tanto che "
        "restituisce l'importo trattenuto (<font face='Courier'>collectedAmount</font>)."))
    s.append(callout("Non esiste una traduzione neutra",
                     "Stessa azione sul contante, esito contabile <b>opposto</b>. Portare il codice "
                     "attuale di peso significa registrare incassi che non ci sono, oppure restituire "
                     "denaro che andava trattenuto. E' una decisione, e va presa prima di scrivere "
                     "una riga di codice.", WARN))

    s.append(P("9.4 Compatibilita': non e' un problema", S_H2))
    s.append(P(
        "Giano e' WPF su .NET Framework 4.7, con <font face='Courier'>csproj</font> in formato "
        "non-SDK e linguaggio fermo a C# 7.3. Sembrava il punto piu' spinoso dell'innesto; non lo e'."))
    s.append(table([
        ["Fatto", "Conseguenza"],
        ["la libreria dichiara gia' <font face='Courier'>net8.0;netstandard2.0;net47</font> e la DLL "
         "net47 e' compilata", "niente da ricompilare"],
        ["le dieci dipendenze runtime sono gia' in <font face='Courier'>Neo/packages.config</font> alla "
         "versione identica (System.Text.Json 10.0.2 in testa)",
         "zero pacchetti nuovi, zero <i>binding redirect</i> nuovi"],
        ["il vincolo C# 7.3 riguarda il codice <b>di Neo</b>, non un assembly referenziato",
         "la libreria resta a <font face='Courier'>LangVersion latest</font>"],
    ], [175, CONTENT_W - 175]))
    s.append(P(
        "Da qui la scelta: <b>DLL sotto <font face='Courier'>ExternalReferences/</font></b>, non "
        "progetto sotto <font face='Courier'>ExternalProjects/</font>. Ricompilare la libreria in "
        "C# 7.3 costerebbe la rimozione dei <i>nullable reference types</i> su oltre un centinaio di punti della "
        "superficie pubblica - che sono l'unica documentazione formale di cosa puo' essere null in "
        "un'API che maneggia denaro - piu' i namespace a livello di file, le proprieta' "
        "<font face='Courier'>init</font>, i pattern relazionali e le <i>switch expression</i>. "
        "Riscrittura invasiva di codice collaudato, beneficio funzionale nullo."))
    s.append(P(
        "Unica fragilita' segnalata: il framer usa <font face='Courier'>string.Contains(string, "
        "StringComparison)</font>, un overload <b>assente dal BCL di .NET Framework</b>. Compila oggi "
        "solo grazie alla combinazione <font face='Courier'>LangVersion=latest</font> + System.Memory; "
        "sostituirlo con <font face='Courier'>IndexOf(..., StringComparison.OrdinalIgnoreCase) &gt;= 0</font> "
        "renderebbe il target net47 indipendente dal compilatore.", S_SMALL))

    s.append(P("9.5 Che cosa la libreria deve offrire a chi la innesta", S_H2))
    s.append(P(
        "Tre vincoli non evidenti dalla sola lettura dell'API, che chi integra scopre tardi se nessuno "
        "glieli dice. Sono documentati anche nel README, sezione <i>Come e' fatto il client</i>."))
    s.append(table([
        ["Vincolo", "Perche' riguarda chi integra"],
        ["a incasso aperto partono solo AN e CM",
         "ogni altro invio - stato, display, stampa - lancia subito "
         "<font face='Courier'>PagAmicoCollectionOpenException</font>. Commit e annullo passano per una "
         "via laterale fuori dal mutex, dopo l'<font face='Courier'>OK</font>, uno solo per incasso. In "
         "Kotlin, se il chiamante dell'incasso viene cancellato, l'esito arriva a "
         "<font face='Courier'>onOrphanFrame</font>"],
        ["il predicato dell'incasso lavora in due fasi",
         "prima dell'<font face='Courier'>OK</font> un testo e' un rifiuto "
         "(<font face='Courier'>PagAmicoRejectedException</font>, o "
         "<font face='Courier'>PagAmicoBusyException</font>): non e' entrato denaro. Dopo, chiudono solo "
         "l'esito, <font face='Courier'>AN</font> e il <font face='Courier'>CM</font> finale; il resto va "
         "all'evento dei <b>frame orfani</b>, che puo' portare importi e va ascoltato. Corretto l'11 "
         "settembre (difetto D3)"],
        ["gli incassi hanno sei cifre di centesimi",
         "massimo <b>9.999,99 EUR</b> per tutti e quattro - contanti, con timeout, POS, automatico - "
         "oltre il quale la libreria solleva un'eccezione <b>prima di trasmettere</b>. L'erogazione "
         "singola ne ha dieci: il tetto e' proprio dell'incasso. Serve un controllo esplicito a "
         "monte. Per i contanti PayPrint conferma il tetto; per il POS lo dice <i>maggiore</i>, in "
         "contrasto con il manuale, dove <font face='Courier'>PO</font> ha sei cifre fisse (p. 17), e "
         "con la libreria (<font face='Courier'>PagAmicoCommands.cs:110</font>): punto aperto"],
    ], [140, CONTENT_W - 140]))

    s.append(P("9.6 Il collo di bottiglia e' nel gestionale, non nel box", S_H2))
    s.append(P(
        "Vale la pena dirlo perche' cambia le priorita' del lavoro. Oggi, quando la finestra di "
        "pagamento di Giano si chiude, l'unica informazione che attraversa il confine verso il resto "
        "dell'applicazione e' <b>un booleano</b>: annullato oppure no. Importo effettivamente "
        "incassato, resto erogato, resto <i>non</i> erogato, identificativo della transazione: niente "
        "di tutto questo esce, e niente viene mai scritto da nessuna parte."))
    s.append(P(
        "Il pagAmico offre <b>piu'</b> dati del VNE - un importo non erogato in un campo dedicato "
        "invece che da ricavare per sottrazione, e un identificativo di movimento persistente sul "
        "dispositivo, riconciliabile a posteriori con lo storico. Ma finche' quella riga resta un "
        "booleano, si perdono esattamente come si perdono oggi."))
    s.append(callout("Conseguenza pratica",
                     "Sostituire il box senza allargare quel confine e' lavoro sprecato: i buchi "
                     "contabili che esistono oggi resterebbero tutti, con un dispositivo diverso "
                     "sotto. Il rovescio positivo e' che con il pagAmico l'esito e' un oggetto unico, "
                     "quindi la decisione <i>finalizzo o no</i> si puo' prendere da un punto solo."))

    s.append(Spacer(1, 12))
    s.append(P("Prime Software S.r.l. - documento tecnico interno", S_SMALL))

    return s


if __name__ == "__main__":
    target = os.path.join(os.path.dirname(os.path.abspath(__file__)), "Integrazione-pagAmico.pdf")
    build(target)
    print("creato:", target)
