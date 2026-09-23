using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.ExterniOdkaz;

/// <summary>
/// Po prvním vyplnění je jediným zdrojem pravdy pracovník, ne tiket (spec §5.6, R8).
/// Harvest ze ServiceDesku píše čtyři datumy a fingerprint — pole `pozadavek` mezi nimi
/// být nesmí. Tudy by se přepis vloudil nejsnáz, proto pin přímo na zdroj.
/// </summary>
public sealed class HarvestNeprepisujePozadavekTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    [Fact]
    public void HarvestSluzby_NesahajiNaPozadavek()
    {
        var sluzbyRoot = Path.Combine(RepoRoot(), "PmTracker.Web", "Services");
        var sluzby = Directory.GetFiles(sluzbyRoot, "*Harvest*.cs", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(sluzbyRoot, "PerTicketMetadata*.cs", SearchOption.AllDirectories))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        sluzby.Should().NotBeEmpty("bez nalezených služeb by test nic nehlídal");

        foreach (var soubor in sluzby)
        {
            File.ReadAllText(soubor).Should().NotContain(".Pozadavek",
                $"{Path.GetFileName(soubor)} nesmí přepsat text, který napsal pracovník");
        }
    }
}
