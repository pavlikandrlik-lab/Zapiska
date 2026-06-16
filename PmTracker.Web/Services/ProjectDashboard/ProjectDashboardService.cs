using Microsoft.EntityFrameworkCore;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Schedules;
using PmTracker.Web.Services.ServiceDesk;

namespace PmTracker.Web.Services.ProjectDashboard;

public sealed class ProjectDashboardService : IProjectDashboardService
{
    private readonly PmTrackerDbContext _dbContext;
    private readonly VyzvyPanelBuilder _vyzvyPanelBuilder;
    private readonly IInformacniSystemQueryService _isQueryService;

    public ProjectDashboardService(
        PmTrackerDbContext dbContext,
        VyzvyPanelBuilder vyzvyPanelBuilder,
        IInformacniSystemQueryService isQueryService)
    {
        _dbContext = dbContext;
        _vyzvyPanelBuilder = vyzvyPanelBuilder;
        _isQueryService = isQueryService;
    }

    public async Task<ProjectDashboardPageViewModel> BuildDashboardPageAsync(int projectId, CancellationToken ct = default)
    {
        var projekt = await _dbContext.Projekty.AsNoTracking()
            .Where(p => p.Id == projectId)
            .Join(_dbContext.CiselnikStavuProjektu.AsNoTracking(), p => p.StavId, s => s.Id, (p, s) => new { p, s })
            .Select(x => new ProjektHeaderViewModel
            {
                Id = x.p.Id,
                Nazev = x.p.CelyNazev,
                Zkratka = x.p.Zkratka,
                Stav = x.s.Nazev
            })
            .FirstOrDefaultAsync(ct);

        if (projekt is null)
        {
            throw new InvalidOperationException($"Projekt s ID {projectId} nebyl nalezen.");
        }

        return new ProjectDashboardPageViewModel
        {
            Projekt = projekt,
            RecordsPanelUrl = $"/projekty/{projectId}/dashboard/records-panel",
            NesPanelUrl = $"/projekty/{projectId}/dashboard/nes-panel",
            StatisticsPanelUrl = $"/projekty/{projectId}/dashboard/statistics-panel",
            VyzvyPanelUrl = $"/projekty/{projectId}/dashboard/vyzvy-panel",
            BackUrl = $"/Projekty/Detail/{projectId}"
        };
    }

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
            var computed = ScheduleDateCalculator.Compute(record.DatumZalozeni, steps, referenceDate);

            var snapshots = computed.Select(c =>
            {
                // Dashboard kategorizace pracuje jen se SKUTEČNĚ vyplněnou skutečností; projekci
                // aktuálního kroku do dneška (vizuální prvek lišty) sem nepouštíme (JeAktualniKrok guard).
                var maRealnouSkutecnost = c.MaSkutecnost && !c.JeAktualniKrok;
                var nazev = HarmonogramKroky.Vse.First(d => d.Poradi == c.Poradi).Nazev;
                var actualEnd = maRealnouSkutecnost ? c.SkutecnostEnd : c.PlanEnd;
                var duration = (c.PlanEnd - c.PlanStart).Days;
                var offset = maRealnouSkutecnost ? (c.SkutecnostEnd - c.PlanEnd).Days : 0;
                return new ScheduleStepSnapshot(c.Poradi, nazev, c.PlanEnd, actualEnd, duration, offset);
            }).ToList();

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
                ProblematicSteps = categorization.ProblematicSteps.Select(ps => new ProjectDashboardStepDetailViewModel
                {
                    StepName = ps.Step.Name,
                    PlannedDate = ps.Step.PlanEndDate,
                    ActualDate = ps.StepCategory == DashboardRecordCategory.AwaitingActual ? null : ps.Step.ActualEndDate,
                    OffsetDays = ps.Step.OffsetDays,
                    DaysSincePlanExpired = ps.StepCategory == DashboardRecordCategory.AwaitingActual
                        ? (referenceDate - ps.Step.PlanEndDate).Days : 0
                }).ToList(),
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

