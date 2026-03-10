using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

[Collection(E2ECollection.CollectionName)]
public sealed class RecordGoalEditorScenariosTests
{
    private readonly E2ETestFixture _fixture;

    public RecordGoalEditorScenariosTests(E2ETestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RecordGoalEditor_ShouldAutoGrow_AndRenderWrappedLineBreaksInRecordCard()
    {
        var page = await _fixture.NewPageAsync();
        const string createdRecordName = "E2E goal auto-grow record";
        var goalFirstLine = new string('A', 220);
        var goalSecondLine = new string('B', 220);
        var goalText = $"{goalFirstLine}\n{goalSecondLine}";

        try
        {
            await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=zaznamy&asUser={_fixture.AdminOsobaId}");
            await page.EvaluateAsync("() => localStorage.setItem('pmtracker.recordEditor.preference', 'modal')");
            await page.ReloadAsync();

            await page.GetByRole(AriaRole.Button, new() { Name = "Nový záznam" }).ClickAsync();

            var modal = page.Locator(".modal-overlay");
            var form = modal.Locator("form[data-record-editor-form='true']");
            await Expect(form).ToBeVisibleAsync();

            await form.Locator("input[name='Nazev']").FillAsync(createdRecordName);

            var subsystemSelect = form.Locator("select[name='Subsystem']");
            var subsystemOptions = await subsystemSelect.Locator("option").CountAsync();
            subsystemOptions.Should().BeGreaterThan(0, "nový záznam musí mít minimálně jeden aktivní subsystém");

            var ownerCandidates = await form.Locator("[data-record-owner-picker] [data-person-picker-source] [data-id]").CountAsync();
            ownerCandidates.Should().BeGreaterThan(0, "vlastník musí být vybratelný ze seznamu osob projektu");
            await form.EvaluateAsync(
                @"formElement => {
                    const firstCandidate = formElement.querySelector('[data-record-owner-picker] [data-person-picker-source] [data-id]');
                    const hiddenInput = formElement.querySelector('[data-record-owner-picker] [data-person-picker-hidden]');
                    const queryInput = formElement.querySelector('[data-record-owner-picker] [data-person-picker-input]');
                    if (!(firstCandidate instanceof HTMLElement)
                        || !(hiddenInput instanceof HTMLInputElement)
                        || !(queryInput instanceof HTMLInputElement)) {
                        return;
                    }

                    const id = (firstCandidate.dataset.id || '').trim();
                    const label = (firstCandidate.dataset.label || '').trim();
                    const email = (firstCandidate.dataset.email || '').trim();
                    hiddenInput.value = id;
                    queryInput.value = email ? `${label} <${email}>` : label;
                    queryInput.setCustomValidity('');
                }");

            var goalInput = form.Locator("textarea[name='Cil'][data-record-goal-autogrow='true']");
            await Expect(goalInput).ToBeVisibleAsync();

            var initialHeight = await goalInput.EvaluateAsync<double>(
                "node => node instanceof HTMLTextAreaElement ? node.getBoundingClientRect().height : 0");
            await goalInput.FillAsync(goalText);
            await page.WaitForTimeoutAsync(120);
            var grownHeight = await goalInput.EvaluateAsync<double>(
                "node => node instanceof HTMLTextAreaElement ? node.getBoundingClientRect().height : 0");
            grownHeight.Should().BeGreaterThan(initialHeight + 8);

            var saveResponseTask = page.WaitForResponseAsync(response =>
                response.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase)
                && response.Url.Contains("/Zaznamy/Save", StringComparison.OrdinalIgnoreCase));
            await form.Locator("button[type='submit']").ClickAsync();

            var saveResponse = await saveResponseTask;
            var savePayload = await saveResponse.TextAsync();
            saveResponse.Ok.Should().BeTrue(savePayload);
            var responseHeaders = await saveResponse.AllHeadersAsync();
            responseHeaders.TryGetValue("content-type", out var contentType);
            contentType.Should().Contain("application/json");

            await Expect(page.Locator(".modal-overlay")).ToHaveCountAsync(0);

            var card = page.Locator(".record-card", new() { HasTextString = createdRecordName }).First;
            await Expect(card).ToBeVisibleAsync();

            var goalSubtitle = card.Locator(".record-goal-subtitle");
            await Expect(goalSubtitle).ToBeVisibleAsync();

            var whiteSpace = await goalSubtitle.EvaluateAsync<string>(
                "node => node instanceof HTMLElement ? getComputedStyle(node).whiteSpace : ''");
            whiteSpace.Should().Contain("pre-wrap");

            var renderedText = await goalSubtitle.EvaluateAsync<string>(
                "node => node instanceof HTMLElement ? node.innerText : ''");
            renderedText.Should().Contain(goalFirstLine);
            renderedText.Should().Contain(goalSecondLine);
            renderedText.Should().Contain("\n");

            var subtitleHeight = await goalSubtitle.EvaluateAsync<double>(
                "node => node instanceof HTMLElement ? node.getBoundingClientRect().height : 0");
            subtitleHeight.Should().BeGreaterThan(30);
        }
        finally
        {
            await DeleteRecordByNameAsync(createdRecordName);
            await page.Context.CloseAsync();
        }
    }

    private static ILocatorAssertions Expect(ILocator locator)
    {
        return Assertions.Expect(locator);
    }

    private async Task DeleteRecordByNameAsync(string recordName)
    {
        await using var connection = new SqlConnection(_fixture.Database.ConnectionString);
        await connection.OpenAsync();

        await using var findCommand = connection.CreateCommand();
        findCommand.CommandText = """
            SELECT TOP (1) id
            FROM dbo.projektove_zaznamy
            WHERE projekt_id = @projectId
              AND nazev = @recordName
            ORDER BY id DESC;
            """;
        findCommand.Parameters.AddWithValue("@projectId", _fixture.ProjectId);
        findCommand.Parameters.AddWithValue("@recordName", recordName);
        var result = await findCommand.ExecuteScalarAsync();
        if (result is null || result == DBNull.Value)
        {
            return;
        }

        var recordId = Convert.ToInt32(result);
        var dependentTables = new[]
        {
            "zaznam_harmonogram_hodnoty",
            "zaznam_spoluprace",
            "zaznam_externi_odkazy",
            "vyjadreni",
            "zaznam_historie_zmen_typu",
            "zaznam_historie_terminu",
            "zaznam_historie_vlastnik",
            "zaznam_historie_subsystem",
            "zaznam_historie_stavu_zaznamu",
            "zaznam_historie_stavu_projektu"
        };

        foreach (var table in dependentTables)
        {
            await using var deleteDependencyCommand = connection.CreateCommand();
            deleteDependencyCommand.CommandText = $"DELETE FROM dbo.{table} WHERE zaznam_id = @recordId;";
            deleteDependencyCommand.Parameters.AddWithValue("@recordId", recordId);
            await deleteDependencyCommand.ExecuteNonQueryAsync();
        }

        await using var deleteRecordCommand = connection.CreateCommand();
        deleteRecordCommand.CommandText = "DELETE FROM dbo.projektove_zaznamy WHERE id = @recordId;";
        deleteRecordCommand.Parameters.AddWithValue("@recordId", recordId);
        await deleteRecordCommand.ExecuteNonQueryAsync();
    }
}
