namespace PmTracker.ServiceDesk.Sql.Entities;

// 2026-09-08: sloupce id_kalk, verze a termin se schválně NEMAPUJÍ. Jejich CLR typy
// v entitě neodpovídaly databázi a čtení celé tabulky na nich padalo
// (InvalidCastException v SqlDataReader) — což shodilo tisk výzvy, prvního konzumenta
// téhle tabulky. Podle sys.columns je verze nvarchar(100), id_kalk nvarchar(30)
// a termin int, ne datum. Nikdo je nečte; lidsky čitelný termín nese text_termin.
// Kdyby je někdo potřeboval, nejdřív si ověř skutečný typ v sys.columns.
internal sealed class HotKalkulaceEntity
{
    public long Id { get; set; }
    public string? Pid { get; set; }
    public string? Akceptace { get; set; }
    public DateTime? Datum { get; set; }
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
    public decimal? PocetL { get; set; }
    public decimal? CenaL { get; set; }
    public string? RozpadLicence { get; set; }
    public string? Popis { get; set; }
    public string? VyjadreniKalk { get; set; }
    public string? TextTermin { get; set; }
}
