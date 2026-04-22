namespace PmTracker.Web.Models.Entities;

public enum VazbaSource : byte
{
    Auto = 1,
    Manual = 2
}

public enum VazbaStav : byte
{
    Active = 1,
    Superseded = 2,
    Deleted = 3
}

/// <summary>
/// Plán C — vazba mezi vyjádřením v ServiceDesku (HOT_VYJADRENI) a krokem
/// harmonogramu (HarmonogramTypEntity.KrokKey) na konkrétním projektovém záznamu.
/// Append-only — nové přiřazení supersedne starší (stav=2), nemažeme.
/// </summary>
public sealed class ZaznamHarmonogramVyjadreniVazbaEntity
{
    public int Id { get; set; }
    public int ZaznamId { get; set; }
    public Guid KrokKey { get; set; }
    public int ExterniOdkazId { get; set; }
    public long HotVyjadreniId { get; set; }
    public DateTime DatumVyjadreni { get; set; }
    public byte Source { get; set; }
    public byte Stav { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? CreatedByOsobaId { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedByOsobaId { get; set; }
}
