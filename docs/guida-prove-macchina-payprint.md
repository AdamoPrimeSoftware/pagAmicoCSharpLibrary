# Prove sulla macchina di PayPrint

*Come preparare e condurre la sessione di prove sul pagAmico di PayPrint con i programmi demo: che cosa chiedere, che cosa portare, che cosa lanciare e che cosa no*

> **In breve.** Le prove da fare sono le 11 di `checklist-macchina-reale.md`. Questa guida dice come farle sulla macchina di PayPrint: che cosa chiedere prima, come collegarsi, quali programmi usare, e soprattutto **quali parti del collaudo automatico non vanno lanciate** su una macchina che non è nostra. Servono quattro cose: un collegamento alla macchina, una persona di PayPrint davanti alla macchina, qualche moneta e banconota, e il nostro banco di prova con il log acceso.

>! **Tre regole prima di tutto.** Mai lanciare **Fill** (esegue incassi veri). Mai il profilo **6 - Collaudo macchina reale** né il collaudo con i gruppi di default: contiene un pagamento POS vero, la chiusura giornaliera del POS, la sostituzione del logo e l'azzeramento delle banconote. Mai il gruppo **riavvii**. Tutto quello che muove denaro o cambia la configurazione si fa solo dal banco, un comando alla volta, dopo averlo detto alla persona di PayPrint.

## Che cosa leggere prima, e in che ordine

Quattro documenti, un'ora in tutto. Servono tutti: questa guida dice *come* si fanno le prove, non
*perché*.

| | Documento | Che cosa ti dà | Quanto |
|---|---|---|---|
| 1 | `esito-risposta-payprint.md` | che cosa ha già detto il fornitore, i tre difetti trovati, e nel **capitolo 5 le 11 domande da fargli a voce** | 15 min |
| 2 | `checklist-macchina-reale.md` | le 11 prove in forma di elenco: cosa fare, cosa guardare, cosa decide ciascuna | 10 min |
| 3 | **questa guida**, capitoli 3, 4 e 5 | come si fanno **su una macchina che non è nostra**: cosa non lanciare mai, i comandi esatti, come leggere il log | 20 min |
| 4 | `prove-banchi-ramo1-2026-09-16.md` | com'è fatto un log giusto: le stesse prove già eseguite sul simulatore, riga per riga | 10 min |

`decisioni-innesto-giano.md` non serve durante la sessione, ma dice **perché** le prove 4, 5 e 9
contano: da lì esce la Decisione 1 di Giano.

> **Se leggi una cosa sola**, leggi il capitolo 5 di questa guida con la checklist accanto, e
> stampa la tabella qui sotto.

### A che cosa serve ciascuna prova

