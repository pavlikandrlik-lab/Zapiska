using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Export;

public sealed class ExportTemplateUseCase(IExportTemplateQueries queries, TimeProvider timeProvider) : IExportTemplateUseCase
{
    public PdfExportTemplateViewModel BuildProjectTemplate(int projektId, CurrentUserContextViewModel currentUser, bool autoPrint)
        => BuildTemplate(queries.GetProjectTemplate(projektId), currentUser, autoPrint);

    public PdfExportTemplateViewModel BuildMeetingTemplate(int jednaniId, CurrentUserContextViewModel currentUser, bool autoPrint)
        => BuildTemplate(queries.GetMeetingTemplate(jednaniId), currentUser, autoPrint);

    public PdfExportTemplateViewModel BuildTaskTemplate(int projektId, int zaznamId, CurrentUserContextViewModel currentUser, bool autoPrint)
        => BuildTemplate(queries.GetTaskTemplate(projektId, zaznamId), currentUser, autoPrint);

    private PdfExportTemplateViewModel BuildTemplate(
        ExportTemplateQueryResult queryResult,
        CurrentUserContextViewModel currentUser,
        bool autoPrint)
    {
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
            Zaznamy = queryResult.Zaznamy
        };
    }
}
