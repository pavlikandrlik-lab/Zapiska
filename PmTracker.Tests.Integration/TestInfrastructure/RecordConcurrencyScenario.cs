using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Integration.TestInfrastructure;

/// <summary>
/// Spec 2026-09-17 — sdílený seed pro testy souběžné editace: projekt, subsystém,
/// osoba a jeden záznam kategorie úkol s krok řádky 1–10.
/// </summary>
internal sealed record RecordConcurrencyScenario(
    int RecordId,
    int ProjectId,
    int OsobaId,
    string CategoryCode,
    string StatusCode,
    string SubsystemCode,
    CurrentUserContextViewModel CurrentUser)
{
    public static async Task<RecordConcurrencyScenario> SeedAsync(
        PmTrackerDbContext dbContext,
        IntegrationTestDataStore store,
        string marker)
    {
        var osobaId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, $"{marker}Osoba");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, marker);
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, $"{marker}_SUB", osobaId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, osobaId, "HOST");

        // Kategorie úkol: kód se mezi produkcí (U) a dev seedem liší, proto se hledá
        // podle názvu — „Úkol". Harmonogram existuje jen u této kategorie.
        var categoryCode = await dbContext.CiselnikKategoriiZaznamu.AsNoTracking()
            .Where(x => x.Nazev.Contains("kol"))
            .OrderBy(x => x.Id)
            .Select(x => x.Kod)
            .FirstAsync();
        var statusCode = await dbContext.CiselnikStavuUkolu.AsNoTracking()
            .OrderBy(x => x.Id).Select(x => x.Kod).FirstAsync();
        var subsystemCode = await dbContext.Subsystemy.AsNoTracking()
            .Where(x => x.Id == subsystemId).Select(x => x.Kod).SingleAsync();

        var currentUser = IntegrationTestHelper.BuildUser(osobaId, isSuperAdmin: true);
        var recordId = store.SaveRecord(new SaveRecordCommand
        {
            ProjektId = projectId,
            Kategorie = categoryCode,
            Stav = statusCode,
            Nazev = $"{marker} zaznam",
            VlastnikId = osobaId,
            DatumZalozeni = new DateTime(2026, 9, 1),
            TerminUkonceni = new DateTime(2026, 12, 31),
            Subsystem = subsystemCode,
            HarmonogramHodnoty = BuildPlanRows()
        }, currentUser);

        return new RecordConcurrencyScenario(
            recordId, projectId, osobaId, categoryCode, statusCode, subsystemCode, currentUser)
        {
            OriginalNazev = $"{marker} zaznam"
        };
    }

    /// <summary>Plán pro kroky 1–10 — bez něj PersistScheduleKroky neudělá nic.</summary>
    public static List<SaveRecordHarmonogramValueCommand> BuildPlanRows()
        => Enumerable.Range(1, 10)
            .Select(i => new SaveRecordHarmonogramValueCommand
            {
                Poradi = i,
                PlanDatum = new DateTime(2026, 9, 1).AddDays(i * 7)
            })
            .ToList();

    /// <summary>Command, který mění jen název — harmonogram posílá beze změny.</summary>
    public SaveRecordCommand BuildRenameCommand(string novyNazev) => new()
    {
        Id = RecordId,
        ProjektId = ProjectId,
        Kategorie = CategoryCode,
        Stav = StatusCode,
        Nazev = novyNazev,
        VlastnikId = OsobaId,
        DatumZalozeni = new DateTime(2026, 9, 1),
        TerminUkonceni = new DateTime(2026, 12, 31),
        Subsystem = SubsystemCode,
        HarmonogramHodnoty = BuildPlanRows()
    };

    /// <summary>Command, který mění jediné plánové datum — název zůstává původní.</summary>
    public SaveRecordCommand BuildScheduleOnlyCommand(int poradi, DateTime planDatum)
    {
        var command = BuildRenameCommand(OriginalNazev);
        var row = command.HarmonogramHodnoty.Single(x => x.Poradi == poradi);
        row.PlanDatum = planDatum;
        return command;
    }

    /// <summary>Command s ručním datem skutečnosti pro zadaný krok a režim.</summary>
    public SaveRecordCommand BuildManualActualCommand(string rezim, int poradi, DateOnly datum)
    {
        var command = BuildRenameCommand(OriginalNazev);
        command.HarmonogramRezim = rezim;
        command.ManualActualKroky = new List<ManualActualKrokDto>
        {
            new() { Poradi = poradi, AbsolutniDatum = datum }
        };
        return command;
    }

    /// <summary>Název, se kterým byl záznam založen — commandy, které název nemění, ho posílají zpět.</summary>
    public string OriginalNazev { get; init; } = string.Empty;
}