| Prova | Risponde alla domanda (cap. 5 dell'esito) | Che cosa decide |
|---|---|---|
| 1 | — | se la libreria parla con quel firmware. Se fallisce, si smette |
| 2 | 1.1, la pausa | se il default della pausa fra comandi può passare da 80 ms a 0 |
| 3 | 1, quanti frame dopo un `CM` e quale campo leggere | se la chiusura sul frame con `errorCode` diverso da `OK` vale anche sulla macchina (difetto D1) |
| 4 | 2 e 10, `CM` a importo superato e `amountPaid` | **Decisione 1 di Giano**: se serve una procedura per rendere l'eccedenza (`PA`) |
| 5 | 3, `AN` con denaro dentro | **Decisione 1 di Giano**: come si riconosce un rimborso incompleto |
| 6 | 4, la forma di `BUSY` | se `PagAmicoFrame.IsBusy` riconosce la forma vera |
| 7 | 7, chiusura forzata | come Giano distingue la chiusura dal pannello da un proprio annullo |
| 8 | 5, `ER` dopo l'`OK` | se l'`ER` va aggiunto ai frame che chiudono l'incasso |
| 9 | 6, riconnessione | la forma dell'API di ripresa, e la ripartenza di Giano dopo un riavvio (Decisione 2) |
| 10 | 8, come ci si accorge che la macchina non risponde | i valori del keepalive, e quindi quando si può togliere il timeout di 5 minuti (difetto D2) |
| 11 | 9, terminatore dopo i pacchetti immagine | se il CR va escluso dopo i pacchetti binari |

## 1. Che cosa chiedere a PayPrint prima della sessione

Da concordare per mail o telefono, prima di fissare il giorno.

| Da chiedere | Perché serve |
|---|---|
| **Come ci si collega**: accesso di rete alla porta del pagAmico (VPN o porta aperta) oppure controllo remoto di un loro PC che sta nella stessa rete della macchina (AnyDesk, TeamViewer) | decide il modo A o B del capitolo 2 |
| **Indirizzo IP e porta** del pagAmico (di fabbrica 9100) | tutti i comandi di questa guida li usano: qui sono scritti `IP` e `9100` |
| **Una persona davanti alla macchina** per tutta la sessione, raggiungibile al telefono o in videochiamata | serve per inserire denaro, guardare il display, staccare il cavo, usare il pannello |
| **Contante di prova**: alcune monete e 2-3 banconote di tagli diversi | prove 3, 4, 5, 7, 8, 9, 10 |
| **Password del pannello** per la chiusura forzata, e se l'erogazione `PA` è abilitata | prova 7 e prova 4 (restituzione dell'eccedenza) |
| **Consenso esplicito** per: chiusura forzata dal pannello, invio di un logo (`SF`) o di un'immagine temporanea (`SI`), stampe di prova | sono prove che cambiano o usano la loro macchina |
| **Versione del firmware** | per confrontarla con la 8.72 dei manuali |
| Una **fascia oraria di 2-3 ore** | la sequenza completa, con le pause, non sta in un'ora |

> **La password non va scritta in chat né nei documenti.** La persona di PayPrint la digita sul pannello, oppure la detta al telefono e la si inserisce solo nella casella del banco.

## 2. Due modi di collegarsi

| | **Modo A** — dal nostro PC | **Modo B** — da un PC di PayPrint |
|---|---|---|
| Come | VPN o porta aperta verso il pagAmico; i programmi girano sul nostro PC | controllo remoto di un loro PC nella rete della macchina; i programmi girano lì |
| Cosa preparare | niente di speciale: i 4 repository aggiornati | il **pacchetto di eseguibili** del paragrafo 2.2 |
| Banco Kotlin e collaudo Kotlin | sì, da IntelliJ o `gradlew` | solo se si installa il banco Compose (paragrafo 2.2) |
| Prova 2 (raffica, pausa a 0 ms) | **poco affidabile**: fra noi e la macchina c'è Internet, i segmenti TCP possono arrivare accorpati o spezzati diversamente | affidabile: stessa rete della cassa vera |
| Prova 9 (riconnessione dallo stesso IP) | la macchina vede l'IP del router o della VPN: va bene, purché resti lo stesso | come in cassa |
| Prova 10 (cavo staccato) | si fa, staccando il cavo **lato macchina** | si fa, staccando il cavo **lato macchina** |

Se PayPrint offre tutti e due, meglio il **modo B**: è la situazione di una cassa vera.

### 2.1 Modo A — dal nostro PC

Fare `git pull` nei 4 repository, poi controllare da PowerShell che la porta risponda:

```
Test-NetConnection IP -Port 9100
```

Se `TcpTestSucceeded` è `False`, fermarsi: la VPN o la porta non sono pronte.

### 2.2 Modo B — il pacchetto da portare sul loro PC

Il giorno prima, sul nostro PC, dalla cartella `pagAmico_CSharp_Demo`. Gli eseguibili sono **autonomi**: sul loro PC non serve installare .NET.

```
dotnet publish PayPrint.PagAmico.WinForms -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ..\pacchetto-prove\WinForms
dotnet publish PayPrint.PagAmico.LiveTest -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ..\pacchetto-prove\LiveTest
dotnet publish PayPrint.PagAmico.Tap      -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ..\pacchetto-prove\Tap
```

Si ottengono tre file, circa 290 MB in tutto (compressi in ZIP molto meno):

