using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

[Collection(E2ECollection.CollectionName)]
public sealed class ProjectListStatusFiltersScenariosTests
{
    private readonly E2ETestFixture _fixture;

    public ProjectListStatusFiltersScenariosTests(E2ETestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ProjectIndexStatusFilters_ShouldHideProjects_AndPersistToLocalStorage()
    {
        var doneProject = await EnsureProjectWithStatusAsync("E2E_DONE_HIDE", "DONE");
        var deletedProject = await EnsureProjectWithStatusAsync("E2E_DELETED_HIDE", "DELETED");
        var page = await _fixture.NewPageAsync();

        try
        {
            var pageErrors = new List<string>();
            page.PageError += (_, error) =>
            {
                pageErrors.Add(error);
            };

            await page.GotoAsync($"{_fixture.BaseUrl}/Projekty?asUser={_fixture.AdminOsobaId}");

            await page.GetByRole(AriaRole.Button, new() { Name = "Filtry" }).ClickAsync();

            var diagnostics = await page.EvaluateAsync<ProjectFilterDiagnostics>(
                "() => ({ " +
                "hideDoneStored: localStorage.getItem('pmtracker.projects.hideDone'), " +
                "hideDeletedStored: localStorage.getItem('pmtracker.projects.hideDeleted'), " +
                "hideDoneChecked: document.querySelector(\"[data-project-status-hide='DONE']\")?.checked ?? null, " +
                "hideDeletedChecked: document.querySelector(\"[data-project-status-hide='DELETED']\")?.checked ?? null " +
                "})");

            var doneCard = page.Locator($"[data-project-status-code='DONE']").Filter(new() { HasTextString = doneProject.Zkratka });
            var deletedCard = page.Locator($"[data-project-status-code='DELETED']").Filter(new() { HasTextString = deletedProject.Zkratka });

            try
            {
                await Expect(doneCard).ToBeHiddenAsync();
                await Expect(deletedCard).ToBeHiddenAsync();
            }
            catch (Exception ex)
            {
                throw new Xunit.Sdk.XunitException(
                    $"DONE checked={diagnostics.HideDoneChecked}, DELETED checked={diagnostics.HideDeletedChecked}, " +
                    $"DONE stored={diagnostics.HideDoneStored ?? "<null>"}, DELETED stored={diagnostics.HideDeletedStored ?? "<null>"}, " +
                    $"pageErrors={string.Join(" | ", pageErrors)}\n{ex.Message}");
            }

            var deletedToggle = page.Locator("[data-project-status-hide='DELETED']");
            await deletedToggle.EvaluateAsync(
                "element => { element.checked = false; element.dispatchEvent(new Event('change', { bubbles: true })); }");

            await Expect(deletedCard).ToBeVisibleAsync();
            await Expect(doneCard).ToBeHiddenAsync();

            var hideDeletedStored = await page.EvaluateAsync<string?>("() => localStorage.getItem('pmtracker.projects.hideDeleted')");
            hideDeletedStored.Should().Be("false");

            await page.ReloadAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Filtry" }).ClickAsync();

            await Expect(page.Locator("[data-project-status-hide='DELETED']")).Not.ToBeCheckedAsync();
            await Expect(page.Locator($"[data-project-status-code='DELETED']").Filter(new() { HasTextString = deletedProject.Zkratka })).ToBeVisibleAsync();
        }
        finally
        {
            await page.Context.CloseAsync();
        }
    }

    private async Task<SeededProject> EnsureProjectWithStatusAsync(string zkratka, string statusCode)
    {
        await using var connection = new SqlConnection(_fixture.Database.ConnectionString);
        await connection.OpenAsync();

        await using var findStatus = connection.CreateCommand();
        findStatus.CommandText = """
            SELECT TOP (1) id
            FROM dbo.ciselnik_stavu_projektu
            WHERE kod = @kod;
            """;
        findStatus.Parameters.AddWithValue("@kod", statusCode);
        var statusId = Convert.ToInt32(await findStatus.ExecuteScalarAsync());

        await using var existingCommand = connection.CreateCommand();
        existingCommand.CommandText = """
            SELECT TOP (1) id
            FROM dbo.projekty
            WHERE zkratka = @zkratka;
            """;
        existingCommand.Parameters.AddWithValue("@zkratka", zkratka);
        var existing = await existingCommand.ExecuteScalarAsync();
        if (existing is not null)
        {
            return new SeededProject(Convert.ToInt32(existing), zkratka);
        }

        await using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = """
            INSERT INTO dbo.projekty (zkratka, cely_nazev, stav_id)
            VALUES (@zkratka, @nazev, @stavId);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """;
        insertCommand.Parameters.AddWithValue("@zkratka", zkratka);
        insertCommand.Parameters.AddWithValue("@nazev", $"{zkratka} Test Project");
        insertCommand.Parameters.AddWithValue("@stavId", statusId);

        var projectId = Convert.ToInt32(await insertCommand.ExecuteScalarAsync());
        return new SeededProject(projectId, zkratka);
    }

    private static ILocatorAssertions Expect(ILocator locator)
    {
        return Assertions.Expect(locator);
    }

    private sealed record SeededProject(int Id, string Zkratka);

    private sealed class ProjectFilterDiagnostics
    {
        public string? HideDoneStored { get; set; }
        public string? HideDeletedStored { get; set; }
        public bool? HideDoneChecked { get; set; }
        public bool? HideDeletedChecked { get; set; }
    }
}
