using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Regression guard: bool model binding pro IsEnabled musí mít přesně JEDEN
/// form input s name="IsEnabled". Předchozí dual hidden+checkbox pattern
/// (hidden value="false" → checkbox value="true") posílal dvě hodnoty a
/// ASP.NET Core SimpleTypeModelBinder bral první → IsEnabled vždy bound jako
/// false (bug 2026-04-29: user reportoval že checkbox nepersistuje po reloadu).
///
/// Po fixu: jediný hidden input "IsEnabled", gov-form-switch bez name (sync
/// přes JS handleDocumentChange → syncSyncCardSwitchHidden).
/// </summary>
public sealed class SyncJobSettingsCardBindingTests
{
    private static string LocateRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PmTracker.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Nepodařilo se najít kořen repozitáře.");
        }

        return directory.FullName;
    }

    private static string ReadSyncCardCshtml()
    {
        var path = Path.Combine(
            LocateRepositoryRoot(),
            "PmTracker.Web", "Views", "Shared", "_SyncJobSettingsCard.cshtml");
        return File.ReadAllText(path);
    }

    [Fact]
    public void IsEnabled_MustBeBoundByExactlyOneFormInput()
    {
        var source = ReadSyncCardCshtml();

        // Spočítej všechny inputy/elementy s name="IsEnabled".
        var nameAttrCount = Regex.Matches(source, @"name=""IsEnabled""").Count;

        nameAttrCount.Should().Be(1,
            "více inputů se stejným name='IsEnabled' způsobuje dvojí submission " +
            "a ASP.NET model binder bere první hodnotu → bool bound nesprávně");
    }

    [Fact]
    public void IsEnabled_MustNotBeOnGovFormSwitch()
    {
        var source = ReadSyncCardCshtml();

        // gov-form-switch neserializuje checked do form-data — pokud by měl
        // name="IsEnabled", browser by ho stejně nepostnul. Návrat k dvojímu
        // pokrytí by reaktivoval bug.
        var govSwitchWithName = Regex.IsMatch(source,
            @"<gov-form-switch[^>]*name=""IsEnabled""", RegexOptions.IgnoreCase);

        govSwitchWithName.Should().BeFalse(
            "gov-form-switch nereflektuje checked do form-data; IsEnabled musí být na hidden inputu");
    }

    [Fact]
    public void IsEnabled_HiddenInputMustHaveSyncMarkerForJs()
    {
        var source = ReadSyncCardCshtml();

        // bootstrap.js → syncSyncCardSwitchHidden hledá [data-sync-card-switch-state]
        // pro update hodnoty při gov-change. Bez tohoto markeru by switch nikdy
        // neaktualizoval hidden → IsEnabled by zůstal na initial value.
        var hasMarker = Regex.IsMatch(source,
            @"<input[^>]*name=""IsEnabled""[^>]*data-sync-card-switch-state");

        hasMarker.Should().BeTrue(
            "hidden input pro IsEnabled potřebuje data-sync-card-switch-state, jinak JS sync handler nenajde target");
    }

    [Fact]
    public void GovFormSwitch_MustHaveDataSyncCardSwitchMarker()
    {
        var source = ReadSyncCardCshtml();

        // bootstrap.js → handleDocumentChange filtruje na gov-form-switch[data-sync-card-switch].
        var hasMarker = Regex.IsMatch(source,
            @"<gov-form-switch[^>]*data-sync-card-switch", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        hasMarker.Should().BeTrue(
            "gov-form-switch potřebuje data-sync-card-switch attr, jinak handleDocumentChange neidentifikuje element");
    }

    [Fact]
    public void AnchorAt_MustDisplayInLocalTime_NotUtc()
    {
        var source = ReadSyncCardCshtml();

        // Bug 2026-04-29: display přes UtcDateTime + bind přes datetime-local
        // (parsuje bez TZ jako server-local) způsobil drift o TZ offset při
        // každém save+reload cyklu. Display + bind musí být v lokálním čase.
        var hasUtcDisplay = Regex.IsMatch(source, @"AnchorAt\.UtcDateTime\.ToString");
        hasUtcDisplay.Should().BeFalse(
            "AnchorAt nesmí být zobrazován v UtcDateTime — datetime-local input parsuje bez TZ jako lokální čas a vznikl by drift");

        var hasLocalDisplay = Regex.IsMatch(source, @"AnchorAt\.LocalDateTime\.ToString");
        hasLocalDisplay.Should().BeTrue(
            "AnchorAt musí být zobrazován jako LocalDateTime, aby display+bind cyklus byl symetrický");
    }
}