| File | Programma |
|---|---|
| `WinForms\PagAmicoDevTool.exe` | banco di prova |
| `LiveTest\PayPrint.PagAmico.LiveTest.exe` | collaudo automatico |
| `Tap\PagAmicoTap.exe` | proxy che registra il traffico |

La cartella `pacchetto-prove` sta fuori dai repository: non va committata. Si passa al loro PC con il trasferimento file del programma di controllo remoto. Nei comandi di questa guida, nel modo B, al posto di `dotnet run --project PayPrint.PagAmico.Tap --` si lancia direttamente `PagAmicoTap.exe`, e così per gli altri.

Per il **banco Kotlin** nel modo B, dalla cartella `pagAmico_Kotlin_Demo` (verificato il 15 settembre; lo strumento WiX lo scarica Gradle da solo):

```
gradlew.bat :pagamico-desktop:packageExe
gradlew.bat :pagamico-desktop:createDistributable
```

Il primo comando produce un **installatore** di circa 55 MB:

```
pagamico-desktop\build\compose\binaries\main\exe\pagAmico Test Bench-1.0.0.exe
```

Il secondo una **cartella pronta** di circa 110 MB, da copiare così com'è e avviare con `pagAmico Test Bench.exe`, senza installazione:

```
pagamico-desktop\build\compose\binaries\main\app\pagAmico Test Bench\
```

Tutte e due contengono il loro Java: sul PC di PayPrint non serve installare niente. Se PayPrint preferisce non installare programmi, si porta la **cartella**. Il runtime incluso contiene il modulo `jdk.net`, necessario per regolare il keepalive (prova 10): prima del 15 settembre mancava, e il banco impacchettato sarebbe rimasto con il keepalive di sistema.

## 3. Che cosa si può lanciare del collaudo automatico

Il collaudo (`LiveTest`) si lancia **sempre indicando i gruppi**: senza gruppi esegue quelli di default, che su una macchina non nostra fanno danni. I profili di Visual Studio 6 e 7 hanno l'indirizzo della nostra macchina (`192.168.1.231`): per PayPrint si usa la riga di comando.

| Gruppo | Che cosa fa | Sulla macchina PayPrint |
|---|---|---|
| `base` | `ST`, decodifica giacenze, `CL`, `LO` | **sì**, è il primo passo |
| `movimenti` | `MV`, `MI`: legge i movimenti di oggi | **sì**, sola lettura |
| `print` | `PTSTAT`: stato della stampante | **sì** |
| `display`, `display2` | finestre, messagebox, lettura codice, lista, input sul loro display | con la persona davanti: alcune finestre aspettano un tasto |
| `print2` | stampa di prova (stili, barcode, ESC/POS) | solo con consenso: consuma carta |
| `collect` | incasso 1,50 con parziali, `AN`, `CM`, **erogazione di 2,00 €** | **no**: si fa a mano dal banco (prove 3-5) |
| `erogazione` | eroga banconote e monete, sposta nel BTA, **azzera le banconote**, `I2`, `IM` | **no** |
| `cash` | cambia soglie, tagli abilitati, **fondo cassa**, azzera il BTA | **no**: cambia la loro configurazione |
| `ricariche` | sessioni di ricarica | **no** |
| `system` | totali POS, ultima transazione, **pagamento POS vero da 3,20 €**, ricarica | **no** |
| `pos2` | **chiusura giornaliera del POS**, primo DLL | **mai** |
| `immagini` | immagine temporanea, **logo permanente (sostituisce il loro)** | solo `SI`/`SR` dal banco, con consenso; `SF` mai |
| `di` | sonda del comando `DI` non documentato | no |
| `riavvii` | riavvia POS e pagAmico | **mai** |

> **La checklist va letta con questa tabella accanto.** La prova 11 della checklist lancia il gruppo `immagini`, che contiene anche `SF` (logo permanente): sulla macchina di PayPrint si fa solo dal banco, con `[SI] Immagine temporanea` e `[SR] Rimuovi immagine`, e con il loro consenso.

## 4. Preparazione il giorno delle prove

Nell'ordine, prima di mandare qualsiasi comando.

### 4.1 Il Tap

