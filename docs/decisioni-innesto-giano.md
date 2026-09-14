# Decisioni da prendere prima dell'adattatore Giano

*14 settembre 2026. Due decisioni che bloccano il disegno dell'adattatore pagAmico in Giano. Qui ci
sono le opzioni e le loro conseguenze, non la scelta. Fonti: `analisi-giano-vne-vs-pagamico.md`,
`esito-risposta-payprint.md` (capitolo 4).*

---

## Decisione 1 — Che cosa significa "annulla senza restituire"

### Il problema

Giano chiama `CancelPayment(paymentId, returnPartialPayment: false, ...)` in tre punti su quattro.
Sul VNE vuol dire: **la macchina trattiene il denaro inserito, la vendita non c'è**.

Sul pagAmico lo stesso gesto fisico (trattenere) è solo `CM`, che però vuol dire **incasso accettato
e chiuso**: l'esito porta l'importo trattenuto (`collectedAmount`). `AN` invece **restituisce** il
denaro. Non esiste un comando "trattieni e annulla".

Stessa azione sul contante, **esito contabile opposto**.

### Opzioni

| | Opzione | Sul contante | In contabilità | Rischi |
|---|---|---|---|---|
| **A** | `returnPartialPayment: false` → `CM` | trattenuto | **incasso parziale registrato** come pagamento (acconto) | cambia il significato per chi usa oggi Giano: ciò che era "annullato" diventa "pagato in parte". Serve una causale o un documento per il parziale |
| **B** | `returnPartialPayment: false` → `AN` | **restituito** | vendita annullata, nessun movimento | cambia il comportamento fisico: il cliente riprende i soldi dove prima li lasciava. Coerente contabilmente, non con l'uso attuale |
| **C** | `CM` sulla macchina + **storno** in Giano | trattenuto | incasso registrato e subito stornato su un conto "denaro trattenuto" | il denaro resta tracciato e la vendita non esiste; richiede un conto/causale nuovo e una procedura di restituzione manuale |

### Da sapere prima di scegliere

- **`CM` a importo superato** (domanda 2 della telefonata, prova 4 della checklist): sul simulatore
  80 € richiesti e 470 € trattenuti senza resto. Se la macchina vera fa lo stesso, con A e C serve
  una procedura per l'eccedenza (`PA`, che il fornitore dice disabilitato di default).
- **`AN` con denaro dentro** (domanda 3, prova 5): se il rimborso può essere incompleto, anche B deve
  registrare quanto non è stato reso.
- **Chi chiama oggi `CancelPayment(false)`** e in quali casi d'uso reali (timeout operatore? cliente
  che si allontana?): decide quale delle tre è "naturale" per chi sta in cassa.

---

## Decisione 2 — Frame orfani e ripartenza dopo un riavvio

### Il problema

La libreria consegna a `OrphanFrame` / `onOrphanFrame` ogni messaggio che nessuna attesa
riconosce: l'esito di un incasso arrivato dopo un timeout o una caduta, l'`AN` di una chiusura
forzata dal pannello, l'esito di un incasso il cui chiamante è stato cancellato. **Questi messaggi
possono portare importi.** Se Giano non li salva, il denaro entrato non risulta da nessuna parte.

Dopo un riavvio di Giano con un incasso aperto, la macchina resta in `IN` (nessun timeout lato
macchina, confermato da PayPrint). `LO` durante l'incasso non si può usare e non restituisce lo
stato corrente, solo l'ultimo JSON trasmesso.

### Da decidere

1. **Dove salvare gli orfani.**

   | Opzione | Pro | Contro |
   |---|---|---|
   | Tabella dedicata nel database di Giano | interrogabile, collegabile alla vendita | se il database non risponde, l'orfano si perde |
   | File locale (il `PagAmicoFileLogger` lo registra già) + import | nessuna dipendenza dal database | va riletto e riconciliato |
   | Entrambi: file sempre, tabella quando possibile | nessuna perdita | due fonti da riconciliare |

2. **Che cosa salvare.** Proposta minima: ora, `response`, `collectedAmount`, `committedAmount`,
   `changeReturn`, `amountUnpaid`, `halted`, frame grezzo, id della vendita aperta in quel momento
   (se c'era).

3. **Chi riconcilia.** Un orfano con importo diverso da zero va mostrato a qualcuno. Opzioni: avviso
   in cassa alla comparsa, elenco a fine giornata nella chiusura, oppure solo segnalazione
   all'amministrazione.

4. **Ripartenza con incasso aperto.** Giano deve ricordare su disco "incasso in corso per la vendita X"
   **prima** di mandare `IN`, e cancellarlo all'esito. Al riavvio, se il segno c'è:
   - riconnettersi dallo stesso IP (accettato, da PayPrint);
   - **non** mandare `ST` né altri comandi finché non si sa se l'incasso è ancora aperto;
   - chiudere con `AN` o `CM` secondo la Decisione 1, oppure chiedere all'operatore la chiusura forzata
     dal pannello.

   Quale strada seguire dipende dalla prova 9 della checklist (i frame arrivano sul nuovo socket?).
   Finché non è provata, la via sicura è la chiusura dal pannello con l'operatore presente.

---

## Che cosa non serve decidere

- **Timeout nella finestra di pagamento**: Giano non ne ha, e il fornitore consiglia di non metterne.
- **Display della cassa durante l'incasso**: escluso dal protocollo, e Giano oggi non lo usa.
- **Innesto tecnico**: DLL `net47` sotto `ExternalReferences/`, dipendenze già presenti in Giano alla
  stessa versione. Il pacchetto si produce con `dotnet pack` (README della libreria).
