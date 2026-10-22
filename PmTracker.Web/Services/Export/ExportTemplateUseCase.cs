using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Services.Export;

public interface IExportTemplateUseCase
{
    Task<PdfExportTemplateViewModel> BuildProjectTemplateAsync(int projektId, CurrentUserContextViewModel currentUser, bool autoPrint, ProjectExportRecordFilters? filters = null, CancellationToken ct = default);
    Task<PdfExportTemplateViewModel> BuildMeetingTemplateAsync(int jednaniId, CurrentUserContextViewModel currentUser, bool autoPrint, CancellationToken ct = default);
    Task<PdfExportTemplateViewModel> BuildTaskTemplateAsync(int projektId, int zaznamId, CurrentUserContextViewModel currentUser, bool autoPrint, CancellationToken ct = default);
}

public sealed class ExportTemplateUseCase(IExportTemplateQueries queries, TimeProvider timeProvider) : IExportTemplateUseCase
{
    public async Task<PdfExportTemplateViewModel> BuildProjectTemplateAsync(int projektId, CurrentUserContextViewModel currentUser, bool autoPrint, ProjectExportRecordFilters? filters = null, CancellationToken ct = default)
        => BuildTemplate(await queries.GetProjectTemplateAsync(projektId, filters, ct), currentUser, autoPrint);

    public async Task<PdfExportTemplateViewModel> BuildMeetingTemplateAsync(int jednaniId, CurrentUserContextViewModel currentUser, bool autoPrint, CancellationToken ct = default)
        => BuildTemplate(await queries.GetMeetingTemplateAsync(jednaniId, ct), currentUser, autoPrint);

    public async Task<PdfExportTemplateViewModel> BuildTaskTemplateAsync(int projektId, int zaznamId, CurrentUserContextViewModel currentUser, bool autoPrint, CancellationToken ct = default)
        => BuildTemplate(await queries.GetTaskTemplateAsync(projektId, zaznamId, ct), currentUser, autoPrint);

    private PdfExportTemplateViewModel BuildTemplate(
        ExportTemplateQueryResult queryResult,
        CurrentUserContextViewModel currentUser,
        bool autoPrint)
    {
        var normalizedVariant = NormalizeVariant(queryResult.ExportVariant);

        return new PdfExportTemplateViewModel
        {
            ExportVariant = queryResult.ExportVariant,
            AutoPrint = autoPrint,
            ProjektId = queryResult.ProjektId,
            ProjektZkratka = queryResult.ProjektZkratka,
            ProjektNazev = queryResult.ProjektNazev,
            JednaniId = queryResult.JednaniId,
            JednaniCislo = queryResult.JednaniCislo,
            JednaniDatum = queryResult.JednaniDatum,
            JednaniMisto = queryResult.JednaniMisto,
            JednaniStav = queryResult.JednaniStav,
            Vytvoril = currentUser.DisplayName,
            VytvorenoDne = timeProvider.GetLocalNow().LocalDateTime,
            SnapshotSummary = queryResult.SnapshotSummary,
            PreparationSummary = queryResult.PreparationSummary,
            Dochazka = queryResult.Dochazka,
            ProjektoveRole = queryResult.ProjektoveRole,
            AppliedRuleSummary = queryResult.AppliedRuleSummary,
            Legenda = queryResult.Legenda,
            Zaznamy = queryResult.Zaznamy,
            NormalizedVariant = normalizedVariant,
            IsMeeting = string.Equals(normalizedVariant, "meeting", StringComparison.Ordinal),
            IsProjectSummary = normalizedVariant.StartsWith("project_", StringComparison.Ordinal),
            DocumentTitle = BuildDocumentTitle(normalizedVariant, queryResult.ProjektNazev, queryResult.JednaniCislo),
            ExportTypeLabel = string.Equals(normalizedVariant, "task_single", StringComparison.Ordinal)
                ? "Jeden úkol"
                : "Kompletní projekt",
            SubsystemGroups = BuildSubsystemGroups(queryResult.Zaznamy, string.Equals(normalizedVariant, "meeting", StringComparison.Ordinal))
        };
    }

    private static string NormalizeVariant(string? exportVariant)
        => string.IsNullOrWhiteSpace(exportVariant)
            ? "project_all"
            : exportVariant.Trim().ToLowerInvariant();

    private static string BuildDocumentTitle(string normalizedVariant, string projectName, int? meetingNumber)
        => normalizedVariant switch
        {
            "meeting" => $"Zápis z jednání projektu {projectName} číslo {meetingNumber}",
            "task_single" => $"Zápis úkolu projektu {projectName}",
            _ => $"Souhrnný zápis projektu {projectName}"
        };

    private static IReadOnlyList<PdfExportSubsystemGroupViewModel> BuildSubsystemGroups(IReadOnlyList<PdfExportRecordViewModel> records, bool useProjectOrder)
        => MinutesRecordOrdering.GroupBySubsystem(records, MinutesKey, useProjectOrder)
            .Select(group => new PdfExportSubsystemGroupViewModel
            {
                Subsystem = group.Subsystem,
                Records = group.Records
            })
            .ToList();

    private static MinutesOrderKey MinutesKey(PdfExportRecordViewModel record)
        => new(record.Subsystem, record.SubsystemKod, record.SubsystemPoradi, record.SubsystemHasProjectOrder,
            record.Kategorie, record.CisloViditelneA, record.CisloViditelneB, record.CisloZaznamu);
}
