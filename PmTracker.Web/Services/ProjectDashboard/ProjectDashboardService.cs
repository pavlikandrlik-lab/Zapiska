using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.ProjectDashboard.Zakladni;
using PmTracker.Web.Services.Schedules;
using PmTracker.Web.Services.ServiceDesk;
using PmTracker.Web.Services.ServiceDesk.Prodleni;

namespace PmTracker.Web.Services.ProjectDashboard;

public sealed class ProjectDashboardService : IProjectDashboardService
{
    private readonly PmTrackerDbContext _dbContext;
    private readonly IProdleniService _prodleni;

    public ProjectDashboardService(PmTrackerDbContext dbContext, IProdleniService prodleni)
    {
        _dbContext = dbContext;
        _prodleni = prodleni;
    }

    public async Task<ProjectDashboardPageViewModel> BuildDashboardPageAsync(int projectId, CancellationToken ct = default)
    {
        var radek = await _dbContext.Projekty.AsNoTracking()
            .Where(p => p.Id == projectId)
            .Join(_dbContext.CiselnikStavuProjektu.AsNoTracking(), p => p.StavId, s => s.Id, (p, s) => new { p, s })
            .Select(x => new
            {
                Header = new ProjektHeaderViewModel
                {
                    Id = x.p.Id,
                    Nazev = x.p.CelyNazev,
                    Zkratka = x.p.Zkratka,
                    Stav = x.s.Nazev
                },
                x.p.ServiceDeskInfoSystemId
            })
            .FirstOrDefaultAsync(ct);

        if (radek is null)
        {
            throw new InvalidOperationException($"Projekt s ID {projectId} nebyl nalezen.");
        }

        return new ProjectDashboardPageViewModel
        {
            Projekt = radek.Header,
            RecordsPanelUrl = $"/projekty/{projectId}/dashboard/records-panel",
            NesPanelUrl = $"/projekty/{projectId}/dashboard/nes-panel",
            StatisticsPanelUrl = $"/projekty/{projectId}/dashboard/statistics-panel",
            SouhrnUrl = $"/projekty/{projectId}/dashboard/souhrn",
            BackUrl = $"/Projekty/Detail/{projectId}",
            IsZkratka = radek.ServiceDeskInfoSystemId is int isId ? SdInfoSystemy.ById(isId)?.Zkratka : null
        };
    }

    internal static ProjectDashboardStepDetailViewModel MapStepDetail(
        ScheduleStepSnapshot step, DashboardRecordCategory category, DateTime referenceDate)
        => new()
        {
            StepName = step.Name,
            PlannedDate = step.PlanEndDate,
            // Jen skutečně vyplněná skutečnost — u kroku bez ní je ActualEndDate dosazený konec plánu.
            ActualDate = step.MaSkutecnost ? step.ActualEndDate : null,
            OffsetDays = step.OffsetDays,
            DaysSincePlanExpired = category == DashboardRecordCategory.AwaitingActual
                ? (referenceDate - step.PlanEndDate).Days : 0
        };

