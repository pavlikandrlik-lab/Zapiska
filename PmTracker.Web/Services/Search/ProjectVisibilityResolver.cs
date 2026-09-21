using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Search;

/// <summary>
/// Spočítá množinu projektů, jejichž záznamy smí uživatel ve vyhledávání vidět.
/// </summary>
public interface IProjectVisibilityResolver
{
    /// <summary>
    /// <c>null</c> = bez omezení (superadmin nebo globální právo číst projekty).
    /// Prázdný seznam = uživatel nevidí žádný projekt, tedy žádné výsledky.
    /// </summary>
    IReadOnlyList<int>? Resolve(CurrentUserContextViewModel user);
}

/// <summary>
/// Jediné autorizační pravidlo vyhledávání: záznam je vidět tehdy, když je vidět
/// jeho projekt — ve stejném rozsahu jako <see cref="CurrentUserContextViewModel.CanAccessProject"/>.
///
/// Dřívější vyhledávání používalo jen <c>VisibleProjectIds</c> (plněno z ObsazeniProjektu),
/// takže držitel projektové role bez obsazení nedostal nic. Stejná třída vady jako
/// commit 55625b2 u projects.read.all.
/// </summary>
public sealed class ProjectVisibilityResolver : IProjectVisibilityResolver
{
    public IReadOnlyList<int>? Resolve(CurrentUserContextViewModel user)
    {
        if (user.IsSuperAdmin)
        {
            return null;
        }

        var authz = user.Authorization;

        // Globální read klíč (projects.read.all u SUPERADMIN, APP_ADMIN, READ_ALL)
        // otevírá všechny projekty — omezovat výčtem by bylo zbytečné i pomalé.
        if (authz is not null && authz.GlobalPermissions.Any(PermissionKeys.GrantsProjectRead))
        {
            return null;
        }

        var fromSnapshot = authz is null
            ? Enumerable.Empty<int>()
            : authz.PerProjectPermissions
                .Where(kvp => kvp.Value.Any(PermissionKeys.GrantsProjectRead))
                .Select(kvp => kvp.Key);

        return user.VisibleProjectIds
            .Concat(fromSnapshot)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();
    }
}
