# Come avviare i 4 progetti pagAmico

## 0. Preparazione (una volta sola)

### Cartelle

I 4 repository vanno clonati **affiancati nella stessa cartella**: le demo trovano la libreria con un percorso relativo.

| Cartella | GitHub (copia di riferimento) | Disco esterno |
|---|---|---|
| `pagAmico_CSharp_Lib` | `github.com/AdamoPrimeSoftware/pagAmicoCSharpLibrary` | `git-payprint-repos\pagAmico_CSharp_Lib.git` |
| `pagAmico_CSharp_Demo` | `github.com/AdamoPrimeSoftware/pagAmicoCSharpDemo` | `git-payprint-repos\pagAmico_CSharp_Demo.git` |
| `pagAmico_Kotlin_Lib` | `github.com/AdamoPrimeSoftware/pagAmicoKotlinLibrary` | `git-payprint-repos\pagAmico_Kotlin_Lib.git` |
| `pagAmico_Kotlin_Demo` | `github.com/AdamoPrimeSoftware/pagAmicoKotlinDemo` | `git-payprint-repos\pagAmico_Kotlin_Demo.git` |

Ogni repository ha due remote, con gli stessi nomi su tutti i PC:

| Remote | Dove punta |
|---|---|
| `github` | GitHub, la copia di riferimento |
| `HardDiskEsterno` | il disco esterno, branch `main`. La lettera cambia secondo il PC: **G:** a casa, **E:** in ufficio |

Il branch è `main` ovunque: sui due PC, su GitHub e sul disco. Il `main` locale segue `github/main`,
quindi `git pull` e `git push` senza argomenti lavorano con GitHub.

| PC | Cartella dei repository | Disco esterno |
|---|---|---|
| casa | `D:\Programmazione\Prime\PerClaude\payPrint` | `G:\git-payprint-repos` |
| ufficio | `C:\Users\adamo\Desktop\Prime\payPrint` | `E:\git-payprint-repos` |

```bash
cd D:\Programmazione\Prime\PerClaude\payPrint      # a casa; in ufficio C:\Users\adamo\Desktop\Prime\payPrint
git clone -o github https://AdamoPrimeSoftware@github.com/AdamoPrimeSoftware/pagAmicoCSharpLibrary.git  pagAmico_CSharp_Lib
git clone -o github https://AdamoPrimeSoftware@github.com/AdamoPrimeSoftware/pagAmicoCSharpDemo.git     pagAmico_CSharp_Demo
git clone -o github https://AdamoPrimeSoftware@github.com/AdamoPrimeSoftware/pagAmicoKotlinLibrary.git  pagAmico_Kotlin_Lib
git clone -o github https://AdamoPrimeSoftware@github.com/AdamoPrimeSoftware/pagAmicoKotlinDemo.git     pagAmico_Kotlin_Demo

# disco esterno come secondo remote, per ogni repository (G: a casa, E: in ufficio)
git -C pagAmico_CSharp_Lib remote add HardDiskEsterno G:\git-payprint-repos\pagAmico_CSharp_Lib.git
```

`-o github` dà al remote il nome `github` invece di `origin`. I nomi delle cartelle devono restare
questi.

**`AdamoPrimeSoftware@` nell'URL** serve se sul PC Git Credential Manager ha già le credenziali di
un altro account GitHub: senza, usa quelle e il push fallisce con *Permission denied* (403). Al primo
accesso si apre il browser: entrare con **AdamoPrimeSoftware**. A un remote già aggiunto senza
account: `git remote set-url github https://AdamoPrimeSoftware@github.com/AdamoPrimeSoftware/<repository>.git`.

**Disco esterno nuovo o vuoto:** prima di aggiungere il remote si crea il repository sul disco come
copia bare di GitHub, per ciascuno dei quattro:

```bash
git clone --bare https://AdamoPrimeSoftware@github.com/AdamoPrimeSoftware/pagAmicoCSharpLibrary.git G:\git-payprint-repos\pagAmico_CSharp_Lib.git
```

Si lavora sugli stessi repository da due PC: **prima di iniziare** `git pull` in tutti e quattro,
**dopo ogni commit** push su GitHub e sul disco esterno. I comandi sono uguali sui due PC:

```bash
git pull                             # da GitHub
git push                             # su GitHub
git push HardDiskEsterno main        # sul disco esterno, se collegato
```