    public async Task<ProjectDashboardRecordsPanelViewModel> BuildRecordsPanelAsync(int projectId, DateTime referenceDate, CancellationToken ct = default)
    {
        var taskCategoryIds = await _dbContext.CiselnikKategoriiZaznamu.AsNoTracking()
            .Where(c => c.Kod == RecordCategoryCodes.TaskShort || c.Kod == RecordCategoryCodes.Task)
            .Select(c => c.Id)
            .ToListAsync(ct);

        if (taskCategoryIds.Count == 0)
        {
            return new ProjectDashboardRecordsPanelViewModel();
        }

        var finalStateIds = await _dbContext.CiselnikStavuUkolu.AsNoTracking()
            .Where(s => s.IsFinal)
            .Select(s => s.Id)
            .ToListAsync(ct);

        var records = await (
                from record in _dbContext.ProjektoveZaznamy.AsNoTracking()
                join subsystem in _dbContext.Subsystemy.AsNoTracking() on record.SubsystemId equals subsystem.Id
                join owner in _dbContext.Osoby.AsNoTracking() on record.VlastnikId equals owner.Id
                where record.ProjektId == projectId
                    && taskCategoryIds.Contains(record.KategorieId)
                    && (!record.StavUkoluId.HasValue || !finalStateIds.Contains(record.StavUkoluId.Value))
                orderby record.DatumZalozeni descending
                select new
                {
                    record.Id,
                    record.CisloViditelne,
                    record.Nazev,
                    record.DatumZalozeni,
                    record.DatumUkonceni,
                    SubsystemKod = subsystem.Kod,
                    SubsystemNazev = subsystem.Nazev,
                    VlastnikJmeno = owner.Jmeno + " " + owner.Prijmeni
                })
            .Take(500)
            .ToListAsync(ct);

        if (records.Count == 0)
        {
            return new ProjectDashboardRecordsPanelViewModel();
        }

        var recordIds = records.Select(r => r.Id).ToList();
        // Datum-model: kroky harmonogramu (plan_datum + skutecnost_datum) z nové tabulky.
        var krokRows = await _dbContext.ZaznamHarmonogramKroky.AsNoTracking()
            .Where(k => recordIds.Contains(k.ZaznamId))
            .ToListAsync(ct);
        var krokyByRecord = krokRows
            .GroupBy(k => k.ZaznamId)
            .ToDictionary(g => g.Key, g => g.GroupBy(x => (int)x.Poradi).ToDictionary(x => x.Key, x => x.First()));

        var dashboardRecords = new List<ProjectDashboardRecordRowViewModel>();

        foreach (var record in records)
        {
            if (!krokyByRecord.TryGetValue(record.Id, out var krokByPoradi) || krokByPoradi.Count == 0)
            {
                continue;
            }

            var steps = HarmonogramKroky.Vse
                .Select(def =>
                {
                    krokByPoradi.TryGetValue(def.Poradi, out var row);
                    return new ScheduleDateStep(def.Poradi, row?.PlanDatum, row?.SkutecnostDatum);
                })
                .ToList();
            var snapshots = DashboardScheduleSnapshotBuilder.Build(record.DatumZalozeni, steps, referenceDate);

            var categorization = DashboardRecordCategorizer.CategorizeRecord(
                snapshots, referenceDate, DashboardRecordCategorizer.DefaultApproachingThresholdDays);

            if (categorization.ProblematicSteps.Count == 0)
            {
                continue;
            }

            dashboardRecords.Add(new ProjectDashboardRecordRowViewModel
            {
                RecordId = record.Id,
                CisloViditelne = record.CisloViditelne ?? record.Id.ToString(),
                Nazev = record.Nazev,
                Subsystem = record.SubsystemNazev,
                SubsystemKod = record.SubsystemKod,
                Vlastnik = record.VlastnikJmeno,
                TerminUkonceni = record.DatumUkonceni,
                Category = categorization.Category,
                WorstOffsetDays = categorization.WorstOffsetDays,
                ProblematicSteps = categorization.ProblematicSteps
                    .Select(ps => MapStepDetail(ps.Step, ps.StepCategory, referenceDate))
                    .ToList(),
                ScheduleUrl = $"/Projekty/Detail/{projectId}?tab=harmonogram&recordId={record.Id}"
            });
        }

        dashboardRecords.Sort((a, b) =>
        {
            var aKey = DashboardRecordCategorizer.SortKey((a.Category, a.WorstOffsetDays));
            var bKey = DashboardRecordCategorizer.SortKey((b.Category, b.WorstOffsetDays));
            var cmp = aKey.Severity.CompareTo(bKey.Severity);
            return cmp != 0 ? cmp : aKey.NegatedOffset.CompareTo(bKey.NegatedOffset);
        });

        return new ProjectDashboardRecordsPanelViewModel { Records = dashboardRecords };
    }

