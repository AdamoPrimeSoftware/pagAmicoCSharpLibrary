# Cosa non sappiamo

*Le domande ancora aperte, divise per chi può rispondere — e cosa cambia a seconda della risposta*

## A che serve questo documento

È il documento da tenere davanti durante la telefonata con PayPrint, insieme a
`md/memoria-claude/esito-risposta-payprint.md`, che dice che cosa ha chiuso la loro risposta scritta dell'11 settembre
e che cosa resta da chiedere. Non spiega il lavoro fatto: per quello ci sono gli altri quattro.
Spiega solo quello che **non sappiamo ancora**, e perché ciascuna cosa conta.

Le incognite sono di tre tipi diversi, e non vanno confuse:

| Tipo | Chi la chiude | Quante |
|---|---|---|
| Comportamenti del dispositivo che i manuali non descrivono | PayPrint, a voce o per iscritto | 6 — dopo la risposta: 3 chiuse, 2 in parte, 1 aperta |
| Cose che si vedono solo provandole su una macchina vera | una macchina, nostra o loro | 5 |
| Decisioni contabili e di prodotto | tu | 9 |

Il primo gruppo era quello urgente, perché **quattro risposte su sei cambiavano la struttura** del
flusso di pagamento, non un dettaglio. La risposta dell'11 settembre ha sciolto il nodo che
bloccava tutto: `CM` si manda durante l'incasso, e l'adattatore di Giano si può disegnare. Le
domande rimaste aperte rallentano, non bloccano; il lavoro da fare subito è nelle due librerie, dove
la risposta ha fatto emergere tre difetti da correggere (vedi `md/memoria-claude/esito-risposta-payprint.md`).

Il terzo gruppo non dipende da nessun altro: sono decisioni che possiamo prendere oggi.

> **Una cosa da tenere presente in tutto il documento.** Nessuna di queste incognite riguarda
> *comandi mancanti*. Il pagAmico sa fare praticamente tutto quello che Giano chiede oggi alla
> cassa VNE. Le incognite riguardano **come si comporta**, e sono più insidiose proprio per
> questo: un comando che manca lo scopri subito, un comando che si comporta diversamente da
> come credevi lo scopri in cassa, con il cliente davanti.

---

## 1. Le sei domande per PayPrint

Nella mail, che numera per blocco, sono i punti 2.1 (è la 1.1 di qui), 2.2 (1.2), 2.3 (1.5),
2.4 (1.6) e 2.5 (1.4). La 1.3 sui parziali è finita fra le tre conferme veloci in fondo, al
punto 2.7, insieme alle due minori — il tetto di `IN` al 2.6 e la password di erogazione al
2.8. Qui c'è il perché, che nella
mail è compresso. PayPrint ha risposto l'11 settembre (`md/fornitore/risposta-payprint-2026-09-11.md`): sotto
ogni domanda c'è lo stato dopo la risposta, come risulta da `md/memoria-claude/esito-risposta-payprint.md`.

### 1.1 Come si manda `CM` mentre un incasso è aperto?

**È la più importante.** Riguarda direttamente i soldi.

**Dopo la risposta (mail, punto 2.1): chiusa.** Durante un incasso la macchina accetta solo `AN` e
`CM`: `CM` si manda mentre l'`IN` è aperto. Il limite era della nostra libreria, non del
dispositivo, e il lavoro da fare è lì (`md/memoria-claude/esito-risposta-payprint.md`, paragrafo 3). Resta da
chiedere quanti messaggi seguono un `CM` e quale porta l'esito (esito, paragrafo 5, domanda 1;
qui, capitolo 2, prova 4).

Il pagAmico ha due modi di chiudere un incasso a metà: `AN` restituisce il contante al cliente,
`CM` lo trattiene e lo registra come incassato. Sono due comandi distinti, con esito contabile
opposto — non è un parametro, è proprio un comando diverso.

