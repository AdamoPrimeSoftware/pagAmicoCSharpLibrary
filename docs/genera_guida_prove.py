# -*- coding: utf-8 -*-
"""
Genera la guida operativa alle prove: come collaudare le librerie pagAmico contro il
simulatore del Dev Kit, avviando i programmi dalle IDE (Visual Studio e IntelliJ IDEA).

    python genera_guida_prove.py

Produce: Guida-prove-pagAmico.pdf nella stessa cartella.
Richiede reportlab (pip install reportlab).

Lo stile e i disegni sono condivisi con genera_documentazione.py: un solo posto da
toccare se cambia l'impaginazione.
"""

import os

from reportlab.lib.pagesizes import A4
from reportlab.lib.units import mm
from reportlab.platypus import (
    BaseDocTemplate, Frame, PageBreak, PageTemplate, Spacer,
)

from genera_documentazione import (
    CONTENT_W, GOOD, MARGIN, MUTED, PAGE_H, PAGE_W, RULE, WARN,
    S_H1, S_H2, S_SMALL, S_SUBTITLE, S_TITLE,
    P, callout, code, figure, sequence_diagram, table,
)

TITLE = "Guida alle prove - pagAmico"

VS = "Visual Studio"
IJ = "IntelliJ IDEA"


# --------------------------------------------------------------------------- documento