    public async Task<ProjectDashboardStatisticsPanelViewModel> BuildStatisticsPanelAsync(int projectId, int year, CancellationToken ct = default)
    {
        var yearStart = new DateTime(year, 1, 1);
        var yearEnd = new DateTime(year, 12, 31);

        var taskCategoryIds = await _dbContext.CiselnikKategoriiZaznamu.AsNoTracking()
            .Where(c => c.Kod == RecordCategoryCodes.TaskShort || c.Kod == RecordCategoryCodes.Task)
            .Select(c => c.Id)
            .ToListAsync(ct);

        var finalStates = await _dbContext.CiselnikStavuUkolu.AsNoTracking()
            .Where(s => s.IsFinal)
            .ToDictionaryAsync(s => s.Id, s => s.Kod, ct);

        // C-3 perf: push year filter to SQL; include prev-year buffer for YoY compare metrics.
        var rangeStart = yearStart.AddYears(-1);
        var rangeEnd = yearEnd.AddYears(1).AddDays(-1);

        var allRecords = await (
                from record in _dbContext.ProjektoveZaznamy.AsNoTracking()
                where record.ProjektId == projectId
                    && taskCategoryIds.Contains(record.KategorieId)
                    && record.DatumUkonceni >= rangeStart
                    && record.DatumUkonceni <= rangeEnd
                select new RecordStatRow
                {
                    Id = record.Id,
                    DatumZalozeni = record.DatumZalozeni,
                    DatumUkonceni = record.DatumUkonceni,
                    StavUkoluId = record.StavUkoluId,
                    SubsystemId = record.SubsystemId
                })
            .ToListAsync(ct);

        var subsystems = await _dbContext.Subsystemy.AsNoTracking()
            .ToDictionaryAsync(s => s.Id, s => new SubsystemInfo(s.Kod, s.Nazev), ct);

        var recordIds = allRecords.Select(r => r.Id).ToList();
        var historyTerminRaw = await _dbContext.ZaznamHistorieTerminu.AsNoTracking()
            .Where(h => recordIds.Contains(h.ZaznamId)
                && h.DatumZmeny >= rangeStart
                && h.DatumZmeny <= rangeEnd)
            .ToListAsync(ct);

        var historyTerminByRecord = historyTerminRaw
            .GroupBy(h => h.ZaznamId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ZaznamHistorieTerminuEntity>)g.ToList());

        var currentYearKpi = ComputeYearKpi(allRecords, finalStates, historyTerminByRecord, yearStart, yearEnd);
        var prevYearKpi = ComputeYearKpi(allRecords, finalStates, historyTerminByRecord,
            new DateTime(year - 1, 1, 1), new DateTime(year - 1, 12, 31));

        var quarters = Enumerable.Range(1, 4).Select(q =>
        {
            var qStart = new DateTime(year, (q - 1) * 3 + 1, 1);
            var qEnd = qStart.AddMonths(3).AddDays(-1);
            return new ProjectDashboardQuarterViewModel
            {
                Quarter = q,
                PlannedCompletions = allRecords.Count(r => r.DatumUkonceni >= qStart && r.DatumUkonceni <= qEnd),
                ActualCompletions = allRecords.Count(r =>
                    r.StavUkoluId.HasValue && finalStates.ContainsKey(r.StavUkoluId.Value)
                    && r.DatumUkonceni >= qStart && r.DatumUkonceni <= qEnd)
            };
        }).ToList();

        var subsystemStats = allRecords
            .GroupBy(r => r.SubsystemId)
            .Select(g =>
            {
                var sub = subsystems.GetValueOrDefault(g.Key);
                var yearRecords = g.Where(r => r.DatumUkonceni >= yearStart && r.DatumUkonceni <= yearEnd).ToList();
                return new ProjectDashboardSubsystemStatsViewModel
                {
                    SubsystemKod = sub?.Kod ?? "-",
                    SubsystemNazev = sub?.Nazev ?? "-",
                    Splneno = yearRecords.Count(r => r.StavUkoluId.HasValue && finalStates.ContainsKey(r.StavUkoluId.Value)),
                    VProdleni = 0, // Requires schedule computation per record — simplified for now
                    Zruseno = 0, // Would need "cancelled" state detection — simplified for now
                    Prodlouzeno = g.Count(r => historyTerminByRecord.TryGetValue(r.Id, out var rh)
                        && rh.Any(h => h.DatumZmeny >= yearStart && h.DatumZmeny <= yearEnd))
                };
            })
            .Where(s => s.Splneno > 0 || s.Prodlouzeno > 0)
            .ToList();

        var attendanceRate = await ComputeAttendanceRateAsync(projectId, yearStart, yearEnd, ct);

        var availableYears = allRecords
            .Select(r => r.DatumZalozeni.Year)
            .Concat(allRecords.Select(r => r.DatumUkonceni.Year))
            .Distinct()
            .OrderDescending()
            .ToList();

