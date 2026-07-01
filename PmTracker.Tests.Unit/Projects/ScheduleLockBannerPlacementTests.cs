using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Projects;

/// <summary>
/// 2026-06-29: banner zámku harmonogramu ("Pro tento záznam už čeká návrh změny harmonogramu.
/// Harmonogram je do rozhodnutí uzamčený.") se zobrazoval UPROSTŘED formuláře — v basic panelu
/// mezi popisem a datumy. Je to ale form-level notice → patří na vrch formuláře (nad záložky),
/// viditelný bez ohledu na aktivní záložku, ne pohřbený pod popisem.
/// </summary>
public sealed class ScheduleLockBannerPlacementTests
{
    private static string LoadText(string relativePath)
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
    public void PendingScheduleLockBanner_RendersAtFormTop_NotInBasicPanel()
    {
        var form = LoadText("PmTracker.Web/Views/Projekty/_EditZaznamForm.cshtml");
        var basic = LoadText("PmTracker.Web/Views/Projekty/_EditZaznamBasicPanel.cshtml");

        form.Should().Contain("PendingScheduleProposalMessage",
            "banner zámku harmonogramu patří na vrch formuláře (form-level notice)");
        basic.Should().NotContain("PendingScheduleProposalMessage",
            "banner zámku nesmí být uprostřed basic panelu pod popisem");
    }

    [Fact]
    public void LockBanner_IsAboveTheTabsRow()
    {
        var form = LoadText("PmTracker.Web/Views/Projekty/_EditZaznamForm.cshtml");

        var bannerIndex = form.IndexOf("PendingScheduleProposalMessage", StringComparison.Ordinal);
        var tabsRowIndex = form.IndexOf("record-editor-tabs-row", StringComparison.Ordinal);
        bannerIndex.Should().BeGreaterThan(0, "banner musí být ve formuláři");
        tabsRowIndex.Should().BeGreaterThan(0);
        bannerIndex.Should().BeLessThan(tabsRowIndex, "banner zámku je nad řádkem záložek, ne uvnitř panelu");
    }
}