Per controllare che un repository sia allineato ai due remote:

```bash
git fetch github
git status -sb                       # "## main...github/main" senza [ahead]/[behind]
git ls-remote HardDiskEsterno refs/heads/main
```

Se `git branch -vv` non mostra `[github/main]` accanto a `main` (per esempio dopo un clone senza
`-o github`), si ricollega il branch con `git branch -u github/main main`.

> **Rinominare un remote cambiando solo le maiuscole** (per esempio `gitHub` → `github`) su Windows
> lascia la configurazione a metà: `git branch -vv` non mostra più `[github/main]`. Si controlla con
> `git config --get-regexp "^branch\.main|^remote\."` e si correggono `remote.github.fetch` e
> `branch.main.remote`.

### Strumenti

| Serve per | Cosa installare |
|---|---|
| C# | .NET SDK 8 (o successivo) + Visual Studio 2022 17.13 o successivo (per aprire i file `.slnx`) |
| Kotlin | JDK 17 (verificato con 17.0.14 e 17.0.20; il 17.0.8 non regola il keepalive TCP) + IntelliJ IDEA. Gradle non va installato: si usa `gradlew` incluso |
| Prove senza macchina | **pagAmico Dev Kit** di PayPrint (`pagAmico-DevKit-Setup-1.0.0.exe`) |
| Script in `docs/` e `strumenti/` | Python 3 |

