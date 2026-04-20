namespace PmTracker.Web.Models.Entities;

public sealed class VyzvaHistorieStavuEntity
{
    public int Id { get; set; }
    public int VyzvaId { get; set; }
    public VyzvaStav? PuvodniStav { get; set; }
    public VyzvaStav NovyStav { get; set; }
    public DateTime DatumZmeny { get; set; }
    public int ZmenilOsobaId { get; set; }
}
