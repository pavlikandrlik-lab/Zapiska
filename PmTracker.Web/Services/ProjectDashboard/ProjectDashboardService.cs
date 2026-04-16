using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Schedules;

namespace PmTracker.Web.Services.ProjectDashboard;

public sealed class ProjectDashboardService : IProjectDashboardService
{
    private readonly PmTrackerDbContext _dbContext;
    private readonly IHarmonogramService _harmonogramService;

    public ProjectDashboardService(PmTrackerDbContext dbContext, IHarmonogramService harmonogramService)
    {
        _dbContext = dbContext;
        _harmonogramService = harmonogramService;
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
            .FirstAsync(ct);

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
                select new
                {
                    record.Id,
                    record.CisloViditelne,
                    record.Nazev,
                    record.DatumZalozeni,
                    record.DatumUkonceni,
                    record.HarmonogramSablonaVerze,
                    SubsystemKod = subsystem.Kod,
                    SubsystemNazev = subsystem.Nazev,
                    VlastnikJmeno = owner.Jmeno + " " + owner.Prijmeni
                })
            .ToListAsync(ct);

        if (records.Count == 0)
        {
            return new ProjectDashboardRecordsPanelViewModel();
        }

        var recordIds = records.Select(r => r.Id).ToList();
        var harmonogramValues = await _dbContext.ZaznamHarmonogramHodnoty.AsNoTracking()
            .Where(h => recordIds.Contains(h.ZaznamId))
            .ToListAsync(ct);

        var valuesByRecord = harmonogramValues
            .GroupBy(v => v.ZaznamId)
            .ToDictionary(g => g.Key, g => (IReadOnlyDictionary<int, int>)g.ToDictionary(v => v.TypId, v => v.HodnotaInt));

        // Cache schemas by version to avoid repeated DB calls
        var schemaCache = new Dictionary<int, HarmonogramSchemaDefinition>();

        var dashboardRecords = new List<ProjectDashboardRecordRowViewModel>();

        foreach (var record in records)
        {
            if (!valuesByRecord.TryGetValue(record.Id, out var values) || values.Count == 0)
            {
                continue;
            }

            if (!schemaCache.TryGetValue(record.HarmonogramSablonaVerze, out var schema))
            {
                schema = await _harmonogramService.GetSchemaForRecordAsync(record.HarmonogramSablonaVerze, ct);
                schemaCache[record.HarmonogramSablonaVerze] = schema;
            }

            if (schema.Kroky.Count == 0)
            {
                continue;
            }

            var stepDefs = schema.Kroky
                .OrderBy(k => k.KrokIndex)
                .Select(k => new ScheduleTimelineStepDefinition
                {
                    StepIndex = k.KrokIndex,
                    Code = k.Kod,
                    Name = k.Nazev,
                    ColorHex = k.BarvaHex,
                    DurationTypeId = k.TrvaniTypId,
                    OffsetTypeId = k.ZpozdeniTypId
                })
                .ToList();

            var computation = ScheduleTimelineCalculator.Compute(record.DatumZalozeni, stepDefs, values);

            var snapshots = computation.Steps.Select(s =>
            {
                var stepDef = stepDefs.FirstOrDefault(d => d.StepIndex == s.StepIndex);
                return new ScheduleStepSnapshot(
                    s.StepIndex,
                    stepDef?.Name ?? $"Krok {s.StepIndex}",
                    s.PlanEndDate,
                    s.ActualEndDate,
                    s.DurationDays,
                    s.OffsetDays);
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

        var allRecords = await (
                from record in _dbContext.ProjektoveZaznamy.AsNoTracking()
                where record.ProjektId == projectId && taskCategoryIds.Contains(record.KategorieId)
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
        var historyTermin = await _dbContext.ZaznamHistorieTerminu.AsNoTracking()
            .Where(h => recordIds.Contains(h.ZaznamId))
            .ToListAsync(ct);

        var currentYearKpi = ComputeYearKpi(allRecords, finalStates, historyTermin, yearStart, yearEnd);
        var prevYearKpi = ComputeYearKpi(allRecords, finalStates, historyTermin,
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
                    Prodlouzeno = g.Count(r => historyTermin.Any(h => h.ZaznamId == r.Id
                        && h.DatumZmeny >= yearStart && h.DatumZmeny <= yearEnd))
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

    public ProjectDashboardNesPanelViewModel BuildNesPanel()
    {
        return new ProjectDashboardNesPanelViewModel { IsServiceDeskIntegrated = false };
    }

    public ProjectDashboardVyzvyPanelViewModel BuildVyzvyPanel()
    {
        return new ProjectDashboardVyzvyPanelViewModel { IsServiceDeskIntegrated = false };
    }

    public async Task<bool> CanAccessDashboardAsync(int projectId, int osobaId, CancellationToken ct = default)
    {
        var activeRoleCodes = await (
                from assignment in _dbContext.ObsazeniProjektu.AsNoTracking()
                join role in _dbContext.CiselnikRoliProjektu.AsNoTracking() on assignment.RoleId equals role.Id
                where assignment.ProjektId == projectId
                    && assignment.OsobaId == osobaId
                    && !assignment.DatumOdebrani.HasValue
                select role.Kod)
            .ToListAsync(ct);

        return ProjectDashboardAuthorizationPolicy.HasDashboardAccess(activeRoleCodes);
    }

    private static YearKpiSnapshot ComputeYearKpi(
        List<RecordStatRow> allRecords,
        Dictionary<int, string> finalStates,
        List<ZaznamHistorieTerminuEntity> historyTermin,
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
        var prodlouzeno = allRecords.Count(r => historyTermin.Any(h =>
            h.ZaznamId == r.Id && h.DatumZmeny >= yearStart && h.DatumZmeny <= yearEnd));

        // On-time: completed records whose actual completion date <= original deadline
        // For simplicity, we compare DatumUkonceni with the earliest history entry's PuvodniDatum
        var onTimeCount = completedRecords.Count(r =>
        {
            var originalDeadline = historyTermin
                .Where(h => h.ZaznamId == r.Id)
                .OrderBy(h => h.DatumZmeny)
                .Select(h => (DateTime?)h.PuvodniDatum)
                .FirstOrDefault() ?? r.DatumUkonceni;
            return r.DatumUkonceni <= originalDeadline;
        });
        var vcasnostPct = splneno > 0 ? Math.Round(100.0 * onTimeCount / splneno, 1) : 0;

        // Delayed and average delay — simplified: records with deadline extension history
        var delayedRecords = yearRecords
            .Where(r => historyTermin.Any(h => h.ZaznamId == r.Id && h.NoveDatum > h.PuvodniDatum))
            .ToList();
        var vProdleni = delayedRecords.Count;
        var prumerneProdleniDni = delayedRecords.Count > 0
            ? Math.Round(delayedRecords.Average(r =>
            {
                var lastChange = historyTermin
                    .Where(h => h.ZaznamId == r.Id && h.NoveDatum > h.PuvodniDatum)
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