    /// <summary>
    /// Záložka NES v prodlení (spec 2026-10-15 §10): přehled počítá <see cref="IProdleniService"/>
    /// (penalizační algoritmus), tady se z něj skládá jen KPI řada a dva žebříčkové grafy.
    /// </summary>
    public async Task<ProjectDashboardNesPanelViewModel> BuildNesPanelAsync(
        int projektId, DateTime reference, CancellationToken ct = default)
    {
        var prehled = await _prodleni.GetPrehledAsync(projektId, ct);
        if (prehled.Stav != ProdleniStavDat.Ok)
        {
            return new ProjectDashboardNesPanelViewModel { Prehled = prehled, ProjektId = projektId, Nyni = reference };
        }

        var tickety = prehled.VProdleni.Select(p => p.TicketId).Distinct().Count();
        var hrozi = prehled.Hrozi.Select(p => p.TicketId).Distinct().Count();
        var nelze = prehled.NelzeVyhodnotit.Count;

        var kpi = ChartData.ForKpi("nes-kpi", "Prodlení ticketů u dodavatele",
        [
            new StatCard("V prodlení u dodavatele", tickety.ToString(CultureInfo.InvariantCulture),
                Status: tickety > 0 ? CardStatus.Critical : CardStatus.Good),
            new StatCard("Penalizovaných dnů", prehled.SoucetDni.ToString(CultureInfo.InvariantCulture),
                Note: "součet za tickety v prodlení"),
            new StatCard("Hrozí prodlení", hrozi.ToString(CultureInfo.InvariantCulture),
                Note: $"NES do {ProdleniService.HroziNesHodin} h SLA, PMP/PNF do {ProdleniService.HroziTerminDni} dní",
                Status: hrozi > 0 ? CardStatus.Warning : CardStatus.Neutral),
            new StatCard("Nelze vyhodnotit", nelze.ToString(CultureInfo.InvariantCulture),
                Note: nelze > 0 ? "chyba dat ve ServiceDesku" : null,
                Status: nelze > 0 ? CardStatus.Warning : CardStatus.Neutral),
        ]);

        // Žebříček: jeden odstín, seřazeno sestupně (spec §12 — dodavatelé nikdy ne duhou).
        var dodavatele = prehled.VProdleni
            .GroupBy(p => string.IsNullOrWhiteSpace(p.Dodavatel) ? "(neuvedeno)" : p.Dodavatel!.Trim())
            .Select(g => (Nazev: g.Key, Dny: g.Sum(p => p.PenalizovaneDny)))
            .OrderByDescending(x => x.Dny).ThenBy(x => x.Nazev, StringComparer.CurrentCulture)
            .ToList();
        var dnyPodleDodavatele = ChartData.ForSeries("nes-dny-podle-dodavatele", ChartKind.HorizontalBar,
            "Penalizované dny podle dodavatele",
            dodavatele.Select(x => x.Nazev).ToList(),
            [new ChartSeries("Penalizované dny", dodavatele.Select(x => (double)x.Dny).ToList())],
            insight: dodavatele.Count > 0
                ? $"Nejvíc {dodavatele[0].Nazev}: {PenaltyReasonFormatter.Dny(dodavatele[0].Dny)}"
                : null) with
        {
            Unit = "dní",
            EmptyMessage = "Žádný ticket u dodavatele není v prodlení.",
        };

        var typy = prehled.VProdleni
            .GroupBy(p => p.TypZaznamu.Trim().ToUpperInvariant())
            .Select(g => (Typ: g.Key, Pocet: g.Select(p => p.TicketId).Distinct().Count()))
            .OrderByDescending(x => x.Pocet).ThenBy(x => x.Typ, StringComparer.Ordinal)
            .ToList();
        var podleTypu = ChartData.ForSeries("nes-podle-typu", ChartKind.HorizontalBar,
            "V prodlení podle typu",
            typy.Select(x => x.Typ).ToList(),
            [new ChartSeries("Tickety v prodlení", typy.Select(x => (double)x.Pocet).ToList())]) with
        {
            EmptyMessage = "Žádný ticket u dodavatele není v prodlení.",
        };

        return new ProjectDashboardNesPanelViewModel
        {
            Prehled = prehled,
            ProjektId = projektId,
            Nyni = reference,
            Kpi = kpi,
            DnyPodleDodavatele = dnyPodleDodavatele,
            PodleTypu = podleTypu,
        };
    }
}
