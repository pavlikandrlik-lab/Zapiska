using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Search;

/// <summary>
/// Post-filter autorizace pro výsledky vyhledávání. Volá se na každý hit z OpenSearch.
/// </summary>
public static class SearchAcl
{
    public static bool IsAccessible(CurrentUserContextViewModel user, SearchHit hit)
    {
        if (user.IsSuperAdmin)
        {
            return true;
        }

        switch (hit.EntityType)
        {
            case EntityDocumentMapper.TypeOsoba:
            case EntityDocumentMapper.TypeSubsystem:
                return true;

            case EntityDocumentMapper.TypeProjekt:
            case EntityDocumentMapper.TypeZaznam:
            case EntityDocumentMapper.TypeJednani:
            case EntityDocumentMapper.TypeVyjadreni:
            case EntityDocumentMapper.TypeZaznamNavrh:
                return hit.ProjektId.HasValue && user.CanAccessProject(hit.ProjektId.Value);

            default:
                return false;
        }
    }
}
