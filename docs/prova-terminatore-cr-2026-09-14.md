# Prova del terminatore CR sul simulatore — 14 settembre 2026

Simulatore del pagAmico Dev Kit su `127.0.0.1:9100`, giacenze riempite con `Fill` prima di ogni giro.

## Collaudo completo (gruppi di default)

| Libreria | Terminatore | Pausa fra invii | Esito |
|---|---|---|---|
| C# | CR | 80 ms | 64/64 |
| C# | CR | 0 ms | 64/64 |
| C# | nessuno | 0 ms | 62/64* |
| Kotlin | CR | 0 ms | 64/64 |
| C# | CR (nuovo default) | 80 ms (default) | 64/64 |
| Kotlin | CR (nuovo default) | 80 ms (default) | 64/64 |

\* I due errori (`PM`, `MF`: `QTAMONETE05`) erano monete da 0,05 esaurite dopo i giri precedenti, non comandi persi.
Ripetuto con CR e 0 ms dopo aver riempito le giacenze: 64/64.

## Raffica a basso livello

Più comandi scritti con una sola `Write` sullo stesso socket, quindi nello stesso segmento TCP:

| Comandi | Nessun terminatore | CR | CR+LF |
|---|---|---|---|
| `ST` `ST` `ST` | 3 risposte | 3 risposte | 3 risposte |
| `DS` `PTSTAT` | `PTSTAT` risponde | `PTSTAT` risponde | `PTSTAT` risponde |
| `CL` `DS` `ST` | `ST` risponde | `ST` risponde | `ST` risponde |

Il difetto osservato in precedenza (secondo comando perso se trasmesso nello stesso segmento senza
terminatore) **non si riproduce con il simulatore attuale**.

## Decisioni

- `CommandTerminator` / `commandTerminator`: default portato da vuoto a **CR**, come indicato da PayPrint per la macchina.
  I due banchi di prova partono con `\r` nella casella del terminatore.
- `MinimumCommandInterval` / `minimumCommandIntervalMs`: **resta 80 ms** finché una raffica con CR non è verificata
  sulla macchina reale. Poi si può portare a 0.
- `LiveTest` (C# e Kotlin) accetta `--terminatore nessuno|cr|crlf` e `--pausa <ms>` per ripetere la prova.

## Da verificare sulla macchina reale

```bash
dotnet run --project PayPrint.PagAmico.LiveTest -- 192.168.1.231 9100 base --terminatore cr --pausa 0
```

Ancora aperti: se il CR serve anche dopo i pacchetti immagine `SF`/`SI` e come si comporta con `PTPRDT`,
che può contenere CR e LF.
