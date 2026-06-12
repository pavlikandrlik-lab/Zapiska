namespace PmTracker.Web.Models.Entities;

/// <summary>
/// Jeden ze 10 pevných kroků harmonogramu konkrétního projektového záznamu.
/// Datum-model: <see cref="PlanDatum"/> a <see cref="SkutecnostDatum"/> jsou absolutní datumy
/// (žádné offsety). <see cref="SkutecnostDatum"/> NULL = krok ještě nenastal; u automatických
/// kroků je to cached výsledek zvoleného kandidáta z vazeb (default MAX / K1-MIN / preferred override).
/// </summary>
public sealed class ZaznamHarmonogramKrokEntity
{
    public int Id { get; set; }
    public int ZaznamId { get; set; }
    public byte Poradi { get; set; }                 // 1..10
    public DateTime? PlanDatum { get; set; }         // plánové datum konce kroku
    public DateTime? SkutecnostDatum { get; set; }   // NULL = nenastal
    public byte SkutecnostZdroj { get; set; }        // 0 Neznamo / 1 Automat / 2 Manual / 3 Historicka
    public byte SkutecnostRezim { get; set; }        // 0 Auto / 1 Manual
    public int? PreferredExterniOdkazId { get; set; }
    public DateTime UpdatedAt { get; set; }
}
