using PayPrint.PagAmico;

namespace PayPrint.PagAmico.Tests;

/// <summary>
/// Processo figlio del test sul registro fra processi: scrive <c>count</c> righe "TX &lt;etichetta&gt;-n" nel
/// file del giorno. Gemello di LoggerWriterProcess.kt.
/// Uso: PayPrint.PagAmico.Tests.dll --scrittore-registro &lt;cartella&gt; &lt;prefisso&gt; &lt;etichetta&gt; &lt;count&gt;
/// </summary>
internal static class LoggerWriterProcess
{
    public const string Argument = "--scrittore-registro";

    public static int Run(string[] args)
    {
        var (dir, prefix, label, count) = (args[1], args[2], args[3], int.Parse(args[4]));
        using var log = new PagAmicoFileLogger(dir, prefix);
        for (var i = 0; i < count; i++) log.Write("TX", $"{label}-{i}");
        return 0;
    }
}