In una finestra di console (nel modo B aprire un prompt dei comandi e lanciare `PagAmicoTap.exe` con gli stessi argomenti: con il doppio clic partirebbe verso il simulatore):

```
dotnet run --project PayPrint.PagAmico.Tap -- --listen 9200 --target IP:9100
```

Nella console del Tap, oltre al traffico, **si possono scrivere comandi**: una riga (per esempio `ST`) parte verso la macchina chiusa da CR, sulla stessa connessione del banco, e compare come `T->M`. Serve alle prove 6 e 9.

### 4.2 Il banco WinForms

| Dove | Che cosa impostare |
|---|---|
| scheda **Connessione**, Indirizzo IP e Porta | `127.0.0.1` e `9200`, cioè attraverso il Tap. Solo per la prova 10: `IP` e `9100`, senza Tap |
| scheda **Connessione**, Terminatore comandi | deve esserci `\r`; se è vuoto, scriverlo |
| scheda **Connessione**, Password erogazione | solo quando serve `PA` |
| pannello del traffico, **registra su file** | acceso |
| pannello del traffico, **diagnostica libreria** | acceso **prima** di Connetti: la riga del keepalive compare solo alla connessione |
| **Finestra log** | aperta e visibile per tutta la sessione |

Poi **Connetti**. Nel log devono comparire, in quest'ordine:

```
i   keepalive TCP: prima sonda dopo 10 s, poi ogni 2 s, caduta dopo 5 sonde senza risposta
i   connesso a 127.0.0.1:9200 (pausa minima fra invii 80 ms, terminatore presente)
+   connesso a 127.0.0.1:9200
```

Se la riga del keepalive non compare, la diagnostica era spenta al momento di Connetti: disconnetti,
accendila, riconnetti. Se dice valori diversi da 10 s / 2 s / 5 sonde, il keepalive non è stato
applicato: con il banco Kotlin succede con un JDK vecchio (serve il 17.0.14 o successivo), ed è la
prova 10 a farne le spese.

### 4.3 Come si legge il log del banco

Una sessione normale, con un incasso chiuso da un `[CM]`, scrive righe di questa forma (dal banco
WinForms; **dal 16 settembre il banco Compose scrive esattamente le stesse**, importi compresi):

```
i     [IN] incasso contanti
TX >  IN000500                                    <- il comando che parte
i     incasso 'IN000500' accettato: da qui chiudono solo IN, AN e il CM finale
i     parziale: incassato 2,00 (monete 2,00, banconote 0,00), da incassare 3,00
i     [CM] commit
TX >  CM
RX <  {"response":"CM",...,"errorCode":"OK","committedAmout":0.0}      <- accettazione
RX <  {"response":"CM",...,"errorCode":"","committedAmout":2.0}        <- esito
+     response=CM  incassato=2,00  trattenuto=2,00 (controllo committedAmount=2,00)
```

Sei cose da saper riconoscere, per non scambiare il normale per un guasto:

| Riga | Che cosa vuol dire |
|---|---|
| `TX >` | il comando è partito davvero. **Dal 16 settembre precede sempre la risposta**: se manca, non è stato trasmesso niente |
| **due righe `+` identiche** dopo un `[AN]` o un `[CM]` a incasso aperto | normale: l'esito lo restituiscono sia l'attesa dell'incasso sia la chiamata di chiusura. Il comando è partito **una volta sola**, e lo dicono le righe `TX` |
| `ERR! frame orfano (nessuna attesa lo riconosce): ...` | un messaggio che nessuno stava aspettando. **Da annotare sempre**: può contenere importi, ed è il canale su cui Giano li salverà |
| `ERR! Incasso aperto: 'ST' non inviato, ...` | il banco ha bloccato un comando laterale: non è un errore della macchina, ed è il comportamento voluto |
| `ERR! Chiusura dell'incasso gia' richiesta con CM: 'AN' non inviato` | seconda chiusura sullo stesso incasso: nulla trasmesso |
| `ERR! stato macchina [E0920]: Monete esaurite` | avviso sullo stato della macchina, non esito del comando: si annota e si va avanti |

