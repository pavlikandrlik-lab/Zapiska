using System.Linq;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Tests.E2E.Scenarios;

[Collection(E2ECollection.CollectionName)]
public sealed class RecordRichTextScenariosTests
{
    private readonly E2ETestFixture _fixture;

    public RecordRichTextScenariosTests(E2ETestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RecordCommentRichText_ShouldAutoGrow_AndPersistFormatting()
    {
        var page = await _fixture.NewPageAsync();
        const string createdRecordName = "E2E rich text comment record";
        int? createdMeetingId = null;

        try
        {
            var ensuredMeeting = await EnsureOpenMeetingAsync();
            if (ensuredMeeting.Created)
            {
                createdMeetingId = ensuredMeeting.MeetingId;
            }

            await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=zaznamy&asUser={_fixture.AdminOsobaId}");
            await page.EvaluateAsync("() => localStorage.setItem('pmtracker.recordEditor.preference', 'modal')");
            await page.ReloadAsync();

            await page.GetByRole(AriaRole.Button, new() { Name = "Nový záznam" }).ClickAsync();

            var modal = page.Locator(".modal-overlay");
            var form = modal.Locator("form[data-record-editor-form='true']");
            await Expect(form).ToBeVisibleAsync();

            await form.Locator("input[name='Nazev']").FillAsync(createdRecordName);

            var ownerCandidates = await form.Locator("[data-record-owner-picker] [data-person-picker-source] [data-id]").CountAsync();
            ownerCandidates.Should().BeGreaterThan(0, "vlastník musí být vybratelný ze seznamu osob projektu");
            await form.EvaluateAsync(
                """
                formElement => {
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
                }
                """);

            var saveRecordResponseTask = page.WaitForResponseAsync(response =>
                response.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase)
                && response.Url.Contains("/Zaznamy/Save", StringComparison.OrdinalIgnoreCase));
            await form.Locator("button[type='submit']").ClickAsync();
            var saveRecordResponse = await saveRecordResponseTask;
            var saveRecordPayload = await saveRecordResponse.TextAsync();
            saveRecordResponse.Ok.Should().BeTrue(saveRecordPayload);

            await Expect(page.Locator(".modal-overlay")).ToHaveCountAsync(0);

            var card = page.Locator(".record-card", new() { HasTextString = createdRecordName }).First;
            await Expect(card).ToBeVisibleAsync();
            await card.Locator("[data-record-toggle]").ClickAsync();

            var recordId = await card.GetAttributeAsync("data-record-id");
            recordId.Should().NotBeNullOrWhiteSpace();

            var editor = card.Locator("form.comment-form .ql-editor").First;
            await Expect(editor).ToBeVisibleAsync();

            var initialHeight = await editor.EvaluateAsync<double>(
                "node => node instanceof HTMLElement ? node.getBoundingClientRect().height : 0");
            var longMultilineText = string.Join("\n", Enumerable.Repeat(new string('A', 180), 14));
            await editor.FillAsync(longMultilineText);
            await page.WaitForTimeoutAsync(120);
            var grownHeight = await editor.EvaluateAsync<double>(
                "node => node instanceof HTMLElement ? node.getBoundingClientRect().height : 0");
            grownHeight.Should().BeGreaterThan(initialHeight + 20);

            await card.EvaluateAsync(
                """
                cardNode => {
                    const textarea = cardNode.querySelector("form.comment-form textarea[name='Text'][data-rich-text='true']");
                    const quill = textarea?._richTextEditor;
                    if (!quill) {
                        throw new Error("Rich text editor is not initialized.");
                    }

                    quill.clipboard.dangerouslyPasteHTML('<p class="ql-indent-1"><strong>Bold</strong> <em>Italic</em> <u>Underline</u> <a href="https://example.com">Link</a></p>');
                }
                """);

            var meetingSelect = card.Locator("form.comment-form select[name='JednaniId']");
            var meetingOptions = await meetingSelect.Locator("option").CountAsync();
            meetingOptions.Should().BeGreaterThan(1, "musí existovat aspoň jedno otevřené jednání pro uložení vyjádření");
            await meetingSelect.SelectOptionAsync(new SelectOptionValue { Index = 1 });

            var saveResponseTask = page.WaitForResponseAsync(response =>
                response.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase)
                && response.Url.Contains("/Zaznamy/AddComment", StringComparison.OrdinalIgnoreCase));
            await card.Locator("form.comment-form button[type='submit']").ClickAsync();

            var saveResponse = await saveResponseTask;
            var savePayload = await saveResponse.TextAsync();
            saveResponse.Ok.Should().BeTrue(savePayload);

            var savedComment = page.Locator($".record-card[data-record-id='{recordId}'] .comment-list .comment-text").First;
            await Expect(savedComment).ToContainTextAsync("Bold");

            var renderedHtml = await savedComment.EvaluateAsync<string>(
                "node => node instanceof HTMLElement ? node.innerHTML : ''");
            renderedHtml.Should().Contain("<strong>Bold</strong>");
            renderedHtml.Should().Contain("href=\"https://example.com/\"");
            renderedHtml.Should().Contain("ql-indent-1");
        }
        finally
        {
            await DeleteRecordByNameAsync(createdRecordName);
            if (createdMeetingId.HasValue)
            {
                await DeleteMeetingAsync(createdMeetingId.Value);
            }
            await page.Context.CloseAsync();
        }
    }

    [Fact]
    public async Task RecordComments_ShouldLoadFiveThenExpandWhilePreservingSortOnLoadedSubset()
    {
        var page = await _fixture.NewPageAsync();
        const string recordName = "E2E paged comments record";
        int? createdMeetingId = null;

        try
        {
            var ensuredMeeting = await EnsureOpenMeetingAsync();
            if (ensuredMeeting.Created)
            {
                createdMeetingId = ensuredMeeting.MeetingId;
            }

            await CreateRecordWithCommentsAsync(recordName, _fixture.AdminOsobaId, ensuredMeeting.MeetingId, 12);

            await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=zaznamy&asUser={_fixture.AdminOsobaId}");
            await page.EvaluateAsync("() => localStorage.setItem('pmtracker.comments.sortDirection', 'asc')");
            await page.ReloadAsync();

            var card = page.Locator(".record-card", new() { HasTextString = recordName }).First;
            await Expect(card).ToBeVisibleAsync();
            await card.Locator("[data-record-toggle]").ClickAsync();

            var comments = card.Locator("[data-comment-item]");
            await Expect(comments).ToHaveCountAsync(5);
            await Expect(card.Locator("[data-record-comments-load-more]")).ToBeVisibleAsync();
            await Expect(card.Locator("[data-record-comments-load-all]")).ToBeVisibleAsync();

            var firstCommentText = (await comments.First.Locator(".comment-text").TextContentAsync())?.Trim();
            firstCommentText.Should().Contain("Paging comment 8");

            await card.Locator("[data-record-comments-load-more]").ClickAsync();
            await Expect(comments).ToHaveCountAsync(10);
            var firstAfterLoadMore = (await comments.First.Locator(".comment-text").TextContentAsync())?.Trim();
            firstAfterLoadMore.Should().Contain("Paging comment 3");

            await card.Locator("[data-record-comments-load-all]").ClickAsync();
            await Expect(comments).ToHaveCountAsync(12);
            var firstAfterLoadAll = (await comments.First.Locator(".comment-text").TextContentAsync())?.Trim();
            firstAfterLoadAll.Should().Contain("Paging comment 1");
        }
        finally
        {
            await DeleteRecordByNameAsync(recordName);
            if (createdMeetingId.HasValue)
            {
                await DeleteMeetingAsync(createdMeetingId.Value);
            }

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

    private async Task CreateRecordWithCommentsAsync(string recordName, int ownerId, int meetingId, int commentCount)
    {
        var dbOptions = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseSqlServer(_fixture.Database.ConnectionString)
            .Options;

        await using var dbContext = new PmTrackerDbContext(dbOptions);
        var subsystemId = await dbContext.ProjektSubsystemy
            .AsNoTracking()
            .Where(x => x.ProjektId == _fixture.ProjectId && x.DatumOdebrani == null)
            .OrderBy(x => x.SubsystemId)
            .Select(x => x.SubsystemId)
            .FirstAsync();
        var categoryId = await dbContext.CiselnikKategoriiZaznamu
            .Where(x => x.Kod == "U")
            .Select(x => x.Id)
            .FirstAsync();
        var taskStateId = await dbContext.CiselnikStavuUkolu
            .Where(x => !x.IsFinal)
            .OrderBy(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync()
            ?? await dbContext.CiselnikStavuUkolu
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .FirstAsync();
        var schemaVersion = await dbContext.HarmonogramSablony
            .OrderByDescending(x => x.IsAktivni)
            .ThenByDescending(x => x.Verze)
            .Select(x => (int?)x.Verze)
            .FirstOrDefaultAsync() ?? 1;
        var nextRecordNumber = (await dbContext.ProjektoveZaznamy
            .Where(x => x.ProjektId == _fixture.ProjectId)
            .Select(x => (int?)x.CisloZaznamu)
            .MaxAsync() ?? 0) + 1;

        var record = new ProjektovyZaznamEntity
        {
            ProjektId = _fixture.ProjectId,
            KategorieId = categoryId,
            AktualniTypUkoluId = null,
            StavUkoluId = taskStateId,
            CisloZaznamu = nextRecordNumber,
            Nazev = recordName,
            Cil = "Paging comments goal",
            Popis = "Paging comments popis",
            VlastnikId = ownerId,
            DatumZalozeni = DateTime.Today,
            DatumUkonceni = DateTime.Today.AddDays(30),
            SubsystemId = subsystemId,
            HarmonogramSablonaVerze = schemaVersion
        };

        dbContext.ProjektoveZaznamy.Add(record);
        await dbContext.SaveChangesAsync();

        for (var index = 1; index <= commentCount; index++)
        {
            dbContext.Vyjadreni.Add(new VyjadreniEntity
            {
                ZaznamId = record.Id,
                JednaniId = meetingId,
                AutorOsobaId = ownerId,
                TextVyjadreni = $"Paging comment {index}",
                DatumVyjadreni = new DateTime(2026, 10, index, 9, 0, 0, DateTimeKind.Utc)
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private async Task<(int MeetingId, bool Created)> EnsureOpenMeetingAsync()
    {
        var dbOptions = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseSqlServer(_fixture.Database.ConnectionString)
            .Options;

        await using var dbContext = new PmTrackerDbContext(dbOptions);
        var openStateId = await dbContext.CiselnikStavuJednani
            .Where(x => x.Kod == "OPEN")
            .Select(x => x.Id)
            .FirstAsync();

        var existingMeetingId = await dbContext.Jednani
            .AsNoTracking()
            .Where(x => x.ProjektId == _fixture.ProjectId && x.StavJednaniId == openStateId)
            .OrderByDescending(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();
        if (existingMeetingId.HasValue)
        {
            return (existingMeetingId.Value, false);
        }

        var nextMeetingNumber = (await dbContext.Jednani
            .AsNoTracking()
            .Where(x => x.ProjektId == _fixture.ProjectId)
            .Select(x => (int?)x.CisloJednani)
            .MaxAsync() ?? 0) + 1;

        var meeting = new JednaniEntity
        {
            ProjektId = _fixture.ProjectId,
            CisloJednani = nextMeetingNumber,
            DatumPlanovane = DateTime.Today,
            CasZacatek = new TimeOnly(9, 0),
            Misto = "E2E",
            StavJednaniId = openStateId
        };

        dbContext.Jednani.Add(meeting);
        await dbContext.SaveChangesAsync();
        return (meeting.Id, true);
    }

    private async Task DeleteMeetingAsync(int meetingId)
    {
        await using var connection = new SqlConnection(_fixture.Database.ConnectionString);
        await connection.OpenAsync();

        await using (var deleteAttendance = connection.CreateCommand())
        {
            deleteAttendance.CommandText = "DELETE FROM dbo.ucast WHERE jednani_id = @meetingId;";
            deleteAttendance.Parameters.AddWithValue("@meetingId", meetingId);
            await deleteAttendance.ExecuteNonQueryAsync();
        }

        await using var deleteMeeting = connection.CreateCommand();
        deleteMeeting.CommandText = "DELETE FROM dbo.jednani WHERE id = @meetingId;";
        deleteMeeting.Parameters.AddWithValue("@meetingId", meetingId);
        await deleteMeeting.ExecuteNonQueryAsync();
    }
}