Il JDK di Gradle si fissa una volta per PC in `%USERPROFILE%\.gradle\gradle.properties`, non nei
repository (il percorso cambia da un PC all'altro; in ufficio è quello qui sotto):

```
org.gradle.java.home=C:/Users/adamo/.jdks/corretto-17.0.20.1
```

In IntelliJ, *Settings → Build Tools → Gradle → Gradle JVM* sullo stesso JDK.

> **Il `java` del PATH non c'entra con quello di Gradle.** In ufficio il PATH ha la **17.0.8**, che
> non regola il keepalive TCP: `gradlew` e IntelliJ usano comunque il JDK di `gradle.properties`, ma
> un banco lanciato a mano con quel `java` resterebbe col keepalive di sistema, prima sonda dopo due
> ore. Si controlla con `java -version` e, nel banco, sulla riga di diagnostica che compare alla
> connessione. Conta per la prova 10 di `checklist-macchina-reale.md`.

### Simulatore

Per provare senza la macchina vera: aprire il **pagAmico Dev Kit** → sezione **Simulatore** → **Avvia**. Il simulatore ascolta su `127.0.0.1:9100`.

---

## 1. pagAmico_CSharp_Lib — libreria C# e test

**Visual Studio:** aprire `PayPrint.PagAmico.slnx`, impostare `PayPrint.PagAmico.Tests` come progetto di avvio, **Ctrl+F5**.
Risultato atteso: `171 test superati, 0 falliti`. In alternativa **Test → Esplora test** e *Esegui tutti*.

**Riga di comando:**

```bash
cd pagAmico_CSharp_Lib
dotnet build
dotnet test                                   # esito sintetico
dotnet run --project PayPrint.PagAmico.Tests  # elenco completo dei test
```

La libreria in sé non si avvia: si usa dalle app del punto 2 o da un altro progetto.

---

## 2. pagAmico_CSharp_Demo — app di prova C#

**Visual Studio:** aprire `PayPrint.PagAmico.Demos.slnx`. Si sceglie il progetto di avvio (tasto destro → *Imposta come progetto di avvio*) e poi il **profilo** dal menu a tendina accanto al pulsante ▶. I profili sono numerati nell'ordine in cui si usano.

| Passo | Progetto | Profilo | Tasto |
|---|---|---|---|
| a. riempire il simulatore di monete e banconote | `PayPrint.PagAmico.Fill` | **2 - Riempi simulatore e allinea fondo cassa** | Ctrl+F5 |
| b. collaudo automatico completo | `PayPrint.PagAmico.LiveTest` | **1 - Collaudo simulatore (completo)** | Ctrl+F5 |
| c. prove a mano con interfaccia | `PayPrint.PagAmico.WinForms` | — | F5 |
| d. vedere il traffico grezzo | `PayPrint.PagAmico.Tap` | **1 - Proxy verso il simulatore (9200 -> 9100)** | F5, poi nel banco usare la porta 9200 |
| esempio minimo | `PayPrint.PagAmico.Demo` | **1 - Demo simulatore** | Ctrl+F5 |

Per la macchina reale: LiveTest profilo **6 - Collaudo macchina reale**, Tap profilo **3**, Demo profilo **2**. LiveTest ha anche profili parziali (2-5: solo base, incassi ed erogazioni, display e stampa, sonda DI).

Per far girare Tap e WinForms insieme: tasto destro sulla soluzione → *Proprietà* → *Più progetti di avvio* → **Avvia** su entrambi.

> **Fill** esegue incassi veri: **mai** su una macchina reale.
> Nel **LiveTest** il gruppo `riavvii` (escluso di default) riavvia POS e macchina.

**Riga di comando:**

```bash
cd pagAmico_CSharp_Demo
dotnet run --project PayPrint.PagAmico.Fill -- 127.0.0.1 9100 --monete 40 --banconote 600 --aggiorna-fondo
dotnet run --project PayPrint.PagAmico.LiveTest -- 127.0.0.1 9100
dotnet run --project PayPrint.PagAmico.LiveTest -- 127.0.0.1 9100 collect    # un solo gruppo
dotnet run --project PayPrint.PagAmico.WinForms
dotnet run --project PayPrint.PagAmico.Tap -- --listen 9200 --target 127.0.0.1:9100
```

Per la macchina reale sostituire `127.0.0.1` con il suo indirizzo (es. `192.168.1.231`).

---

## 3. pagAmico_Kotlin_Lib — libreria Kotlin e test

**IntelliJ IDEA:** *File → Open* sulla cartella `pagAmico_Kotlin_Lib`, attendere l'import di Gradle, poi scegliere la configurazione dal menu in alto a destra e premere ▶:

| Configurazione | Cosa fa |
|---|---|
| **1 - Test offline** | 172 test di autoverifica, non serve nulla acceso |
| **2 - Collaudo simulatore** | collaudo su `127.0.0.1:9100` (simulatore acceso) |
| **4 - Collaudo macchina reale** | collaudo su `192.168.1.231:9100` |

Se IntelliJ chiede il JDK: *File → Project Structure → SDK* → 17.

**Riga di comando:**

```bash
cd pagAmico_Kotlin_Lib
gradlew.bat test                   # esito sintetico, fallisce se un test non passa
gradlew.bat :pagamico-lib:run      # elenco completo dei test
gradlew.bat :pagamico-lib:run "-PmainClass=it.payprint.pagamico.test.LiveTestKt" "--args=127.0.0.1 9100"
```

---

## 4. pagAmico_Kotlin_Demo — banco di prova Compose

**IntelliJ IDEA:** *File → Open* sulla cartella `pagAmico_Kotlin_Demo` (la libreria viene inclusa da sola dalla cartella accanto), configurazione **3 - Banco di prova (Compose)**, ▶.
Nel banco, **Simulatore locale** si collega a `127.0.0.1:9100`.

**Riga di comando:**

```bash
cd pagAmico_Kotlin_Demo
gradlew.bat :pagamico-desktop:run
gradlew.bat :pagamico-desktop:packageExe      # installer Windows
```

---

## Sequenza consigliata per una sessione di prove

1. test offline: punto 1 e/o punto 3 (configurazione 1);
2. avviare il simulatore nel Dev Kit;
3. riempire le giacenze: punto 2a;
4. collaudo automatico: punto 2b e/o punto 3 (configurazione 2);
5. prove a mano: punto 2c o punto 4;
6. a fine giornata, rileggere i log:

```bash
cd pagAmico_CSharp_Lib
python strumenti\analizza_log.py                    # log di oggi
python strumenti\analizza_log.py --data 2026-09-03  # un giorno preciso
```

I log stanno in `%LOCALAPPDATA%\PayPrint.PagAmico\logs`.

## Problemi comuni

| Sintomo | Causa |
|---|---|
| progetto demo non trova la libreria | repository non affiancati, o cartelle rinominate |
| il collaudo va in timeout al primo comando | simulatore non avviato, o porta diversa da 9100 |
| erogazioni che falliscono (`amountUnpaid` valorizzato) | simulatore senza monete: rilanciare Fill (2a) |
| `PagAmicoCollectionOpenException` | un incasso è ancora aperto: annullarlo (`AN`) o chiuderlo (`CM`) |
