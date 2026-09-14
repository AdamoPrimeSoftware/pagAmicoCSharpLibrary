# -*- coding: utf-8 -*-
"""
Converte i documenti Markdown di questa cartella in PDF, con lo stesso stile dei due
documenti generati a mano (genera_documentazione.py e genera_guida_prove.py).

    python genera_pdf_da_md.py                 tutti i quadro-*.md
    python genera_pdf_da_md.py quadro-01-*.md  solo quelli indicati

Sottoinsieme di Markdown riconosciuto - volutamente piccolo, cosi' la resa e' prevedibile:

    # Titolo            copertina (solo il primo, e la riga successiva in corsivo e' il sottotitolo)
    ## Capitolo         titolo di primo livello
    ### Sezione         titolo di secondo livello
    #### Sotto          titolo di terzo livello
    testo               paragrafo giustificato
    - voce / 1. voce    elenco puntato o numerato
    | a | b |           tabella (con la riga di trattini sotto l'intestazione)
    ```...```           blocco di codice
    > **Titolo**        riquadro; il grassetto iniziale diventa il titolo del riquadro
      > testo           (>! rosso, >+ verde)
    ---                 salto pagina
    **g** *c* `m`       grassetto, corsivo, monospaziato

Richiede reportlab (pip install reportlab).
"""

import glob
import os
import re
import sys

from reportlab.lib.units import mm
from reportlab.pdfbase.pdfmetrics import stringWidth
from reportlab.platypus import (
    BaseDocTemplate, Frame, PageBreak, PageTemplate, Paragraph, Spacer,
)

from genera_documentazione import (
    ACCENT, CONTENT_W, GOOD, INK, MARGIN, MUTED, PAGE_H, PAGE_W, RULE, WARN,
    S_BODY, S_H1, S_H2, S_SMALL, S_SUBTITLE, S_TITLE,
    P, callout, code, table,
)

from reportlab.lib.styles import ParagraphStyle

S_H3 = ParagraphStyle("h3", parent=S_BODY, fontName="Helvetica-Bold",
                      fontSize=10, leading=13, textColor=INK,
                      spaceBefore=9, spaceAfter=3, alignment=0)
S_LI = ParagraphStyle("li", parent=S_BODY, leftIndent=11, bulletIndent=2, spaceAfter=3)


# --------------------------------------------------------------------------- testo in linea

def inline(text):
    """Da Markdown in linea al sottoinsieme XML che capisce reportlab."""
    # prima le entita', altrimenti si mangiano i tag che aggiungo dopo
    text = text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")

    # monospaziato: prima di tutto il resto, cosi' non interpreto il markup dentro il codice
    segnaposto = []

    def _mono(m):
        segnaposto.append(m.group(1))
        return "\x00%d\x00" % (len(segnaposto) - 1)

    text = re.sub(r"`([^`]+)`", _mono, text)

    text = re.sub(r"\*\*([^*]+)\*\*", r"<b>\1</b>", text)
    text = re.sub(r"(?<![\w*])\*([^*\n]+)\*(?![\w*])", r"<i>\1</i>", text)
    text = re.sub(r"~~([^~]+)~~", r"<strike>\1</strike>", text)
    # [testo](link) -> solo il testo: i PDF di questa cartella non hanno collegamenti
    text = re.sub(r"\[([^\]]+)\]\([^)]*\)", r"\1", text)

    def _restore(m):
        return "<font face='Courier' size='8.3'>%s</font>" % segnaposto[int(m.group(1))]

    return re.sub(r"\x00(\d+)\x00", _restore, text)


def _plain(text):
    """Testo senza markup, per misurare la larghezza delle colonne."""
    t = re.sub(r"`([^`]+)`", r"\1", text)
    t = re.sub(r"\*\*([^*]+)\*\*", r"\1", t)
    t = re.sub(r"\[([^\]]+)\]\([^)]*\)", r"\1", t)
    return t.strip()


# --------------------------------------------------------------------------- tabelle

def _split_row(line):
    line = line.strip()
    if line.startswith("|"):
        line = line[1:]
    if line.endswith("|"):
        line = line[:-1]
    return [c.strip() for c in line.split("|")]


def _is_separator(line):
    cells = _split_row(line)
    return bool(cells) and all(re.fullmatch(r":?-{2,}:?", c) for c in cells)


PADDING = 12.0          # 6 punti per lato, come nello stile delle tabelle
_LARGHEZZE = {}         # memoria delle misure: stringWidth e' lento e lo chiamiamo tanto


def _misura(parola, grassetto=False, courier=False):
    chiave = (parola, grassetto, courier)
    if chiave not in _LARGHEZZE:
        if courier:
            font, corpo = "Courier", 8.3
        else:
            font, corpo = ("Helvetica-Bold" if grassetto else "Helvetica"), 8.6
        _LARGHEZZE[chiave] = stringWidth(parola, font, corpo)
    return _LARGHEZZE[chiave]


