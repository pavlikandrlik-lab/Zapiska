namespace PmTracker.ServiceDesk.Sql.Entities;

internal sealed class HotZaznamEntity
{
    public long Radek { get; set; }
    public string Id { get; set; } = string.Empty;
    public string? Pid { get; set; }
    public string? TypZaznamu { get; set; }
    public string? Strucne { get; set; }
    public string? Popis { get; set; }
    public string? Stav { get; set; }

    // Bug #1 fix: DB typ je smalldatetime, nikoli int
    public DateTime? Splneno { get; set; }

    public DateTime? SlaDeadline { get; set; }
    public DateTime? Datum { get; set; }

    // NOVÉ — pro filtraci a zobrazení "v prodlení" dashboardu
    public string? Modul { get; set; }
    public string? Subsystem { get; set; }

    public DateTime? TermPl { get; set; }
    public DateTime? DatResT { get; set; }
    public DateTime? DatDod { get; set; }

    public string? Dulezitost { get; set; }
    public string? Zavaznost { get; set; }

    public string? Dodavatel { get; set; }
    public string? ResTym { get; set; }

    public string? Zpracoval { get; set; }
    public string? Uzivatel { get; set; }
    public string? Email { get; set; }
    public string? ZalHfu { get; set; }

    public byte PriznakZamceni { get; set; }
    public bool? PriznakGdpr { get; set; }
    public bool Schvaleno { get; set; }
}
