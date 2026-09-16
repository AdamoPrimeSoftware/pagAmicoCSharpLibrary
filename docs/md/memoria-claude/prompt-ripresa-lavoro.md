# Prompt di ripresa del lavoro

*Aggiornato al 16 settembre 2026. La prima parte serve a te, per ricordarti dove siamo; la seconda è
il testo da incollare come primo messaggio di una sessione nuova. **Va riscritto a fine sessione**,
non lasciato invecchiare.*

---

## Dove siamo

**Che cosa esiste.** Un SDK per la cassa rendiresto PayPrint pagAmico (protocollo TCP-IP 2.33, FW
8.72), in quattro repository affiancati: due librerie gemelle, C# e Kotlin, che generano le stesse
stringhe; due banchi di prova, WinForms e Compose; il collaudo automatico, il proxy Tap, il
riempitore del simulatore e i generatori della documentazione.

**Quanto è verificato.**

| | Stato |
|---|---|
| Test offline | **171 in C#, 172 in Kotlin**, tutti verdi |
| Collaudo sul simulatore | **64 passi su 64**, in tutti e due i linguaggi |
| Prove a mano sui banchi | le cinque del ramo 1, superate il 16 settembre (`md/verbali/prove-banchi-ramo1-2026-09-16.md`) |
| Prove su una macchina fisica | **nessuna**: è il limite grosso |

**I tre difetti emersi dalla risposta del fornitore.** D1 (il commit leggeva l'accettazione, e
riportava trattenuto 0,00) e D3 (un testo qualsiasi chiudeva l'attesa dell'incasso) sono **corretti
dall'11 settembre** e riprovati a mano il 16. **D2 è aperto per scelta**: il timeout di 5 minuti
abbandona un incasso ancora aperto, e si toglie solo dopo la prova del cavo staccato su una macchina
vera (prova 10).

**Giano.** L'analisi è fatta, il codice no. La linea scelta è **replicare il comportamento del VNE**,
aggiungendo il log degli importi che oggi si perdono; la Decisione 1 — che cosa significa «annulla
senza restituire» — è **rimandata alle prove 4 e 5** sulla macchina. La stima resta di
quindici-ventidue giornate per la sostituzione alla pari.

**Quindi mancano tre cose, in quest'ordine:** le prove su una macchina vera (ramo 2), le decisioni
contabili che spettano al titolare (capitolo 3 di `md/guide/quadro-05-cosa-non-sappiamo.md`), e poi
l'adattatore in Giano (ramo 3).

| Se… | La sessione è |
|---|---|
| c'è il via libera per la macchina di PayPrint, o è arrivata la nostra | **ramo 2 — le prove** |
| le decisioni contabili sono prese | **ramo 3 — l'adattatore** |
| nessuna delle due | **ramo 4 — le piccole cose**, che non dipendono da nessuno |

*(Il ramo 1, provare a mano i due banchi sul simulatore, è chiuso dal 16 settembre.)*

---

## Il prompt da incollare

Copia da qui in giù come primo messaggio della sessione nuova, tenendo **solo il ramo** che ti
interessa.

---

Contesto. Due posti, con percorsi diversi sui due PC:

| | PC di casa | PC dell'ufficio |
|---|---|---|
| SDK pagAmico | `D:\Programmazione\Prime\PerClaude\payPrint` | `C:\Users\adamo\Desktop\Prime\payPrint` |
| Giano | `D:\Programmazione\Prime\PerClaude\GianoITA` | `C:\Users\adamo\Desktop\Prime\Programmi_x_Claude\GianoITA` |
| disco esterno | `G:\git-payprint-repos` | `E:\git-payprint-repos` |

- **L'SDK pagAmico**, in quattro repository affiancati: `pagAmico_CSharp_Lib` (libreria C#
multi-target, test, **tutta la documentazione**, analizzatore di log), `pagAmico_CSharp_Demo` (banco
WinForms, collaudo, Tap, Fill, Demo), `pagAmico_Kotlin_Lib` (libreria gemella Kotlin, test,
collaudo), `pagAmico_Kotlin_Demo` (banco Compose). Ogni repository ha i remote `github` e
`HardDiskEsterno`: **prima di iniziare, `git pull` in tutti e quattro.**
- **Giano**, il nostro gestionale, che pilota già una cassa automatica **VNE** e a cui va aggiunto il
pagAmico **accanto**, non al suo posto.

La documentazione sta tutta in `pagAmico_CSharp_Lib\docs`: sorgenti in `md/<tipo>/`, PDF generati in
`pdf/<stesso tipo>/`, indice in `docs/README.md`. **Leggi prima questi, in quest'ordine, e non
rifare il lavoro che contengono:**