def _minimo_cella(testo, grassetto):
    """Larghezza sotto la quale la cella non riesce a mandare a capo: la parola piu' lunga.

    Il monospaziato non si spezza mai (un nome di metodo o un codice comando va tenuto
    intero), quindi conta con le metriche del Courier."""
    if not testo:
        return 0.0
    massimo = 0.0
    for pezzo in re.findall(r"`([^`]+)`", testo):
        massimo = max(massimo, _misura(pezzo, courier=True))
    for parola in _plain(testo).split():
        massimo = max(massimo, _misura(parola, grassetto=grassetto))
    return massimo


def _widths(rows):
    """Larghezze di colonna: prima si garantisce a ognuna quanto le serve per non spezzare
    le parole, poi si distribuisce l'avanzo in proporzione a quanto testo contiene."""
    n = len(rows[0])

    minimi, pesi = [], []
    for c in range(n):
        celle = [(r[c] if c < len(r) else "") for r in rows]
        minimi.append(min(CONTENT_W / 2.0,
                          max(_minimo_cella(t, i == 0) for i, t in enumerate(celle)) + PADDING))
        lung = [len(_plain(t)) for t in celle]
        # la media conta piu' del massimo: una cella lunga non deve prendersi mezza pagina
        pesi.append(max(1.0, (sum(lung) / float(len(lung))) * 0.65 + max(lung) * 0.35))

    avanzo = CONTENT_W - sum(minimi)
    if avanzo <= 0:
        # non ci sta nemmeno il minimo: si ripiega sul proporzionale puro
        tot = sum(minimi) or float(n)
        return [CONTENT_W * m / tot for m in minimi]

    tot = sum(pesi)
    w = [minimi[c] + avanzo * pesi[c] / tot for c in range(n)]
    w[-1] += CONTENT_W - sum(w)
    return w


# --------------------------------------------------------------------------- riquadri

def _callout_color(marcatore):
    return {"!": WARN, "+": GOOD}.get(marcatore, ACCENT)


def _flush_callout(buf, marcatore, story):
    if not buf:
        return
    testo = " ".join(buf).strip()

    # Il marcatore di colore si accetta in due forme: fuori dal grassetto (>! **Titolo**) e
    # dentro (> **! Titolo**). La seconda e' quella che viene naturale scrivendo, quindi va
    # riconosciuta: altrimenti il "!" resta stampato nel titolo e il riquadro non prende colore.
    m = re.match(r"\*\*\s*([!+])\s*(.*)$", testo, re.S)
    if m:
        marcatore = m.group(1)
        testo = "**" + m.group(2)

    # Il grassetto iniziale diventa titolo solo se chiude davvero una frase: se dopo c'e' una
    # virgola la frase prosegue, e spezzarla renderebbe il riquadro illeggibile.
    titolo, corpo = "", testo
    m = re.match(r"\*\*(.+?)\*\*(.*)$", testo, re.S)
    if m:
        resto = m.group(2)
        chiude = (not resto.strip()) or resto[:1] in (".", ":")
        if chiude and len(m.group(1)) < 90 and resto.strip(" .:"):
            titolo, corpo = m.group(1), resto.lstrip(" .:")

    story.append(callout(inline(titolo), inline(corpo), _callout_color(marcatore)))
    story.append(Spacer(1, 5))
    del buf[:]


# --------------------------------------------------------------------------- conversione

