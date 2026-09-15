## Dove siamo

> **Aggiornamento del 15 settembre.** L'SDK è diviso in **quattro repository** affiancati in
> `D:\Programmazione\Prime\PerClaude\payPrint` (a casa), ciascuno con i remote `github` e
> `HardDiskEsterno` (`docs/avvio-progetti.md`). Fatti dopo l'11 settembre: terminatore CR di default,
> keepalive TCP regolato (10 s / 2 s / 5 sonde), test offline **170 in C# e 171 in Kotlin**, Tap con
> comandi dalla console, banco Kotlin impacchettato. Decisioni per Giano: **replicare il VNE** con il
> log degli importi, Decisione 1 rimandata alle prove 4 e 5 (`docs/decisioni-innesto-giano.md`). Le
> prove sulla macchina di PayPrint si preparano con `docs/guida-prove-macchina-payprint.pdf`. Il
> resto di questa sezione è la situazione all'11 settembre.

**PayPrint ha risposto** (11 settembre, `docs/risposta-payprint-2026-09-11.md`), e l'analisi
verificata della risposta sta in **`docs/esito-risposta-payprint.md`**. Leggerlo è il primo passo di
qualsiasi sessione.

**Il blocco è caduto**: durante un incasso la macchina accetta `CM`. L'adattatore di Giano si può
disegnare.

**Ma la risposta ha fatto emergere tre difetti nelle nostre librerie** che il collaudo non vedeva:

|  | Difetto | Stato |
| --- | --- | --- |
| **D1** | `CommitAsync` chiudeva sull'accettazione e riportava trattenuto 0,00 | **corretto l'11 settembre** |
| **D2** | il timeout di 5 minuti abbandona un incasso aperto senza mandare `AN` (`PagAmicoClient.cs:74`, `.kt:97`) | aperto: va dopo le prove e il keepalive |
| **D3** | qualsiasi testo o `ER` chiudeva l'attesa dell'incasso: un `BUSY` la perdeva | **corretto l'11 settembre** |

**Il ramo 1 è fatto** (11 settembre, sera): punti 1-8 del capitolo 3 dell'esito, nelle due
librerie. 121 test offline per parte (35 nuovi, di sequenza, contro un finto pagAmico su
127.0.0.1), 64 passi di collaudo sul simulatore in C# e in Kotlin, e il passo `[IN]+[CM]` ora
dimostra un commit (trattenuto 470,00 dove leggeva 0,00). Tre cose emerse facendolo sono in fondo
al capitolo 3 dell'esito: un `CM` in ritardo sul simulatore che ha fatto da risposta a un `ST`,
un `ER` dopo l'`OK` che ora non chiude l'incasso (la domanda 5 della telefonata decide se deve),
un `CM` che resta senza esito se l'incasso si chiude prima.

**Anche la seconda passata è fatta** (stessa sera, sempre in fondo al capitolo 3 dell'esito): i
comandi semplici attendono la propria risposta, il mutex del registro C# segue il file del
giorno (era un difetto), e i test sono passati a **168 per parte**, coprendo annullo prima
dell'`OK`, `PO`/`IM`/`I2`, chiusura dal pannello, timeout, cadute, pausa, terminatore, immagini,
registro su file ed errori.

Il fornitore offre **telefonata e collegamento alla sua macchina**. Le undici domande per la
telefonata stanno nel capitolo 5 dell'esito. Mancano ancora le **decisioni contabili** (capitolo 3
di `quadro-05`).

Quindi la prossima sessione può essere una di tre cose:

| Se… | Allora la sessione è |
| --- | --- |
| nessuna condizione — si può fare sempre | **provare a mano i due banchi**: ramo 1 (piccolo) |
| c'è il via libera per il collegamento alla macchina di PayPrint, o è arrivata la nostra | **le prove**: ramo 2 |
| le decisioni contabili sono prese | **disegno dell'adattatore**, poi codice: ramo 3 |

---

## Il prompt da incollare

Copia da qui in giù come primo messaggio della nuova sessione, scegliendo il ramo 1, 2 o 3 in
fondo e cancellando gli altri due.

---

Contesto. Due posti:

- `D:\Programmazione\Prime\PerClaude\payPrint` — l'SDK pagAmico in quattro repository affiancati:
`pagAmico_CSharp_Lib` (libreria C# multi-target, test, documentazione, analizzatore di log),
`pagAmico_CSharp_Demo` (banco WinForms, collaudo, Tap, Fill, Demo), `pagAmico_Kotlin_Lib`
(libreria gemella Kotlin, test, collaudo), `pagAmico_Kotlin_Demo` (banco Compose). Remote `github`
e `HardDiskEsterno` (G: a casa, E: in ufficio): prima di iniziare `git pull` in tutti e quattro.
- `D:\Programmazione\Prime\PerClaude\GianoITA` — Giano, il nostro gestionale, che pilota già una
cassa automatica **VNE** e a cui va aggiunto il pagAmico **accanto**, non al suo posto.
**Leggi prima questi, in quest'ordine, e non rifare il lavoro che contengono** (tutti in
`pagAmico_CSharp_Lib\docs`):
1. `esito-risposta-payprint.md` — la risposta del fornitore verificata, i tre difetti, cosa cambia
nelle librerie e in Giano
2. `decisioni-innesto-giano.md` — le decisioni prese e quelle rimandate
3. `quadro-01-quadro-insieme.md` — il quadro generale
4. `quadro-04-giano-dove-siamo.md` — Giano, il precedente OPOS, la stima
5. `quadro-05-cosa-non-sappiamo.md` — le decisioni aperte
6. `analisi-giano-vne-vs-pagamico.md` — il dettaglio riga per riga
`quadro-02` (le librerie) e `quadro-03` (i programmi di prova) servono se tocchi quelle parti.
### I fatti da non rimettere in discussione

**Detti dal fornitore** (`risposta-payprint-2026-09-11.md`):