1. `md/memoria-claude/esito-risposta-payprint.md` — la risposta del fornitore verificata sul codice,
i tre difetti, che cosa cambia nelle librerie e in Giano, e le 11 domande per la telefonata
2. `md/memoria-claude/decisioni-innesto-giano.md` — le decisioni prese e quelle rimandate
3. `md/guide/quadro-01-quadro-insieme.md` — il quadro generale
4. `md/guide/quadro-04-giano-dove-siamo.md` — Giano, il precedente OPOS, la stima
5. `md/guide/quadro-05-cosa-non-sappiamo.md` — le decisioni aperte
6. `md/memoria-claude/analisi-giano-vne-vs-pagamico.md` — il dettaglio riga per riga

`quadro-02` (le librerie) e `quadro-03` (i programmi di prova) servono se tocchi quelle parti; in
`md/verbali/` c'è com'è andata nelle sessioni di prove già fatte.

### I fatti da non rimettere in discussione

**Detti dal fornitore** (`md/fornitore/risposta-payprint-2026-09-11.md`):

- Durante `IN` la macchina accetta **solo `AN` e `CM`**. `ST` va mandato solo a transazione chiusa.
- **Nessun timeout** dopo `IN`: l'incasso resta aperto finché non lo chiude il client (`AN`/`CM`) o
l'operatore dal pannello, e in quel caso arriva un `AN`. Non va messo un timeout nel client.
- **I comandi vanno terminati con CR o CR+LF.** Con il terminatore la pausa fra comandi non serve.
- **I parziali `p` sono cumulativi**, uno a ogni aggiunta di contante.
- **Tetto 9.999,99 €** per i contanti.
- La riconnessione è accettata **dallo stesso IP**.

**Verificati sui sorgenti di Giano**, due volte:

- **Giano usa cinque comandi VNE nel flusso di vendita** (apri incasso, poll, annullo, elenco
pendenti, logout). Un sesto è un heartbeat fuori dalla vendita, tre stanno in rami irraggiungibili,
uno è solo della finestra di prova, diciannove non hanno chiamanti.
- **Il pagamento automatico è raggiungibile da un solo ramo dell'applicazione**, ExpressTakeaway.
- **Dalla finestra di pagamento esce un booleano.** Importo incassato, resto erogato, resto non
erogato, identificativo: niente attraversa il confine, niente viene persistito.
- **Giano non ha nessun timeout** nella finestra di pagamento: il consiglio del fornitore è coerente
con quello che c'è.
- **La compatibilità non è un problema.** La libreria dichiara già `net47`, la DLL è compilata, e le
sue dieci dipendenze sono già in `Neo\packages.config` alla versione identica. Va sotto
`ExternalReferences\`, **non** ricompilata sotto `ExternalProjects\`.
- **`IAutomatedPaymentMachine` non è un'astrazione di famiglia**: importa il namespace VNE e
restituisce tipi VNE. Sei membri vanno **eliminati**, uno rimappato, due rifatti.

**Verificati sul simulatore e sui banchi** (11, 14 e 16 settembre):

- **D1 e D3 sono corretti.** A incasso aperto partono solo `AN` e `CM`, per una via laterale e uno
solo per incasso; ogni altro invio lancia `PagAmicoCollectionOpenException` senza trasmettere nulla;
il predicato dell'incasso lavora in due fasi; ciò che nessuna attesa riconosce va all'evento dei
frame orfani (`OrphanFrame` / `onOrphanFrame`). In Kotlin, se il chiamante dell'incasso viene
cancellato, l'esito arriva a `onOrphanFrame`.
- **Dopo un `CM` arrivano due frame**: l'accettazione ha `errorCode` `OK` e `committedAmout` 0,
l'esito ha `errorCode` vuoto e l'importo. La libreria chiude sul secondo. *(`committedAmout`, senza
la n, è il nome vero del campo nel protocollo.)*
- **Il terminatore CR è il default** dal 14 settembre, con la pausa di 80 ms ancora attiva; sul
simulatore attuale la raffica non perde comandi nemmeno senza terminatore.
- **Il keepalive TCP è regolato** (10 s / 2 s / 5 sonde): una caduta si vede in circa 20 s. In Kotlin
serve un JDK 17.0.14 o successivo.
- **La riga `TX` del log esce prima dei byte** (correzione del 16 settembre): se quella riga non c'è,
non è stato trasmesso niente.
- **Il cliente virtuale del simulatore inserisce circa 200 € al secondo:** per tenere aperto un
incasso e fare le prove a mano servono importi da 3.000-9.000 €.
- **Il collaudo è 64 passi** con i gruppi di default, 71 accendendo anche la sonda `DI` e i riavvii.

### Vincoli di lavoro

- **Le due librerie si toccano insieme**, con gli stessi nomi di metodi, asserzioni e passi. Le
divergenze note (annullo, caduta di connessione, registro fra processi) sono in `quadro-02`.
- **Dopo ogni pezzo**: test offline verdi in C# e in Kotlin, collaudo sul simulatore, documenti
aggiornati e PDF rigenerati.
- **Dopo ogni commit, push su `github` e su `HardDiskEsterno`.**
- **I PDF sono generati**: si modifica il `.md` in `docs\md\...` e si rilancia
`python docs\generatori\genera_pdf_da_md.py`. Mai modificare un PDF a mano.
- **A fine sessione riscrivi `md/memoria-claude/prompt-ripresa-lavoro.md`** (questo documento) con lo
stato nuovo.
- **Giano/Neo è C# 7.3**, csproj non-SDK: ogni file nuovo va elencato a mano in `<Compile Include>`.
Niente record, niente switch expression, niente DI. Il vincolo vale per il codice **di Neo**: la
libreria referenziata resta com'è.
- **Prima di modificare il repository git di Neo, chiedimi il via libera.** Il branch corrente è
`main`: se si scrive, si scrive su un branch nuovo. Le convenzioni di Neo stanno nelle skill
`legacy-wpf-*` in `.claude\skills\`, e ogni stringa UI nuova va in tutti e tre i file
`StringResources`.
- **Non lanciare `PagAmicoFill` contro una macchina reale**: esegue incassi veri. Solo 127.0.0.1.
- **Il gruppo `pos2` del collaudo include la ricarica certificati del POS.** Non è innocuo su una
macchina in esercizio.

---

### RAMO 2 — le prove sulla macchina

*(usa questo se c'è il via libera per la macchina di PayPrint, o se è arrivata la nostra)*

Segui `md/guide/guida-prove-macchina-payprint.md`: dice che cosa leggere prima, che cosa chiedere a
PayPrint, che cosa **non** si può lanciare su una macchina che non è nostra, come si legge il log e
le 11 prove una per una. La lista da tenere aperta durante la sessione è
`md/checklist/checklist-macchina-reale.md`.

Le prove che si potevano fare sul simulatore sono già fatte: terminatore e raffica il 14 settembre
(`md/verbali/prova-terminatore-cr-2026-09-14.md`), le cinque sui banchi il 16
(`md/verbali/prove-banchi-ramo1-2026-09-16.md`). **Non rifarle.**

Col Tap acceso, sempre. Dopo ogni prova aggiorna `md/memoria-claude/esito-risposta-payprint.md` e i
documenti che la riguardano, e scrivi il verbale in `md/verbali/`.

**La macchina di PayPrint è di terzi.** Niente che svuoti le giacenze, azzeri le banconote o lasci un
incasso aperto senza un accordo esplicito con Viglione, e senza il mio via libera.

Che cosa decidono le prove: se la chiusura sul frame giusto vale anche sulla macchina (prova 3), la
Decisione 1 di Giano (4 e 5), la forma di `BUSY` (6), la chiusura forzata dal pannello (7), se un
`ER` chiude l'incasso (8), la ripresa dopo una caduta (9), i valori del keepalive e quindi D2 (10).

### RAMO 3 — l'adattatore in Giano

*(usa questo se sono state prese le decisioni contabili del capitolo 3 di `quadro-05`)*

Le decisioni sono: **[incollale qui]**.

Poi, in quest'ordine:

1. **Disegna l'adattatore, prima di scriverlo.** L'SDK è `Task`-based con avanzamento a callback;
Giano è a thread e `Thread.Sleep`. Voglio vedere la forma del punto di contatto — quali membri, che
cosa restituisce l'operazione di incasso, come viaggiano avanzamento, annullo e commit, dove
finiscono gli `await`, che cosa succede se la connessione cade, come si riconosce la chiusura forzata
dal pannello — prima che venga scritta una riga.
2. **Poi il nuovo contratto di famiglia**: come diventa `IAutomatedPaymentMachine` con due marche
dentro.
3. **Poi il codice**, un pezzo alla volta, partendo dal livello dispositivo e dalla registrazione
hardware — **non dalla UI**.

L'ordine è quello che ha funzionato per il cassetto OPOS: **prima il pezzo fuori sagoma, provato da
solo contro l'hardware, poi tutto il resto in un colpo.**

### RAMO 4 — le piccole cose

*(usa questo quando non c'è altro di pronto: non dipendono da nessuno)*

- **La guida delle prove e la checklist raccontano le stesse 11 prove** in due posti (capitolo 5 di
`md/guide/guida-prove-macchina-payprint.md` e `md/checklist/checklist-macchina-reale.md`): prima o
poi divergeranno. Da valutare se la checklist diventa l'unico elenco e la guida tiene solo le
differenze per una macchina di terzi.
- **Il dettaglio giacenze ha etichette diverse nei due banchi** (`monete disponibili :` contro
`monete disponibili   :`): cosmetico, ma sono gemelli.
- **`PagAmicoFill` non ha una protezione vera**: è solo scritto nei documenti che non va usato su una
macchina reale. Si potrebbe rifiutare un indirizzo diverso da `127.0.0.1` senza un'opzione esplicita.
- **Il registro su file fra due processi Kotlin veri** non è mai stato provato (`quadro-02`).

**Non** toccare, finché non ci sono le prove sulla macchina: il default della pausa di 80 ms, il
timeout dell'incasso (D2), la chiusura dell'incasso su un `ER` dopo l'`OK`.

Se qualcosa non torna, chiedimi.
