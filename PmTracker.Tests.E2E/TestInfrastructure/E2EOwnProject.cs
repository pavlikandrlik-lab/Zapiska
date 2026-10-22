using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.E2E.TestInfrastructure;

/// <summary>
/// Vlastní projekt pro E2E (spec 2026-10-22). Má unikátní kód, žádné záznamy jiných testů a žádné
/// uložené výchozí filtry, takže test zná přesnou sadu karet. Dva subsystémy jsou v pořadí projektu
/// proti abecedě: „… B“ má pořadí 1, „… A“ pořadí 2. Úklid není potřeba — E2E databáze vzniká pro
/// každý běh (stejně jako u MeetingOverviewYearGroupingScenariosTests).
/// </summary>
public sealed class E2EOwnProject
{
    private readonly string _connectionString;
    private readonly int _ownerOsobaId;

    private E2EOwnProject(string connectionString, int ownerOsobaId, int projectId, int firstSubsystemId, int secondSubsystemId)
    {
        _connectionString = connectionString;
        _ownerOsobaId = ownerOsobaId;
        ProjectId = projectId;
        FirstSubsystemId = firstSubsystemId;
        SecondSubsystemId = secondSubsystemId;
    }

    public int ProjectId { get; }

    /// <summary>Subsystém s pořadím 1 (název „… B“).</summary>
    public int FirstSubsystemId { get; }

    /// <summary>Subsystém s pořadím 2 (název „… A“).</summary>
    public int SecondSubsystemId { get; }

    private static PmTrackerDbContext CreateDbContext(string connectionString) => new(
        new DbContextOptionsBuilder<PmTrackerDbContext>().UseSqlServer(connectionString).Options);

    public static async Task<E2EOwnProject> CreateAsync(string connectionString, int ownerOsobaId)
    {
        await using var db = CreateDbContext(connectionString);
        var code = $"E2E{Guid.NewGuid():N}"[..12];
        var project = new ProjektEntity
        {
            Zkratka = code,
            CelyNazev = $"E2E vlastni projekt {code}",
            // Běžící projekt — první řádek číselníku je DELETED (projekt jen pro čtení, bez vyjádření).
            StavId = await db.CiselnikStavuProjektu.Where(x => x.Kod == "RUN").Select(x => x.Id).FirstAsync(),
            PouzivatIdentJednani = false
        };
        var first = new SubsystemEntity { Kod = $"{code}B", Nazev = $"{code} B" };
        var second = new SubsystemEntity { Kod = $"{code}A", Nazev = $"{code} A" };
        db.Projekty.Add(project);
        db.Subsystemy.AddRange(first, second);
        await db.SaveChangesAsync();
        db.ProjektSubsystemy.AddRange(
            new ProjektSubsystemEntity { ProjektId = project.Id, SubsystemId = first.Id, Poradi = 1, DatumPrirazeni = DateTime.Today.AddDays(-90) },
            new ProjektSubsystemEntity { ProjektId = project.Id, SubsystemId = second.Id, Poradi = 2, DatumPrirazeni = DateTime.Today.AddDays(-90) });
        await db.SaveChangesAsync();
        return new E2EOwnProject(connectionString, ownerOsobaId, project.Id, first.Id, second.Id);
    }

    /// <summary>Úkol založený před 30 dny. <paramref name="closed"/> = konečný stav, <paramref name="withSchedule"/> = plán 10 kroků po týdnu.</summary>
    public async Task<int> AddTaskAsync(int cisloZaznamu, int subsystemId, bool closed = false, bool withSchedule = false)
    {
        await using var db = CreateDbContext(_connectionString);
        var categoryId = await db.CiselnikKategoriiZaznamu.Where(x => x.Kod == "U").Select(x => x.Id).FirstAsync();
        var stateId = await db.CiselnikStavuUkolu.Where(x => x.IsFinal == closed).OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
        var zalozeni = DateTime.Today.AddDays(-30);
        var record = new ProjektovyZaznamEntity
        {
            ProjektId = ProjectId,
            KategorieId = categoryId,
            StavUkoluId = stateId,
            CisloZaznamu = cisloZaznamu,
            Nazev = $"E2E zaznam {cisloZaznamu}",
            Cil = $"E2E cil {cisloZaznamu}",
            Popis = $"E2E popis {cisloZaznamu}",
            VlastnikId = _ownerOsobaId,
            DatumZalozeni = zalozeni,
            DatumUkonceni = DateTime.Today.AddDays(45),
            SubsystemId = subsystemId
        };
        db.ProjektoveZaznamy.Add(record);
        await db.SaveChangesAsync();

        if (withSchedule)
        {
            for (var poradi = 1; poradi <= 10; poradi++)
            {
                db.ZaznamHarmonogramKroky.Add(new ZaznamHarmonogramKrokEntity
                {
                    ZaznamId = record.Id,
                    Poradi = (byte)poradi,
                    PlanDatum = zalozeni.AddDays(7 * poradi),
                    SkutecnostRezim = HarmonogramManualSteps.IsManual(poradi)
                        ? (byte)SkutecnostRezimEnum.Manual
                        : (byte)SkutecnostRezimEnum.Auto,
                    SkutecnostZdroj = (byte)SkutecnostZdrojEnum.Neznamo,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            await db.SaveChangesAsync();
        }

        return record.Id;
    }

    /// <summary>Otevřené jednání; datum volit v minulosti (memory: jednání s dnešním datem odsouvá jiná).</summary>
    public async Task<int> AddOpenMeetingAsync(int cisloJednani, DateTime datum)
    {
        await using var db = CreateDbContext(_connectionString);
        var meeting = new JednaniEntity
        {
            ProjektId = ProjectId,
            CisloJednani = cisloJednani,
            DatumPlanovane = datum,
            CasZacatek = new TimeOnly(9, 0),
            Misto = "E2E vlastni projekt",
            StavJednaniId = await db.CiselnikStavuJednani.Where(x => x.Kod == "OPEN").Select(x => x.Id).FirstAsync()
        };
        db.Jednani.Add(meeting);
        await db.SaveChangesAsync();
        return meeting.Id;
    }
}
