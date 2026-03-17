using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Modules.Export.Queries;

namespace PmTracker.Web.Modules.Export;

public sealed class ExportTemplateQueries : IExportTemplateQueries
{
    private readonly PmTrackerDbContext _dbContext;
    private readonly IExportRoleProjectionBuilder _exportRoleProjectionBuilder;
    private readonly IExportAttendanceProjectionBuilder _exportAttendanceProjectionBuilder;
    private readonly IExportRecordProjectionBuilder _exportRecordProjectionBuilder;
    private readonly IExportTemplateSummaryBuilder _exportTemplateSummaryBuilder;

    public ExportTemplateQueries(
        PmTrackerDbContext dbContext,
        IExportRoleProjectionBuilder exportRoleProjectionBuilder,
        IExportAttendanceProjectionBuilder exportAttendanceProjectionBuilder,
        IExportRecordProjectionBuilder exportRecordProjectionBuilder,
        IExportTemplateSummaryBuilder exportTemplateSummaryBuilder)
    {
        _dbContext = dbContext;
        _exportRoleProjectionBuilder = exportRoleProjectionBuilder;
        _exportAttendanceProjectionBuilder = exportAttendanceProjectionBuilder;
        _exportRecordProjectionBuilder = exportRecordProjectionBuilder;
        _exportTemplateSummaryBuilder = exportTemplateSummaryBuilder;
    }

    public ExportTemplateQueryResult GetProjectTemplate(int projektId)
    {
        var project = _dbContext.Projekty.AsNoTracking().FirstOrDefault(x => x.Id == projektId)
            ?? throw new InvalidOperationException($"Projekt {projektId} nebyl nalezen.");
        var summary = _exportTemplateSummaryBuilder.BuildProjectSummary();

        return new ExportTemplateQueryResult
        {
            ExportVariant = "project_all",
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
            ProjektoveRole = _exportRoleProjectionBuilder.BuildProjectRoleRows(project.Id),
            AppliedRuleSummary = summary.AppliedRuleSummary,
            Legenda = summary.Legenda,
            Zaznamy = _exportRecordProjectionBuilder.BuildExportRecords(projektId, null, null, limitComments: false, applyMeetingSnapshotRules: false),
            Dochazka = []
        };
    }

    public ExportTemplateQueryResult GetMeetingTemplate(int jednaniId)
    {
        var meeting = _dbContext.Jednani.AsNoTracking().FirstOrDefault(x => x.Id == jednaniId)
            ?? throw new InvalidOperationException($"Jednání {jednaniId} nebylo nalezeno.");

        var project = _dbContext.Projekty.AsNoTracking().First(x => x.Id == meeting.ProjektId);
        var status = _dbContext.CiselnikStavuJednani.AsNoTracking().FirstOrDefault(x => x.Id == meeting.StavJednaniId);
        var summary = _exportTemplateSummaryBuilder.BuildMeetingSummary(status?.Nazev);

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
            ProjektoveRole = _exportRoleProjectionBuilder.BuildProjectRoleRows(project.Id),
            AppliedRuleSummary = summary.AppliedRuleSummary,
            Legenda = summary.Legenda,
            Zaznamy = _exportRecordProjectionBuilder.BuildExportRecords(project.Id, meeting.Id, null, limitComments: true, applyMeetingSnapshotRules: true),
            Dochazka = _exportAttendanceProjectionBuilder.BuildAttendanceGroups(meeting.Id, project.Id)
        };
    }

    public ExportTemplateQueryResult GetTaskTemplate(int projektId, int zaznamId)
    {
        var project = _dbContext.Projekty.AsNoTracking().FirstOrDefault(x => x.Id == projektId)
            ?? throw new InvalidOperationException($"Projekt {projektId} nebyl nalezen.");

        var lastMeeting = _dbContext.Jednani.AsNoTracking()
            .Where(x => x.ProjektId == projektId)
            .OrderByDescending(x => x.CisloJednani)
            .FirstOrDefault();
        var summary = _exportTemplateSummaryBuilder.BuildTaskSummary();

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
            Zaznamy = _exportRecordProjectionBuilder.BuildExportRecords(projektId, lastMeeting?.Id, zaznamId, limitComments: true, applyMeetingSnapshotRules: false),
            Dochazka = []
        };
    }
}
