# pagAmico — libreria C#

Libreria client per la cassa rendiresto **PayPrint pagAmico** (protocollo TCP-IP rev. 2.33, FW 8.72).

| Progetto | Contenuto |
|---|---|
| `PayPrint.PagAmico` | libreria (`net8.0`, `netstandard2.0`, `net47`; sui target legacy la sola dipendenza `System.Text.Json`) |
| `PayPrint.PagAmico.Tests` | 169 test offline: stringhe dei manuali, sequenze di incasso contro un finto pagAmico, registro su file |

Le app di prova (WinForms, LiveTest, Tap, Fill, Demo) stanno nel repository **pagAmico_CSharp_Demo**.

Questo repository contiene anche la documentazione di tutto l'SDK (C# e Kotlin):

- [`docs/avvio-progetti.md`](docs/avvio-progetti.md): **come avviare i 4 progetti**
- `docs/quadro-01..05-*`: documenti di orientamento, da leggere in ordine a partire da `quadro-01-quadro-insieme.pdf`
- `docs/Integrazione-pagAmico.pdf`, `docs/Guida-prove-pagAmico.pdf`: manuali tecnici (generati dagli script `genera_*.py`, non modificare i PDF a mano)
- `strumenti/analizza_log.py`: rilegge i log di una sessione di prove

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
