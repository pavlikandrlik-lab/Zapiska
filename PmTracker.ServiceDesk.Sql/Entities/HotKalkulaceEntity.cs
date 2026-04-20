namespace PmTracker.ServiceDesk.Sql.Entities;

internal sealed class HotKalkulaceEntity
{
    public long Id { get; set; }
    public string? Pid { get; set; }
    public int? IdKalk { get; set; }
    public int? Verze { get; set; }
    public string? Akceptace { get; set; }
    public DateTime? Datum { get; set; }
    public DateTime? Termin { get; set; }
    public decimal? PracnostA { get; set; }
    public decimal? PracnostP { get; set; }
    public decimal? PracnostT { get; set; }
    public decimal? PracnostI { get; set; }
    public decimal? SazbaA { get; set; }
    public decimal? SazbaP { get; set; }
    public decimal? SazbaT { get; set; }
    public decimal? SazbaI { get; set; }
    public decimal? CenaA { get; set; }
    public decimal? CenaP { get; set; }
    public decimal? CenaT { get; set; }
    public decimal? CenaI { get; set; }
    public decimal? Cena { get; set; }
    public decimal? SazbaL { get; set; }
    public int? PocetL { get; set; }
    public decimal? CenaL { get; set; }
    public string? RozpadLicence { get; set; }
    public string? Popis { get; set; }
    public string? VyjadreniKalk { get; set; }
    public string? TextTermin { get; set; }
}
