# Prove a mano dei due banchi — ramo 1

*16 settembre 2026. Scopo: verificare sul simulatore che i due banchi (WinForms e Compose) si
comportino come le librerie corrette l'11 settembre. Sono le prove del ramo 1 di
`prompt-prossima-sessione-giano.md`. Nessuna richiede la macchina vera.*

**Il punto 6 del ramo 1 è già fatto:** `cancelCurrent()`, che nel banco Compose non chiamava più
nessuno, è stata tolta. Il pulsante `[AN]` usa `cancelOperation()`, come il `[AN]` del WinForms usa
`CancelAsync()`: a incasso aperto passano per la via laterale e restituiscono **l'esito
dell'incasso**.

---

## Preparazione

1. **Simulatore acceso**: Dev Kit → *Simulatore* → **Avvia** (ascolta su `127.0.0.1:9100`).
2. **Giacenze piene**, altrimenti i resti falliscono per mancanza di monete:

   ```bash
   cd pagAmico_CSharp_Demo
   dotnet run --project PayPrint.PagAmico.Fill -- 127.0.0.1 9100 --monete 40 --banconote 600 --aggiorna-fondo
   ```

3. **Il banco da provare** (le prove si ripetono su tutti e due):

   ```bash
   dotnet run --project PayPrint.PagAmico.WinForms          # oppure F5 da Visual Studio
   cd ../pagAmico_Kotlin_Demo && gradlew.bat :pagamico-desktop:run
   ```

4. Nella scheda **Connessione**: host `127.0.0.1`, porta `9100`, terminatore `\r`, e **attivare
   diagnostica libreria e registrazione su file**. Poi *Connetti*.

> **Il criterio che vale in tutte le prove: le righe `TX`.** Il banco scrive una riga `TX` per ogni
> comando che parte davvero. Quando una prova dice «nulla trasmesso», vuol dire che in quel momento
> nel log **non** deve comparire una nuova riga `TX`.

---

## 1. Incasso, poi `[AN]` a incasso aperto

**Fare:** scheda *Incasso*, importo `5.00`, `[IN] Contanti`. Inserire 2,00 € dal pannello del
simulatore, aspettare la riga del parziale, poi premere `[AN] Annulla`.

**Deve comparire:**

- `TX  AN` una volta sola;
- una riga di esito con `response=AN` e gli importi: `incassato=2,00` e `restituito=2,00`
  (il nome dei campi dipende da come risponde il simulatore);
- **niente** «operazione annullata dal client»: l'annullo non passa più dalla cancellazione del
  chiamante.

**Decide:** che il pulsante `[AN]` restituisca l'esito dell'incasso e non un esito vuoto.

**Esito:**

## 2. Incasso, poi `[CM]`

**Fare:** `[IN] Contanti` da `5.00`, inserire 2,00 €, poi `[CM] Commit`.

**Deve comparire:** una riga con `response=CM` e
`trattenuto=2,00 (controllo committedAmount=2,00)`.

- Se i due importi sono **diversi**, copiare la riga: è la domanda 1 della telefonata.
- Se il trattenuto è **0,00**, il difetto D1 è tornato: fermarsi e segnalarlo.
- Un eventuale **terzo frame `CM`** in ritardo comparirà come *frame orfano*: annotarlo, è la stessa
  domanda 1.

**Esito:**

## 3. Seconda chiusura sullo stesso incasso

**Fare:** `[IN] Contanti`, inserire qualcosa, premere `[CM] Commit` e **subito dopo** `[AN] Annulla`
(due pulsanti diversi, quindi il secondo click è possibile).

**Deve comparire** una riga di errore, una delle due, **e nessuna nuova riga `TX`**:

| Se il commit è ancora in corso | `Chiusura dell'incasso gia' richiesta con CM: 'AN' non inviato` |
|---|---|
| Se il commit si è già chiuso | `Nessun incasso aperto: 'AN' non inviato` |

Sul simulatore il `CM` risponde in pochi millisecondi, quindi il secondo messaggio è il più
probabile: va bene lo stesso, **quello che conta è che non parta un secondo comando**.

**Esito:**

## 4. Un altro comando a incasso aperto

**Fare:** con un incasso aperto (non chiuderlo), andare nella scheda *Contanti* e premere
`[ST] Richiesta situazione`; poi provare anche un comando del display.

**Deve comparire:**
`Incasso aperto: 'ST' non inviato, durante l'incasso la macchina accetta solo AN e CM`,
**senza** riga `TX`. Con la diagnostica accesa compare anche
`'ST' NON inviato: incasso aperto, la macchina accetta solo AN e CM`.

Poi chiudere l'incasso con `[AN]`.

**Decide:** che il blocco degli invii laterali (punto 4 delle correzioni dell'11 settembre) regga
anche premendo i pulsanti a mano.

**Esito:**

## 5. Frame orfani nel log

**Fare:** scheda *Ricarica*, premere `[RC/RS/VC/VS] Mista`, inserire qualche moneta nel simulatore,
poi `[FR] Fine ricarica`.

**Deve comparire:** una o più righe
`frame orfano (nessuna attesa lo riconosce): ...` con dentro i parziali della ricarica.

**Decide:** che i messaggi che nessuno attende arrivino davvero all'evento dei frame orfani — è il
canale su cui in Giano si salveranno gli importi (Decisione 2 di `decisioni-innesto-giano.md`).

**Esito:**

---

## Dopo le prove

- I log stanno in `%LOCALAPPDATA%\PayPrint.PagAmico\logs` (`winforms-*` e `compose-*`); si rileggono
  con `python strumenti\analizza_log.py`.
- Riportare gli esiti qui sopra, e segnalare: righe con importi diversi da quelli attesi, frame
  orfani inattesi, qualunque differenza fra il banco WinForms e quello Compose.
- Se una prova fallisce, la correzione va fatta **nelle due librerie insieme**, con un test offline
  che riproduce i frame visti.

**Non** toccare in questa sessione: il default del terminatore, la pausa di 80 ms, il timeout di 5
minuti (D2) e la chiusura dell'incasso su `ER` dopo l'`OK`. Vanno dopo le prove sulla macchina vera.
