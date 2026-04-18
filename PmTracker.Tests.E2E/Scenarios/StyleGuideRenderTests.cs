using FluentAssertions;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

[Collection(E2ECollection.CollectionName)]
public sealed class StyleGuideRenderTests
{
    private readonly E2ETestFixture _fixture;

    public StyleGuideRenderTests(E2ETestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task StyleGuide_VraciHttp200_AObsahujePmKomponenty()
    {
        var page = await _fixture.NewPageAsync();

        var response = await page.GotoAsync($"{_fixture.BaseUrl}/StyleGuide");
        response!.Status.Should().Be(200);

        // Počkáme na hydrataci gov Web Components
        await page.WaitForSelectorAsync("gov-button", new() { Timeout = 5000 });

        var buttonCount = await page.Locator("gov-button").CountAsync();
        buttonCount.Should().BeGreaterThan(4, "StyleGuide zobrazuje alespoň 4 varianty pm-button");

        var alertCount = await page.Locator("gov-message").CountAsync();
        alertCount.Should().Be(4, "StyleGuide zobrazuje 4 pm-alert varianty");

        var badgeCount = await page.Locator("gov-tag").CountAsync();
        badgeCount.Should().Be(5, "StyleGuide zobrazuje 5 pm-badge variant");

        var fieldCount = await page.Locator("gov-form-control").CountAsync();
        fieldCount.Should().BeGreaterThanOrEqualTo(3, "StyleGuide má alespoň 3 pm-field ukázky");

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task StyleGuide_ObsahujeSekceFaze2A()
    {
        var page = await _fixture.NewPageAsync();

        var response = await page.GotoAsync($"{_fixture.BaseUrl}/StyleGuide");
        response!.Status.Should().Be(200);

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        foreach (var section in new[] { "select", "textarea", "checkbox", "radio", "switch" })
        {
            var count = await page.Locator($"[data-styleguide-section=\"{section}\"]").CountAsync();
            count.Should().Be(1, $"Sekce {section} musí být přesně jednou ve StyleGuide (Fáze 2A)");
        }

        // Sanity: gov komponenty odpovídající novým pm-* wrapperům jsou přítomné
        (await page.Locator("gov-form-select").CountAsync())
            .Should().BeGreaterThanOrEqualTo(3, "3 pm-select ukázky ve StyleGuide");
        (await page.Locator("gov-form-checkbox").CountAsync())
            .Should().BeGreaterThanOrEqualTo(3, "3 pm-checkbox ukázky");
        (await page.Locator("gov-form-radio-group").CountAsync())
            .Should().BeGreaterThanOrEqualTo(2, "2 pm-radio-group (vertical + horizontal)");
        (await page.Locator("gov-form-switch").CountAsync())
            .Should().BeGreaterThanOrEqualTo(3, "3 pm-switch ukázky");

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task StyleGuide_ObsahujeSekceFaze2B()
    {
        var page = await _fixture.NewPageAsync();

        var response = await page.GotoAsync($"{_fixture.BaseUrl}/StyleGuide");
        response!.Status.Should().Be(200);

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        foreach (var section in new[] { "link", "tabs", "card", "pagination" })
        {
            var count = await page.Locator($"[data-styleguide-section=\"{section}\"]").CountAsync();
            count.Should().Be(1, $"Sekce {section} musí být přesně jednou ve StyleGuide (Fáze 2B)");
        }

        // Sanity: gov komponenty odpovídající novým pm-* wrapperům jsou přítomné
        (await page.Locator("gov-link").CountAsync())
            .Should().BeGreaterThanOrEqualTo(4, "4 pm-link ukázky ve StyleGuide");
        (await page.Locator("gov-tabs").CountAsync())
            .Should().BeGreaterThanOrEqualTo(2, "2 pm-tabs (horizontal + chip)");
        (await page.Locator("gov-card").CountAsync())
            .Should().BeGreaterThanOrEqualTo(2, "2 pm-card (default + klikací)");
        (await page.Locator("gov-pagination").CountAsync())
            .Should().BeGreaterThanOrEqualTo(3, "3 pm-pagination (uprostřed, první, poslední)");

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task StyleGuide_PmButton_Klikatelne_PropagujeClick()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/StyleGuide");
        await page.WaitForSelectorAsync("gov-button", new() { Timeout = 5000 });

        await page.EvaluateAsync(@"() => {
            window.__clickCount = 0;
            document.addEventListener('click', () => { window.__clickCount += 1; }, true);
        }");

        var firstButton = page.Locator("gov-button").First;
        await firstButton.ClickAsync();

        var count = await page.EvaluateAsync<int>("() => window.__clickCount");
        count.Should().BeGreaterThan(0, "gov-click adaptér propaguje jako nativní click");

        await page.Context.CloseAsync();
    }
}
