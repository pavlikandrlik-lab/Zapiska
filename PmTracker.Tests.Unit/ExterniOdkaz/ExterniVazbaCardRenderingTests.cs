using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.ExterniOdkaz;

public sealed class ExterniVazbaCardRenderingTests
{
    private static string ReadView()
    {
        var path = Path.Combine(
            FindRepoRoot(), "PmTracker.Web", "Views", "Projekty", "_EditZaznamExternalPanel.cshtml");
        return File.ReadAllText(path);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir!.FullName;
    }

    [Fact]
    public void ExternalPanel_ShouldUseNewCardGridClasses()
    {
        var view = ReadView();
        view.Should().Contain("external-field-cislo");
        view.Should().Contain("external-field-typ");
        view.Should().Contain("external-field-cena");
        view.Should().Contain("external-field-vyzva");
        // Akce rozděleny do dvou slotů: chat (vpravo nahoře) a trash (vpravo dole) — layout v4.
        view.Should().Contain("external-action-chat");
        view.Should().Contain("external-action-trash");
        view.Should().Contain("external-dates");
    }

    [Fact]
    public void ExternalPanel_ShouldHaveTypReadonly()
    {
        var view = ReadView();
        view.Should().Contain("data-external-type-display");
        view.Should().NotContain("data-external-type-select");
    }

    [Fact]
    public void ExternalPanel_ShouldHaveCislo6DigitPattern()
    {
        var view = ReadView();
        view.Should().Contain("pattern=\"[0-9]{6}\"");
        view.Should().Contain("maxlength=\"6\"");
    }

    [Fact]
    public void ExternalPanel_ShouldUseIconRemoveButton()
    {
        var view = ReadView();
        view.Should().Contain("data-external-remove");
        view.Should().Contain("gov-icon");
        // 2026-04-27: koš sjednocen na trash3 (Bootstrap Icons 1.11.3, přidána do
        // wwwroot/assets/icons/components/). Původní trash měl zaoblenější tvar.
        view.Should().Contain("name=\"trash3\"");
        view.Should().NotMatchRegex("name=\"trash\"(?!3)",
            "name=\"trash\" je obsoletní — vše sjednoceno na trash3 napříč aplikací");
    }

    [Fact]
    public void ExternalPanel_ShouldUseChatIconButton()
    {
        var view = ReadView();
        view.Should().Contain("data-external-chat-open");
        // User volba 2026-04-27: chat-left-text (Bootstrap Icons) — vizuálně
        // jasnější pro „vyjádření" (chat bubble s textovými řádky uvnitř).
        // Předchozí chat-dots (jen tři tečky uvnitř bubliny) bylo méně sdílné.
        view.Should().Contain("chat-left-text");
        view.Should().NotContain("chat-dots",
            "chat-dots byl nahrazen chat-left-text — odebrat referenci, ať nenakukuje obsoletní jméno");
    }

    [Fact]
    public void ExternalPanel_ShouldLabelSwitchAsVyzva()
    {
        var view = ReadView();
        view.Should().Contain(">Výzva<");
        view.Should().NotContain("Zařadit do další výzvy");
    }

    [Fact]
    public void ExternalPanel_ChatAndTrashShouldUseSecondaryVariant()
    {
        // User požadavek 2026-04-27: chat + trash ikony musí být vizuálně shodné
        // s edit + proposal ikonami na kartě záznamu (pm-button variant Secondary,
        // size Small). Předtím Ghost (neutral base) — vypadalo "gov-inspired".
        var view = ReadView();
        view.Should().NotContain("variant=\"Ghost\"",
            "chat ani trash tlačítko nesmí používat Ghost variantu — kvůli vizuální konzistenci s ikonami Upravit/Návrh harmonogramu na kartě záznamu (Secondary).");
        view.Should().Contain("data-external-chat-open=\"true\"");
        view.Should().Contain("data-external-remove=\"true\"");
    }

    [Fact]
    public void ExternalPanel_DatesShouldHaveDataAttributeForSyncJs()
    {
        // sync.js cílí na [data-external-dates] aby po nalezení/nenalezení ticketu
        // odhalil/skryl celou skupinu 4 datumů.
        var view = ReadView();
        view.Should().Contain("data-external-dates");
    }

    [Fact]
    public void ExternalPanel_EmptyDatesShouldUseCalendarPlaceholder()
    {
        // User požadavek 2026-04-27: prázdné datumy mají vidět ikonu kalendáře
        // jako vizuální signál „tady má být datum, zatím není".
        // Místo legacy textu „Záznam ještě nebyl převeden do archivu."
        var view = ReadView();
        view.Should().Contain("name=\"calendar3\"",
            "prázdná datumová pole musí mít gov-icon calendar3 placeholder");
        view.Should().Contain("value placeholder",
            "prázdné datum používá .value.placeholder CSS class pro styling ikony");
        view.Should().NotContain("Záznam ještě nebyl převeden do archivu.",
            "legacy hláška pro Datum převzetí byla nahrazena uniformním placeholder-em");
    }

    [Fact]
    public void ExternalPanel_DatesShouldBeVisibleWhenTicketResolved()
    {
        // ticketResolved = !string.IsNullOrWhiteSpace(vazba.Typ).
        // Když je Typ vyplněn (ticket byl už dohledán v SD), datumy se zobrazí
        // VŽDY — nezávisle na tom, zda je v DB harvestnutý nějaký krok.
        var view = ReadView();
        view.Should().Contain("ticketResolved",
            "podmínka zobrazení datumů musí být ticketResolved (Typ vyplněn), ne hasAnyDate");
        view.Should().NotContain("hasAnyDate",
            "stará podmínka hasAnyDate nesmí zůstat — datumy se mají vidět vždy po nalezení ticketu");
    }
}
