using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Services.Export;

/// <summary>
/// Orchestrační dotazy pro export šablon — deleguje na builder services (role, docházka, záznamy, summary).
/// </summary>
public interface IExportTemplateQueries
{
    Task<ExportTemplateQueryResult> GetProjectTemplateAsync(int projektId, CurrentUserContextViewModel currentUser, ProjectExportRecordFilters? filters = null, CancellationToken ct = default);
    Task<ExportTemplateQueryResult> GetMeetingTemplateAsync(int jednaniId, CancellationToken ct = default);
    Task<ExportTemplateQueryResult> GetTaskTemplateAsync(int projektId, int zaznamId, CancellationToken ct = default);
}

public sealed class ExportTemplateQueries : IExportTemplateQueries
{
    private readonly PmTrackerDbContext dbContext;
    private readonly IExportRoleProjectionBuilder exportRoleProjectionBuilder;
    private readonly IExportAttendanceProjectionBuilder exportAttendanceProjectionBuilder;
    private readonly IExportRecordProjectionBuilder exportRecordProjectionBuilder;
    private readonly IExportTemplateSummaryBuilder exportTemplateSummaryBuilder;

    public ExportTemplateQueries(
        PmTrackerDbContext dbContext,
        IExportRoleProjectionBuilder exportRoleProjectionBuilder,
        IExportAttendanceProjectionBuilder exportAttendanceProjectionBuilder,
        IExportRecordProjectionBuilder exportRecordProjectionBuilder,
        IExportTemplateSummaryBuilder exportTemplateSummaryBuilder)
    {
        this.dbContext = dbContext;
        this.exportRoleProjectionBuilder = exportRoleProjectionBuilder;
        this.exportAttendanceProjectionBuilder = exportAttendanceProjectionBuilder;
        this.exportRecordProjectionBuilder = exportRecordProjectionBuilder;
        this.exportTemplateSummaryBuilder = exportTemplateSummaryBuilder;
    }

    public async Task<ExportTemplateQueryResult> GetProjectTemplateAsync(int projektId, CurrentUserContextViewModel currentUser, ProjectExportRecordFilters? filters = null, CancellationToken ct = default)
    {
        var project = await dbContext.Projekty.AsNoTracking().FirstOrDefaultAsync(x => x.Id == projektId, ct)
            ?? throw new InvalidOperationException($"Projekt {projektId} nebyl nalezen.");
        var normalizedFilters = NormalizeProjectExportFilters(filters);
        var appliedRuleSummary = normalizedFilters is { UseCurrentFilters: true, HasRelevantFilters: true }
            ? await BuildProjectFilterSummaryAsync(normalizedFilters, currentUser.OsobaId, ct)
            : Array.Empty<string>();
        var summary = normalizedFilters is { UseCurrentFilters: true, HasRelevantFilters: true }
            ? exportTemplateSummaryBuilder.BuildFilteredProjectSummary(appliedRuleSummary)
            : exportTemplateSummaryBuilder.BuildProjectSummary();

        return new ExportTemplateQueryResult
        {
            ExportVariant = normalizedFilters is { UseCurrentFilters: true, HasRelevantFilters: true } ? "project_filtered" : "project_all",
            ProjektId = project.Id,
            ProjektZkratka = project.Zkratka,
            ProjektNazev = project.CelyNazev,
            JednaniId = null,
            JednaniCislo = null,
            JednaniDatum = null,
            JednaniMisto = null,
            JednaniStav = summary.JednaniStav,
            SnapshotSummary = summary.SnapshotSummary,
            PreparationSummary = summary.PreparationSummary,
            ProjektoveRole = await exportRoleProjectionBuilder.BuildProjectRoleRowsAsync(project.Id, ct),
            AppliedRuleSummary = summary.AppliedRuleSummary,
            Legenda = summary.Legenda,
            Zaznamy = await exportRecordProjectionBuilder.BuildExportRecordsAsync(projektId, null, null, limitComments: false, applyMeetingSnapshotRules: false, normalizedFilters, currentUser.OsobaId, ct),
            Dochazka = []
        };
    }

