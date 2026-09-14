using System;
using Xunit;

namespace PayPrint.PagAmico.Tests;

/// <summary>
/// Espone i test offline a <c>dotnet test</c> e a Esplora test di Visual Studio. Le verifiche restano quelle di
/// Program (anche <c>dotnet run</c> funziona): qui si eseguono tutte e si fallisce elencando quelle non passate.
/// </summary>
public class SuiteTests
{
    [Fact]
    public void TestOffline()
    {
        var (passed, failures) = Program.RunAll();
        Assert.True(failures.Count == 0,
            $"{failures.Count} test falliti su {passed + failures.Count}:{Environment.NewLine}" +
            string.Join(Environment.NewLine, failures));
    }
}
