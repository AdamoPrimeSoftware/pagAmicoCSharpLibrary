# -*- coding: utf-8 -*-
"""
Legge i file di log prodotti dalle librerie pagAmico e dice se una sessione di prove e'
andata bene o dove si e' rotta.

    python analizza_log.py                      tutti i log di oggi
    python analizza_log.py --data 2026-09-03    di un giorno preciso
    python analizza_log.py percorso\\di\\un.log   un file specifico

Cerca da solo in %LOCALAPPDATA%\\PayPrint.PagAmico\\logs.

Che cosa controlla, in ordine di gravita':
  1. comandi rimasti senza risposta (esclusi quelli che per protocollo non rispondono)
  2. comandi inviati troppo ravvicinati: sotto i 30 ms il pagAmico ne perde uno
  3. risposte di errore (ER) con il codice e la spiegazione
  4. errorList: sottoscorta, troppe monete, cassetto BTA pieno
  5. attese scadute e messaggi non sollecitati, dalla diagnostica interna
  6. copertura: quali comandi implementati non sono mai stati provati

Non serve la macchina ne' il simulatore: lavora sui file gia' scritti.
"""

import argparse
import json
import os
import re
import sys
from collections import Counter, OrderedDict
from datetime import datetime


# --------------------------------------------------------------------------- tabelle

# Comandi che per protocollo NON rispondono: la loro assenza di risposta non e' un difetto.
# Fonte: Guida pagAmico Dev Kit.
SILENZIOSI = {"CL", "DS", "DC", "QA", "CO", "DT", "DG", "TS"}

# Comandi che una risposta ce l'hanno, ma non subito o non sempre. Segnalarli come "senza
# risposta" sarebbe gridare al lupo su una sessione andata bene.
#   ID  popola la lista e risponde "EX" solo quando l'utente preme il bottone di uscita
#   SF, SI, SR  invii binari e ripristino del logo: il manuale non prevede riscontro
DIFFERITI = {
    "ID": "risponde EX solo quando l'utente chiude la lista",
    "SF": "invio binario del logo, nessun riscontro previsto",
    "SI": "invio binario dell'immagine, nessun riscontro previsto",
    "SR": "ripristino del logo, nessun riscontro previsto",
}

# Oltre questa distanza fra due comandi si considera finita una sessione e cominciata la
# successiva: un file giornaliero contiene piu' avvii dello stesso programma, e senza questo
# taglio la media degli intervalli verrebbe falsata dalle pause fra un avvio e l'altro.
STACCO_SESSIONE_MS = 60_000

# Tutti i comandi implementati dalle librerie, per categoria. Serve per la copertura:
# dire non solo "quello che hai provato funziona" ma anche "questo non l'hai mai toccato".
CATEGORIE = OrderedDict([
    ("incasso",      ["IN", "I2", "PO", "IM", "AN", "CM"]),
    ("erogazione",   ["PA", "P2", "PM"]),
    ("manutenzione", ["M2", "MF", "BT", "AZ", "AF", "SM", "SB", "EM", "EB", "RI"]),
    ("ricariche",    ["RM", "R3", "RB", "R2", "RC", "RS", "VC", "VS", "FR"]),
    ("stato",        ["ST", "CL", "LO"]),
    ("pos",          ["PL", "PR", "PS", "PZ", "PP"]),
    ("movimenti",    ["MV", "MI"]),
    ("display",      ["QR", "QA", "DT", "DG", "DS", "DM", "DC", "DI", "TS", "ID", "CO"]),
    ("immagini",     ["SF", "SI", "SR"]),
    ("stampa",       ["PT"]),
])

# I due comandi che il manuale marca come deprecati: la loro assenza non e' una lacuna.
DEPRECATI = {"PB", "MB"}

