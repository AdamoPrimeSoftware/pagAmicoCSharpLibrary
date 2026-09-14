# Risposta di Raffaele Viglione (PayPrint) — 11 settembre 2026

Risposta alla mail `mail-payprint-domande-protocollo.md` (domande 1.1-1.2 sul protocollo, 2.1-2.8
sull'innesto). Testo come ricevuto, senza correzioni: è la fonte primaria, le interpretazioni stanno
altrove.

---

> Ti rispondo in ordine:
>
> * Non è necessario nessun delay tra i comandi, basta che terminino con CR oCR+LF, il simulatore
>   probabilmente ha qualche difficoltà.
>
> Il comando IN accetta solo AN per chiudere e annullare la transazione restituendo l'eventuale
> incassato e CM per chiudere la transazione accettando l'incassato e popolando il campo
> `collectedAmount`
> Il campo `amountUnpaid come documentato nel manuale contiene l'eventuale resto non erogato per
> mancanza di monete.`
>
> Non devi inviare ST durante un incasso, verrebbe ignorato.
> La transazione può essere annullata lato pagamico con una procedura di chiusura forzata (tenendo
> premuto sulla parola RESTO del display e mettendo la password) in quel caso ti torna un AN.
>
> * La risposta CMD ERROR viene inviata quando la macchina riceve un comando sconosciuto.
>
> La risposta BUSY quando il comando ricevuto non può essere eseguito perché impegnata in un incasso
> o altro.
>
> * Importo massimo 9.999.99€ per contanti, maggiore per il pos.
> * Dopo IN non c'è nessun timeout, la connessione resta attiva, se cade la rete accetta la
>   riconnessione dallo stesso IP. Non mettere un timeout nella tua procedura, se ti occorre un
>   timeout usa il comandi I2 che ha un tempo di disconnessione ma te lo sconsiglio.
> * I parziali "p" vendono inviati ogni volta che viene aggiunto del contante e sono cumulativi.
>
> * Il pagamento non dovrebbe essere abilitato di default mentre la password dovrebbe essere già
>   inserita ma non la ricordo.
> * Il comando ST va inviato solo dopo che la transazione è conclusa o annullata, mai durante.
>
> Comunque ti consiglio di chiamarmi se vuoi maggiori informazioni, inoltre appena sei pronto per
> fare qualche test ti posso far collegare alla mia macchina con il mio IP in modo da poter
> verificare che l'integrazione proceda correttamente.
