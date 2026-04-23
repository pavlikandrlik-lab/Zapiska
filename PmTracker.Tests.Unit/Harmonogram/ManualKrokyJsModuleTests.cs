using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Harmonogram;

/// <summary>
/// Plán D Task 9: file-text assertions pro JS modul manualKroky.js —
/// zaručuje existenci, veřejné API, navázání eventů a konzistentní sync
/// do site.bundle.js. Projekt má manuální bundle concat (viz CLAUDE.md
/// project_bundle_sync); bez testu by synchronizace mohla tiše odjet.
/// </summary>
public sealed class ManualKrokyJsModuleTests
{
    private const string ModulePath = "PmTracker.Web/wwwroot/js/modules/harmonogram/manualKroky.js";
    private const string BundlePath = "PmTracker.Web/wwwroot/js/site.bundle.js";

    private static string LoadRepoText(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PmTracker.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Nepodařilo se najít kořen repozitáře (PmTracker.sln).");
        }

        var full = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(full).Should().BeTrue($"soubor musí existovat na cestě {full}");
        return File.ReadAllText(full);
    }

    [Fact]
    public void Module_File_Exists()
    {
        var text = LoadRepoText(ModulePath);
        text.Should().NotBeNullOrWhiteSpace("manualKroky.js musí existovat v modules/harmonogram/");
    }

    [Fact]
    public void Module_Exposes_PublicApi_WithInit()
    {
        var text = LoadRepoText(ModulePath);
        text.Should().Contain("pmManualKroky", "veřejné API následuje konvenci pm* ostatních modulů");
        text.Should().Contain("init", "musí vystavit init() pro bootstrap po DOMContentLoaded");
    }

    [Fact]
    public void Module_Listens_ForChange_OnManualKrokInput()
    {
        var text = LoadRepoText(ModulePath);
        text.Should().Contain("data-manual-krok-input",
            "modul se váže na inputy s data-manual-krok-input (kontrakt z _ScheduleBlockManualCell)");
        text.Should().Contain("change",
            "reaguje na change event (ne input), aby se spouštěl jen při commitu hodnoty");
    }

    [Fact]
    public void Module_Rejects_FutureDates()
    {
        var text = LoadRepoText(ModulePath);
        text.Should().Contain("nemůže být v budoucnu",
            "budoucí datum se odmítá (odpovídá server-side ManualProposalFieldValidator)");
    }

    [Fact]
    public void Bundle_Contains_ManualKrokyModuleSection()
    {
        var text = LoadRepoText(BundlePath);
        text.Should().Contain(
            "PmTracker.Web/wwwroot/js/modules/harmonogram/manualKroky.js",
            "site.bundle.js je manuální concat; přidání modulu musí být promítnuto do bundle");
    }

    [Fact]
    public void Bundle_Inits_ManualKrokyModule()
    {
        var text = LoadRepoText(BundlePath);
        text.Should().Contain("pmManualKroky",
            "bootstrap části bundle musí volat pmManualKroky.init() po DOMContentLoaded");
    }
}
