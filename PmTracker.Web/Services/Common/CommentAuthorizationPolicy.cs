using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Common;

/// <summary>
/// Rozhoduje o povoleních komentářů v per-action authz modelu (redesign 2026-04-23).
/// Backdoor přes <c>records.edit</c> zrušen — admin s <c>comments.edit.any</c> / <c>comments.delete.any</c>
/// smí upravovat/mazat cizí komentáře; autor vždy svoje (comments.edit.own / delete.own).
/// Subsystem lead varianta zachována pro scenário „VEDOUCI_SUBSYSTEMU přidává vyjádření
/// v DRAFT jednání".
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

    public bool CanAddComment(CurrentUserContextViewModel currentUser, int projektId, IReadOnlyCollection<int> subsystemLeadEquivalentOsobaIds, bool isDraftMeeting)
    {
        // Obecný klíč comments.add pokrývá běžné role (VP, ADM_PROJ, PROJ_MAN, GEST, VEDOUCI, METODIK).
        if (currentUser.HasPermission(PermissionKeys.CommentsAdd, projektId))
        {
            return true;
        }

        // Subsystem lead fallback v DRAFT jednání (pokud nemá comments.add, ale má subsystem lead roli).
        return isDraftMeeting && CanCommentAsSubsystemLeader(currentUser, projektId, subsystemLeadEquivalentOsobaIds);
    }

    public bool CanModifyComment(CurrentUserContextViewModel currentUser, int projektId, IReadOnlyCollection<int> subsystemLeadEquivalentOsobaIds, int commentAuthorOsobaId, bool isDraftMeeting)
    {
        // Admin varianta: comments.edit.any pokrývá úpravu libovolného komentáře.
        if (currentUser.HasPermission(PermissionKeys.CommentsEditAny, projektId))
        {
            return true;
        }

        // Autor vlastního komentáře + subsystem lead v DRAFT jednání.
        return commentAuthorOsobaId > 0
            && currentUser.OsobaId == commentAuthorOsobaId
            && currentUser.HasPermission(PermissionKeys.CommentsEditOwn, projektId)
            && (!isDraftMeeting || CanCommentAsSubsystemLeader(currentUser, projektId, subsystemLeadEquivalentOsobaIds));
    }

    public bool CanDeleteComment(CurrentUserContextViewModel currentUser, int projektId, int commentAuthorOsobaId)
    {
        // Admin: comments.delete.any libovolný komentář.
        if (currentUser.HasPermission(PermissionKeys.CommentsDeleteAny, projektId))
        {
            return true;
        }

        // Autor vlastního: comments.delete.own.
        return commentAuthorOsobaId > 0
            && currentUser.OsobaId == commentAuthorOsobaId
            && currentUser.HasPermission(PermissionKeys.CommentsDeleteOwn, projektId);
    }

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