    public async Task<ExportTemplateQueryResult> GetMeetingTemplateAsync(int jednaniId, CancellationToken ct = default)
    {
        var meeting = await dbContext.Jednani.AsNoTracking().FirstOrDefaultAsync(x => x.Id == jednaniId, ct)
            ?? throw new InvalidOperationException($"Jednání {jednaniId} nebylo nalezeno.");

        var project = await dbContext.Projekty.AsNoTracking().FirstOrDefaultAsync(x => x.Id == meeting.ProjektId, ct)
            ?? throw new InvalidOperationException($"Projekt {meeting.ProjektId} nebyl nalezen.");
        var status = await dbContext.CiselnikStavuJednani.AsNoTracking().FirstOrDefaultAsync(x => x.Id == meeting.StavJednaniId, ct);
        var summary = exportTemplateSummaryBuilder.BuildMeetingSummary(status?.Nazev);

        return new ExportTemplateQueryResult
        {
            ExportVariant = "meeting",
            ProjektId = project.Id,
            ProjektZkratka = project.Zkratka,
            ProjektNazev = project.CelyNazev,
            JednaniId = meeting.Id,
            JednaniCislo = meeting.CisloJednani,
            JednaniDatum = meeting.DatumPlanovane,
            JednaniMisto = meeting.Misto,
            JednaniStav = summary.JednaniStav,
            SnapshotSummary = summary.SnapshotSummary,
            PreparationSummary = summary.PreparationSummary,
            ProjektoveRole = await exportRoleProjectionBuilder.BuildProjectRoleRowsAsync(project.Id, ct),
            AppliedRuleSummary = summary.AppliedRuleSummary,
            Legenda = summary.Legenda,
            Zaznamy = await exportRecordProjectionBuilder.BuildExportRecordsAsync(project.Id, meeting.Id, null, limitComments: true, applyMeetingSnapshotRules: true, null, null, ct),
            Dochazka = await exportAttendanceProjectionBuilder.BuildAttendanceGroupsAsync(meeting.Id, project.Id, ct)
        };
    }

    public async Task<ExportTemplateQueryResult> GetTaskTemplateAsync(int projektId, int zaznamId, CancellationToken ct = default)
    {
        var project = await dbContext.Projekty.AsNoTracking().FirstOrDefaultAsync(x => x.Id == projektId, ct)
            ?? throw new InvalidOperationException($"Projekt {projektId} nebyl nalezen.");

        var lastMeeting = await dbContext.Jednani.AsNoTracking()
            .Where(x => x.ProjektId == projektId)
            .OrderByDescending(x => x.CisloJednani)
            .FirstOrDefaultAsync(ct);
        var summary = exportTemplateSummaryBuilder.BuildTaskSummary();

        return new ExportTemplateQueryResult
        {
            ExportVariant = "task_single",
            ProjektId = project.Id,
            ProjektZkratka = project.Zkratka,
            ProjektNazev = project.CelyNazev,
            JednaniId = lastMeeting?.Id,
            JednaniCislo = lastMeeting?.CisloJednani,
            JednaniDatum = lastMeeting?.DatumPlanovane,
            JednaniMisto = lastMeeting?.Misto,
            JednaniStav = summary.JednaniStav,
            SnapshotSummary = summary.SnapshotSummary,
            PreparationSummary = summary.PreparationSummary,
            ProjektoveRole = [],
            AppliedRuleSummary = summary.AppliedRuleSummary,
            Legenda = summary.Legenda,
            Zaznamy = await exportRecordProjectionBuilder.BuildExportRecordsAsync(projektId, lastMeeting?.Id, zaznamId, limitComments: true, applyMeetingSnapshotRules: false, null, null, ct),
            Dochazka = []
        };
    }

    private static ProjectExportRecordFilters? NormalizeProjectExportFilters(ProjectExportRecordFilters? filters)
    {
        if (filters is null)
        {
            return null;
        }

        return filters with
        {
            Subsystem = string.IsNullOrWhiteSpace(filters.Subsystem) ? null : filters.Subsystem.Trim(),
            Kategorie = string.IsNullOrWhiteSpace(filters.Kategorie) ? null : filters.Kategorie.Trim(),
            Stav = string.IsNullOrWhiteSpace(filters.Stav) ? null : filters.Stav.Trim(),
            Typ = string.IsNullOrWhiteSpace(filters.Typ) ? null : filters.Typ.Trim(),
            VlastnikId = filters.VlastnikId > 0 ? filters.VlastnikId : null,
            JednaniVyjadreniStavId = filters.JednaniVyjadreniStavId > 0 ? filters.JednaniVyjadreniStavId : null
        };
    }