        if (!availableYears.Contains(year))
        {
            availableYears.Insert(0, year);
        }

        return new ProjectDashboardStatisticsPanelViewModel
        {
            SelectedYear = year,
            AvailableYears = availableYears,
            Kpi = new ProjectDashboardKpiViewModel
            {
                Splneno = currentYearKpi.Splneno,
                Zruseno = currentYearKpi.Zruseno,
                Preneseno = currentYearKpi.Preneseno,
                VcasnostPlneniPct = currentYearKpi.VcasnostPct,
                VProdleni = currentYearKpi.VProdleni,
                PrumerneProdleniDni = currentYearKpi.PrumerneProdleniDni,
                Prodlouzeno = currentYearKpi.Prodlouzeno,
                SplnenoPrevYear = prevYearKpi.Splneno,
                ZrusenoPrevYear = prevYearKpi.Zruseno,
                PrenesenoPrevYear = prevYearKpi.Preneseno,
                VcasnostPlneniPctPrevYear = prevYearKpi.VcasnostPct,
                VProdleniPrevYear = prevYearKpi.VProdleni,
                PrumerneProdleniDniPrevYear = prevYearKpi.PrumerneProdleniDni,
                ProdlouzenoPrevYear = prevYearKpi.Prodlouzeno
            },
            Quarters = quarters,
            SubsystemStats = subsystemStats,
            AttendanceRate = attendanceRate,
            IsServiceDeskIntegrated = false
        };
    }

    public async Task<ProjectDashboardNesPanelViewModel> BuildNesPanelAsync(
        int projektId, DateTime reference, CancellationToken ct = default)
    {
        // Plán 5 Sprint B Task 4: unhardcode — NES panel aktivován, pokud projekt
        // má napojení na IS (projekty.servicedesk_info_system_id). Volá Sprint A
        // query service pro tickety v prodlení (NES SLA / PMP+PNF DatResT).
        var projekt = await _dbContext.Projekty.AsNoTracking()
            .Where(p => p.Id == projektId)
            .Select(p => new { p.ServiceDeskInfoSystemId })
            .FirstOrDefaultAsync(ct);

        if (projekt?.ServiceDeskInfoSystemId is not int isId)
        {
            // Bez napojení nebo projekt nenalezen — UI render placeholder s odkazem na edit.
            return new ProjectDashboardNesPanelViewModel { IsServiceDeskIntegrated = false };
        }

        var infoSystem = SdInfoSystemy.ById(isId);
        var prodlene = await _isQueryService.GetProdleneAsync(isId, reference, ct);

        var items = prodlene
            .Select(p => new NesPanelItemViewModel
            {
                TicketId = p.Id,
                Pid = p.Pid,
                TypZaznamu = p.TypZaznamu,
                Strucne = p.Strucne,
                Dodavatel = p.Dodavatel,
                Termin = p.Termin,
                DniProdleni = p.DniProdleni,
                Stav = p.Stav,
                ServiceDeskUrl = ServiceDeskUrlBuilder.ForTicket(p.Id)
            })
            .ToList();

        return new ProjectDashboardNesPanelViewModel
        {
            IsServiceDeskIntegrated = true,
            ServiceDeskInfoSystemId = isId,
            IsZkratka = infoSystem?.Zkratka,
            Items = items,
            PocetVProdleni = items.Count,
            PrumerneProdleniDni = items.Count > 0 ? items.Average(x => x.DniProdleni) : 0.0
        };
    }

    public Task<ProjectDashboardVyzvyPanelViewModel> BuildVyzvyPanelAsync(
        int projektId, bool muzeEditovat, CancellationToken ct)
    {
        // Per-action redesign 2026-04-23: muzeEditovat rozhoduje caller přes
        // HasPermission(VyzvyCreate). CanUserEditProjectVyzvyAsync (hardcoded role codes)
        // byl smazán — nálezy 1, 3, 9 z authz-ui-serverside-mismatch.md uzavřené.
        return _vyzvyPanelBuilder.BuildAsync(projektId, muzeEditovat, ct);
    }

    // CanAccessDashboardAsync smazáno 2026-04-23 — nahrazeno Policy atributem
    // [Authorize(Policy = "permission:dashboard.view")] na endpointech ProjectDashboardController.
    // Hardkódovaný whitelist rolí (ProjectDashboardAuthorizationPolicy) smazán.

    private static YearKpiSnapshot ComputeYearKpi(
        List<RecordStatRow> allRecords,
        Dictionary<int, string> finalStates,
        Dictionary<int, IReadOnlyList<ZaznamHistorieTerminuEntity>> historyByRecord,
        DateTime yearStart,
        DateTime yearEnd)
    {
        var yearRecords = allRecords
            .Where(r => r.DatumUkonceni >= yearStart && r.DatumUkonceni <= yearEnd)
            .ToList();

        var completedRecords = yearRecords
            .Where(r => r.StavUkoluId.HasValue && finalStates.ContainsKey(r.StavUkoluId.Value))
            .ToList();

        var splneno = completedRecords.Count;

        // Carry-over: records planned for this year but not completed (no final state)
        var preneseno = yearRecords.Count(r => !r.StavUkoluId.HasValue || !finalStates.ContainsKey(r.StavUkoluId.Value));

        // Extended: records whose deadline was changed during this year
        var prodlouzeno = allRecords.Count(r =>
            historyByRecord.TryGetValue(r.Id, out var rh)
            && rh.Any(h => h.DatumZmeny >= yearStart && h.DatumZmeny <= yearEnd));

        // On-time: completed records whose actual completion date <= original deadline
        var onTimeCount = completedRecords.Count(r =>
        {
            if (!historyByRecord.TryGetValue(r.Id, out var rh) || rh.Count == 0)
            {
                return true; // No deadline change history — considered on-time
            }

            var originalDeadline = rh.OrderBy(h => h.DatumZmeny).First().PuvodniDatum;
            return r.DatumUkonceni <= originalDeadline;
        });
        var vcasnostPct = splneno > 0 ? Math.Round(100.0 * onTimeCount / splneno, 1) : 0;

        // Delayed and average delay — records with deadline extension history
        var delayedRecords = yearRecords
            .Where(r => historyByRecord.TryGetValue(r.Id, out var rh)
                && rh.Any(h => h.NoveDatum > h.PuvodniDatum))
            .ToList();
        var vProdleni = delayedRecords.Count;
        var prumerneProdleniDni = delayedRecords.Count > 0
            ? Math.Round(delayedRecords.Average(r =>
            {
                var lastChange = historyByRecord[r.Id]
                    .Where(h => h.NoveDatum > h.PuvodniDatum)
                    .OrderByDescending(h => h.DatumZmeny)
                    .First();
                return (lastChange.NoveDatum - lastChange.PuvodniDatum).TotalDays;
            }), 1)
            : 0;

        return new YearKpiSnapshot(splneno, 0, preneseno, vcasnostPct, vProdleni, prumerneProdleniDni, prodlouzeno);
    }

    private async Task<double?> ComputeAttendanceRateAsync(int projectId, DateTime yearStart, DateTime yearEnd, CancellationToken ct)
    {
        var presentStateId = await _dbContext.CiselnikStavuUcasti.AsNoTracking()
            .Where(s => s.Kod == "PRESENT")
            .Select(s => s.Id)
            .FirstOrDefaultAsync(ct);

        if (presentStateId == 0)
        {
            return null;
        }

        var meetingIds = await _dbContext.Jednani.AsNoTracking()
            .Where(m => m.ProjektId == projectId && m.DatumPlanovane >= yearStart && m.DatumPlanovane <= yearEnd)
            .Select(m => m.Id)
            .ToListAsync(ct);

        if (meetingIds.Count == 0)
        {
            return null;
        }

        var attendance = await _dbContext.Ucast.AsNoTracking()
            .Where(u => meetingIds.Contains(u.JednaniId))
            .ToListAsync(ct);

        if (attendance.Count == 0)
        {
            return null;
        }

        var present = attendance.Count(u => u.StavUcastiId == presentStateId);
        return Math.Round(100.0 * present / attendance.Count, 1);
    }

    private sealed class RecordStatRow
    {
        public int Id { get; init; }
        public DateTime DatumZalozeni { get; init; }
        public DateTime DatumUkonceni { get; init; }
        public int? StavUkoluId { get; init; }
        public int SubsystemId { get; init; }
    }

    private sealed record SubsystemInfo(string Kod, string Nazev);

    private sealed record YearKpiSnapshot(
        int Splneno, int Zruseno, int Preneseno, double VcasnostPct,
        int VProdleni, double PrumerneProdleniDni, int Prodlouzeno);
}
