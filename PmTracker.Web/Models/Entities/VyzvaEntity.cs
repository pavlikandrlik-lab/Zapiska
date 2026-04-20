namespace PmTracker.Web.Models.Entities;

public sealed class VyzvaEntity
{
    public int Id { get; set; }
    public int ProjektId { get; set; }
    public string Kod { get; set; } = string.Empty;
    public int PoradoveVRoce { get; set; }
    public int Rok { get; set; }
    public VyzvaStav Stav { get; set; }
    public DateTime DatumZalozeni { get; set; }
    public int ZalozilOsobaId { get; set; }
    public DateTime? DatumOdeslani { get; set; }
    public int? OdeslalOsobaId { get; set; }
    public string MistoPlneniSnapshot { get; set; } = string.Empty;
    public string CisloRamcoveSmlouvySnapshot { get; set; } = string.Empty;
}
