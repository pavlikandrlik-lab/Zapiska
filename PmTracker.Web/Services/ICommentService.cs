using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services;

public interface ICommentService
{
    Task AddCommentAsync(AddCommentCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task UpdateCommentAsync(UpdateCommentCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task DeleteCommentAsync(DeleteCommentCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);

    /// <summary>
    /// Ke kterým z nabízených jednání smí uživatel k záznamu přidat vyjádření. Stejné vstupy
    /// i stejná ICommentAuthorizationPolicy.CanAddComment jako AddCommentAsync — UI tak
    /// nenabídne jednání, které by server při uložení odmítl (2026-10-13).
    /// </summary>
    Task<IReadOnlySet<int>> GetMeetingIdsAllowingNewCommentAsync(
        int zaznamId,
        IReadOnlyCollection<JednaniOptionViewModel> meetings,
        CurrentUserContextViewModel currentUser,
        CancellationToken ct = default);
    Task SaveMeetingNoteAsync(SaveMeetingNoteCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task SaveMeetingNotesBatchAsync(int meetingId, IEnumerable<(int ZaznamId, string Text)> rows, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
}
