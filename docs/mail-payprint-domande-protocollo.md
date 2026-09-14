# Bozza risposta a Raffaele Viglione (PayPrint)

**A:** r.viglione@payprint.it
**Oggetto:** Re: pagAmico – domande sul protocollo e sull'integrazione nel gestionale

> **Da inviare:** dal saluto fino alla firma.
> La sezione finale «Non in questa mail» **non fa parte del messaggio**: è il materiale messo da
> parte per un secondo giro, dopo che sarà arrivata una risposta a queste domande.

---

Ciao Raffaele,

grazie del simulatore, l'ho installato ed usato.
(Mancherebbe una scrollbar verticale sulla prima colonna, dove sono presenti le categorie con
l'icona: se uno non massimizza la finestra si perdono le categorie più in basso, che sforano
l'altezza della finestra.)

Sto scrivendo una libreria C# e una libreria Kotlin per il pagAmico, più due applicazioni desktop
di test.

Ti chiedo alcune cose che dai manuali non si riesce a capire bene, divise in due blocchi (un
blocco riguardante il protocollo, l'altro riguardante l'integrazione nel nostro gestionale).


Blocco 1 — protocollo

1.1) Distanza minima fra due comandi: Due comandi inviati troppo ravvicinati vengono letti come
un'unica stringa, e il secondo si perde senza risposta. Nelle librerie imponiamo una pausa di 80 ms
fra un invio e il successivo, valore ricavato a occhio.

Esiste una distanza minima garantita dal firmware?


1.2) Terminatore dei comandi: Inviamo senza alcun terminatore e il dispositivo risponde
correttamente.

È la forma giusta, o i comandi vanno chiusi con CR, LF o CR+LF?



Blocco 2 — l'innesto nel gestionale

Nel nostro gestionale la cassa che pilotiamo oggi espone una API REST senza stato: la transazione
vive sulla macchina, ha un identificativo, e la si interroga quando si vuole. pagAmico invece ha
una connessione persistente e gestisce una transazione alla volta.


2.1) Mentre un `[IN]` è in corso, il dispositivo accetta altri comandi sullo stesso socket?

Ci serve per due cose: chiudere l'incasso trattenendo il contante (`[CM]`) e
interrogare lo stato (`[ST]`) per l'heartbeat. Nella nostra libreria i comandi sono serializzati,
perché il protocollo non correla le risposte alle richieste; ma `[IN]` resta sospeso per tutta la
durata dell'incasso, e quindi tutto il resto si accoda. La serializzazione la togliamo noi se
serve: quello che ci serve sapere è se il dispositivo accetta `[CM]` e `[ST]` sullo stesso socket
mentre l'incasso è aperto, o se va aperta una seconda connessione.


2.2) Sulla risposta `AN` arrivano anche `collectedAmount` e `amountUnpaid`?

Dalle prove fatte fino ad adesso, sulla risposta `AN` vediamo `changeReturn`, `halted`,
`errorType`. Senza l'incassato non abbiamo il termine di paragone: sappiamo quanto è stato
restituito, ma non quanto era entrato, e quindi non possiamo accorgerci di una restituzione
incompleta.
(quindi ipoteticamente è denaro del cliente che resta nella macchina senza che il gestionale lo
sappia)


2.3) Mentre un incasso è aperto, la macchina manda qualcosa che nessuno le ha chiesto?

Lo chiediamo soprattutto per il testo: `CMD ERROR`, `OK comando`, `BT1`, `EX`, il contenuto di un
barcode letto. Noi distinguiamo le risposte JSON da quelle testuali, e una riga di testo che
arrivasse durante un `[IN]` la prenderemmo per l'esito dell'incasso.
Se succede ci adeguiamo noi, ci serve solo sapere se succede, e in quali casi.


2.4) Un incasso rimasto aperto, come si chiude?

Se il client smette di attendere — noi abbiamo un timeout di cinque minuti — o se cade la rete a
metà incasso, la macchina che cosa fa, e come si chiude l'incasso rimasto aperto?
E collegata: riconnettendoci, c'è un modo di chiedere alla macchina se ha una transazione in corso
e a che punto è? `[LO]` ci dà l'ultimo JSON trasmesso, che non è detto sia lo stato corrente.


2.5) `amountPaid`: è l'incassato al netto del resto, o il totale erogato?


Le tre che seguono sono conferme veloci: su queste ci adeguiamo noi nel gestionale, ci basta un sì
o un no.


