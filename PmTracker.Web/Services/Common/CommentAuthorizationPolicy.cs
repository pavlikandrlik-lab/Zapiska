using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Common;

/// <summary>
/// Kdo smí přidat, upravit a smazat vyjádření (spec 2026-11-10, docs/specs/record-comments.md):
/// <list type="bullet">
/// <item>přímá projektová / globální role s <c>comments.add</c> (admini, Vlastník, Admin projektu, PM, Gestor)
/// přidává v Přípravě i Otevřeno u všech subsystémů („vyšší bere“, R4);</item>
/// <item>subsystémové role (vedoucí, zástupce, metodik) jen v Přípravě a jen u záznamů svého subsystému (R1, R3);</item>
/// <item>vlastní vyjádření smí upravit a smazat, kdo by ho teď na tomtéž místě smel přidat, s klíčem
/// <c>comments.edit.own</c> / <c>comments.delete.own</c> (R2, R5); cizí jen s <c>comments.edit.any</c> / <c>comments.delete.any</c>;</item>
/// <item>výjimka 2026-06-16: vedoucí a zástupce s <c>meetings.notes.subsystemlead</c> v Přípravě u svého
/// subsystému i bez klíčů <c>comments.*</c>.</item>
/// </list>
/// Uzavřené jednání a uzavřený záznam hlídá CommentService, ne tato třída.
/// </summary>
public sealed class CommentAuthorizationPolicy : ICommentAuthorizationPolicy
{
    public bool CanCommentAsSubsystemLeader(CurrentUserContextViewModel currentUser, int projektId, IReadOnlyCollection<int> subsystemLeadEquivalentOsobaIds)
    {
        if (currentUser.OsobaId <= 0 || subsystemLeadEquivalentOsobaIds.Count == 0)
        {
            return false;
        }

        if (!subsystemLeadEquivalentOsobaIds.Contains(currentUser.OsobaId))
        {
            return false;
        }

        // Per-action redesign: subsystem lead v jednání píše zápis (meetings.notes.subsystemlead).
        return currentUser.HasPermission(PermissionKeys.MeetingsNotesSubsystemLead, projektId);
    }

    public bool CanAddComment(CurrentUserContextViewModel currentUser, CommentScope scope)
    {
        // R4: přímá projektová / globální role s comments.add → kdekoli v projektu, Příprava i Otevřeno.
        if (currentUser.HasDirectProjectPermission(PermissionKeys.CommentsAdd, scope.ProjektId))
        {
            return true;
        }

        // R1: subsystémové role jen v Přípravě…
        if (!scope.IsDraftMeeting)
        {
            return false;
        }

        // …a jen u záznamu svého subsystému (vedoucí, zástupce, metodik — R3). Zděděný comments.add
        // bez role v subsystému záznamu nestačí.
        if (currentUser.OsobaId > 0
            && scope.SubsystemRoleOsobaIds.Contains(currentUser.OsobaId)
            && currentUser.HasPermission(PermissionKeys.CommentsAdd, scope.ProjektId))
        {
            return true;
        }

        // Výjimka 2026-06-16: vedoucí / zástupce s meetings.notes.subsystemlead i bez comments.add.
        return CanCommentAsSubsystemLeader(currentUser, scope.ProjektId, scope.SubsystemLeadEquivalentOsobaIds);
    }

    public bool CanEditComment(CurrentUserContextViewModel currentUser, CommentScope scope, int commentAuthorOsobaId)
        => currentUser.HasPermission(PermissionKeys.CommentsEditAny, scope.ProjektId)
            || (IsOwn(currentUser, commentAuthorOsobaId) && CanChangeOwn(currentUser, scope, PermissionKeys.CommentsEditOwn));

    public bool CanDeleteComment(CurrentUserContextViewModel currentUser, CommentScope scope, int commentAuthorOsobaId)
        => currentUser.HasPermission(PermissionKeys.CommentsDeleteAny, scope.ProjektId)
            || (IsOwn(currentUser, commentAuthorOsobaId) && CanChangeOwn(currentUser, scope, PermissionKeys.CommentsDeleteOwn));

    /// <summary>R2: vlastní smí změnit, kdo by ho teď na tomtéž místě smel přidat a má klíč akce; nebo výjimka 2026-06-16.</summary>
    private bool CanChangeOwn(CurrentUserContextViewModel currentUser, CommentScope scope, string ownKey)
        => (CanAddComment(currentUser, scope) && currentUser.HasPermission(ownKey, scope.ProjektId))
            || (scope.IsDraftMeeting && CanCommentAsSubsystemLeader(currentUser, scope.ProjektId, scope.SubsystemLeadEquivalentOsobaIds));

    private static bool IsOwn(CurrentUserContextViewModel currentUser, int commentAuthorOsobaId)
        => commentAuthorOsobaId > 0 && currentUser.OsobaId == commentAuthorOsobaId;

    public bool CanSaveMeetingNote(
        CurrentUserContextViewModel currentUser,
        int projektId,
        IReadOnlyCollection<int> subsystemLeadEquivalentOsobaIds,
        bool isDraftMeeting)
    {
        // F3.8 duální gate pro zápis z jednání (record-level service filter).
        // Plný přístup: meetings.notes.edit (APP_ADMIN, VLASTNIK_PROJEKTU, ADM_PROJ, PROJ_MAN).
        if (currentUser.HasPermission(PermissionKeys.MeetingsNotesEdit, projektId))
        {
            return true;
        }

        // Subsystem lead — pouze v DRAFT jednání a jen pro vlastní subsystém (lead-equivalent list).
        return isDraftMeeting
            && CanCommentAsSubsystemLeader(currentUser, projektId, subsystemLeadEquivalentOsobaIds);
    }
}
