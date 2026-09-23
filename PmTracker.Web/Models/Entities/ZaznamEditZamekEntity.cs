namespace PmTracker.Web.Models.Entities;

/// <summary>
/// Spec 2026-09-17 §4.1 — advisory zámek karty záznamu. Jeden řádek na záznam,
/// expiruje TTL bez heartbeatu (vyhodnocuje se při získávání, žádný úklidový job).
/// </summary>
public sealed class ZaznamEditZamekEntity
{
    public int ZaznamId { get; set; }
    public int OsobaId { get; set; }
    public DateTime ZiskanoAt { get; set; }
    public DateTime HeartbeatAt { get; set; }
}