> **Attenzione all'incasso che si chiude da solo.** Se il denaro inserito raggiunge l'importo
> richiesto, l'incasso si chiude e i pulsanti premuti dopo finiscono su un incasso **già chiuso**:
> la macchina risponde lo stesso, ma la prova non prova più niente. Per questo le prove 3-9 chiedono
> di inserire **meno** dell'importo richiesto. Nel log si riconosce dalla riga
> `incasso 'IN000500' chiuso` comparsa **prima** del click.

### 4.4 Gli appunti

Un file di testo con l'ora: per ogni prova ora di inizio, che cosa si è fatto, che cosa ha fatto fisicamente la macchina (lo racconta la persona di PayPrint). Alla fine della sessione i log valgono più degli appunti, ma solo gli appunti dicono che cosa è successo **fisicamente**.

---

## 5. Le prove, una per una

Numerazione di `checklist-macchina-reale.md`. Prima di ogni prova dire alla persona di PayPrint che cosa sta per succedere.

### Prova 1 — collaudo di base, senza soldi

```
dotnet run --project PayPrint.PagAmico.LiveTest -- 127.0.0.1 9200 base
```

Tutti i passi OK. Se fallisce qui, **fermarsi**: la libreria non parla con questo firmware, e le altre prove non hanno senso. Salvare l'output.

### Prova 2 — terminatore e raffica

```
dotnet run --project PayPrint.PagAmico.LiveTest -- 127.0.0.1 9200 base,movimenti --terminatore cr --pausa 0
dotnet run --project PayPrint.PagAmico.LiveTest -- 127.0.0.1 9200 base --terminatore nessuno --pausa 0
```

Nel Tap: ogni comando ha la sua risposta, nessun «comando senza risposta». Con CR a 0 ms tutto OK → la pausa di default si potrà portare a 0. Nel modo A il risultato vale poco (vedi capitolo 2): annotarlo come indicativo.

### Prova 3 — incasso e chiusura con CM

Banco, scheda **Incasso**: importo `5,00`, **[IN] Contanti**. La persona inserisce **2,00 €**. Poi **[CM] Commit**.

Guardare nel log: quanti frame `CM` arrivano, `errorCode` di ciascuno, `collectedAmount` e `committedAmount` nel frame finale, eventuali `CM` in ritardo fra i frame orfani (righe «frame orfano»).

### Prova 4 — CM a importo superato

Importo `3,00`, **[IN] Contanti**. La persona inserisce **una banconota da 5**. Subito **[CM] Commit**.

Chiedere alla persona se la macchina **dà il resto**. Nel log: `changeCoins`, `changeBanknotes`, `amountPaid`, `amountUnpaid`. Se non dà il resto, l'eccedenza si restituisce con **[PA] Eroga importo** (password), solo se PayPrint è d'accordo.

### Prova 5 — annullo con denaro dentro

Importo `10,00`, **[IN] Contanti**. La persona inserisce **4,00 €**. Poi **[AN] Annulla**.

Chiedere se il denaro viene **reso tutto**. Nel log, frame `AN`: `collectedAmount`, `changeReturn`, `amountUnpaid`.

> **Le prove 4 e 5 decidono la Decisione 1 di Giano** (`decisioni-innesto-giano.md`): che cosa significa "annulla senza restituire". Annotare con cura importi e comportamento fisico.

### Prova 6 — BUSY durante l'incasso

Importo `5,00`, **[IN] Contanti**, nessun denaro. Nella **console del Tap** scrivere `ST` e premere Invio, poi `DS`.

Nel Tap, righe `T->M` e la risposta: testo `BUSY`, `ER` con `E100`/`errorType 99`, oppure niente. Nel banco la risposta deve comparire come **frame orfano** e l'incasso deve **restare aperto**. Poi **[AN] Annulla**.

### Prova 7 — chiusura forzata dal pannello

Solo con il consenso. Importo `5,00`, **[IN] Contanti**, la persona inserisce **1,00 €**, poi chiude l'incasso **dal pannello** (parola RESTO o rettangolo in alto a sinistra) e digita la password.

