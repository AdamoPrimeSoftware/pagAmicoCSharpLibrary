# Documentazione dell'SDK pagAmico

Due alberi paralleli con le stesse sottocartelle: i **sorgenti** in `md/`, i **PDF generati** in
`pdf/`. Dentro, i documenti sono divisi per **tipo**, cioè per chi li legge e quando.

| Cartella | Che cos'è | Chi la legge |
|---|---|---|
| `guide/` | come si fa una cosa, e come stanno le cose | **tu**, per capire o per preparare una sessione |
| `checklist/` | liste da tenere aperte mentre si lavora | **tu**, durante la sessione |
| `verbali/` | che cosa è successo in una sessione di prove, con le righe di log | chi vuole sapere com'è andata, e chi deve rifare la prova |
| `fornitore/` | materiale di PayPrint com'è arrivato | riferimento, non si modifica |
| `memoria-claude/` | il contesto per riprendere il lavoro: prompt di ripresa, analisi, decisioni | **Claude**, all'inizio di una sessione nuova |

## Che cosa c'è

### `guide/` — per te

| Documento | Che cosa ti dà | PDF |
|---|---|---|
| `avvio-progetti.md` | come avviare i 4 progetti su un PC, i due remote, i JDK | no |
| `guida-prove-macchina-payprint.md` | **la sessione di prove da PayPrint**: che cosa leggere prima, che cosa chiedere, che cosa non lanciare mai, come si legge il log | sì |
| `quadro-01-quadro-insieme.md` | che cos'è tutto questo lavoro, com'è nato, dove siamo | sì |
| `quadro-02-le-due-librerie.md` | che cosa fanno le due librerie e quali problemi risolvono | sì |
| `quadro-03-i-programmi-di-prova.md` | a cosa serve ciascun programma, e quando si apre quello e non un altro | sì |
| `quadro-04-giano-dove-siamo.md` | Giano: che cosa c'è già, che cosa cambia, quanto lavoro è | sì |
| `quadro-05-cosa-non-sappiamo.md` | le domande aperte e le decisioni che spettano a te | sì |

I due manuali tecnici stanno solo in PDF, perché non hanno un `.md`: `pdf/guide/Integrazione-pagAmico.pdf`
(protocollo e libreria) e `pdf/guide/Guida-prove-pagAmico.pdf` (come si conducono le prove). Li
generano gli script `generatori/genera_documentazione.py` e `generatori/genera_guida_prove.py`.

### `checklist/` — da tenere aperta

| Documento | Che cosa ti dà | PDF |
|---|---|---|
| `checklist-macchina-reale.md` | le 11 prove su una macchina vera: cosa fare, cosa guardare, cosa decide ciascuna | sì |

### `verbali/` — com'è andata

| Documento | Che cosa ti dà | PDF |
|---|---|---|
| `prova-terminatore-cr-2026-09-14.md` | la prova del terminatore CR e della raffica sul simulatore | no |
| `prove-banchi-ramo1-2026-09-16.md` | le cinque prove a mano sui due banchi, riga di log per riga di log | sì |

### `fornitore/` — materiale di PayPrint

| Documento | Che cos'è |
|---|---|
| `mail-payprint-domande-protocollo.md` | la mail con le domande sul protocollo, come è stata mandata |
| `risposta-payprint-2026-09-11.md` | la risposta del fornitore, così com'è arrivata |

### `memoria-claude/` — per riprendere il lavoro

| Documento | Che cosa ti dà | PDF |
|---|---|---|
| `prompt-ripresa-lavoro.md` | **il prompt da incollare** all'inizio di una sessione nuova: dove sono le cose, i fatti da non rimettere in discussione, i vincoli, e i rami di lavoro aperti | no |
| `esito-risposta-payprint.md` | la risposta del fornitore verificata sul codice: i tre difetti, che cosa cambia nelle librerie, **le 11 domande per la telefonata** | sì |
| `decisioni-innesto-giano.md` | le due decisioni per l'adattatore di Giano: quelle prese e quelle rimandate | no |
| `analisi-giano-vne-vs-pagamico.md` | il confronto riga per riga fra la cassa VNE di oggi e il pagAmico | no |

## Regole

- **I PDF sono generati, non si modificano.** Si cambia il `.md` in `md/...` e si rilancia, dalla
  cartella `docs`:

  ```
  python generatori\genera_pdf_da_md.py                       rigenera i PDF che esistono gia'
  python generatori\genera_pdf_da_md.py nome-del-file.md      solo quello (basta il nome)
  ```

  Il PDF va da sé in `pdf/` nella stessa sottocartella del sorgente.
- **Un `.md` senza PDF è voluto:** il PDF si fa solo per i documenti che si leggono di seguito o si
  stampano. Per dargliene uno basta nominarlo una volta nel comando qui sopra.
- **I riferimenti fra documenti si scrivono come percorso dentro `docs/`**: per esempio
  `md/guide/quadro-01-quadro-insieme.md`, `pdf/checklist/checklist-macchina-reale.pdf`. Così un
  riferimento resta valido da qualunque documento.