ERRORI = {
    "E100": "comando non eseguibile ora (errorType dice perche')",
    "E200": "accettatori vuoti",
    "E201": "stacker banconote vuoto",
    "E202": "hopper monete vuoto",
    "E300": "importo oltre il limite erogabile",
    "E301": "importo oltre la disponibilita'",
    "E302": "banconote insufficienti per comporre il pagamento",
    "E303": "monete insufficienti per comporre il pagamento",
    "E500": "il POS non risponde o ha rifiutato (errorType = codice Ingenico)",
}

ERROR_TYPE = {
    "0": "fuori servizio", "1": "ok", "2": "avvio", "3": "setup",
    "5": "reboot", "99": "occupato",
}

# errorList = E + 4 cifre: pagamento, monete, banconote, cassetto BTA
ERROR_LIST = [
    ("pagamento", {"1": "importo superiore alla disponibilita' di monete",
                   "2": "importo non erogabile"}),
    ("monete",    {"1": "sottoscorta", "2": "troppe monete", "9": "esaurite"}),
    ("banconote", {"1": "sottoscorta", "2": "troppe", "5": "entrambi i cassetti vuoti",
                   "6": "un cassetto vuoto", "9": "un taglio esaurito"}),
    ("cassetto BTA", {"1": "prossimo al riempimento", "2": "pieno"}),
]

# Sotto questa distanza il pagAmico legge due comandi come uno solo e ne perde uno.
SOGLIA_RAVVICINATI_MS = 30

# I banchi di prova scrivono la direzione dopo la sigla ("TX >  IN001000", "RX <  {...}"),
# le librerie no ("TX  ST"): il marcatore e' opzionale. La sigla arriva a 4 caratteri
# perche' i banchi usano anche "ERR!".
RIGA = re.compile(
    r"^(?P<ts>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3})\s+"
    r"(?P<kind>\S{1,4})\s+(?:[<>]\s+)?(?P<payload>.*)$")


# --------------------------------------------------------------------------- lettura

class Riga:
    __slots__ = ("ts", "kind", "payload")

    def __init__(self, ts, kind, payload):
        self.ts = ts
        self.kind = kind
        self.payload = payload


def cartella_predefinita():
    base = os.environ.get("LOCALAPPDATA")
    if base:
        return os.path.join(base, "PayPrint.PagAmico", "logs")
    return os.path.join(os.path.expanduser("~"), ".payprint-pagamico", "logs")


def leggi(percorso):
    righe = []
    with open(percorso, encoding="utf-8", errors="replace") as f:
        for testo in f:
            m = RIGA.match(testo.rstrip("\n"))
            if not m:
                # riga di continuazione di un messaggio multiriga: si attacca alla precedente
                if righe:
                    righe[-1].payload += " " + testo.strip()
                continue
            ts = datetime.strptime(m.group("ts"), "%Y-%m-%d %H:%M:%S.%f")
            righe.append(Riga(ts, m.group("kind"), m.group("payload")))
    return righe


# --------------------------------------------------------------------------- analisi

def sigla(comando):
    """
    Le prime due lettere identificano il comando; la stampa usa il prefisso PT.
    Gli invii binari non finiscono nel log come stringa di comando ma come segnaposto
    "[SF] payload binario di N byte": la sigla va letta da li' dentro.
    """
    testo = comando.strip()
    if testo.startswith("[") and "]" in testo:
        return testo[1:testo.index("]")].strip().upper()[:2]
    if testo.startswith("PT"):
        return "PT"
    return testo[:2].upper()


def decodifica_error_list(valore):
    """E0110 -> ['monete: sottoscorta']. Stringa vuota se e' tutto a posto."""
    if not valore or not valore.startswith("E") or len(valore) < 5:
        return []
    avvisi = []
    for cifra, (nome, mappa) in zip(valore[1:5], ERROR_LIST):
        if cifra != "0" and cifra in mappa:
            avvisi.append("%s: %s" % (nome, mappa[cifra]))
    return avvisi


def json_di(payload):
    testo = payload.strip()
    if not testo.startswith("{"):
        return None
    try:
        return json.loads(testo)
    except ValueError:
        return None