2.6) Il tetto di `[IN]` è 9.999,99 €?

Sono sei cifre di centesimi, mentre `[PA]` ne ha dieci. Mettiamo un controllo esplicito a monte,
ci serve solo sapere che il limite è quello.


2.7) I parziali `{"response":"p"}` arrivano per ogni pezzo inserito, o possono essere accorpati?

Li usiamo per mostrare al cliente quanto ha inserito finora; se possono accorparsi, il totale a
schermo lo costruiamo in un altro modo.


2.8) Le macchine che consegnate hanno una password di erogazione impostata, e come si configura?


Grazie ancora,

Adamo B.
Prime Software S.r.l.

---
---

# Non in questa mail — messo da parte per un secondo giro

**Niente di quanto segue va nel primo messaggio.** Sono cose che non servono all'innesto in Giano e
che diluirebbero le domande di sopra: il display pilotabile, le immagini, la stampa e i movimenti
sono **capacità nuove** che il pagAmico offre e che la cassa attuale non ha, non lacune da colmare
adesso. Si mandano quando sarà arrivata una risposta al primo giro.

## A. Le due domande di protocollo rimandate

**A1. Formato completo di `[DI]`.** Il comando non c'è nel manuale 2.33 e non compare nemmeno
nell'elenco di *Integrazione 1.2*. La guida del Dev Kit lo dà come
`DI|dim|stile|colore|titolo|…|tastiera`, coi puntini. Dentro il catalogo comandi
dell'applicazione c'è però un esempio a dodici campi:

    DI|16|0|1|SCRIVI NOME|18|1|0|OK|ANNULLA|ESCI|3

che leggeremmo come: dimensione, stile e colore del titolo; il titolo; gli stessi tre attributi
per il campo di input; le tre etichette dei pulsanti; il tipo di tastiera. **È corretta questa
lettura?** Oggi inviamo una forma a sette campi e il simulatore la accetta, ma così non compaiono
né i pulsanti né la formattazione del campo.

*Nota per noi:* il simulatore accetta `DI` in tutte le forme che gli abbiamo passato — sette campi,
sei campi, con il titolo in prima posizione — e risponde sempre col testo digitato. È il motivo per
cui non siamo riusciti a ricavare da lì il formato giusto: se validasse la lunghezza dei campi come
fa per gli altri comandi, il punto si chiuderebbe da solo.

**A2. Incapsulamento di `[SF]` e `[SI]`.** A pagina 66 del manuale 2.33 le due forme non
coincidono. Il testo dice:

    "SF" + CHR(255) + CHR(255) + immagine + CHR(254) + CHR(254) + "||"

e l'esempio Python subito sotto costruisce:

    b"SF\xFF\xFF||" + immagine + b"||\xFE\xFE"

Le doppie pipe cambiano posizione. **Quale accetta il firmware 8.72?** E già che ci siamo: oltre
al rettangolo consigliato di 571 × 520 dip, **c'è un limite in byte?** Vorremmo capire se
conviene ridimensionare l'immagine prima di spedirla.

*Nota per noi:* nella libreria sono implementati **entrambi** i layout, selezionabili, con quello
descritto a testo come predefinito. Finché non si usano le immagini in Giano, l'ambiguità non
costa niente.

## B. Riscontro sul simulatore

Nessuno ci ha chiesto un riscontro: si manda solo se la conversazione si apre.

- **Suggerimento sull'interruttore "Il cliente inserisce l'importo esatto".** Con l'interruttore
  spento il cliente virtuale arrotonda per eccesso, e riempire le giacenze diventa impossibile:
  ogni ciclo di tagli si chiude a saldo zero, perché l'ultimo giro restituisce come resto tutto
  quello che i precedenti avevano messo dentro. Ci abbiamo perso un po' prima di capirlo. Forse
  vale un avviso nella sezione Simulatore.

- **Se in una prossima versione si potessero forzare tre condizioni** — resto insufficiente,
  incasso interrotto a metà, messaggio spontaneo sul socket — sarebbe di grande aiuto per chi
  integra: sono esattamente i punti 2.1, 2.2 e 2.3 della mail, e oggi dal simulatore non sono
  raggiungibili.

- **Il catalogo comandi dell'applicazione è ottimo**, più preciso dei manuali su parecchi punti:
  la mappa degli array, i tipi di barcode, gli enumerativi del display. Su `DI` è l'unica fonte
  esistente.

