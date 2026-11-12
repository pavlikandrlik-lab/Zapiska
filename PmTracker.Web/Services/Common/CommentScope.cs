namespace PmTracker.Web.Services.Common;

/// <summary>
/// Kde se vyjádření přidává nebo mění: projekt záznamu, stav jednání a osoby subsystému záznamu
/// (spec 2026-11-10). Skládá ho jediné místo — <c>CommentService.ResolveCommentScopeAsync</c>.
/// </summary>
/// <param name="ProjektId">Projekt záznamu.</param>
/// <param name="IsDraftMeeting">Jednání je v Přípravě (DRAFT).</param>
/// <param name="SubsystemRoleOsobaIds">Vedoucí, zástupce a metodik subsystému záznamu.</param>
/// <param name="SubsystemLeadEquivalentOsobaIds">Vedoucí a zástupce subsystému záznamu (výjimka 2026-06-16).</param>
public sealed record CommentScope(
    int ProjektId,
    bool IsDraftMeeting,
    IReadOnlyCollection<int> SubsystemRoleOsobaIds,
    IReadOnlyCollection<int> SubsystemLeadEquivalentOsobaIds);
