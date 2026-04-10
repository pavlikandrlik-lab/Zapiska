using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;

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
        var harmonogramRows = await dbContext.ZaznamHarmonogramHodnoty.AsNoTracking()
            .Where(x => taskRecordIds.Contains(x.ZaznamId))
            .ToListAsync(ct);
        var harmonogramByRecord = harmonogramRows
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyDictionary<int, int>)group.ToDictionary(item => item.TypId, item => item.HodnotaInt));
        var schemaCache = new Dictionary<int, HarmonogramSchemaDefinition>();
        var ownerIds = taskRecords.Select(x => x.AktualniVlastnikId).Distinct().ToList();
        var ownerRows = await dbContext.Osoby.AsNoTracking()
            .Where(x => ownerIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.Titul,
                x.Jmeno,
                x.Prijmeni,
                x.OrganizacniCelekId
            })
            .ToListAsync(ct);
        var ownerById = ownerRows.ToDictionary(x => x.Id);
        var ownerOrgUnitIds = ownerById.Values
            .Where(x => x.OrganizacniCelekId.HasValue)
            .Select(x => x.OrganizacniCelekId!.Value)
            .Distinct()
            .ToList();
        var ownerOrgCodeRows = await dbContext.CiselnikOrganizacniCelky.AsNoTracking()
            .Where(x => ownerOrgUnitIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Kod })
            .ToListAsync(ct);
        var ownerOrgCodes = ownerOrgCodeRows.ToDictionary(x => x.Id, x => string.IsNullOrWhiteSpace(x.Kod) ? null : x.Kod.Trim());
        var schemaVersions = taskRecords
            .Select(x => x.HarmonogramSablonaVerze > 0 ? x.HarmonogramSablonaVerze : 0)
            .Distinct()
            .ToList();
        foreach (var schemaVersion in schemaVersions)
        {
            schemaCache[schemaVersion] = await harmonogramService.GetSchemaForRecordAsync(schemaVersion, ct);
        }

        var today = timeProvider.GetLocalNow().LocalDateTime.Date;

        return taskRecords
            .Select(record =>
            {
                var deadline = (record.AktualniTermin ?? record.DatumZalozeni).Date;
                var schemaVersion = record.HarmonogramSablonaVerze > 0 ? record.HarmonogramSablonaVerze : 0;
                var schema = schemaCache[schemaVersion];

                var harmonogramHodnoty = harmonogramByRecord.TryGetValue(record.Id, out var harmonogramValues)
                    ? harmonogramValues
                    : EmptyIntMap;
                var vypocet = harmonogramService.BuildHarmonogramVypocetPublic(record.DatumZalozeni, schema.Kroky, harmonogramHodnoty);
                var souhrn = harmonogramService.BuildHarmonogramSouhrn(vypocet, deadline);
                var owner = ownerById.GetValueOrDefault(record.AktualniVlastnikId);
                var ownerOrgCode = owner?.OrganizacniCelekId is int orgId ? ownerOrgCodes.GetValueOrDefault(orgId) : null;
                var ownerDisplay = owner is null
                    ? record.AktualniVlastnik
                    : BuildDisplayName(owner.Titul, owner.Jmeno, owner.Prijmeni, owner.Id);
                var hasVisualDuration = souhrn.CelkoveTrvaniDni > 0;

                if (!hasVisualDuration)
                {
                    return null;
                }

                var kroky = vypocet
                    .Select(krok => new ProjektHarmonogramKrokViewModel
                    {
                        KrokIndex = krok.KrokIndex,
                        Nazev = krok.Nazev,
                        TrvaniDni = krok.TrvaniDni,
                        OdchylkaDni = krok.ZpozdeniDni,
                        BarvaHex = krok.BarvaHex,
                        PlanStart = krok.PlanStartDatum.Date,
                        PlanEnd = krok.BaselineDatum.Date,
                        RealStart = krok.RealStartDatum.Date,
                        RealEnd = krok.PosunuteDatum.Date
                    })
                    .ToList();

                var compactAxisStart = record.DatumZalozeni.Date;
                var compactAxisEnd = new[] { deadline, souhrn.SkutecneDokonceni.Date, compactAxisStart }.Max();
                var compactAxisDays = Math.Max(1, (compactAxisEnd - compactAxisStart).Days);
                var breakdownDates = kroky
                    .SelectMany(krok => new[] { krok.PlanStart, krok.PlanEnd, krok.RealStart, krok.RealEnd })
                    .ToList();
                var breakdownAxisStart = breakdownDates.Count > 0 ? breakdownDates.Min() : compactAxisStart;
                var breakdownAxisEnd = breakdownDates.Count > 0
                    ? new[] { breakdownDates.Max(), deadline, breakdownAxisStart }.Max()
                    : compactAxisEnd;
                var breakdownAxisDays = Math.Max(1, (breakdownAxisEnd - breakdownAxisStart).Days);
                ApplyProjectScheduleVisuals(kroky, compactAxisStart, compactAxisDays, breakdownAxisStart, breakdownAxisDays);

                var compactDeadlinePercent = ToAxisPercent(deadline, compactAxisStart, compactAxisDays);
                var compactTodayPercent = ToAxisPercent(today, compactAxisStart, compactAxisDays);
                var breakdownTodayPercent = ToAxisPercent(today, breakdownAxisStart, breakdownAxisDays);

                return new ProjektHarmonogramUkolViewModel
                {
                    ZaznamId = record.Id,
                    CisloZaznamu = record.CisloZaznamu,
                    CisloViditelne = record.CisloViditelne,
                    Nazev = record.Nazev,
                    KategorieKod = record.KategorieKod,
                    KategorieNazev = record.KategorieNazev,
                    TypUkoluKod = record.TypUkoluKod,
                    TypUkolu = record.TypUkolu,
                    Stav = record.Stav,
                    StavKod = record.StavKod,
                    SubsystemKod = record.AktualniSubsystemKod,
                    Subsystem = record.AktualniSubsystem,
                    Vlastnik = ownerDisplay,
                    VlastnikOrgKod = ownerOrgCode,
                    VlastnikId = record.AktualniVlastnikId,
                    IsAktivniStav = record.IsAktivniStav,
                    DatumZalozeni = record.DatumZalozeni.Date,
                    TerminUkonceni = deadline,
                    BaselineDokonceni = souhrn.BaselineDokonceni,
                    SkutecneDokonceni = souhrn.SkutecneDokonceni,
                    CelkoveTrvaniDni = souhrn.CelkoveTrvaniDni,
                    CelkovaOdchylkaDni = souhrn.CelkovaOdchylkaDni,
                    DelkaDoTerminuDni = Math.Max(0, (deadline - record.DatumZalozeni.Date).Days),
                    Stihame = souhrn.Stihame,
                    PrekroceniDni = souhrn.PrekroceniDni,
                    DelayBarvaHex = schema.DelayBarvaHex,
                    MaVizualniTrvani = hasVisualDuration,
                    Kroky = kroky,
                    CompactAxisStart = compactAxisStart,
                    CompactAxisEnd = compactAxisEnd,
                    BreakdownAxisStart = breakdownAxisStart,
                    BreakdownAxisEnd = breakdownAxisEnd,
                    CompactDeadlinePercent = compactDeadlinePercent,
                    CompactTodayPercent = compactTodayPercent,
                    BreakdownTodayPercent = breakdownTodayPercent,
                    FormatCompactDeadlinePercent = FormatPercent(compactDeadlinePercent),
                    FormatCompactTodayPercent = FormatPercent(compactTodayPercent),
                    FormatBreakdownTodayPercent = FormatPercent(breakdownTodayPercent),
                    CompactTodayTitle = $"Dnes: {today:dd.MM.yyyy}"
                };
            })
            .Where(x => x is not null)
            .Cast<ProjektHarmonogramUkolViewModel>()
            .ToList();
    }

    private static void ApplyProjectScheduleVisuals(
        IReadOnlyList<ProjektHarmonogramKrokViewModel> kroky,
        DateTime compactAxisStart,
        int compactAxisDays,
        DateTime breakdownAxisStart,
        int breakdownAxisDays)
    {
        var previousPlanRight = 0d;
        var previousActualRight = 0d;
        var skippedCompactActualWidth = 0d;

        foreach (var krok in kroky)
        {
            var rawPlanLeft = ToAxisPercent(krok.PlanStart, compactAxisStart, compactAxisDays);
            var rawPlanRight = ToAxisPercent(krok.PlanEnd, compactAxisStart, compactAxisDays);
            var rawActualLeft = ToAxisPercent(krok.RealStart, compactAxisStart, compactAxisDays);
            var rawActualRight = ToAxisPercent(krok.RealEnd, compactAxisStart, compactAxisDays);
            krok.HasCompactVisualDuration = krok.TrvaniDni > 0;

            if (krok.HasCompactVisualDuration)
            {
                var planLeft = Math.Max(previousPlanRight, rawPlanLeft);
                var planRight = Math.Max(planLeft, rawPlanRight);
                var planWidth = Math.Max(0d, planRight - planLeft);
                krok.CompactPlanLeftPercent = FormatPercent(planLeft);
                krok.CompactPlanWidthStyle = planWidth > 0d
                    ? $"calc({FormatPercent(planWidth)}% + 1px)"
                    : "0%";
                krok.CompactPlanTitle = $"{krok.Nazev}: plán {krok.PlanStart:dd.MM.yyyy} - {krok.PlanEnd:dd.MM.yyyy}";
                previousPlanRight = planRight;

                var adjustedActualLeft = Math.Max(0d, rawActualLeft - skippedCompactActualWidth);
                var adjustedActualRight = Math.Max(adjustedActualLeft, rawActualRight - skippedCompactActualWidth);
                var actualLeft = Math.Max(previousActualRight, adjustedActualLeft);
                var actualRight = Math.Max(actualLeft, adjustedActualRight);
                var actualWidth = Math.Max(0d, actualRight - actualLeft);
                krok.CompactActualLeftPercent = FormatPercent(actualLeft);
                krok.CompactActualWidthStyle = actualWidth > 0d
                    ? $"calc({FormatPercent(actualWidth)}% + 1px)"
                    : "0%";
                krok.CompactActualTitle = $"{krok.Nazev}: skutečnost {krok.RealStart:dd.MM.yyyy} - {krok.RealEnd:dd.MM.yyyy}";
                previousActualRight = actualRight;
            }
            else
            {
                krok.CompactPlanLeftPercent = FormatPercent(rawPlanLeft);
                krok.CompactPlanWidthStyle = "0%";
                krok.CompactPlanTitle = $"{krok.Nazev}: plán {krok.PlanStart:dd.MM.yyyy} - {krok.PlanEnd:dd.MM.yyyy}";
                krok.CompactActualLeftPercent = FormatPercent(Math.Max(0d, rawActualLeft - skippedCompactActualWidth));
                krok.CompactActualWidthStyle = "0%";
                krok.CompactActualTitle = $"{krok.Nazev}: skutečnost {krok.RealStart:dd.MM.yyyy} - {krok.RealEnd:dd.MM.yyyy}";
                skippedCompactActualWidth += Math.Max(0d, rawActualRight - rawActualLeft);
            }

            var breakdownPlanLeft = ToAxisPercent(krok.PlanStart, breakdownAxisStart, breakdownAxisDays);
            var breakdownPlanRight = ToAxisPercent(krok.PlanEnd, breakdownAxisStart, breakdownAxisDays);
            var breakdownActualLeft = ToAxisPercent(krok.RealStart, breakdownAxisStart, breakdownAxisDays);
            var breakdownActualRight = ToAxisPercent(krok.RealEnd, breakdownAxisStart, breakdownAxisDays);
            krok.BreakdownPlanLeftPercent = FormatPercent(breakdownPlanLeft);
            krok.BreakdownPlanWidthPercent = FormatPercent(Math.Max(0d, breakdownPlanRight - breakdownPlanLeft));
            krok.BreakdownActualLeftPercent = FormatPercent(breakdownActualLeft);
            krok.BreakdownActualWidthPercent = FormatPercent(Math.Max(0d, breakdownActualRight - breakdownActualLeft));
            krok.HasBreakdownVisualDuration = krok.TrvaniDni > 0;
            krok.OffsetLabel = krok.OdchylkaDni > 0
                ? $"+{krok.OdchylkaDni} dnů"
                : $"{krok.OdchylkaDni} dnů";
            krok.OffsetCssClass = krok.OdchylkaDni > 0
                ? "late"
                : krok.OdchylkaDni < 0
                    ? "ahead"
                    : null;
        }
    }

    private static double ToAxisPercent(DateTime value, DateTime axisStart, int axisDays)
        => Math.Clamp(((value.Date - axisStart.Date).Days * 100d) / axisDays, 0d, 100d);

    private static string FormatPercent(double value)
        => value.ToString("0.####", CultureInfo.InvariantCulture);
}
