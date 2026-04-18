using FluentAssertions;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

[Collection(E2ECollection.CollectionName)]
public sealed class EventBusAdapterTests
{
    private readonly E2ETestFixture _fixture;

    public EventBusAdapterTests(E2ETestFixture fixture) { _fixture = fixture; }

    [Fact]
    public async Task GovClick_BubblesAs_NativeClick()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/");

        await page.EvaluateAsync(@"() => {
            window.__clickCount = 0;
            document.addEventListener('click', () => { window.__clickCount += 1; });

            const gb = document.createElement('gov-button');
            gb.setAttribute('data-test', 'eventbus-adapter');
            document.body.appendChild(gb);
            gb.dispatchEvent(new CustomEvent('gov-click', { bubbles: true, composed: true }));
        }");

        var count = await page.EvaluateAsync<int>("() => window.__clickCount");
        count.Should().Be(1, "adaptér přeloží gov-click na nativní click");

        await page.Context.CloseAsync();
    }
}