- **In Simulatore → Log del dispositivo manca un "copia tutto"**: al momento le righe si copiano
  solo una per una, e per portarsi via una sessione intera diventa scomodo.

## C. Le nostre librerie, se ti va di guardarle

Se ti interessa, ti passiamo volentieri il codice della libreria C# e di quella Kotlin. Non è
un'offerta commerciale: le abbiamo scritte per noi, ma dato che ci dicevi che una libreria C# non
esiste, se guardandole trovi che possano servire ad altri integratori — o se semplicemente vuoi
verificare che stiamo interpretando bene il protocollo — te le mandiamo senza problemi. Sono
documentate e accompagnate dalla sequenza di collaudo.

Se guardi solo una cosa, guarda come gestiamo la serializzazione dei comandi e l'attesa
dell'incasso: è il punto su cui vertono le domande 2.1, 2.3 e 2.4, ed è dove è più probabile che
stiamo sbagliando noi.

## D. Tre punti sui manuali

Nel caso siano utili per le prossime revisioni:

- **Tipi di barcode.** Il manuale *Protocollo di stampa 2.00* si contraddice: la tabella di
  `PTBCBC` a pagina 3 dice `7 = CODE128` e `8 = CODE93`, le note di aggiornamento a pagina 5 dello
  stesso documento dicono `7 = CODE93` e `8 = CODE1128`. Abbiamo seguito la seconda, che coincide
  col Dev Kit, ma una conferma ci fa comodo.

- **Comando `[ID]`.** In *Integrazione 1.2* a pagina 11 il paragrafo si intitola `[ID] Dati
  tabella di output` ma la riga "Formato di richiesta completo" indica `TD<JSON>`. Noi mandiamo
  `ID<JSON>`, come il Dev Kit.

- **Causali dei movimenti.** Nell'elenco di `MV`, `011` e `071` valgono entrambe "Scarico Monete",
  `041` e `061` entrambe "Scarico Banconote". Se c'è una distinzione operativa fra le due coppie
  ci servirebbe conoscerla, perché dobbiamo classificare i movimenti nel gestionale.

## E. Sul collegamento al tuo pagAmico

Da chiedere **dopo** la risposta al primo giro, o durante la telefonata — e comunque superato il
giorno in cui arriva la nostra macchina.

Ci interessa molto, ma non vogliamo combinare guai su una macchina tua. La nostra idea sarebbe
partire dai soli comandi di lettura — `ST`, `CL`, `LO`, `MV`, `MI` — più display e stampa, e
concordare con te se e quanto possiamo spingerci su incassi ed erogazioni. Alcuni dei nostri passi
di collaudo azzerano le banconote o svuotano il BTA: quelli non li lanciamo senza il tuo via
libera esplicito.

Sappiamo anche **che cosa ci servirebbe vedere**, e sono quattro prove sole: un incasso portato a
termine guardando che cosa arriva davvero nei parziali (punto 2.7); un annullo con denaro dentro,
per vedere i campi della risposta `AN` (punto 2.2); un `CM` durante un incasso, fatto come lo
fareste voi (punto 2.1); e un incasso lasciato scadere, per capire come si chiude (punto 2.4).
Sono tutte cose che si fanno con pochi euro in monete e che non sporcano le giacenze.

## F. Tolto nello sfoltimento

Materiale che era nella bozza lunga e che non è andato in nessuna delle due parti. Serve solo come
promemoria del perché è caduto.

- **«A che punto siamo»** — il paragrafo sul collaudo a 64 passi, sulle due librerie allineate
  comando per comando e sulle 36 righe di scontrino identiche byte per byte. Sostituito da una riga
  sola in apertura. Motivo: due librerie scritte dalle stesse persone che concordano fra loro non
  dimostrano niente sulla correttezza rispetto al protocollo, e un lettore tecnico lo vede subito.

- **La premessa «un pagAmico fisico non l'abbiamo mai avuto in mano»** — tolta su indicazione di
  Adamo: PayPrint lo sa già, e una macchina è in arrivo.

- **La proposta di telefonata e l'indicazione delle domande prioritarie** — tolte da Adamo
  nell'ultima revisione. Restano segnalate qui perché la mail, così com'è, presenta dieci voci
  senza gerarchia: le più costose da rispondere (2.1, 2.2, 2.3) sono anche le uniche che bloccano
  il lavoro.