class Analisi:
    def __init__(self, percorso, righe):
        self.percorso = percorso
        self.righe = righe
        self.comandi = Counter()
        self.senza_risposta = []
        self.ravvicinati = []
        self.errori = []
        self.esiti = Counter()
        self.avvisi_scorte = Counter()
        self.attese_scadute = []
        self.non_sollecitati = []
        self.risposte = Counter()
        self.tempi = []
        self.intervalli = []
        self.differiti = Counter()
        self.sessioni = 1
        self.disconnessioni = []

    # ------------------------------------------------------------------
    def esegui(self):
        ultimo_tx = None          # ultimo comando inviato: serve per gli intervalli
        in_attesa = None          # comando che aspetta ancora il suo esito
        for riga in self.righe:
            if riga.kind == "TX":
                s = sigla(riga.payload)
                self.comandi[s] += 1
                if ultimo_tx is not None:
                    gap = (riga.ts - ultimo_tx[0].ts).total_seconds() * 1000
                    if gap > STACCO_SESSIONE_MS:
                        self.sessioni += 1          # nuovo avvio dentro lo stesso file
                    else:
                        self.intervalli.append(gap)
                        if gap < SOGLIA_RAVVICINATI_MS:
                            self.ravvicinati.append((riga.ts, ultimo_tx[1], s, gap))
                ultimo_tx = [riga, s, False]

                # I silenziosi non rispondono mai. Infilarne uno mentre si aspetta l'esito
                # di un DM o di un DI - il "Chiudi" premuto prima che l'utente scelga - non
                # deve far passare per persa la risposta che sta ancora per arrivare.
                if s not in SILENZIOSI:
                    self._chiudi(in_attesa)
                    in_attesa = ultimo_tx

            elif riga.kind == "RX":
                if in_attesa is not None:
                    if not in_attesa[2]:
                        self.tempi.append((riga.ts - in_attesa[0].ts).total_seconds() * 1000)
                    in_attesa[2] = True
                self._risposta(riga)

            elif riga.kind == "..":
                testo = riga.payload
                if "scaduta" in testo or "SCADUTA" in testo:
                    self.attese_scadute.append((riga.ts, testo))
                if "non sollecitat" in testo:
                    self.non_sollecitati.append((riga.ts, testo))

            elif riga.kind == "--" and "disconness" in riga.payload:
                self.disconnessioni.append((riga.ts, riga.payload))

        self._chiudi(in_attesa)
        return self

    def _chiudi(self, ultimo_tx):
        if ultimo_tx is None or ultimo_tx[2]:
            return
        if ultimo_tx[1] in SILENZIOSI:
            return
        if ultimo_tx[1] in DIFFERITI:
            self.differiti[ultimo_tx[1]] += 1
            return
        self.senza_risposta.append((ultimo_tx[0].ts, ultimo_tx[0].payload[:60]))

    def _risposta(self, riga):
        dati = json_di(riga.payload)
        if dati is None:
            self.risposte[riga.payload.strip()[:20] or "(vuota)"] += 1
            return

        tipo = str(dati.get("response", "?"))
        self.risposte[tipo] += 1

        codice = str(dati.get("errorCode", "")).strip()
        tipo_err = str(dati.get("errorType", "")).strip()
        if codice not in ("", "None", "OK"):
            if tipo == "ER":
                self.errori.append((riga.ts, codice, tipo_err))
            else:
                # comando riuscito che porta comunque un codice: e' un esito, non un guasto.
                # Esempio: IM risponde CONT per dire che il cliente ha pagato in contanti.
                self.esiti[(codice, tipo_err)] += 1

        for chiave in ("errorList", "errorlist"):
            if chiave in dati:
                for avviso in decodifica_error_list(str(dati[chiave])):
                    self.avvisi_scorte[avviso] += 1
                break

    # ------------------------------------------------------------------
    @property
    def provati(self):
        return set(self.comandi)

    def mancanti(self):
        fuori = OrderedDict()
        for categoria, comandi in CATEGORIE.items():
            assenti = [c for c in comandi if c not in self.provati and c not in DEPRECATI]
            if assenti:
                fuori[categoria] = assenti
        return fuori


