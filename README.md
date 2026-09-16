# pagAmico — libreria C#

Libreria client per la cassa rendiresto **PayPrint pagAmico** (protocollo TCP-IP rev. 2.33, FW 8.72).

| Progetto | Contenuto |
|---|---|
| `PayPrint.PagAmico` | libreria (`net8.0`, `netstandard2.0`, `net47`; sui target legacy la sola dipendenza `System.Text.Json`) |
| `PayPrint.PagAmico.Tests` | 171 test offline: stringhe dei manuali, sequenze di incasso contro un finto pagAmico, registro su file |

Le app di prova (WinForms, LiveTest, Tap, Fill, Demo) stanno nel repository **pagAmico_CSharp_Demo**.

Questo repository contiene anche la documentazione di tutto l'SDK (C# e Kotlin). In `docs/` **i PDF
sono generati dai `.md`** con `genera_pdf_da_md.py`: si modifica il sorgente e si rilancia, mai il PDF
a mano.

| Se devi… | Leggi |
|---|---|
| avviare i 4 progetti su un PC nuovo | [`avvio-progetti.md`](docs/avvio-progetti.md) |
| capire dove siamo, in generale | `docs/quadro-01..05-*`, in ordine, da [`quadro-01-quadro-insieme.pdf`](docs/quadro-01-quadro-insieme.pdf) |
| **preparare le prove sulla macchina di PayPrint** | [`guida-prove-macchina-payprint.pdf`](docs/guida-prove-macchina-payprint.pdf): comincia da lì, dice che cosa leggere prima, in che ordine, e a che cosa serve ogni prova |
| condurre le prove, foglio alla mano | [`checklist-macchina-reale.pdf`](docs/checklist-macchina-reale.pdf), le 11 prove |
| sapere com'è fatto un log giusto | [`prove-banchi-ramo1-2026-09-16.pdf`](docs/prove-banchi-ramo1-2026-09-16.pdf): le stesse prove eseguite sul simulatore, riga per riga |
| sapere che cosa ha detto il fornitore e che cosa chiedergli | [`esito-risposta-payprint.pdf`](docs/esito-risposta-payprint.pdf), con le 11 domande nel capitolo 5 |
| il dettaglio del protocollo, o dei programmi di prova | [`Integrazione-pagAmico.pdf`](docs/Integrazione-pagAmico.pdf), [`Guida-prove-pagAmico.pdf`](docs/Guida-prove-pagAmico.pdf) |
| rileggere i log di una sessione | `strumenti/analizza_log.py` |

## Compilare e testare

Aprire `PayPrint.PagAmico.slnx` in Visual Studio, oppure:

```bash
dotnet build
dotnet test                                   # oppure Esplora test di Visual Studio
dotnet run --project PayPrint.PagAmico.Tests  # stessi test, con l'elenco completo a video
```

## Pacchetto NuGet

```bash
dotnet pack PayPrint.PagAmico -c Release -o artifacts
```

Produce `artifacts/PayPrint.PagAmico.1.0.0.nupkg` (net8.0, netstandard2.0, net47) e i simboli `.snupkg`. Per usarlo da un altro progetto si aggiunge la cartella come sorgente NuGet locale; per un csproj .NET Framework non-SDK (come Giano) basta la DLL in `lib/net47`. La versione si cambia in `<Version>` del csproj.

## Uso

```csharp
using var client = new PagAmicoClient("192.168.1.29", 9100);
await client.ConnectAsync();

var stato = await client.GetStatusAsync();                      // [ST]
Console.WriteLine(stato.BanknotesAvailableByDenomination[20]);  // banconote da 20 EUR disponibili

var esito = await client.CollectCashAsync(                      // [IN]
    10.50m,
    new Progress<PagAmicoResponse>(p => Console.WriteLine($"incassato {p.CollectedAmount}")));

if (esito.AmountUnpaid > 0)
    Console.WriteLine($"resto non erogato: {esito.AmountUnpaid} EUR");
```

Log su file (un file al giorno in `%LOCALAPPDATA%\PayPrint.PagAmico\logs`):

```csharp
using var logger = new PagAmicoFileLogger();
logger.Attach(client);
```

## Note sul protocollo

- Importi in richiesta in **centesimi** a lunghezza fissa (`IN001050` = 10,50 EUR), in risposta in **euro**; si usa sempre `decimal`. Tetto dell'incasso: 9.999,99 EUR.
- Il protocollo non ha un terminatore unico (JSON, `|\` per `MV`/`MI`, testo puro): il framer riassembla e separa i messaggi.
- I comandi sono serializzati; a incasso aperto sono ammessi solo `AN` e `CM`, gli altri lanciano `PagAmicoCollectionOpenException`.
- Fra due invii c'è una pausa minima di 80 ms (`MinimumCommandInterval`), necessaria sul simulatore senza terminatore.
- I messaggi che nessuna attesa riconosce arrivano all'evento `OrphanFrame`: possono contenere importi.