def story_da_markdown(righe):
    s = []
    i = 0
    n = len(righe)
    titolo_fatto = False
    para = []
    cbuf = []
    cmark = ""

    def flush_para():
        if para:
            s.append(P(inline(" ".join(para).strip())))
            del para[:]

    while i < n:
        riga = righe[i].rstrip("\n")
        nudo = riga.strip()

        # ---- blocco di codice
        if nudo.startswith("```"):
            flush_para()
            _flush_callout(cbuf, cmark, s)
            i += 1
            blocco = []
            while i < n and not righe[i].strip().startswith("```"):
                blocco.append(righe[i].rstrip("\n"))
                i += 1
            i += 1
            s.append(code("\n".join(blocco)))
            continue

        # ---- riquadro
        if nudo.startswith(">"):
            flush_para()
            corpo = nudo[1:]
            if corpo[:1] in ("!", "+"):
                cmark = corpo[0]
                corpo = corpo[1:]
            cbuf.append(corpo.strip())
            i += 1
            continue
        if cbuf and not nudo:
            _flush_callout(cbuf, cmark, s)
            cmark = ""
            i += 1
            continue

        # ---- riga vuota
        if not nudo:
            flush_para()
            i += 1
            continue

        # ---- salto pagina
        if re.fullmatch(r"-{3,}|\*{3,}|_{3,}", nudo):
            flush_para()
            s.append(PageBreak())
            i += 1
            continue

        # ---- titoli
        m = re.match(r"(#{1,4})\s+(.*)$", nudo)
        if m:
            flush_para()
            livello, testo = len(m.group(1)), m.group(2).strip()
            if livello == 1 and not titolo_fatto:
                titolo_fatto = True
                s.append(Spacer(1, 30 * mm))
                s.append(P(inline(testo), S_TITLE))
                # sottotitolo: la prima riga non vuota successiva, se e' in corsivo
                j = i + 1
                while j < n and not righe[j].strip():
                    j += 1
                if j < n:
                    sub = righe[j].strip()
                    if sub.startswith("*") and sub.endswith("*") and not sub.startswith("**"):
                        s.append(P(inline(sub.strip("*")), S_SUBTITLE))
                        i = j
                s.append(Spacer(1, 10 * mm))
            else:
                stile = {1: S_H1, 2: S_H1, 3: S_H2, 4: S_H3}[livello]
                s.append(P(inline(testo), stile))
            i += 1
            continue

        # ---- tabella
        if nudo.startswith("|") and i + 1 < n and _is_separator(righe[i + 1]):
            flush_para()
            intestazione = _split_row(nudo)
            i += 2
            righe_tab = [intestazione]
            while i < n and righe[i].strip().startswith("|"):
                cells = _split_row(righe[i])
                cells += [""] * (len(intestazione) - len(cells))
                righe_tab.append(cells[:len(intestazione)])
                i += 1
            dati = [[inline(c) for c in r] for r in righe_tab]
            s.append(table(dati, _widths(righe_tab)))
            s.append(Spacer(1, 5))
            continue

        # ---- elenchi
        m = re.match(r"([-*+]|\d+[.)])\s+(.*)$", nudo)
        if m:
            flush_para()
            numerato = not m.group(1) in ("-", "*", "+")
            contatore = 0
            voci = []
            while i < n:
                r = righe[i].strip()
                mm_ = re.match(r"([-*+]|\d+[.)])\s+(.*)$", r)
                if mm_:
                    contatore += 1
                    voci.append([mm_.group(2)])
                elif r and voci and righe[i][:1] in (" ", "\t"):
                    voci[-1].append(r)          # continuazione indentata
                else:
                    break
                i += 1
            for k, v in enumerate(voci, 1):
                marcatore = "%d." % k if numerato else "•"
                s.append(Paragraph(inline(" ".join(v)), S_LI, bulletText=marcatore))
            s.append(Spacer(1, 4))
            continue

        # ---- paragrafo
        para.append(nudo)
        i += 1

    flush_para()
    _flush_callout(cbuf, cmark, s)
    return s


# --------------------------------------------------------------------------- documento

def build(md_path, pdf_path=None, titolo_piede=None):
    with open(md_path, encoding="utf-8") as fh:
        righe = fh.readlines()

    if pdf_path is None:
        pdf_path = os.path.splitext(md_path)[0] + ".pdf"

    if titolo_piede is None:
        titolo_piede = "documento"
        for r in righe:
            if r.startswith("# "):
                titolo_piede = _plain(r[2:]).strip()
                break

    doc = BaseDocTemplate(pdf_path, pagesize=(PAGE_W, PAGE_H),
                          leftMargin=MARGIN, rightMargin=MARGIN,
                          topMargin=MARGIN, bottomMargin=MARGIN + 6 * mm,
                          title=titolo_piede, author="Prime Software S.r.l.")

    frame = Frame(MARGIN, MARGIN + 6 * mm, CONTENT_W, PAGE_H - 2 * MARGIN - 6 * mm, id="main")

    def decorate(canvas, document):
        canvas.saveState()
        canvas.setFont("Helvetica", 7.5)
        canvas.setFillColor(MUTED)
        if document.page > 1:
            canvas.drawString(MARGIN, MARGIN + 2 * mm, titolo_piede)
            canvas.drawRightString(PAGE_W - MARGIN, MARGIN + 2 * mm, str(document.page))
            canvas.setStrokeColor(RULE)
            canvas.setLineWidth(0.5)
            canvas.line(MARGIN, MARGIN + 5.4 * mm, PAGE_W - MARGIN, MARGIN + 5.4 * mm)
        canvas.restoreState()

    doc.addPageTemplates([PageTemplate(id="all", frames=[frame], onPage=decorate)])
    doc.build(story_da_markdown(righe))
    return pdf_path


def main(argv):
    qui = os.path.dirname(os.path.abspath(__file__))
    if argv:
        sorgenti = []
        for a in argv:
            trovati = glob.glob(a if os.path.isabs(a) else os.path.join(qui, a))
            if not trovati:
                print("nessun file per: %s" % a)
            sorgenti += trovati
    else:
        sorgenti = sorted(glob.glob(os.path.join(qui, "quadro-*.md")))

    if not sorgenti:
        print("niente da convertire")
        return 1

    for md in sorgenti:
        out = build(md)
        print("creato: %s" % out)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
