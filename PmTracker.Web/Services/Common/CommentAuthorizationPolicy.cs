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

    public bool CanAddComment(CurrentUserContextViewModel currentUser, int projektId, IReadOnlyCollection<int> subsystemLeadEquivalentOsobaIds, bool isSubsystemLeadEquivalentInProject, bool isDraftMeeting)
    {
        // Vícevrstvá autorizace (rozhodnutí 2026-06-16):
        // 1) PŘÍMÁ projektová (či globální) role s comments.add → komentuje kdekoli v projektu
        //    (princip „vyšší bere"; pokrývá i kombinaci projektová role + vedoucí subsystému).
        if (currentUser.HasDirectProjectPermission(PermissionKeys.CommentsAdd, projektId))
        {
            return true;
        }

        // 2) Vedoucí / zástupce vedoucího subsystému → jen VLASTNÍ subsystém a jen v DRAFT jednání.
        //    comments.add zděděný čistě ze subsystémové role NEopravňuje komentovat cizí subsystém.
        if (isSubsystemLeadEquivalentInProject)
        {
            return isDraftMeeting && CanCommentAsSubsystemLeader(currentUser, projektId, subsystemLeadEquivalentOsobaIds);
        }

        // 3) Ostatní držitelé comments.add bez vedoucí-subsystému role (např. METODIK subsystému
        //    přes zděděný grant) — zachované chování dle efektivní sady oprávnění.
        return currentUser.HasPermission(PermissionKeys.CommentsAdd, projektId);
    }

    public bool CanModifyComment(CurrentUserContextViewModel currentUser, int projektId, IReadOnlyCollection<int> subsystemLeadEquivalentOsobaIds, int commentAuthorOsobaId, bool isDraftMeeting)
    {
        // Admin varianta: comments.edit.any pokrývá úpravu libovolného komentáře.
        if (currentUser.HasPermission(PermissionKeys.CommentsEditAny, projektId))
        {
            return true;
        }

        var isOwn = commentAuthorOsobaId > 0 && currentUser.OsobaId == commentAuthorOsobaId;
        if (!isOwn)
        {
            return false;
        }

        // Autor vlastního komentáře s comments.edit.own (v DRAFT navíc omezeno na vlastní subsystém).
        if (currentUser.HasPermission(PermissionKeys.CommentsEditOwn, projektId)
            && (!isDraftMeeting || CanCommentAsSubsystemLeader(currentUser, projektId, subsystemLeadEquivalentOsobaIds)))
        {
            return true;
        }

        // Rozhodnutí 2026-06-16 (implikovaný CRUD): vedoucí subsystému (meetings.notes.subsystemlead)
        // smí upravit/smazat VLASTNÍ komentář svého subsystému v DRAFT jednání i bez samostatného
        // comments.edit.own grantu. Mimo DRAFT / cizí subsystém → ne (CanCommentAsSubsystemLeader).
        return isDraftMeeting && CanCommentAsSubsystemLeader(currentUser, projektId, subsystemLeadEquivalentOsobaIds);
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
