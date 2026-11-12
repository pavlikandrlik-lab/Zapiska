namespace PmTracker.Web.Services.ServiceDesk.Prodleni;

/// <summary>Český popis důvodu, proč penále ticketu nejde spočítat (sekce „Nelze vyhodnotit", Excel).</summary>
public static class PenaltyChybaText
{
    public static string Popis(PenaltyChyba chyba, string? dulezitost) => chyba switch
    {
        PenaltyChyba.ZadneUdalosti => "Ticket nemá ve ServiceDesku žádnou předávku.",
        PenaltyChyba.ChybiIniciacniUdalost => "Ticket nemá iniciační předání dodavateli (U1).",
        PenaltyChyba.SlaChybiProDulezitost => string.IsNullOrWhiteSpace(dulezitost)
            ? "Ticket nemá vyplněnou důležitost, takže k němu nejde najít SLA."
            : $"Důležitost „{dulezitost.Trim()}“ nemá v katalogu SLA žádné hodiny.",
        PenaltyChyba.NeznamyTyp => "Neznámý typ záznamu — penále se počítá jen pro NES, PMP a PNF.",
        _ => throw new ArgumentOutOfRangeException(nameof(chyba), chyba, "Neznámá chyba výpočtu."),
    };
}
