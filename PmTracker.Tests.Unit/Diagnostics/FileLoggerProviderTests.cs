using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using PmTracker.Web.Logging;
using Xunit;

namespace PmTracker.Tests.Unit.Diagnostics;

/// <summary>
/// 2026-09-08: aplikace neměla žádný trvalý cíl logů — psala jen do konzole, kterou IIS
/// v in-process režimu zahazuje. Chyby tak nebylo kde dohledat. Tenhle provider je
/// bez další závislosti (offline nasazení, viz docs) a zapisuje do denního souboru.
/// </summary>
public sealed class FileLoggerProviderTests
{
    private static string DocasnyAdresar()
        => Path.Combine(Path.GetTempPath(), "pmtracker-log-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Log_ZapiseUrovenKategoriiZpravuIVyjimku()
    {
        var adresar = DocasnyAdresar();
        try
        {
            using (var provider = new FileLoggerProvider(new FileLoggerOptions { Directory = adresar }))
            {
                var logger = provider.CreateLogger("PmTracker.Web.Controllers.ExportController");
                logger.Log(
                    LogLevel.Error, new EventId(0), "tisk vyzvy selhal",
                    new InvalidOperationException("boom"), (s, _) => s);
            }

            var soubor = Directory.GetFiles(adresar, "*.log").Should().ContainSingle().Which;
            var obsah = File.ReadAllText(soubor);

            obsah.Should().Contain("ERROR", "úroveň musí být v logu vidět");
            obsah.Should().Contain("ExportController", "bez kategorie není poznat, kde chyba nastala");
            obsah.Should().Contain("tisk vyzvy selhal");
            obsah.Should().Contain("InvalidOperationException", "typ výjimky je první vodítko");
            obsah.Should().Contain("boom", "a její zpráva druhé");
        }
        finally
        {
            if (Directory.Exists(adresar)) Directory.Delete(adresar, true);
        }
    }

    /// <summary>
    /// Do logu patří jen chyby (uživatel 2026-09-08: „ať se tam zapisují jen chyby").
    /// Na Warning padaly do souboru i běžné provozní hlášky a adresář by rostl zbytečně;
    /// na Information dokonce každý SQL příkaz EF Core — jeden běh testové sady z toho
    /// udělal 5,2 MB. Pro hlubší diagnostiku si admin úroveň dočasně sníží
    /// v Logging:File:MinimumLevel.
    /// </summary>
    [Fact]
    public void VychoziUroven_JeError_TakzeVarovaniSeNezapisuji()
    {
        var adresar = DocasnyAdresar();
        try
        {
            using (var provider = new FileLoggerProvider(new FileLoggerOptions { Directory = adresar }))
            {
                var logger = provider.CreateLogger("Test");
                logger.Log(LogLevel.Warning, new EventId(0), "bezne varovani", null, (s, _) => s);
                logger.Log(LogLevel.Error, new EventId(0), "tohle chceme videt", null, (s, _) => s);
            }

            var obsah = string.Join("\n",
                Directory.GetFiles(adresar, "*.log").SelectMany(File.ReadAllLines));

            obsah.Should().NotContain("bezne varovani", "do souboru patří jen chyby");
            obsah.Should().Contain("tohle chceme videt");
        }
        finally
        {
            if (Directory.Exists(adresar)) Directory.Delete(adresar, true);
        }
    }

    /// <summary>
    /// Log nesmí v adresáři hnít navěky (uživatel 2026-09-08). Týden je kompromis:
    /// chyba z pátku je v pondělí ještě k dispozici, ale nic se nedrží měsíce.
    /// </summary>
    [Fact]
    public void VychoziRetence_JeTyden()
    {
        new FileLoggerOptions().RetainedDays.Should().Be(7);
    }

    /// <summary>
    /// Stáří se posuzuje podle data v NÁZVU souboru, ne podle času posledního zápisu.
    /// Čas zápisu umí přepsat kdejaká zálohovací nebo antivirová služba, a pak by se
    /// mazalo špatně. Navíc je jen tak možné mazání otestovat s podvrženými hodinami.
    /// </summary>
    [Fact]
    public void Uklid_SmazeStareSouboryPodleDataVNazvu()
    {
        var adresar = DocasnyAdresar();
        Directory.CreateDirectory(adresar);
        try
        {
            File.WriteAllText(Path.Combine(adresar, "pmtracker-20260101.log"), "stary");
            File.WriteAllText(Path.Combine(adresar, "pmtracker-20260907.log"), "vcerejsi");
            File.WriteAllText(Path.Combine(adresar, "poznamky.txt"), "cizi soubor");

            var hodiny = new FakeTimeProvider(new DateTimeOffset(2026, 9, 8, 10, 0, 0, TimeSpan.Zero));
            using var provider = new FileLoggerProvider(
                new FileLoggerOptions { Directory = adresar, RetainedDays = 7 }, hodiny);

            File.Exists(Path.Combine(adresar, "pmtracker-20260101.log"))
                .Should().BeFalse("leden je dávno za hranicí týdne");
            File.Exists(Path.Combine(adresar, "pmtracker-20260907.log"))
                .Should().BeTrue("včerejší log se ještě hodí");
            File.Exists(Path.Combine(adresar, "poznamky.txt"))
                .Should().BeTrue("cizí soubory v adresáři se nemažou");
        }
        finally
        {
            if (Directory.Exists(adresar)) Directory.Delete(adresar, true);
        }
    }

    /// <summary>
    /// Úklid se nesmí dělat jen při startu. Aplikace na IIS běží dlouho a mezi restarty
    /// by se staré soubory nikdy nesmazaly — proto se uklízí i při přelomu dne.
    /// </summary>
    [Fact]
    public void Uklid_ProbehneIPriPrelomuDne()
    {
        var adresar = DocasnyAdresar();
        try
        {
            var hodiny = new FakeTimeProvider(new DateTimeOffset(2026, 9, 8, 23, 0, 0, TimeSpan.Zero));
            using var provider = new FileLoggerProvider(
                new FileLoggerOptions { Directory = adresar, RetainedDays = 1 }, hodiny);

            var logger = provider.CreateLogger("Test");
            logger.Log(LogLevel.Error, new EventId(0), "prvni den", null, (s, _) => s);
            File.Exists(Path.Combine(adresar, "pmtracker-20260908.log")).Should().BeTrue();

            hodiny.Advance(TimeSpan.FromDays(2));
            logger.Log(LogLevel.Error, new EventId(0), "treti den", null, (s, _) => s);

            File.Exists(Path.Combine(adresar, "pmtracker-20260910.log"))
                .Should().BeTrue("nový den píše do nového souboru");
            File.Exists(Path.Combine(adresar, "pmtracker-20260908.log"))
                .Should().BeFalse("přelom dne uklidil, co je za retencí");
        }
        finally
        {
            if (Directory.Exists(adresar)) Directory.Delete(adresar, true);
        }
    }

    /// <summary>
    /// Filtrování úrovní zůstává na standardní konfiguraci Logging:LogLevel — provider
    /// zapisuje, co mu framework pošle, ale pod nastavené minimum nejde.
    /// </summary>
    [Fact]
    public void Log_PodMinimalniUrovni_Nezapisuje()
    {
        var adresar = DocasnyAdresar();
        try
        {
            using (var provider = new FileLoggerProvider(
                new FileLoggerOptions { Directory = adresar, MinimumLevel = LogLevel.Warning }))
            {
                var logger = provider.CreateLogger("Test");
                logger.Log(LogLevel.Information, new EventId(0), "ticho", null, (s, _) => s);
            }

            Directory.GetFiles(adresar, "*.log").SelectMany(File.ReadAllLines)
                .Should().BeEmpty("Information je pod nastaveným minimem");
        }
        finally
        {
            if (Directory.Exists(adresar)) Directory.Delete(adresar, true);
        }
    }

    /// <summary>
    /// Logování nesmí shodit aplikaci ani když adresář nejde založit — na produkci
    /// typicky kvůli chybějícímu právu zápisu pro identitu app poolu.
    /// </summary>
    [Fact]
    public void NedostupnyAdresar_Neshodiaplikaci()
    {
        // Cesta ukazuje na SOUBOR, takže Directory.CreateDirectory selže na obou OS.
        var soubor = Path.Combine(Path.GetTempPath(), "pmtracker-log-" + Guid.NewGuid().ToString("N") + ".tmp");
        File.WriteAllText(soubor, "obsazeno");
        var zaloha = DocasnyAdresar();
        try
        {
            var act = () =>
            {
                using var provider = new FileLoggerProvider(
                    new FileLoggerOptions { Directory = soubor, FallbackDirectory = zaloha });
                provider.CreateLogger("Test")
                    .Log(LogLevel.Error, new EventId(0), "spadne to?", null, (s, _) => s);
            };

            act.Should().NotThrow("selhání logování nesmí být horší než chyba, kterou loguje");
        }
        finally
        {
            File.Delete(soubor);
            if (Directory.Exists(zaloha)) Directory.Delete(zaloha, true);
        }
    }

    /// <summary>
    /// Tiché vypnutí logu by uživatele vrátilo přesně tam, odkud jsme vyšli — k chybě
    /// bez jediné stopy. Když hlavní adresář nejde použít, log spadne do záložního.
    /// </summary>
    [Fact]
    public void NedostupnyAdresar_ZapiseDoZalozniho()
    {
        var soubor = Path.Combine(Path.GetTempPath(), "pmtracker-log-" + Guid.NewGuid().ToString("N") + ".tmp");
        File.WriteAllText(soubor, "obsazeno");
        var zaloha = DocasnyAdresar();
        try
        {
            using (var provider = new FileLoggerProvider(
                new FileLoggerOptions { Directory = soubor, FallbackDirectory = zaloha }))
            {
                provider.CreateLogger("Test")
                    .Log(LogLevel.Error, new EventId(0), "musi byt videt", null, (s, _) => s);
            }

            var zapsany = Directory.GetFiles(zaloha, "*.log").Should().ContainSingle().Which;
            File.ReadAllText(zapsany).Should().Contain("musi byt videt",
                "jinak by se chyba zase nikde neobjevila");
        }
        finally
        {
            File.Delete(soubor);
            if (Directory.Exists(zaloha)) Directory.Delete(zaloha, true);
        }
    }
}
