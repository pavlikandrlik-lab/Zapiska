using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Schedules;

namespace PmTracker.Web.Services;

public sealed partial class ProjectService
{
    private async Task<List<ProjektHarmonogramUkolViewModel>> BuildProjectScheduleRowsAsync(IReadOnlyList<ZaznamCardViewModel> records, CancellationToken ct)
    {
        var taskRecords = records
            .Where(x => x.JeUkol)
            .ToList();

        if (taskRecords.Count == 0)
        {
            return [];
        }

        var taskRecordIds = taskRecords.Select(x => x.Id).ToList();

        // Datum-model: kroky harmonogramu (plan_datum + skutecnost_datum) z nové tabulky.
        var krokRows = await dbContext.ZaznamHarmonogramKroky.AsNoTracking()
            .Where(x => taskRecordIds.Contains(x.ZaznamId))
            .ToListAsync(ct);
        var krokyByRecord = krokRows
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<ZaznamHarmonogramKrokEntity>)g.ToList());
        var emptyKroky = (IReadOnlyCollection<ZaznamHarmonogramKrokEntity>)Array.Empty<ZaznamHarmonogramKrokEntity>();

        var ownerIds = taskRecords.Select(x => x.AktualniVlastnikId).Distinct().ToList();
        var ownerRows = await dbContext.Osoby.AsNoTracking()
            .Where(x => ownerIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.Titul,
                x.Jmeno,
                x.Prijmeni
            })
            .ToListAsync(ct);
        var ownerById = ownerRows.ToDictionary(x => x.Id);

        var todayDate = timeProvider.GetUtcNow().UtcDateTime.Date;

        return taskRecords
            .Select(record =>
            {
                var deadline = (record.AktualniTermin ?? record.DatumZalozeni).Date;
                var kroky = krokyByRecord.TryGetValue(record.Id, out var k) ? k : emptyKroky;

                var souhrn = HarmonogramDateBlokBuilder.BuildSouhrn(record.DatumZalozeni, kroky, deadline, todayDate);
                var owner = ownerById.GetValueOrDefault(record.AktualniVlastnikId);
                var ownerDisplay = owner is null
                    ? record.AktualniVlastnik
                    : BuildDisplayName(owner.Titul, owner.Jmeno, owner.Prijmeni, owner.Id);

                if (souhrn.CelkoveTrvaniDni <= 0)
                {
                    return null;
                }

                var sharedSteps = HarmonogramDateBlokBuilder.BuildKroky(record.DatumZalozeni, kroky, todayDate);
                var barLayout = HarmonogramDateBlokBuilder.BuildBarLayout(record.DatumZalozeni, kroky, deadline, todayDate);

                return new ProjektHarmonogramUkolViewModel
                {
                    ZaznamId = record.Id,
                    CisloViditelne = record.CisloViditelne,
                    Nazev = record.Nazev,
                    TypUkolu = record.TypUkolu,
                    TypUkoluKod = record.TypUkoluKod,
                    Stav = record.Stav,
                    StavKod = record.StavKod,
                    SubsystemKod = record.AktualniSubsystemKod,
                    Subsystem = record.AktualniSubsystem,
                    SubsystemPoradi = record.AktualniSubsystemPoradi,
                    SubsystemHasProjectOrder = record.AktualniSubsystemHasProjectOrder,
                    Vlastnik = ownerDisplay,
                    VlastnikOsobaId = record.AktualniVlastnikId,
                    KategorieKod = record.KategorieKod,
                    Kategorie = record.KategorieNazev,
                    JeAktivni = record.IsAktivniStav,
                    JednaniVyjadreniStavyKody = record.VyjadreniJednaniStavyKody,
                    Stihame = souhrn.Stihame,
                    HarmonogramBlok = BuildScheduleBlockViewModel(
                        record.Id,
                        "project-readonly",
                        record.DatumZalozeni,
                        deadline,
                        "#dc2626",
                        souhrn,
                        sharedSteps,
                        overviewLayout: barLayout)
                };
            })
            .Where(x => x is not null)
            .Cast<ProjektHarmonogramUkolViewModel>()
            .ToList();
    }
}
