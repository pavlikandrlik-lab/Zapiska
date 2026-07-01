using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Meetings;

/// <summary>
/// Pokrývá specifikaci docs/specs/meeting-time-picker.md.
///
/// Time picker v modalech jednání (Nové/Upravit) musí nabízet pouze časy
/// v rozsahu 06:00–22:45 po 15 minutách. Test kontroluje ESM zdrojový modul
/// pickers/time.js (legacy site.bundle.js odstraněn 2026-06-16; aplikace běží na site.js).
/// </summary>
public sealed class MeetingTimePickerRangeTests
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
    public void PickerSource_ShouldUseSixToTwentyTwoRange()
    {
        // Fáze 3B Task 3: time picker logika přesunuta do pickers/time.js
        var source = LoadText("PmTracker.Web/wwwroot/js/modules/pickers/time.js");

        source.Should().MatchRegex(
            @"for\s*\(\s*let\s+hour\s*=\s*6\s*;\s*hour\s*<=\s*22\s*;",
            "zdrojový modul pickers/time.js musí omezit rozsah na 6–22");

        // Safety-net: neexistuje v modulu žádný for-loop s 0..24
        source.Should().NotMatchRegex(
            @"for\s*\(\s*let\s+hour\s*=\s*0\s*;\s*hour\s*<\s*24\s*;",
            "modul pickers/time.js nesmí obsahovat starý rozsah 0–24");
    }

    [Fact]
    public void SpecDocument_ShouldExist()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PmTracker.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull();
        var specPath = Path.Combine(directory!.FullName, "docs", "specs", "meeting-time-picker.md");
        File.Exists(specPath).Should().BeTrue($"specifikace musí existovat na {specPath}");
    }

    [Fact]
    public void MeetingModal_ShouldUseAppTimeFieldContract()
    {
        var modal = LoadText("PmTracker.Web/Views/Jednani/NewMeetingModal.cshtml");

        modal.Should().Contain("data-app-time-field", "modal musí používat custom app-time-field picker");
        modal.Should().Contain("data-app-time-grid", "modal musí obsahovat grid placeholder pro časové volby");
        modal.Should().Contain("name=\"CasZacatek\"", "CasZacatek je pojmenovaný input");
    }
}
