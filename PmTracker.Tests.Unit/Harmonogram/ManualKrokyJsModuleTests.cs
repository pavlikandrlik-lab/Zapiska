using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Harmonogram;

/// <summary>
/// Plán D Task 9: file-text assertions pro ESM JS modul manualKroky.js —
/// zaručuje existenci, veřejné API a navázání eventů. (Bundle-sync testy
/// odstraněny 2026-06-16 spolu s legacy site.bundle.js — aplikace běží na ESM site.js.)
/// </summary>
public sealed class ManualKrokyJsModuleTests
{
    private const string ModulePath = "PmTracker.Web/wwwroot/js/modules/harmonogram/manualKroky.js";

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
}