Nel banco: frame `AN` con `halted` = `TRUE`. Chiedere quale gesto ha funzionato e se il denaro è stato reso.

### Prova 8 — errore dopo l'OK

Solo se la persona lo ritiene innocuo. Incasso aperto, banconota inserita male o rovinata.

Arriva un `ER` che chiude l'incasso, o la macchina resta in attesa? Se resta in attesa: **[AN] Annulla**.

### Prova 9 — caduta e riconnessione

1. Importo `5,00`, **[IN] Contanti**, la persona inserisce **1,00 €**.
2. **Chiudere il banco** (la finestra). Il Tap chiude anche la connessione verso la macchina.
3. Riaprire il banco, stesse impostazioni, **Connetti** (sempre attraverso il Tap, dallo stesso PC).
4. La persona inserisce **altro 1,00 €**.
5. Guardare se i parziali arrivano nel banco (come frame orfani: la libreria non sa dell'incasso).
6. Chiudere dalla **console del Tap** scrivendo `AN`: la macchina risponde? Rende il denaro?

Se la macchina non risponde sulla nuova connessione, chiudere l'incasso dal pannello (con la persona). **Questa prova decide** come Giano riparte dopo un riavvio.

### Prova 10 — cavo staccato

**Senza Tap**: banco su `IP` e `9100`, diagnostica accesa **prima** di Connetti.

1. Importo `5,00`, **[IN] Contanti**.
2. Segnare l'ora. La persona **stacca il cavo di rete della macchina** (non quello del PC) per 1 minuto.
3. Guardare dopo quanti secondi il banco segnala «disconnesso»: atteso circa **20 s**.
4. La persona riattacca il cavo. Chiedere in che stato è la macchina (ancora in incasso?) e chiudere dal pannello se serve.

Se c'è tempo, ripetere con il **banco Kotlin** (JDK 17 aggiornato), diagnostica accesa.

### Prova 11 — immagini e stampa

Solo con il consenso, e **solo dal banco**: scheda **Display**, **[SI] Immagine temporanea...** con un'immagine piccola, poi **[SR] Rimuovi immagine**. Scheda **Stampa**: **Stampa scontrino** e **[PTPRDT] Invio ESC/POS** con un testo che contiene un a capo.

Mai **[SF] Invia logo...**: sostituisce il logo di PayPrint.

---

## 6. A fine sessione

**Disconnetti** nel banco, Ctrl+C nel Tap. Poi copiare la cartella dei log (nel modo B dal loro PC al nostro):

```
%LOCALAPPDATA%\PayPrint.PagAmico\logs
```

Contiene i log del banco (`winforms-*.log`), del collaudo (`collaudo-*.log`) e del Tap (`tap-*.log`). Sul nostro PC, rileggerli con:

```
cd pagAmico_CSharp_Lib
python strumenti\analizza_log.py --data AAAA-MM-GG
```

Lo script legge i log del banco e del collaudo; quelli del Tap si leggono a mano.

- Nel modo B: cancellare `pacchetto-prove` dal loro PC, se PayPrint lo preferisce.
- Riportare gli esiti in `esito-risposta-payprint.md` (capitolo 1 e punti 11-13) e in `decisioni-innesto-giano.md`.

## 7. Se qualcosa non va

| Sintomo | Che cosa fare |
|---|---|
| Connetti fallisce | Tap acceso? porta 9200 nel banco? Nel modo A: `Test-NetConnection IP -Port 9100` |
| Un comando non risponde mai | controllare che il terminatore sia `\r`; nel Tap cercare righe `ATTENZIONE` |
| `PagAmicoCollectionOpenException` | c'è un incasso aperto: **[AN] Annulla** o **[CM] Commit** prima di altri comandi |
| Incasso aperto e banco bloccato | chiudere dalla console del Tap (`AN`) oppure dal pannello, con la persona |
| Il banco dice «disconnesso» senza motivo | nel modo A: VPN caduta. Riconnettere e ricominciare dalla prova in corso |
| La macchina resta in uno stato strano | fermarsi e chiedere alla persona di PayPrint; niente riavvii da remoto |