Il problema è tecnico. La nostra libreria manda un comando alla volta, perché il protocollo non
correla le risposte alle richieste: se ne mandi due insieme, non sai di chi è la risposta che
arriva. Ma il comando di incasso resta sospeso per tutta la durata dell'incasso — fino a cinque
minuti — e in quel tempo tiene il turno. Un `CM` mandato nel frattempo **si mette in coda invece
di arrivare**.

Per `AN` ce la caviamo, perché lo mandiamo per una strada laterale. Per `CM` la libreria oggi una
strada laterale non ce l'ha: va aggiunta, come quella di `AN`. Scavalcare la coda non è più un modo
di aggirare un limite del dispositivo: è la correzione da fare nella libreria.

> **L'indizio che ci faceva pensare di non essere i soli.** Il nostro collaudo automatico, per provare
> `CM`, non usa il metodo di incasso della libreria: usa una scorciatoia. Cioè per collaudare quel
> comando abbiamo dovuto aggirare la nostra stessa API. Dopo la risposta l'indizio si legge al
> contrario: la via è prevista dal dispositivo, e la scorciatoia serviva perché mancava nella nostra
> API. Quel passo del collaudo, per di più, non aveva mai dimostrato un commit (difetto D1
> dell'esito). Dall'11 settembre la via c'è, e il collaudo la usa.

**Che cosa cambiava a seconda della risposta** — è arrivato il primo caso. Il secondo cade per
deduzione (che `CM` viaggi sulla stessa connessione dell'`IN` il fornitore non lo scrive, ma è
implicito), il terzo cade del tutto:

- Se esiste una sequenza prevista che non conosciamo, la adottiamo e il flusso resta semplice.
- Se serve una **seconda connessione** al dispositivo, cambia il disegno: due connessioni per
  box, con tutto quello che comporta (chi le apre, chi le chiude, cosa succede se una cade).
- Se `CM` non è invocabile durante un incasso, allora **quella funzione a Giano non c'è**, e la
  decisione 3.1 qui sotto si semplifica per forza: resta solo `AN`.

**Per il comando di stato la risposta è no.** Ci servirebbe come battito cardiaco, per sapere che il
box è vivo, ma durante un incasso non va mandato: il fornitore dice che verrebbe ignorato, e che
`ST` va usato solo a transazione conclusa o annullata. È una regola del dispositivo, non solo della
nostra coda. Durante l'incasso l'unico segnale di vita è il keepalive TCP, regolato dal 14
settembre a circa 20 s ma non ancora provato con un cavo staccato (vedi D2 nell'esito).

### 1.2 Che campi arrivano nella risposta di annullo?

**Dopo la risposta (mail, punto 2.2): ancora aperta.** Il fornitore spiega che `amountUnpaid` è il
resto non erogato per mancanza di monete, ma la frase riguarda il resto di un incasso, non il
rimborso di un annullo: se su `AN` arrivino `collectedAmount` e `amountUnpaid` non lo dice. Nei log
del simulatore non c'è un solo `AN` con denaro dentro. Va provato sulla macchina (capitolo 2,
prova 2).

Quando annulliamo restituendo il contante, la macchina ci dice quanto ha restituito. Non sappiamo
se ci dice anche **quanto aveva incassato**.

Senza quel secondo numero non abbiamo il termine di paragone, e non possiamo accorgerci di una
restituzione **incompleta** — cioè del caso in cui la macchina non aveva le monete giuste e ha
reso meno di quanto aveva preso.

> **! Perché è il caso che fa più paura.** È denaro del cliente che resta dentro la macchina
> mentre il gestionale crede che sia stato reso. Oggi, con la cassa VNE, questo caso è gestito
> male: l'operatore preme OK, l'ordine si finalizza per intero, e non viene scritta nemmeno una
> riga di registro. Col pagAmico avremmo lo strumento per chiuderlo — ma solo se quel campo
> arriva.

Nel nostro codice quel campo non compare mai associato a una risposta di annullo. Può darsi che
arrivi comunque e che non l'abbiamo mai osservato, perché sul simulatore non riusciamo a
provocare la situazione.

### 1.3 Che cosa contiene esattamente un messaggio parziale?

**Dopo la risposta (mail, punto 2.7): chiusa**, tranne i campi: il fornitore non li elenca, e si
vedono sul simulatore (capitolo 2, prova 1).

Durante un incasso la macchina manda messaggi di avanzamento, uno per ogni pezzo inserito. Li
usiamo per mostrare al cliente quanto ha messo dentro finora.

Non abbiamo un campione documentato di uno di questi messaggi, e non sappiamo quali campi porti.
Il resto l'ha chiarito il fornitore: un parziale arriva ogni volta che viene aggiunto del contante,
ed è **cumulativo** — porta il totale inserito fino a quel momento, non il singolo pezzo.

Contava perché durante un incasso **non possiamo interrogare nulla** (vedi 1.1). Essendo
cumulativi, però, un parziale perso non costa nulla: il totale arriva col successivo e con l'esito
finale.

### 1.4 Che cosa significa `amountPaid`?

**Dopo la risposta (mail, punto 2.5): chiusa dal manuale.** Il fornitore non ha risposto, ma il
manuale lo definisce già; resta da confermare se comprende il resto.

Nelle risposte ci sono tre campi sugli importi erogati: le monete rese, le banconote rese, e un
terzo campo chiamato `amountPaid`. Scrivevamo che non è documentato da nessuna parte, ed era un
errore nostro: il manuale lo definisce come "Importo erogato totale" (pp. 9 e 59).

Ci serve perché la cassa che usiamo oggi dà "resto erogato" come un numero solo, e col pagAmico
dobbiamo ricostruirlo. Sommare monete e banconote sembra ovvio, e un importo erogato totale
sembrerebbe proprio quel numero; ma nel simulatore `amountPaid` vale l'erogato di `PA` ed è 0 sul
resto di un incasso. Se comprenda il resto va confermato: non vogliamo indovinare su un numero che
finisce in contabilità.

### 1.5 La macchina può mandare messaggi di sua iniziativa?

**Dopo la risposta (mail, punto 2.3): parziale.** `CMD ERROR` arriva per un comando sconosciuto,
`BUSY` per un comando che la macchina non può eseguire perché è impegnata in un incasso o in altro:
sono risposte a comandi nostri, non messaggi spontanei, e su codici a barre, `BT1` ed `EX` il
fornitore non dice nulla. Ma il rischio descritto qui sotto diventa concreto per un'altra via: un
`BUSY` provocato da noi durante un incasso — testo o `ER` che sia, la forma va chiesta — chiudeva
l'attesa dell'incasso, e la transazione si perdeva (difetto D3 dell'esito). Corretto l'11
settembre: a incasso aperto la libreria non manda più niente che non sia `AN` o `CM`, e dopo
l'accettazione un testo non chiude l'incasso ma finisce fra i frame orfani.

La nostra libreria distingue le risposte strutturate da quelle testuali. Quando arriva una
risposta testuale la interpreta come esito del comando in corso.

Se durante un incasso la macchina mandasse spontaneamente una riga di testo — la lettura di un
codice a barre, un avviso — noi la scambieremmo per l'esito dell'incasso e **perderemmo la
transazione**.

> **! La regola prudenziale ora è una regola del dispositivo.** Mentre un incasso è aperto non
> mandiamo altro al box: niente display, niente stampa, niente diagnostica. La macchina accetta solo
> `AN` e `CM`, e il resto lo ignora o lo respinge con `BUSY`: non è più una limitazione nostra da
> togliere, e il display del pagAmico durante l'incasso non si usa. Oggi però la libreria non la fa
> rispettare: ogni invio che non sia `AN` o `CM` va bloccato lì (`md/memoria-claude/esito-risposta-payprint.md`,
> paragrafo 3).

### 1.6 Che cosa succede se il software si arrende a metà incasso?

**Dopo la risposta (mail, punto 2.4): parziale.** Ora sappiamo che cosa fa la macchina; non sappiamo
ancora come si riprende un incasso dopo una caduta.

La nostra libreria ha un timeout di cinque minuti. Allo scadere smette di attendere, e basta: non
manda `AN`. La macchina invece un timeout non ce l'ha, e il fornitore dice di non metterlo nemmeno
nella nostra procedura; sconsiglia anche `I2`, l'incasso con tempo di disconnessione. Così com'è,
la libreria abbandona un incasso ancora aperto mentre la macchina continua a incassare: è il
difetto D2 dell'esito.

L'incasso resta aperto e il cliente può ancora infilare denaro. Si chiude dalla cassa con `AN` o
`CM`, oppure dal pannello con una chiusura forzata — tenendo premuto sulla parola RESTO del display
e inserendo la password — che ci arriva come `AN` (il manuale, a p. 19, descrive un gesto diverso:
va chiesto). Se cade la rete, la macchina accetta la riconnessione dallo stesso IP. Quello che non
sappiamo ancora è come si scopre quanto è entrato nel frattempo: il fornitore non dice se dopo la
riconnessione i messaggi arrivano sul nuovo socket, né come chiedere lo stato. Stessa cosa per un
riavvio del PC.

Il comando di recupero che abbiamo rimanda l'**ultimo** messaggio trasmesso, che non è
necessariamente lo stato corrente — e durante un incasso non si può nemmeno mandare, perché la
macchina accetta solo `AN` e `CM`. Un "hai una transazione in corso, e a che punto è?" non lo
conosciamo, e il fornitore non ne indica uno (esito, punto 2.4).

> **Perché ci teniamo.** Sulla cassa che usiamo oggi questo si fa, ed è la rete di sicurezza per
> il PC che si riavvia in mezzo a un pagamento. Perderla è un passo indietro, e vogliamo sapere
> con che cosa la sostituiamo.

### Le altre due, minori

**Tetto d'importo.** L'incasso ha sei cifre di centesimi, quindi il massimo è 9.999,99 €.
L'erogazione ne ha dieci. Il fornitore conferma (mail, punto 2.6): per i contanti il massimo è
9.999,99 €. Per il POS dice "maggiore", ma nel manuale `PO` ha sei cifre fisse (p. 17): con quale
formato, va chiesto. Sono importi fuori scala per la ristorazione, ma preferiamo mettere un
controllo esplicito a monte sapendo che il limite è quello vero, piuttosto che scoprirlo in cassa:
la nostra libreria solleva un'eccezione **prima ancora di trasmettere**.

**Password di erogazione.** I comandi di erogazione accettano una password concatenata. Il
fornitore risponde a metà (mail, punto 2.8): la password c'è, ma non la ricorda, e l'erogazione di
default non dovrebbe essere abilitata. Bisogna quindi farsela dare — e una password serve anche per
la chiusura forzata dal pannello (1.6): se sia la stessa, va chiesto. Con ogni probabilità serve una
colonna nuova nel nostro database, e va deciso come conservarla — perché viaggerebbe in chiaro sul
socket.

---

## 2. Le cinque cose che si vedono solo su una macchina

Il simulatore di PayPrint è stato decisivo per costruire le librerie, ma ha un limite netto: non
sa riprodurre le situazioni sbagliate. Non si può forzare un resto insufficiente, e non c'è uno
stato da cui ripartire dopo una caduta.

| # | Che cosa si vuole vedere | Come si provoca | Serve una macchina vera? |
|---|---|---|---|
| 1 | i campi veri di un messaggio parziale | un incasso qualsiasi, guardando il traffico grezzo | no, basta il simulatore col proxy acceso |
| 2 | i campi della risposta di annullo | incasso, inserire meno del dovuto, annullare | sì |
| 3 | una restituzione incompleta vera | svuotare le monete, poi chiedere un incasso che richieda resto | sì |
| 4 | come si chiude un incasso in corso con `CM`: quanti messaggi arrivano e quale porta l'esito (che si possa, l'ha confermato PayPrint; il simulatore ne manda due, l'accettazione con `errorCode` a `OK` e l'esito finale: vedi D1 nell'esito) | avviare un incasso e mandare `CM` mentre è aperto | no, il simulatore l'ha già mostrato; va confermato da PayPrint o sulla loro macchina |
| 5 | che cosa arriva dopo una caduta di rete a incasso aperto, e dopo una chiusura forzata dal pannello (che l'incasso non scada, l'ha detto PayPrint) | avviare un incasso, staccare la rete e riconnettersi dallo stesso IP; poi chiuderlo dal pannello | sì |

Sono le stesse domande del capitolo 1, viste dall'altro lato. PayPrint ha risposto: la 1, la 4 e
la 5 diventano in parte conferme (che i parziali siano cumulativi, che `CM` si possa mandare, che
l'incasso non scada), ma ciascuna ha ancora qualcosa da scoprire; la 2 e la 3 restano scoperte.

> **+ Sono prove economiche.** Si fanno tutte con pochi euro in monete, e nessuna richiede di
> lasciare la macchina in uno stato strano. **Le tre che vogliono una macchina vera sono la 2, la
> 3 e la 5**, e occupano mezz'ora in tutto. La prova 3 è l'unica che sporca: va svuotata la cassa
> monete prima.

Il collegamento al loro pagAmico l'ha offerto PayPrint stesso. Quando ci colleghiamo, l'ordine
giusto è: prima le letture innocue per prendere confidenza, e con quelle la prova dei comandi
terminati da CR senza pausa (domande 1.1 e 1.2 della mail: il fornitore dice che con CR o CR+LF la
pausa non serve; finché non è provato sulla macchina, la pausa di 80 ms della libreria resta come
rete di sicurezza — vedi l'esito, paragrafo 3); poi la prova 4, poi la 2 e la 5, e la 3 solo con
il loro via libera esplicito.

---

## 3. Le nove decisioni che spettano a te

Nessuna di queste dipende da PayPrint. Sono scelte nostre, e la maggior parte va presa **prima**
di scrivere il codice, non durante.

### 3.1 Quando l'operatore annulla con denaro dentro: restituire o trattenere?

**È la decisione più pesante del progetto.**

Oggi, con la cassa VNE, Giano fa una cosa sola: chiede alla macchina di **trattenere** il denaro
già inserito, e registra l'operazione come **annullata**. Cioè: nessuna vendita, e i soldi restano
nel dispositivo.

Il comando pagAmico che compie lo stesso gesto fisico è `CM`, ma nel loro modello `CM` significa
"**incasso accettato** e chiuso", e il fornitore l'ha confermato: chiude "accettando l'incassato".
L'importo trattenuto il testo del manuale lo chiama `committedAmount` (p. 44), il fornitore e il
diagramma di p. 13 `collectedAmount`; nel messaggio finale il simulatore li valorizza entrambi
(vedi D1 nell'esito). Le librerie leggono `collectedAmount` e usano `committedAmount` come
controllo.

> **! Stessa azione sul contante, esito contabile opposto.** Non esiste una traduzione neutra.
> Portare il codice attuale così com'è significa registrare incassi che non ci sono, oppure
> restituire soldi che andavano trattenuti.

Le strade sono due:

- **Passare a `AN`**, cioè restituire sempre. È il comportamento che il cliente si aspetta ed è
  contabilmente pulito. Ma cambia quello che Giano fa oggi.
- **Usare `CM` e registrare una vendita parziale.** Più vicino al codice esistente, ma richiede
  di gestire il caso "ho incassato meno del dovuto" in tutta la catena a valle — tender, scontrino,
  fiscale. E anche "ho trattenuto più del dovuto": sul simulatore un `CM` ha trattenuto 470 € su
  80 richiesti, senza resto (esito, paragrafo 4; se la macchina vera faccia lo stesso, va chiesto).
  Rendere l'eccedenza richiede `PA`, che di default non dovrebbe essere abilitato.

La risposta alla domanda 1.1 (punto 2.1 della mail) è arrivata: `CM` è invocabile durante un
incasso, quindi la decisione non è presa d'ufficio e restano aperte tutte e due le strade.
L'inversione contabile è confermata, non risolta.

### 3.2 Il buco del resto non erogato: si chiude o si accetta?

Oggi, quando la macchina non riesce a erogare tutto il resto, Giano mostra l'importo mancante,
l'operatore preme OK, **l'ordine si finalizza per intero** e i soldi restano dentro. Non viene
scritta nemmeno una riga di registro.

Col pagAmico avremmo il dato per accorgercene in un campo dedicato (`amountUnpaid`: il fornitore
conferma che contiene il resto non erogato per mancanza di monete), e avremmo anche lo strumento per
rimediare: un comando di erogazione con cui rendere la differenza — che però, dice il fornitore, di
default non dovrebbe essere abilitato.

La decisione contabile va presa comunque, ed è indipendente dal dispositivo:

- si registra la differenza da qualche parte e si rende all'operatore il compito di sistemarla?
- si eroga automaticamente, se la macchina può?
- si blocca la finalizzazione finché non è risolto?

### 3.3 Si allarga il confine fra la finestra di pagamento e il resto di Giano?

Oggi, quando la finestra di pagamento si chiude, l'unica informazione che passa al resto
dell'applicazione è **un booleano**: annullato oppure no. Importo effettivamente incassato, resto
erogato, resto non erogato, identificativo della transazione: niente di tutto questo esce, e
niente viene mai scritto da nessuna parte.

> **! Questa è la decisione che determina se il lavoro serve a qualcosa.** Il pagAmico offre
> *più* dati della cassa attuale, incluso un identificativo di movimento che il dispositivo
> conserva nel proprio database e che si potrebbe riconciliare a posteriori. Ma finché quella
> riga resta un booleano, si perdono esattamente come si perdono oggi — e nessuno dei buchi
> contabili esistenti si chiude. Sostituire il box senza toccare quel punto è lavoro sprecato.

### 3.4 L'identificativo del movimento va conservato?

Oggi non esiste **nessuna chiave** che leghi il contante dentro la macchina agli scontrini di
Giano. Se domani c'è una discrepanza di cassa, non c'è modo di ricostruire quale incasso
corrisponde a quale scontrino.

Il pagAmico assegna un identificativo a ogni movimento e lo conserva. Scriverlo insieme allo
scontrino costa poco e risolve la riconciliazione. Va deciso adesso perché tocca il record di
finalizzazione, cioè il database.

### 3.5 Le altre cinque

| Decisione | Il punto |
|---|---|
| Cosa fare se l'incasso non finisce | niente timeout: il fornitore dice di non metterlo, la macchina non ne ha e l'incasso resta aperto (`I2` ne ha uno, ma il fornitore lo sconsiglia). I cinque minuti di oggi non sono una rete di sicurezza: allo scadere la libreria abbandona un incasso ancora aperto e la macchina continua a incassare (difetto D2 dell'esito). Resta da decidere chi chiude: l'operatore dalla cassa, con `AN` o `CM`, o la chiusura forzata dal pannello? |
| Il bottone *Clear* della pagina Hardware | oggi svuota la coda dei pagamenti pendenti. Col pagAmico quella coda non esiste: si rimuove, o si sostituisce con una diagnostica? |
| Il rifornimento si rimuove o si riattiva? | oggi è morto in due modi indipendenti — il flag che lo abilita non viene mai acceso, e il pulsante sta in una colonna larga zero. Col pagAmico esisterebbero i comandi, ma è lavoro nuovo |
| Più postazioni sullo stesso box | oggi si prende sempre il primo box e la cosa funziona perché la cassa VNE è senza stato. Col pagAmico ogni box è una connessione con una sola transazione: serve una regola su chi può aprirla |
| Si usa il display del pagAmico? | oggi tutto il dialogo col cliente è sulla finestra del PC, perché la cassa VNE non offre nulla. Il pagAmico ha un display pilotabile. È un'opportunità, ma solo prima e dopo l'incasso: durante, la macchina accetta solo `AN` e `CM` (domanda 1.5) |

---

## 4. Come impostare la telefonata

La telefonata ora la propone PayPrint stesso, nella risposta dell'11 settembre. Un'ora basta e
avanza, se si va in quest'ordine.

**Primo, ringraziare e dare il riscontro.** Il simulatore è stato decisivo e va detto. Le tre
osservazioni utili per la loro BETA sono: che il simulatore accetta il comando `DI` in qualunque
forma (e quindi da lì non si ricava il formato giusto), l'interruttore dell'importo esatto che
rende impossibile riempire le giacenze, e il fatto che le tre situazioni che ci interessano di
più — resto insufficiente, incasso interrotto, messaggio spontaneo — non si possono provocare.

**Secondo, dire a che punto siamo.** Due librerie complete, C# e Kotlin, che passano lo stesso
collaudo sul simulatore. Va detto però che la loro risposta ci ha fatto trovare tre difetti, che
il collaudo non vedeva (D1-D3 dell'esito): due li abbiamo già corretti, il terzo aspetta le prove
sulla macchina. Non è una richiesta di aiuto: è il
contesto che rende sensate le domande, e la prima nasce proprio da D1. E l'offerta di passargli il
codice va fatta qui, perché è genuina e cambia il tono della conversazione.

**Terzo, le domande che restano.** La mail ha già avuto risposta per iscritto, e `CM` durante
l'incasso (1.1 qui, punto 2.1 della mail) è chiusa. Al telefono se ne reggono due o tre; l'elenco
in ordine di peso è in `md/memoria-claude/esito-risposta-payprint.md`, paragrafo 5. In testa: quanti messaggi
arrivano dopo un `CM` e quale porta l'esito, se un `CM` a importo già superato rende il resto, e i
campi della risposta di annullo (1.2 qui, punto 2.2 della mail).

**Quarto, il collegamento alla loro macchina**, che PayPrint ci ha offerto: va accettato. Qui la
posizione da tenere è chiara: partiamo dalle sole letture, e sulle prove che toccano il contante
chiediamo il via libera prova per prova. Delle cinque prove del capitolo 2, quattro si possono fare
sulla loro macchina e occupano mezz'ora in tutto; la 3, che richiede di svuotare la cassa monete,
solo con il loro via libera esplicito.

> **+ La cosa da portare a casa.** La risposta chiara su `CM` durante l'incasso è già arrivata per
> iscritto: era l'unica incognita che bloccava il disegno del flusso di pagamento invece di
> rallentarlo, e il blocco è caduto. Dalla telefonata ora serve soprattutto sapere che cosa arriva
> dopo un `CM`: quanti messaggi, e quale porta l'esito.

---

## 5. Dove trovare il dettaglio

| Se ti serve | Guarda |
|---|---|
| il quadro generale del lavoro | *Quadro d'insieme* (documento 1) |
| come sono fatte le librerie | *Le due librerie* (documento 2), e per il dettaglio tecnico *Integrazione pagAmico* |
| a che serve ciascun programma di prova | *I programmi di prova* (documento 3), e per l'uso pratico *Guida alle prove* |
| che cosa c'è già in Giano | *Giano: dove siamo* (documento 4), e per il dettaglio riga per riga `md/memoria-claude/analisi-giano-vne-vs-pagamico.md` |
| il testo delle domande per PayPrint | `md/fornitore/mail-payprint-domande-protocollo.md` |
| la risposta di PayPrint, e che cosa ha chiuso | `md/fornitore/risposta-payprint-2026-09-11.md` e `md/memoria-claude/esito-risposta-payprint.md` |