def build(path):
    doc = BaseDocTemplate(path, pagesize=A4,
                          leftMargin=MARGIN, rightMargin=MARGIN,
                          topMargin=MARGIN, bottomMargin=MARGIN + 6 * mm,
                          title=TITLE, author="Prime Software S.r.l.")

    frame = Frame(MARGIN, MARGIN + 6 * mm, CONTENT_W, PAGE_H - 2 * MARGIN - 6 * mm, id="main")

    def decorate(canvas, document):
        canvas.saveState()
        canvas.setFont("Helvetica", 7.5)
        canvas.setFillColor(MUTED)
        if document.page > 1:
            canvas.drawString(MARGIN, MARGIN + 2 * mm, TITLE)
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
    s.append(Spacer(1, 32 * mm))
    s.append(P("Guida alle prove", S_TITLE))
    s.append(P("Come collaudare le librerie e i banchi di prova pagAmico<br/>"
               "contro il simulatore del pagAmico Dev Kit, dalle IDE", S_SUBTITLE))
    s.append(Spacer(1, 12 * mm))

    s.append(table([
        ["Che cosa si prova", "Libreria C#, libreria Kotlin, i due banchi di prova, il collaudo "
                              "automatico, il proxy di analisi"],
        ["Contro che cosa", "il pagAmico emulato dal Dev Kit di PayPrint, in locale su 127.0.0.1"],
        ["Da dove si lancia", "Visual Studio per i progetti C#, IntelliJ IDEA per i moduli Kotlin. "
                              "Tutto quello che serve e' gia' configurato: nessun comando da digitare"],
        ["Quanto dura", "una decina di minuti per il giro completo automatico nei due linguaggi; "
                        "il resto e' esplorazione a mano"],
        ["Serve la macchina", "no. Tutto quello che segue si fa senza il pagAmico in ufficio"],
    ], [95, CONTENT_W - 95], header=False))

    s.append(Spacer(1, 8 * mm))
    s.append(callout("Come leggere questa guida",
                     "Il capitolo 2 e' l'indice delle configurazioni pronte: i profili di "
                     "Visual Studio e le configurazioni di IntelliJ sono <b>numerati nell'ordine in cui "
                     "si eseguono</b>, quindi il menu a tendina dell'IDE e' la sequenza delle prove, "
                     "dall'alto verso il basso. I capitoli da 3 a 7 sono quella sequenza spiegata, "
                     "dall'8 al 12 gli strumenti e i casi particolari. Il <b>13</b> sta a se': sono le "
                     "prove che mancano per innestare il pagAmico nel gestionale: quelle a cui PayPrint "
                     "non ha ancora risposto vanno fatte prima di scrivere il codice che le usa. In "
                     "appendice gli equivalenti da riga di "
                     "comando, per quando serve automatizzare."))

    s.append(PageBreak())

    # ---------------------------------------------------------------- 1
    s.append(P("1. Preparazione", S_H1))
    s.append(P(
        "Serve una volta sola. Il codice sta in quattro repository, da clonare <b>affiancati</b> nella "
        "stessa cartella e con questi nomi: le demo trovano la libreria con un percorso relativo. "
        "Ogni repository ha due remote, <font face='Courier'>github</font> e "
        "<font face='Courier'>HardDiskEsterno</font> (G: a casa, E: in ufficio); i comandi di clone e "
        "di push sono in <font face='Courier'>docs/avvio-progetti.md</font>."))

    s.append(table([
        ["IDE", "Che cosa si apre", "Al primo avvio"],
        [VS, "<font face='Courier'>pagAmico_CSharp_Demo\\PayPrint.PagAmico.Demos.slnx</font>: le "
             "cinque demo piu' la libreria, presa dalla cartella accanto. Per i soli test: "
             "<font face='Courier'>pagAmico_CSharp_Lib\\PayPrint.PagAmico.slnx</font>",
         "ripristina i pacchetti NuGet da solo. Compilare la soluzione una volta "
         "(<b>Compila &gt; Compila soluzione</b>). Per i file .slnx serve Visual Studio 2022 17.13 o successivo"],
        [IJ, "la <b>cartella</b> <font face='Courier'>pagAmico_Kotlin_Demo</font> (banco, con la libreria "
             "inclusa da <font face='Courier'>includeBuild</font>) oppure "
             "<font face='Courier'>pagAmico_Kotlin_Lib</font> (test e collaudo)",
         "avvia la sincronizzazione Gradle e scarica le dipendenze. La prima volta ci vuole qualche "
         "minuto, le successive sono immediate"],
    ], [70, 165, CONTENT_W - 235]))

    s.append(P(
        "Servono inoltre il <b>pagAmico Dev Kit</b> di PayPrint (contiene il simulatore, si installa "
        "con <font face='Courier'>pagAmico-DevKit-Setup-1.0.0.exe</font> e finisce in "
        "<font face='Courier'>C:\\Program Files\\pagAmico Dev Kit</font>), il <b>.NET 8 SDK</b> "
        "(gia' presente se Visual Studio 2022 e' aggiornato) e un <b>JDK 17</b> aggiornato, indicato a "
        "Gradle in <font face='Courier'>%USERPROFILE%\\.gradle\\gradle.properties</font> "
        "(<font face='Courier'>docs/avvio-progetti.md</font>)."))

    s.append(P("1.1 I sette progetti C#", S_H2))
    s.append(table([
        ["Progetto", "A che serve", "Si avvia?"],
        ["PayPrint.PagAmico", "la libreria", "no, e' una libreria"],
        ["PayPrint.PagAmico.Tests", "170 test offline, solo 127.0.0.1", "si, nessun argomento"],
        ["PayPrint.PagAmico.WinForms", "il banco di prova", "si, nessun argomento"],
        ["PayPrint.PagAmico.LiveTest", "collaudo automatico, 64 passi", "si, 7 profili"],
        ["PayPrint.PagAmico.Fill", "riempie le giacenze del simulatore", "si, 4 profili"],
        ["PayPrint.PagAmico.Tap", "proxy che registra il traffico", "si, 3 profili"],
        ["PayPrint.PagAmico.Demo", "esempio d'uso minimo", "si, 2 profili"],
    ], [130, 175, CONTENT_W - 305]))

    s.append(P("1.2 I due moduli Kotlin", S_H2))
    s.append(table([
        ["Modulo", "A che serve", "Si avvia?"],
        ["pagamico-lib", "la libreria, piu' i test offline, il collaudo e la demo",
         "si, 3 configurazioni"],
        ["pagamico-desktop", "il banco di prova Compose for Desktop", "si, 1 configurazione"],
    ], [95, 210, CONTENT_W - 305]))

    s.append(PageBreak())

    # ---------------------------------------------------------------- 2
    s.append(P("2. Le configurazioni gia' pronte", S_H1))
    s.append(P(
        "Nessuna delle prove chiede di scrivere argomenti a mano: sono tutti salvati nel repository, "
        "quindi compaiono nell'IDE appena si apre il progetto. I nomi sono numerati nell'ordine in cui "
        "si eseguono."))

    s.append(P("2.1 Visual Studio: i profili di avvio", S_H2))
    s.append(P(
        "Ogni progetto C# che vuole argomenti ha un file "
        "<font face='Courier'>Properties\\launchSettings.json</font>. Visual Studio li mostra nel "
        "<b>menu a tendina accanto al tasto verde di avvio</b>: si sceglie il profilo, si preme F5 "
        "e gli argomenti sono quelli. Per cambiare progetto: tasto destro sul progetto in Esplora "
        "soluzioni, <b>Imposta come progetto di avvio</b>."))

    s.append(table([
        ["Progetto", "Profilo", "Argomenti"],
        ["LiveTest", "1 - Collaudo simulatore (completo)", "127.0.0.1 9100"],
        ["", "2 - Collaudo simulatore (solo base)", "127.0.0.1 9100 base"],
        ["", "3 - Collaudo simulatore (incassi ed erogazioni)", "127.0.0.1 9100 collect,erogazione"],
        ["", "4 - Collaudo simulatore (display e stampa)",
         "127.0.0.1 9100 display,display2,print,print2"],
        ["", "5 - Collaudo simulatore (sonda DI)", "127.0.0.1 9100 di"],
        ["", "6 - Collaudo macchina reale", "192.168.1.231 9100"],
        ["", "7 - Collaudo macchina reale (solo base)", "192.168.1.231 9100 base"],
        ["Fill", "1 - Riempi simulatore (predefinito)", "127.0.0.1 9100"],
        ["", "2 - Riempi simulatore e allinea fondo cassa",
         "127.0.0.1 9100 --monete 40 --banconote 1200 --aggiorna-fondo"],
        ["", "3 - Solo monete", "127.0.0.1 9100 --monete 40 --banconote 0"],
        ["", "4 - Solo banconote a 1200", "127.0.0.1 9100 --monete 0 --banconote 1200"],
        ["Tap", "1 - Proxy verso il simulatore (9200 -&gt; 9100)",
         "--listen 9200 --target 127.0.0.1:9100"],
        ["", "2 - Proxy verso il simulatore con dump esadecimale",
         "--listen 9200 --target 127.0.0.1:9100 --hex"],
        ["", "3 - Proxy verso la macchina reale", "--listen 9200 --target 192.168.1.231:9100"],
        ["Demo", "1 - Demo simulatore", "127.0.0.1 9100 1,50"],
        ["", "2 - Demo macchina reale", "192.168.1.231 9100 1,50"],
    ], [55, 190, CONTENT_W - 245], code_cols=(2,)))

    s.append(P(
        "<b>Tests</b> e <b>WinForms</b> non hanno profili perche' non vogliono argomenti: basta "
        "impostarli come progetto di avvio e premere F5.", S_SMALL))

    s.append(P("2.2 IntelliJ IDEA: le configurazioni di esecuzione", S_H2))
    s.append(P(
        "Sono nelle cartelle <font face='Courier'>.run</font> dei due repository Kotlin (1, 2 e 4 in "
        "<font face='Courier'>pagAmico_Kotlin_Lib</font>, 3 in <font face='Courier'>pagAmico_Kotlin_Demo</font>) "
        "e IntelliJ le carica da solo. "
        "Compaiono nel <b>menu a tendina in alto a destra</b>, accanto al tasto verde di avvio."))

    s.append(table([
        ["Configurazione", "Che cosa lancia"],
        ["1 - Test offline", "i 171 test offline della libreria Kotlin"],
        ["2 - Collaudo simulatore", "i 64 passi contro 127.0.0.1:9100"],
        ["3 - Banco di prova (Compose)", "l'applicazione desktop con tutti i comandi"],
        ["4 - Collaudo macchina reale", "i 64 passi contro 192.168.1.231:9100"],
    ], [130, CONTENT_W - 130]))

    s.append(P(
        "Sono quattro, non una per combinazione: le configurazioni di IntelliJ stanno tutte nello "
        "stesso menu, quindi ogni voce in piu' e' rumore. Per provare un gruppo solo si modificano "
        "gli argomenti della <b>2</b>, aggiungendo i gruppi come terzo argomento separati da virgola "
        "(<font face='Courier'>--args=\"127.0.0.1 9100 collect,erogazione\"</font>); per la demo si "
        "cambia la classe di avvio in "
        "<font face='Courier'>-PmainClass=it.payprint.pagamico.demo.DemoKt</font>. In Visual Studio "
        "i profili sono piu' numerosi perche' stanno sotto il rispettivo progetto: non si vedono "
        "finche' non si seleziona quello.", S_SMALL))

    s.append(callout("Se l'IP della macchina non e' 192.168.1.231",
                     "E' un segnaposto. Il giorno in cui arriva il pagAmico si legge l'indirizzo vero a "
                     "display, in <i>Menu Servizi</i>, e si corregge una volta nei profili: in "
                     "Visual Studio da <b>Debug &gt; Proprieta' di debug</b>, in IntelliJ modificando la "
                     "configurazione. Da quel momento resta salvato."))

    s.append(PageBreak())

    # ---------------------------------------------------------------- 3
    s.append(P("3. Passo 1: i test offline", S_H1))
    s.append(P(
        "Si fanno per primi perche' non serve niente acceso: confrontano le stringhe generate dalla "
        "libreria con gli esempi <b>letterali</b> dei manuali. Durano un paio di secondi. Se qui "
        "qualcosa fallisce e' inutile passare al simulatore: il difetto e' nella composizione dei "
        "comandi, non nella comunicazione."))

    s.append(table([
        ["", "Che cosa fare", "Atteso"],
        [VS, "tasto destro su <b>PayPrint.PagAmico.Tests</b>, <b>Imposta come progetto di avvio</b>, "
             "poi <b>Ctrl+F5</b> (senza debug, cosi' la finestra resta aperta)",
         "<font face='Courier'>170 test superati, 0 falliti</font>"],
        [IJ, "configurazione <b>1 - Test offline</b>, tasto verde",
         "<font face='Courier'>171 test superati, 0 falliti</font> nella finestra <i>Run</i>"],
    ], [70, 210, CONTENT_W - 280]))

    s.append(P(
        "Vanno rilanciati dopo ogni modifica alla libreria, prima di qualsiasi altra prova.", S_SMALL))

    # ---------------------------------------------------------------- 4
    s.append(P("4. Passo 2: accendere il simulatore", S_H1))
    s.append(P(
        "Aprire <font face='Courier'>pagamico_devkit.exe</font>, andare nella sezione "
        "<b>Simulatore</b> e premere <b>Avvia</b>. Il Dev Kit apre un socket TCP sulla porta "
        "<b>9100</b> e da quel momento si comporta come un pagAmico: accetta i comandi documentati, "
        "risponde con gli stessi JSON e aggiorna le giacenze a ogni operazione."))
    s.append(P("Lasciarlo aperto per tutta la sessione: chiudendolo cadono tutte le connessioni."))

    s.append(P("4.1 Verifica veloce che sia in ascolto", S_H2))
    s.append(P(
        "Dal terminale integrato dell'IDE (in Visual Studio <b>Visualizza &gt; Terminale</b>, in "
        "IntelliJ la linguetta <i>Terminal</i> in basso):"))
    s.append(code("""
netstat -ano | findstr :9100
"""))
    s.append(P("Una riga in stato <font face='Courier'>LISTENING</font> vuol dire che il simulatore "
               "e' pronto. Nessuna riga: non e' partito, ricontrollare il Dev Kit.", S_SMALL))

    s.append(callout("Se la porta 9100 e' occupata",
                     "La 9100 e' la porta di stampa RAW standard: capita che sia gia' presa da un driver "
                     "di stampante. In quel caso si sceglie un'altra porta nel simulatore, e la si "
                     "corregge nei profili di avvio al posto di 9100. Sulla macchina vera il manuale "
                     "consiglia comunque di spostarsi, per esempio sulla 43775.", WARN))

    # ---------------------------------------------------------------- 5
    s.append(P("5. Passo 3: riempire le giacenze", S_H1))
    s.append(P(
        "Il simulatore parte vuoto, e dopo qualche giro di prove si svuota di nuovo. Senza monete "
        "l'erogazione del resto smette di funzionare: il collaudo comincia a segnalare "
        "<font face='Courier'>amountUnpaid</font> valorizzato e "
        "<font face='Courier'>errorList</font> con la seconda cifra a 9. Non e' un difetto della "
        "libreria, e' la cassa vuota."))

    s.append(callout(
        "Prima accendere l'interruttore, o non riempie niente",
        "Dev Kit, sezione <i>Simulatore</i>: <b>Il cliente inserisce l'importo esatto</b>. Se e' "
        "spento, il cliente virtuale paga sempre col taglio successivo e la macchina deve erogare "
        "il resto. Il ciclo dei tagli si chiude allora a saldo zero: cinque giri accumulano due "
        "euro in monete e il sesto, pagato con una banconota da 5, li restituisce tutti come "
        "resto. Sessanta giri per non spostare nulla. Con l'interruttore acceso non c'e' resto e "
        "l'incassato resta tutto dentro.", WARN))

    s.append(table([
        ["", "Che cosa fare"],
        ["", "Dev Kit, sezione <i>Simulatore</i>: accendere <b>Il cliente inserisce l'importo "
             "esatto</b>"],
        [VS, "chiudere la connessione del banco (<b>Disconnetti</b>): il simulatore serve un client "
             "per volta, altrimenti Fill non riesce nemmeno a collegarsi"],
        [VS, "progetto di avvio <b>PayPrint.PagAmico.Fill</b>, profilo "
             "<b>2 - Riempi simulatore e allinea fondo cassa</b>, Ctrl+F5"],
        [IJ, "non serve: il riempimento e' solo lato C#, e riempie la stessa macchina che poi provera' "
             "anche il collaudo Kotlin"],
    ], [70, CONTENT_W - 70]))

    s.append(P(
        "Dura qualche minuto: riempie eseguendo <b>incassi</b> ripetuti, alternando i tagli "
        "(0,05 / 0,10 / 0,20 / 0,50 / 1,00 / 2,00 per le monete, 5 / 10 / 20 / 50 per le banconote) "
        "cosi' la macchina si ritrova spiccioli di ogni valore e riesce a comporre qualsiasi resto. "
        "E' quello che porta <font face='Courier'>errorList</font> da <font face='Courier'>E0910</font> "
        "a <font face='Courier'>E0110</font>. Il profilo 2 chiude mandando "
        "<font face='Courier'>AF</font>, che riallinea il fondo cassa contabile alla giacenza reale: "
        "durante le prove le due grandezze si scollano."))

    s.append(callout("Mai contro una macchina vera",
                     "<font face='Courier'>PagAmicoFill</font> riempie eseguendo incassi reali. Sul "
                     "simulatore li completa un cliente virtuale; su una macchina vera aprirebbe "
                     "sessioni di incasso a ripetizione. Per questo non esiste un profilo che punti a "
                     "un indirizzo diverso da 127.0.0.1, e non va aggiunto.", WARN))

    s.append(P("5.1 Perche' non si usano le sessioni di ricarica", S_H2))
    s.append(P(
        "Sarebbe la strada naturale, ma sul simulatore non funziona: "
        "<font face='Courier'>RC</font>, <font face='Courier'>RS</font>, "
        "<font face='Courier'>RM</font> e <font face='Courier'>RB</font> aprono regolarmente la "
        "sessione, ma non entra nulla, perche' sulla macchina vera il contante lo inserisce "
        "fisicamente l'operatore e il cliente virtuale del simulatore si attiva solo sugli incassi. "
        "Verificato: <font face='Courier'>RS</font> seguito da <font face='Courier'>FR</font> lascia "
        "le giacenze identiche.", S_SMALL))

    s.append(PageBreak())

    # ---------------------------------------------------------------- 6
    s.append(P("6. Passo 4: il collaudo automatico", S_H1))
    s.append(P(
        "E' la prova che conta: 64 passi in sequenza che esercitano <b>tutti</b> i comandi "
        "implementati, con esito per ciascuno. Va lanciato una volta per linguaggio."))

    s.append(table([
        ["", "Che cosa fare"],
        [VS, "progetto di avvio <b>PayPrint.PagAmico.LiveTest</b>, profilo "
             "<b>1 - Collaudo simulatore (completo)</b>, Ctrl+F5"],
        [IJ, "configurazione <b>2 - Collaudo simulatore</b>, tasto verde"],
    ], [70, CONTENT_W - 70]))

    s.append(P("6.1 Come si legge l'esito", S_H2))
    s.append(P("Ogni passo si annuncia mentre viene eseguito, e alla fine c'e' il riepilogo:"))
    s.append(code("""
--- [B1] ST stato macchina
    => OK  monete 41,75 EUR, banconote 1200 EUR, 96 movimenti

============================================================================
OK    [B1] ST stato macchina             monete 41,75 EUR, banconote 1200 EUR
OK    [C3] IN annullo dal client         annullato, incassato 0,50, reso 0,50
OK    [P2] P2 erogazione taglio scelto   erogate 1 banconote da 10 EUR
...

64 passi riusciti, 0 falliti
"""))
    s.append(table([
        ["Esito", "Significato"],
        ["64 passi riusciti, 0 falliti", "tutto a posto. Il processo esce con codice 0. Sono i passi "
                                        "dei 13 gruppi di default: nel sorgente le chiamate sono 49, ma tre "
                                        "gruppi girano in ciclo. Con il profilo della sonda DI e i riavvii "
                                        "si arriva a 71"],
        ["qualche FAIL", "la riga porta il tipo di eccezione e il messaggio. Prima di sospettare la "
                         "libreria, guardare il capitolo 10: quasi sempre e' la cassa vuota o il "
                         "simulatore rimasto occupato"],
        ["CONNESSIONE FALLITA", "il simulatore non e' partito, oppure e' su un'altra porta"],
    ], [130, CONTENT_W - 130]))

    s.append(P(
        "Il collaudo scrive anche un file di log completo, e ne stampa il percorso come prima riga: "
        "<font face='Courier'>Log della sessione: ...\\collaudo-2026-09-03.log</font>.", S_SMALL))

    s.append(P("6.2 Provare un gruppo solo", S_H2))
    s.append(P(
        "Il giro completo dura qualche minuto. Quando si sta lavorando su un'area sola conviene un "
        "gruppo alla volta: in Visual Studio sono i profili dal 2 al 5, in IntelliJ si modificano gli "
        "argomenti della configurazione 2. Sotto, che cosa copre ciascun gruppo."))
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
        ["di", "sonda le quattro varianti plausibili del comando DI"],
        ["riavvii", "PZ e RI. Esclusi di default: riavviano POS e macchina"],
    ], [70, CONTENT_W - 70], code_cols=(0,)))

    s.append(PageBreak())

    # ---------------------------------------------------------------- 7
    s.append(P("7. Passo 5: le prove a mano dai banchi", S_H1))
    s.append(P(
        "I due banchi di prova sono gemelli: stesse nove schede, stessi comandi, stesso pannello del "
        "traffico. Servono per provare un singolo comando, riprodurre un caso segnalato, o vedere "
        "coi propri occhi che cosa risponde la macchina."))

    s.append(table([
        ["", "Che cosa fare"],
        [VS, "tasto destro su <b>PayPrint.PagAmico.WinForms</b>, <b>Imposta come progetto di avvio</b>, "
             "poi F5. Non ha profili: non vuole argomenti"],
        [IJ, "configurazione <b>3 - Banco di prova (Compose)</b>, tasto verde. La prima volta Gradle "
             "compila anche il modulo desktop, poi e' immediato"],
    ], [70, CONTENT_W - 70]))

    s.append(P("7.1 Collegarsi", S_H2))
    s.append(P(
        "Scheda <b>Connessione</b>, pulsante <b>Simulatore locale</b>: imposta 127.0.0.1 e 9100 e "
        "connette in un colpo solo. Il pallino in alto diventa verde. Per la macchina vera si "
        "scrivono IP e porta a mano e si preme <b>Connetti</b>."))
    s.append(P(
        "Sempre in questa scheda ci sono le due opzioni di protocollo: il <b>terminatore comandi</b> "
        "(parte con <font face='Courier'>\\r</font>, cioe' CR, come chiede PayPrint e come il default "
        "delle librerie dal 14 settembre; <font face='Courier'>\\r\\n</font> per CR+LF, vuoto per "
        "nessuno) e la <b>password</b> richiesta dai "
        "comandi che muovono denaro (<font face='Courier'>PA, P2, PM, M2, MF, BT, AF, AZ</font>)."))

    s.append(P("7.2 Le nove schede", S_H2))
    s.append(table([
        ["Scheda", "Che cosa ci si fa"],
        ["Connessione", "collegamento, opzioni di protocollo, percorso del file di log"],
        ["Incasso", "IN, I2, PO, IM, con annullo e commit"],
        ["Contanti", "giacenze, travasi, azzeramenti, soglie"],
        ["Ricariche", "le sessioni di carico e scarico, ciascuna da chiudere con FR"],
        ["POS", "pagamenti con carta, ristampe, stato del terminale"],
        ["Display", "messaggi, QR, liste, finestra di input, immagini"],
        ["Stampa", "lo scontrino completo e ogni singolo comando PT, diviso in stile, font e layout, "
                   "contenuto, servizio"],
        ["Sistema", "stato, reset, riavvii, movimenti"],
        ["Console", "invio libero di una stringa qualsiasi, con cronologia sulle frecce su/giu'. "
                    "E' la scheda che serve per provare un comando non ancora previsto "
                    "dall'interfaccia"],
    ], [80, CONTENT_W - 80]))

    s.append(P("7.3 Uno scenario completo da provare", S_H2))
    s.append(P(
        "Il piu' istruttivo e' un incasso interrotto a meta': mostra i parziali, l'annullo e il resto, "
        "cioe' i tre punti dove un'integrazione fatta male si rompe."))

    s.append(figure(sequence_diagram(
        ["Banco di prova", "Libreria", "Simulatore"],
        [
            (0, 1, "Incasso 10,50", "call"),
            (1, 2, "IN001050", "call"),
            (2, 1, "response OK", "reply"),
            (2, 1, "p - collectedAmount 5,00", "reply"),
            (2, 1, "p - collectedAmount 10,00", "reply"),
            (0, 1, "Annulla", "call"),
            (1, 2, "AN", "call"),
            (2, 1, "AN - changeReturn 10,00", "reply"),
        ], row=19), "Un incasso annullato: la macchina restituisce quanto aveva gia' preso."))

    s.append(table([
        ["Passo", "Che cosa fare", "Che cosa deve succedere"],
        ["1", "scheda <b>Incasso</b>, importo 10,50, premere <b>Avvia incasso</b>",
         "nel traffico compare <font face='Courier'>IN001050</font> e subito dopo "
         "<font face='Courier'>response OK</font>"],
        ["2", "attendere qualche secondo",
         "arrivano messaggi <font face='Courier'>p</font> con "
         "<font face='Courier'>collectedAmount</font> che cresce: e' il cliente virtuale che infila "
         "denaro"],
        ["3", "premere <b>Annulla</b> prima che arrivi a 10,50",
         "parte <font face='Courier'>AN</font>, la macchina rende quanto raccolto e chiude con "
         "<font face='Courier'>response AN</font> e l'importo reso in "
         "<font face='Courier'>changeReturn</font>. Se invece arriva "
         "<font face='Courier'>response IN</font> l'annullo e' stato <b>tardivo</b>: il cliente "
         "virtuale aveva gia' completato, e va alzato <i>Ritardo fra un pezzo e l'altro</i> nel "
         "simulatore"],
        ["4", "scheda <b>Contanti</b>, premere <b>Stato</b>",
         "le giacenze sono tornate come prima: quello che era entrato e' uscito"],
    ], [30, 145, CONTENT_W - 175]))

    s.append(callout("Il passo che conta davvero",
                     "Il terzo. Se l'annullo non parte, la macchina resta occupata e ogni comando "
                     "successivo risponde <font face='Courier'>E100</font> con "
                     "<font face='Courier'>errorType 99</font>. E' esattamente il difetto che il "
                     "collaudo ha fatto emergere sulla libreria Kotlin, dove la cancellazione veniva "
                     "scambiata per un timeout e <font face='Courier'>AN</font> non veniva mai "
                     "trasmesso.", GOOD))

    s.append(PageBreak())

    # ---------------------------------------------------------------- 8
    s.append(P("8. Guardare i log", S_H1))
    s.append(P(
        "Tre livelli, attivabili indipendentemente. Il primo dice <i>che cosa</i> e' passato sul filo, "
        "il secondo <i>perche'</i> e' andata come e' andata, il terzo li conserva."))

    s.append(table([
        ["Livello", "Dove si accende", "Che cosa registra"],
        ["Traffico", "sempre attivo, pannello in basso nei banchi",
         "le stringhe inviate e i messaggi ricevuti, cosi' come sono"],
        ["Diagnostica", "casella <b>diagnostica libreria</b>",
         "connessione, pause imposte fra un invio e l'altro, byte letti dal socket, separazione dei "
         "messaggi, resto rimasto nel buffer, attese soddisfatte o scadute, messaggi non sollecitati"],
        ["File", "casella <b>registra su file</b>, attiva di default",
         "tutto quanto sopra, un file al giorno, in append, apribile mentre viene scritto"],
    ], [60, 115, CONTENT_W - 175]))

    s.append(P("8.1 Dove finiscono i file", S_H2))
    s.append(code("""
%LOCALAPPDATA%\\PayPrint.PagAmico\\logs
"""))
    s.append(P(
        "Un file per programma e per giorno, cosi' i due banchi possono girare insieme senza "
        "mescolare i tracciati: <font face='Courier'>winforms-2026-09-03.log</font>, "
        "<font face='Courier'>compose-...</font>, <font face='Courier'>collaudo-...</font>, "
        "<font face='Courier'>demo-...</font>, <font face='Courier'>tap-...</font>. Nella scheda "
        "<b>Connessione</b> c'e' il percorso del file corrente, e il pulsante "
        "<b>Apri cartella log</b> la apre in Esplora risorse.", S_SMALL))

    s.append(P("8.2 Farsi rileggere una sessione", S_H2))
    s.append(P(
        "Nella cartella <font face='Courier'>strumenti</font> c'e' "
        "<font face='Courier'>analizza_log.py</font>: legge i file della giornata e dice se la "
        "sessione e' andata bene o dove si e' rotta. Cerca i comandi rimasti senza risposta (esclusi "
        "quelli che per protocollo non rispondono e quelli che rispondono solo su azione "
        "dell'utente), gli invii troppo ravvicinati, gli errori con la spiegazione, lo stato delle "
        "scorte, le attese scadute, e chiude con l'elenco dei comandi che in quella sessione non "
        "sono mai stati provati."))
    s.append(code("""
python strumenti/analizza_log.py                    i log di oggi
python strumenti/analizza_log.py --data 2026-09-03  di un giorno preciso
"""))

    s.append(P("8.3 Che cosa si vede con la diagnostica accesa", S_H2))
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
        "Le righe che contano sono le ultime due <b>quando non compaiono</b>. Se la traccia si ferma su "
        "<i>in attesa</i> e poi dice <i>attesa scaduta</i> senza aver letto un solo byte, il comando "
        "non e' mai arrivato alla macchina: il problema non e' nell'interpretazione della risposta ma "
        "nell'invio. Le prime righe dicono i valori applicati alla connessione: keepalive TCP, CR "
        "come terminatore e 80 ms di pausa. PayPrint dice che con CR o CR+LF la pausa non serve; "
        "finche' non e' provato sulla macchina resta come rete di sicurezza."))
    s.append(P(
        "Nei programmi da riga di comando (collaudo, demo) la diagnostica e' sempre accesa e compare "
        "nella finestra di output dell'IDE, con il prefisso <font face='Courier'>..</font>.", S_SMALL))

    s.append(P("8.4 La finestra separata", S_H2))
    s.append(P(
        "Il pulsante <b>Finestra log</b> apre il traffico in una finestra a se', con filtri per "
        "direzione, ricerca testuale, scorrimento automatico ed esportazione. Serve quando si vuole "
        "tenere d'occhio il traffico su un monitor e usare il banco di prova sull'altro."))

    # ---------------------------------------------------------------- 9
    s.append(P("9. Il proxy di analisi", S_H1))
    s.append(P(
        "Il pannello del traffico mostra i messaggi <i>logici</i>, gia' ricomposti dalla libreria. "
        "Il proxy invece registra <b>ogni segmento TCP cosi' com'e' arrivato</b>: e' l'unico modo per "
        "vedere due comandi partiti troppo vicini, o una risposta spezzata in piu' pezzi."))

    s.append(figure(sequence_diagram(
        ["Banco di prova", "Tap (9200)", "Simulatore (9100)"],
        [
            (0, 1, "IN001050", "call"),
            (1, 2, "IN001050  (inoltrato tale e quale)", "call"),
            (2, 1, "response OK", "reply"),
            (1, 0, "response OK", "reply"),
            (1, 1, "annota orario, direzione, distanza dal precedente", "self"),
        ], row=20), "Il proxy si inserisce fra client e macchina senza modificare un byte."))

    s.append(P("9.1 Avviarlo insieme al banco di prova", S_H2))
    s.append(P(
        "Il proxy e il banco devono girare contemporaneamente, quindi servono due processi. In "
        "Visual Studio si fa dalle proprieta' della soluzione, senza aprire una seconda istanza:"))
    s.append(table([
        ["#", "Che cosa fare"],
        ["1", "tasto destro sulla <b>soluzione</b> in Esplora soluzioni, <b>Proprieta'</b>"],
        ["2", "<b>Progetto di avvio</b>, opzione <b>Piu' progetti di avvio</b>"],
        ["3", "mettere <b>Avvia</b> su <b>PayPrint.PagAmico.Tap</b> e su "
              "<b>PayPrint.PagAmico.WinForms</b>, <b>Nessuno</b> su tutti gli altri"],
        ["4", "F5: partono entrambi. Nel banco, scheda Connessione, scrivere porta <b>9200</b> "
              "invece di 9100 e premere <b>Connetti</b>"],
    ], [20, CONTENT_W - 20]))
    s.append(P(
        "Per il banco Kotlin la cosa e' anche piu' semplice: si avvia il proxy da Visual Studio "
        "(profilo <b>1 - Proxy verso il simulatore</b>) e il banco da IntelliJ, sono due IDE "
        "distinte. Poi porta 9200 nel banco.", S_SMALL))

    s.append(P("9.2 Che cosa mostra", S_H2))
    s.append(code("""
15:57:41.892  C->M  [#1]              2 byte  ST
              [#1] risposta a 'ST' dopo 34 ms
15:57:42.447  C->M  [#2]              2 byte  DS
15:57:42.447  C->M  [#2] + 0,001s     6 byte  PTSTAT
15:57:42.448  !!  [#2] ATTENZIONE: comando inviato solo 1 ms dopo il precedente.
                  Il pagAmico legge il buffer del socket in un colpo solo: sotto i
                  30 ms il secondo comando puo' essere ignorato senza risposta.
15:57:44.472  --  [#2] sessione chiusa. client 2 segmenti / 8 byte,
                  macchina 0 segmenti / 0 byte.
"""))
    s.append(P(
        "L'ultima riga e' la prova del difetto: la macchina, qui il simulatore, non ha risposto affatto "
        "a comandi mandati senza terminatore. PayPrint attribuisce il fenomeno al simulatore e dice "
        "che con CR o CR+LF la pausa non serve; sul simulatore attuale, il 14 settembre, la raffica "
        "e' passata con e senza CR, sulla macchina e' ancora da provare. A fine sessione il "
        "proxy calcola minimo, media e massimo sia delle pause del client sia dei tempi di risposta "
        "della macchina.", S_SMALL))

    s.append(P("9.3 L'uso piu' utile: osservare il Dev Kit", S_H2))
    s.append(P(
        "Il Dev Kit di PayPrint non e' solo il simulatore: e' anche un client. Impostando "
        "<b>9200</b> come porta dentro il Dev Kit, si vede esattamente che cosa manda "
        "l'applicazione del costruttore, comprese le pause che tiene fra un comando e l'altro. "
        "E' la risposta autorevole ai punti su cui i manuali tacciono o si contraddicono: il formato "
        "del comando <font face='Courier'>DI</font>, quale dei due incapsulamenti delle immagini sia "
        "quello giusto, se dopo i pacchetti immagine serva il terminatore. Per i comandi il "
        "terminatore l'ha chiarito PayPrint: CR o CR+LF. Il profilo <b>2</b> aggiunge il dump "
        "esadecimale, indispensabile per l'invio delle immagini."))
    s.append(P(
        "Una riga scritta nella console del proxy (per esempio <font face='Courier'>ST</font>, oppure "
        "<font face='Courier'>#2 ST</font> per la sessione 2) parte verso la macchina chiusa da CR, "
        "sulla stessa connessione del client, e compare nel log come <font face='Courier'>T-&gt;M</font>. "
        "Serve a mandare un comando a incasso aperto, cosa che la libreria blocca, e a chiudere con "
        "<font face='Courier'>AN</font> o <font face='Courier'>CM</font> un incasso ripreso dopo una "
        "riconnessione (prove 6 e 9 di <font face='Courier'>docs/checklist-macchina-reale.md</font>). "
        "Verso la macchina il proxy regola il keepalive TCP come la libreria.", S_SMALL))

    s.append(PageBreak())

    # ---------------------------------------------------------------- 10
    s.append(P("10. Quando qualcosa non va", S_H1))
    s.append(P(
        "Quasi tutti i fallimenti hanno una di queste sei cause, e nessuna e' un difetto della "
        "libreria."))

    s.append(table([
        ["Sintomo", "Causa probabile", "Come si verifica e si risolve"],
        ["Connessione rifiutata",
         "simulatore non avviato, o porta diversa",
         "<font face='Courier'>netstat -ano | findstr :9100</font> dal terminale dell'IDE. "
         "Riavviare il Dev Kit"],
        ["Ogni comando risponde E100 con errorType 99",
         "la macchina e' rimasta occupata da un'operazione precedente non chiusa",
         "mandare <font face='Courier'>AN</font> dalla scheda Console del banco, o "
         "<font face='Courier'>FR</font> se era aperta una sessione di ricarica. Se non basta, "
         "riavviare il simulatore"],
        ["amountUnpaid valorizzato, errorList con la seconda cifra a 9",
         "monete finite: il resto non e' componibile",
         "rilanciare il profilo <b>2</b> di <b>Fill</b> (capitolo 5)"],
        ["Un passo del collaudo fallisce con QTABANCONOTE",
         "il taglio richiesto non e' piu' in macchina, di solito perche' un passo precedente l'ha "
         "erogato",
         "e' il motivo per cui i passi sulle banconote leggono <font face='Courier'>ST</font> prima di "
         "scegliere il taglio. Se ricapita, ricaricare e rilanciare"],
        ["Un comando non riceve risposta, l'attesa scade",
         "comando senza terminatore, inviato troppo presto dopo il precedente",
         "accendere la <b>diagnostica libreria</b>: se dice <i>attesa scaduta</i> senza aver letto "
         "byte, e' questo. Oppure mettere il proxy in mezzo e cercare la riga "
         "<font face='Courier'>ATTENZIONE</font>. Il rimedio indicato da PayPrint e' il terminatore "
         "CR, default dal 14 settembre: controllare che la casella del capitolo 7.1 non sia vuota"],
        ["Nessuna risposta, ma il comando e' corretto",
         "e' uno dei comandi che per protocollo non rispondono",
         "<font face='Courier'>CL, DS, DC, QA, CO, DT, DG, TS</font> non prevedono risposta. "
         "Il promemoria e' anche nella scheda Connessione"],
    ], [105, 125, CONTENT_W - 230]))

    s.append(P("10.1 Problemi dell'IDE, non del protocollo", S_H2))
    s.append(table([
        ["Sintomo", "Rimedio"],
        ["La finestra del programma si chiude subito e non si legge nulla",
         "in Visual Studio usare <b>Ctrl+F5</b> (avvia senza debug): tiene la finestra aperta fino a "
         "un tasto. Con F5 si chiude alla fine del processo"],
        ["I profili non compaiono nel menu di avvio",
         "il progetto selezionato non e' quello di avvio, oppure la soluzione non e' stata ricaricata. "
         "I profili stanno in <font face='Courier'>Properties\\launchSettings.json</font> di ciascun "
         "progetto"],
        ["Le configurazioni non compaiono in IntelliJ",
         "e' stata aperta la cartella sbagliata: va aperta <font face='Courier'>kotlin</font>, quella "
         "che contiene <font face='Courier'>settings.gradle.kts</font> e la cartella "
         "<font face='Courier'>.run</font>"],
        ["IntelliJ segnala errori di compilazione appena aperto",
         "la sincronizzazione Gradle non e' finita. Attendere, o forzarla dall'icona di ricarica nel "
         "pannello Gradle"],
    ], [140, CONTENT_W - 140]))

    s.append(callout("Prima di segnalare un difetto",
                     "Allegare il file di log della sessione "
                     "(<font face='Courier'>%LOCALAPPDATA%\\PayPrint.PagAmico\\logs</font>) con la "
                     "<b>diagnostica accesa</b>. Senza quello, dal solo traffico non si distingue un "
                     "comando mai partito da una risposta mai arrivata."))

    s.append(PageBreak())

    # ---------------------------------------------------------------- 11
    s.append(P("11. Riepilogo: la sequenza completa", S_H1))
    s.append(P("Da eseguire nell'ordine. La colonna dell'IDE dice esattamente che cosa selezionare."))
    s.append(table([
        ["#", "Passo", "Visual Studio", "IntelliJ IDEA"],
        ["1", "test offline", "progetto <b>Tests</b>, Ctrl+F5", "config. <b>1</b>"],
        ["2", "accendere il simulatore", "Dev Kit, sezione Simulatore, <b>Avvia</b>", "-"],
        ["3", "riempire le giacenze", "progetto <b>Fill</b>, profilo <b>2</b>", "-"],
        ["4", "collaudo completo", "progetto <b>LiveTest</b>, profilo <b>1</b>", "config. <b>2</b>"],
        ["5", "prove a mano", "progetto <b>WinForms</b>, F5", "config. <b>3</b>"],
        ["6", "se qualcosa non torna",
         "diagnostica accesa nel banco; poi <b>Tap</b> profilo <b>1</b> e porta 9200",
         "diagnostica accesa nel banco"],
    ], [18, 100, 190, CONTENT_W - 308]))

    s.append(P(
        "Atteso al termine: <b>170 su 170</b> offline in C# e <b>171 su 171</b> in Kotlin, "
        "<b>64 su 64</b> di collaudo in entrambi i linguaggi.", S_SMALL))

    # ---------------------------------------------------------------- 12
    s.append(P("12. Il giorno in cui arriva la macchina", S_H1))
    s.append(P(
        "Cambia pochissimo, ed e' voluto: il simulatore e la macchina parlano lo stesso protocollo "
        "sulla stessa porta. I profili per la macchina reale ci sono gia'."))

    s.append(table([
        ["Cosa", "Con il simulatore", "Con la macchina"],
        ["profilo da usare", "profili 1-5 (LiveTest)",
         "profilo <b>7 - Collaudo macchina reale (solo base)</b> per primo, poi il <b>6</b>. "
         "In IntelliJ la configurazione <b>4</b>"],
        ["indirizzo", "127.0.0.1", "l'IP del pagAmico, leggibile a display in <i>Menu Servizi</i>. "
                                   "Da correggere una volta nei profili"],
        ["porta", "9100", "9100 di fabbrica, da spostare (il manuale suggerisce 43775)"],
        ["riempimento", "profilo 2 di <b>Fill</b>",
         "<b>mai</b>. Il contante lo carica l'operatore, con una sessione di ricarica"],
        ["gruppo riavvii", "escluso di default",
         "escluso, salvo prova voluta: riavvia POS e macchina"],
        ["terminatore", "<b>CR</b>, default dal 14 settembre, provato",
         "<b>CR</b> (o CR+LF), come chiede PayPrint"],
        ["pausa fra comandi", "80 ms; con CR regge anche a 0 ms",
         "PayPrint la dice non necessaria con il terminatore CR o CR+LF. Resta a 80 ms come rete "
         "di sicurezza finche' non e' provato (<font face='Courier'>--pausa 0</font>)"],
    ], [72, 118, CONTENT_W - 190]))

    s.append(callout("Le prime tre cose da fare sulla macchina vera",
                     "1. Avviare il <b>Tap</b> col profilo <b>3 - Proxy verso la macchina reale</b> "
                     "fin dalla prima connessione: il tracciato della prima sessione vale piu' di "
                     "qualsiasi prova successiva. "
                     "2. Cominciare dal profilo <b>7</b>, solo il gruppo "
                     "<font face='Courier'>base</font>, non dal collaudo completo. "
                     "3. Controllare il terminatore <b>CR</b> che chiede PayPrint: e' il default "
                     "delle librerie, nei banchi <font face='Courier'>\\r</font> nella casella del "
                     "capitolo 7.1, nel collaudo <font face='Courier'>--terminatore cr</font>. Gli 80 "
                     "ms di pausa restano come rete di sicurezza finche' la prova 2 di "
                     "<font face='Courier'>docs/checklist-macchina-reale.md</font> non mostra che con "
                     "CR i comandi passano anche senza; il valore sta in una sola proprieta' "
                     "(<font face='Courier'>MinimumCommandInterval</font> in C#, "
                     "<font face='Courier'>minimumCommandIntervalMs</font> in Kotlin).", GOOD))

    s.append(PageBreak())

    # ---------------------------------------------------------------- 13
    s.append(P("13. Le prove che mancano per l'innesto in Giano", S_H1))
    s.append(P(
        "I 64 passi del collaudo dimostrano che le librerie parlano il protocollo. Non dimostrano "
        "che il <b>flusso di pagamento</b> del gestionale regga, perche' quello dipende da come si "
        "comporta la macchina in situazioni che il simulatore non sa riprodurre. L'analisi "
        "dell'innesto in Giano (documento <i>Integrazione pagAmico</i>, capitolo 9; l'elenco dei "
        "punti aperti e' nella sua sezione 8.2) ha isolato <b>cinque punti che nessuno ha ancora "
        "osservato</b>."))
    s.append(P(
        "Per tre (la 1, la 4 e, in parte, la 5) c'e' ora la risposta di PayPrint dell'11 settembre: "
        "da osservare diventano da confermare. La 4 era il vincolo che bloccava il disegno del "
        "flusso, e la risposta lo scioglie: <font face='Courier'>CM</font> si manda durante "
        "l'incasso. Il lavoro che resta e' nelle librerie "
        "(<font face='Courier'>docs/esito-risposta-payprint.md</font>, sezione 3). La 2 e la 3 "
        "restano da osservare, prima di scrivere il codice che le usa.", S_SMALL))

    s.append(P("13.1 Le cinque prove", S_H2))
    s.append(table([
        ["Che cosa si vuole vedere", "Come si provoca", "Perche' conta"],
        ["<b>1.</b> che campi porta davvero un <b>parziale</b>",
         "un incasso qualsiasi, guardando il traffico grezzo col <b>Tap</b> invece dell'esito finale",
         "l'interfaccia mostra al cliente quanto ha inserito, e durante un incasso non si puo' "
         "interrogare nulla. Per PayPrint i parziali arrivano a ogni aggiunta di contante e sono "
         "<b>cumulativi</b>: un parziale perso non costa nulla, la prova lo conferma"],
        ["<b>2.</b> se sulla risposta di <b>annullo</b> arriva l'incassato",
         "avviare un incasso, inserire meno del dovuto, annullare",
         "senza l'incassato non si sa se la restituzione e' stata <b>incompleta</b>: e' denaro del "
         "cliente che resta in macchina senza che il gestionale lo sappia"],
        ["<b>3.</b> una <b>restituzione incompleta</b> vera",
         "svuotare le monete dal banco WinForms, scheda <b>Manutenzione</b> "
         "(<font face='Courier'>MF</font>, <font face='Courier'>AZ</font>, o soglie a zero con "
         "<font face='Courier'>SM</font>), poi chiedere un incasso che richieda resto. <b>Fill non "
         "serve</b>: riempie soltanto, si ferma appena la giacenza raggiunge il bersaglio",
         "e' il caso peggiore del flusso attuale, e l'unico per cui il pagAmico offre un campo "
         "dedicato invece di una sottrazione"],
        ["<b>4.</b> che il commit <b>chiuda</b> davvero un incasso in corso",
         "avviare un incasso e mandare il commit mentre e' aperto",
         "la via c'e': per PayPrint durante <font face='Courier'>IN</font> la macchina accetta "
         "<font face='Courier'>CM</font>. Dall'11 settembre la libreria manda il commit a incasso "
         "aperto e chiude sull'esito invece che sull'accettazione (difetto D1, corretto sul "
         "simulatore): la prova verifica che sulla macchina valga lo stesso"],
        ["<b>5.</b> che cosa fa la macchina se il <b>client si arrende</b>",
         "avviare un incasso, staccare la rete a meta' e ricollegarsi <b>dallo stesso IP</b>; poi "
         "chiuderlo con la chiusura forzata dal pannello (gesto e password da chiarire), che torna "
         "come <font face='Courier'>AN</font>. Non lasciarlo scadere: PayPrint dice di non mettere "
         "timeout, e allo scadere dei 5 minuti la libreria lo abbandona aperto (difetto D2)",
         "per PayPrint dopo <font face='Courier'>IN</font> non c'e' timeout: l'incasso resta aperto "
         "finche' non viene chiuso. Da vedere se dopo la riconnessione parziali ed esito arrivano sul "
         "nuovo socket, e quanto e' entrato nel frattempo. E' la rete di sicurezza per il PC che si "
         "riavvia in mezzo a un pagamento"],
    ], [125, 145, CONTENT_W - 270]))

    s.append(callout("Perche' il simulatore non basta",
                     "Il simulatore non permette di forzare un resto insufficiente, non mostra i "
                     "parziali come li manda la macchina, e non ha uno stato da cui ripartire dopo "
                     "una caduta. Le prove 2, 3 e 5 <b>richiedono una macchina vera</b>. La 1 e la 4 "
                     "si possono tentare sul simulatore col Tap acceso, ma il risultato va confermato "
                     "comunque.", WARN))

    s.append(P("13.2 Come registrarle", S_H2))
    s.append(P(
        "Tutte e cinque vanno fatte <b>col Tap acceso</b> (capitolo 9), non dal solo banco di prova: "
        "quello che serve non e' l'esito che la libreria restituisce, ma i byte che la macchina manda "
        "davvero, nell'ordine in cui li manda. Il tracciato del proxy si rilegge dal file "
        "<font face='Courier'>tap-AAAA-MM-GG.log</font>, che porta il riepilogo gia' in fondo; "
        "<font face='Courier'>strumenti/analizza_log.py</font> serve per il log <i>di libreria</i> "
        "della stessa sessione e i tracciati del proxy li scarta di proposito."))
    s.append(P(
        "Le stesse domande, riscritte per PayPrint con il contesto che serve a loro, stanno in "
        "<font face='Courier'>docs/mail-payprint-domande-protocollo.md</font>: la prova 1 e' il punto "
        "2.7, la 2 il punto 2.2, la 4 il punto 2.1, la 5 il punto 2.4; la prova 3 non ha un punto "
        "suo, perche' e' la verifica sul campo di quello che il punto 2.2 chiede a parole. PayPrint "
        "ha risposto l'11 settembre "
        "(<font face='Courier'>docs/risposta-payprint-2026-09-11.md</font>, analisi in "
        "<font face='Courier'>docs/esito-risposta-payprint.md</font>): la 2.1 e la 2.7 sono chiuse, "
        "la 2.4 in parte, quindi le prove 1, 4 e 5 diventano una conferma invece che una scoperta. "
        "La 2.2 resta aperta, e con lei la 2 e la 3.", S_SMALL))

    s.append(callout("Una prova da non fare",
                     "Nessuna delle cinque va lanciata contro una macchina di terzi, compresa quella "
                     "che PayPrint offre di collegare da remoto, senza via libera esplicito: la 3 "
                     "richiede di svuotare le giacenze e la 5 lascia deliberatamente un incasso "
                     "aperto, che sulla macchina non scade da solo e va chiuso esplicitamente "
                     "(<font face='Courier'>AN</font>, <font face='Courier'>CM</font> o chiusura "
                     "forzata dal pannello). Sul <b>simulatore</b>, invece, si fa qualsiasi cosa.", GOOD))

    s.append(PageBreak())

    # ---------------------------------------------------------------- appendice
    s.append(P("Appendice. Gli stessi passi da riga di comando", S_H1))
    s.append(P(
        "Servono solo per automatizzare (una catena di build, una prova rapida senza aprire l'IDE). "
        "Vanno dati dalla cartella che contiene i quattro repository."))
    s.append(code("""
dotnet run --project pagAmico_CSharp_Lib/PayPrint.PagAmico.Tests
dotnet run --project pagAmico_CSharp_Demo/PayPrint.PagAmico.Fill -- 127.0.0.1 9100 `
    --monete 40 --banconote 1200 --aggiorna-fondo
dotnet run --project pagAmico_CSharp_Demo/PayPrint.PagAmico.LiveTest -- 127.0.0.1 9100
dotnet run --project pagAmico_CSharp_Demo/PayPrint.PagAmico.WinForms
dotnet run --project pagAmico_CSharp_Demo/PayPrint.PagAmico.Tap -- --listen 9200 --target 127.0.0.1:9100
"""))
    s.append(code("""
cd pagAmico_Kotlin_Lib
.\\gradlew.bat :pagamico-lib:run
.\\gradlew.bat :pagamico-lib:run "-PmainClass=it.payprint.pagamico.test.LiveTestKt" "--args=127.0.0.1 9100"
cd ..\\pagAmico_Kotlin_Demo
.\\gradlew.bat :pagamico-desktop:run
"""))
    s.append(P(
        "Le virgolette attorno agli interi argomenti servono: senza, PowerShell spezza "
        "<font face='Courier'>-PmainClass=...</font> e Gradle riceve un parametro monco. "
        "L'apice inverso e' la continuazione di riga di PowerShell.", S_SMALL))

    s.append(Spacer(1, 10))
    s.append(P("Prime Software S.r.l. - documento tecnico interno", S_SMALL))

    return s


if __name__ == "__main__":
    target = os.path.join(os.path.dirname(os.path.abspath(__file__)), "Guida-prove-pagAmico.pdf")
    build(target)
    print("creato:", target)