# --------------------------------------------------------------------------- stampa

def barra(titolo):
    print()
    print(titolo)
    print("-" * max(len(titolo), 40))


def statistiche(valori):
    if not valori:
        return "nessuna"
    return "min %.0f ms, media %.0f ms, max %.0f ms" % (
        min(valori), sum(valori) / len(valori), max(valori))


def riporta(a):
    nome = os.path.basename(a.percorso)
    print()
    print("=" * 78)
    print("  %s" % nome)
    print("=" * 78)

    if not a.righe:
        print("file vuoto.")
        return 0

    inizio, fine = a.righe[0].ts, a.righe[-1].ts
    durata = (fine - inizio).total_seconds()
    print("dalle %s alle %s  (%.0f secondi, %d righe%s)" % (
        inizio.strftime("%H:%M:%S"), fine.strftime("%H:%M:%S"), durata, len(a.righe),
        ", %d avvii distinti" % a.sessioni if a.sessioni > 1 else ""))
    print("comandi inviati: %d  |  risposte ricevute: %d" % (
        sum(a.comandi.values()), sum(a.risposte.values())))
    print("distanza fra comandi: %s  (pause fra un avvio e l'altro escluse)"
          % statistiche(a.intervalli))
    print("tempo di risposta:    %s" % statistiche(a.tempi))

    problemi = 0

    if a.differiti:
        print("senza riscontro ma per progetto: %s" % "  ".join(
            "%s x%d" % (c, n) for c, n in sorted(a.differiti.items())))

    # 1 --------------------------------------------------------------- senza risposta
    if a.senza_risposta:
        problemi += len(a.senza_risposta)
        barra("!! %d comandi rimasti senza risposta" % len(a.senza_risposta))
        for ts, testo in a.senza_risposta[:15]:
            print("   %s  %s" % (ts.strftime("%H:%M:%S.%f")[:-3], testo))
        if len(a.senza_risposta) > 15:
            print("   ... e altri %d" % (len(a.senza_risposta) - 15))
        print("   I comandi che per protocollo non rispondono (%s) sono gia' esclusi."
              % ", ".join(sorted(SILENZIOSI)))

    # 2 --------------------------------------------------------------- ravvicinati
    if a.ravvicinati:
        problemi += len(a.ravvicinati)
        barra("!! %d comandi inviati a meno di %d ms dal precedente"
              % (len(a.ravvicinati), SOGLIA_RAVVICINATI_MS))
        for ts, prima, dopo, gap in a.ravvicinati[:10]:
            print("   %s  %s -> %s dopo %.0f ms" % (
                ts.strftime("%H:%M:%S.%f")[:-3], prima, dopo, gap))
        print("   Il pagAmico legge il buffer del socket in un colpo solo: uno dei due si perde.")

    # 3 --------------------------------------------------------------- errori
    if a.errori:
        barra("Risposte di errore: %d" % len(a.errori))
        visti = Counter((c, t) for _, c, t in a.errori)
        for (codice, tipo_err), quante in visti.most_common():
            spiega = ERRORI.get(codice, "")
            if codice == "E100" and tipo_err in ERROR_TYPE:
                spiega = "%s -> %s" % (spiega, ERROR_TYPE[tipo_err])
            elif tipo_err:
                spiega = "%s (errorType %s)" % (spiega, tipo_err)
            print("   %-6s x%-3d %s" % (codice, quante, spiega))

    if a.esiti:
        barra("Esiti riportati da comandi riusciti")
        for (codice, tipo_err), quante in a.esiti.most_common():
            print("   %-14s x%-3d %s" % (codice, quante,
                                         ("errorType %s" % tipo_err) if tipo_err else ""))
        print("   Non sono guasti: sono comandi andati a buon fine che riportano un esito.")

    # 4 --------------------------------------------------------------- scorte
    if a.avvisi_scorte:
        barra("Stato delle scorte segnalato dalla macchina")
        for avviso, quante in a.avvisi_scorte.most_common():
            print("   x%-4d %s" % (quante, avviso))

    # 5 --------------------------------------------------------------- diagnostica
    if a.attese_scadute:
        problemi += len(a.attese_scadute)
        barra("!! %d attese scadute" % len(a.attese_scadute))
        for ts, testo in a.attese_scadute[:10]:
            print("   %s  %s" % (ts.strftime("%H:%M:%S.%f")[:-3], testo[:100]))

    if a.non_sollecitati:
        barra("Messaggi arrivati senza un comando in sospeso: %d" % len(a.non_sollecitati))
        for ts, testo in a.non_sollecitati[:5]:
            print("   %s  %s" % (ts.strftime("%H:%M:%S.%f")[:-3], testo[:100]))

    # 6 --------------------------------------------------------------- copertura
    barra("Comandi provati: %d diversi" % len(a.provati))
    for categoria, comandi in CATEGORIE.items():
        provati = [c for c in comandi if c in a.provati]
        if provati:
            print("   %-13s %s" % (categoria, "  ".join(
                "%s x%d" % (c, a.comandi[c]) for c in provati)))

    mancanti = a.mancanti()
    if mancanti:
        barra("Mai provati in questa sessione")
        for categoria, comandi in mancanti.items():
            print("   %-13s %s" % (categoria, "  ".join(comandi)))
        print("   Non e' un errore: e' quello che resta da coprire a mano.")

    # verdetto ---------------------------------------------------------
    barra("Esito")
    if problemi == 0:
        print("   Nessun problema di comunicazione.")
        if a.errori:
            print("   Ci sono risposte di errore, ma sono risposte: la macchina ha ricevuto e")
            print("   capito i comandi. Vanno lette come esito, non come guasto del trasporto.")
    else:
        print("   %d punti da guardare, elencati sopra." % problemi)

    return problemi


