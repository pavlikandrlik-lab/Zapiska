namespace PmTracker.ServiceDesk.Sql.Entities;

/// <summary>
/// intranetNEW.dbo.HOT_VYJADRENI — jednotlivá vyjádření/komentáře k tiketům.
/// Schema podle SD_servicedesk/hotline.txt:
///   id, typ, pid, datum, zpracoval, popis, tym, export, id_export, viditelne_dodavateli
/// Join na HOT_ZAZNAMY přes pid (ne přes id!).
/// </summary>
internal sealed class HotVyjadreniEntity
{
    public long Id { get; set; }
    public string? Typ { get; set; }
    public string? Pid { get; set; }
    public DateTime? Datum { get; set; }
    public string? Zpracoval { get; set; }
    public string? Popis { get; set; }
    public string? Tym { get; set; }
    public int? ViditelneDodavateli { get; set; }
}