    private async Task<IReadOnlyList<string>> BuildProjectFilterSummaryAsync(ProjectExportRecordFilters filters, int currentUserOsobaId, CancellationToken ct)
    {
        var appliedRules = new List<string>();

        if (!string.IsNullOrWhiteSpace(filters.Subsystem))
        {
            var subsystemRow = await dbContext.Subsystemy.AsNoTracking()
                .Where(x => x.Kod == filters.Subsystem || (string.IsNullOrEmpty(x.Kod) && x.Nazev == filters.Subsystem))
                .Select(x => new { x.Kod, x.Nazev })
                .FirstOrDefaultAsync(ct);
            var subsystemLabel = subsystemRow is null
                ? null
                : string.IsNullOrWhiteSpace(subsystemRow.Kod)
                    ? subsystemRow.Nazev
                    : $"{subsystemRow.Kod} - {subsystemRow.Nazev}";
            appliedRules.Add($"Subsystém: {(string.IsNullOrWhiteSpace(subsystemLabel) ? filters.Subsystem : subsystemLabel)}");
        }

        if (!string.IsNullOrWhiteSpace(filters.Kategorie))
        {
            var categoryLabel = await dbContext.CiselnikKategoriiZaznamu.AsNoTracking()
                .Where(x => x.Kod == filters.Kategorie)
                .Select(x => x.Nazev)
                .FirstOrDefaultAsync(ct);
            appliedRules.Add($"Kategorie: {(string.IsNullOrWhiteSpace(categoryLabel) ? filters.Kategorie : categoryLabel)}");
        }

        if (!string.IsNullOrWhiteSpace(filters.Stav))
        {
            var taskStateLabel = await dbContext.CiselnikStavuUkolu.AsNoTracking()
                .Where(x => x.Kod == filters.Stav)
                .Select(x => x.Nazev)
                .FirstOrDefaultAsync(ct);
            appliedRules.Add($"Stav úkolu: {(string.IsNullOrWhiteSpace(taskStateLabel) ? filters.Stav : taskStateLabel)}");
        }

        if (!string.IsNullOrWhiteSpace(filters.Typ))
        {
            var taskTypeLabel = await dbContext.CiselnikTypuUkolu.AsNoTracking()
                .Where(x => x.Kod == filters.Typ)
                .Select(x => x.Nazev)
                .FirstOrDefaultAsync(ct);
            appliedRules.Add($"Typ úkolu: {(string.IsNullOrWhiteSpace(taskTypeLabel) ? filters.Typ : taskTypeLabel)}");
        }

        if (filters.VlastnikId.HasValue)
        {
            var ownerRow = await dbContext.Osoby.AsNoTracking()
                .Where(x => x.Id == filters.VlastnikId.Value)
                .Select(x => new { x.Titul, x.Jmeno, x.Prijmeni })
                .FirstOrDefaultAsync(ct);
            var ownerLabel = ownerRow is null
                ? null
                : string.Join(" ", new[] { ownerRow.Titul, ownerRow.Jmeno, ownerRow.Prijmeni }.Where(value => !string.IsNullOrWhiteSpace(value)));
            appliedRules.Add($"Vlastník: {(string.IsNullOrWhiteSpace(ownerLabel) ? $"Osoba #{filters.VlastnikId.Value}" : ownerLabel)}");
        }

        if (filters.Aktivni)
        {
            appliedRules.Add("Pouze aktivní úkoly");
        }

        if (filters.Mine)
        {
            var mineLabel = currentUserOsobaId > 0
                ? "Jen mé záznamy"
                : "Jen mé záznamy (bez identifikované osoby)";
            appliedRules.Add(mineLabel);
        }

        if (filters.JednaniVyjadreniStavId.HasValue)
        {
            var meetingStateLabel = await dbContext.CiselnikStavuJednani.AsNoTracking()
                .Where(x => x.Id == filters.JednaniVyjadreniStavId.Value)
                .Select(x => x.Nazev)
                .FirstOrDefaultAsync(ct);
            appliedRules.Add($"Jednání-vyjádření: {(string.IsNullOrWhiteSpace(meetingStateLabel) ? filters.JednaniVyjadreniStavId.Value.ToString(CultureInfo.InvariantCulture) : meetingStateLabel)}");
        }

        return appliedRules;
    }
}