# --------------------------------------------------------------------------- avvio

def main():
    p = argparse.ArgumentParser(description="Analizza i log delle librerie pagAmico.")
    p.add_argument("file", nargs="*", help="file di log da analizzare")
    p.add_argument("--data", help="giorno da analizzare, formato aaaa-mm-gg (default: oggi)")
    p.add_argument("--cartella", help="cartella dei log (default: %%LOCALAPPDATA%%\\PayPrint.PagAmico\\logs)")
    args = p.parse_args()

    percorsi = list(args.file)
    if not percorsi:
        cartella = args.cartella or cartella_predefinita()
        giorno = args.data or datetime.now().strftime("%Y-%m-%d")
        if not os.path.isdir(cartella):
            print("Cartella dei log non trovata: %s" % cartella)
            return 2
        percorsi = sorted(os.path.join(cartella, n) for n in os.listdir(cartella)
                          if n.endswith(".log") and giorno in n)
        if not percorsi:
            print("Nessun log del %s in %s" % (giorno, cartella))
            print("File presenti: %s" % ", ".join(sorted(os.listdir(cartella))[-10:]))
            return 2

    tap = [x for x in percorsi if os.path.basename(x).startswith("tap-")]
    percorsi = [x for x in percorsi if x not in tap]
    if tap:
        print("Saltati %d tracciati del proxy (formato proprio, con il riepilogo gia' in fondo "
              "al file): %s" % (len(tap), ", ".join(os.path.basename(x) for x in tap)))

    if not percorsi:
        print("Nessun log di libreria da analizzare.")
        return 2

    totale = 0
    for percorso in percorsi:
        totale += riporta(Analisi(percorso, leggi(percorso)).esegui())

    print()
    print("=" * 78)
    print("  %d file analizzati, %d punti da guardare in tutto." % (len(percorsi), totale))
    return 1 if totale else 0


if __name__ == "__main__":
    sys.exit(main())