- Durante `IN` la macchina accetta **solo `AN` e `CM`**. `ST` va mandato solo a transazione chiusa.
- **Nessun timeout** dopo `IN`: l'incasso resta aperto finché non lo chiude il client (`AN`/`CM`)
o l'operatore dal pannello, e in quel caso arriva un `AN`. Non va messo un timeout nel client.
- **I comandi vanno terminati con CR o CR+LF.** Con il terminatore la pausa fra comandi non serve.
- **I parziali `p` sono cumulativi**, uno a ogni aggiunta di contante.
- **Tetto 9.999,99 €** per i contanti.
- La riconnessione è accettata **dallo stesso IP**.
**Verificati sui sorgenti**, due volte:
- **Giano usa cinque comandi VNE nel flusso di vendita** (apri incasso, poll, annullo, elenco
pendenti, logout). Un sesto è un heartbeat fuori dalla vendita, tre stanno in rami
irraggiungibili, uno è solo della finestra di prova, diciannove non hanno chiamanti.
- **Il pagamento automatico è raggiungibile da un solo ramo dell'applicazione**, ExpressTakeaway.
- **Dalla finestra di pagamento esce un booleano.** Importo incassato, resto erogato, resto non
erogato, identificativo: niente attraversa il confine, niente viene persistito.
- **Giano non ha nessun timeout** nella finestra di pagamento: il consiglio del fornitore è
coerente con quello che c'è.
- **La compatibilità non è un problema.** La libreria dichiara già `net47`, la DLL è compilata, e
le sue dieci dipendenze sono già in `Neo\packages.config` alla versione identica. Va sotto
`ExternalReferences\`, **non** ricompilata sotto `ExternalProjects\`.
- **`IAutomatedPaymentMachine` non è un'astrazione di famiglia**: importa il namespace VNE e
restituisce tipi VNE. Sei membri vanno **eliminati**, uno rimappato, due rifatti.
- **Il collaudo è 64 passi** con i gruppi di default, 71 accendendo anche la sonda `DI` e i
riavvii. **Il passo `[IN]+[CM]` dimostra un commit solo dall'11 settembre** (difetto D1, corretto):
prima risultava OK leggendo l'accettazione.
- **D1 e D3 sono corretti, con i punti 1-8 del capitolo 3 dell'esito.** A incasso aperto partono
solo `AN` e `CM` (via laterale, uno solo per incasso), il resto lancia
`PagAmicoCollectionOpenException`; il predicato dell'incasso lavora in due fasi; ciò che nessuna
attesa riconosce va all'evento dei frame orfani (`OrphanFrame` / `onOrphanFrame`). In Kotlin,
se il chiamante dell'incasso viene cancellato, l'esito arriva a `onOrphanFrame`.
### Vincoli di lavoro

- **Le due librerie si toccano insieme**, con gli stessi nomi di metodi, asserzioni e passi. Le
divergenze note (annullo, caduta di connessione, registro fra processi) sono in `quadro-02`.
- **Dopo ogni commit, push su `github` e su `HardDiskEsterno`.**
- **Giano/Neo è C# 7.3**, csproj non-SDK: ogni file nuovo va elencato a mano in
`<Compile Include>`. Niente record, niente switch expression, niente DI. Il vincolo vale per il
codice **di Neo**: la libreria referenziata resta com'è.
- **Prima di modificare il repository git di Neo, chiedimi il via libera.** Il branch corrente è
`main`: se si scrive, si scrive su un branch nuovo.
- **Non lanciare `PagAmicoFill` contro una macchina reale**: esegue incassi veri. Solo 127.0.0.1.
- **Il gruppo `pos2` del collaudo include la ricarica certificati del POS.** Non è innocuo su una
macchina in esercizio.
- Le convenzioni di Neo stanno nelle skill `legacy-wpf-*` in `.claude\skills\`. Ogni stringa UI
nuova va in tutti e tre i file `StringResources`.
- I PDF sono **generati**: si modifica il sorgente (`docs\genera_*.py` o il `.md`) e si rilancia,
mai il PDF.
---

### RAMO 1 — provare a mano i due banchi

*(usa questo quando non c'è altro di pronto: non dipende da nessuno. I punti 1-8 del capitolo 3
dell'esito e la seconda passata sui test sono fatti dall'11 settembre: non rifarli)*

I due banchi (WinForms e Compose) compilano, ma dopo le modifiche dell'11 settembre nessuno li ha
aperti. Sul simulatore, in tutti e due:

1. Un incasso, poi `[AN]` a incasso aperto: deve comparire l'esito dell'incasso.
2. Un incasso, poi `[CM]`: il trattenuto letto da `collectedAmount`.
3. Un secondo `[AN]` o `[CM]` sullo stesso incasso: errore «chiusura già richiesta», nulla
trasmesso.
4. Un altro comando a incasso aperto (stato, display): errore di incasso aperto, nulla trasmesso.
5. I frame orfani nel log (per esempio i parziali di una ricarica `VS`).
6. Nel banco Compose `cancelCurrent()` non la usa più nessuno: toglierla o ricollegarla.

**Non** toccare ancora: il default del terminatore, la pausa, il timeout (D2), la chiusura
dell'incasso su `ER` dopo l'`OK`. Quelli vanno dopo le prove.
Dopo ogni pezzo: test offline verdi in C# e in Kotlin, collaudo sul simulatore, documenti
aggiornati e PDF rigenerati.

### RAMO 2 — le prove

*(usa questo se c'è il via libera per la macchina di PayPrint, o se è arrivata la nostra)*

Due fasi, nell'ordine:

1. **Simulatore**: terminatore CR e CR+LF sui comandi normali, su quelli a lunghezza controllata e
su quelli con password in coda; la prova «raffica» (due comandi in una sola scrittura).
2. **Macchina vera**: `CM` durante `IN` (quanti frame, quale campo); `AN` con denaro dentro; un
comando mandato durante `IN` per vedere la forma di `BUSY`; riconnessione a incasso aperto; la
raffica con CR.
La fase 1 è fatta (14 settembre, `prova-terminatore-cr-2026-09-14.md`). Per la fase 2 segui
`guida-prove-macchina-payprint.md` e `checklist-macchina-reale.md`.
Col proxy acceso, sempre. Dopo ogni prova aggiorna l'esito e i documenti che la riguardano.
**La macchina di PayPrint è di terzi.** Niente che svuoti le giacenze, azzeri le banconote o
lasci un incasso aperto senza un accordo esplicito con Viglione, e senza il mio via libera.

### RAMO 3 — le decisioni sono prese

*(usa questo se hai risposto alle decisioni contabili del capitolo 3 di `quadro-05`)*

Le decisioni sono: **[incollale qui]**.

Adesso, in quest'ordine:

1. **Disegna l'adattatore, prima di scriverlo.** L'SDK è `Task`-based con avanzamento a callback;
Giano è a thread e `Thread.Sleep`. Voglio vedere la forma del punto di contatto — quali membri,
che cosa restituisce l'operazione di incasso, come viaggiano avanzamento, annullo e commit,
dove finiscono gli `await`, che cosa succede se la connessione cade, come si riconosce la
chiusura forzata dal pannello — prima che venga scritta una riga.
2. **Poi il nuovo contratto di famiglia**: come diventa `IAutomatedPaymentMachine` con due marche
dentro.
3. **Poi il codice**, un pezzo alla volta, partendo dal livello dispositivo e dalla registrazione
hardware — **non dalla UI**.
Presupposto: le correzioni di D1 e D3 sono fatte (11 settembre). Senza, l'adattatore si costruirebbe su
una libreria che perde transazioni.
L'ordine è quello che ha funzionato per il cassetto OPOS: **prima il pezzo fuori sagoma, provato
da solo contro l'hardware, poi tutto il resto in un colpo.**

Se qualcosa non torna, chiedimi.