namespace PmTracker.Web.Services.ServiceDesk.Prodleni;

/// <summary>Kódy odůvodnění 1:1 s texty „Txx | …" v Hotline.cs. T09/T10 jsou v originálu vypnuté
/// (zakomentovaný blok) a v portu nejsou (spec §5.3).</summary>
public enum PenaltyReasonKod
{
    /// <summary>Ticket přešel mezi dodavateli, počítá se zvlášť.</summary>
    T00,
    /// <summary>NES obrátka proběhla v rámci SLA.</summary>
    T01,
    /// <summary>Obrátka nedoběhla, ticket je u dodavatele, počítá se do teď.</summary>
    T02,
    /// <summary>Obrátka proběhla včas.</summary>
    T03,
    /// <summary>Obrátka nebyla včas, penalizováno od data předání.</summary>
    T04,
    /// <summary>Obrátka nebyla včas, penalizováno od deadlinu.</summary>
    T05,
    /// <summary>Prodloužení bez žádosti, obrátka včas.</summary>
    T06,
    /// <summary>Prodloužení bez žádosti, obrátka pozdě.</summary>
    T07,
    /// <summary>Deadline pro výpočet prodloužení nastaven na datum předání.</summary>
    T08,
    /// <summary>Prodloužení po žádosti, konec obrátky včas.</summary>
    T11,
    /// <summary>Prodloužení po žádosti, konec obrátky pozdě.</summary>
    T12,
    /// <summary>Neznámá kombinace parametrů (chyba dat).</summary>
    T13,
    /// <summary>Prominutí dnů do schváleného termínu.</summary>
    T14,
}

/// <summary>Strukturované odůvodnění; český text skládá <see cref="PenaltyReasonFormatter"/>.</summary>
/// <param name="ObratkaOd">Začátek obrátky (předání dodavateli).</param>
/// <param name="ObratkaDo">Konec obrátky (převzetí); u otevřené obrátky „teď", u T01 otevřené obrátky null.</param>
/// <param name="Deadline">Termín, od kterého se penalizuje (T03/T05/T08; u T07 skutečný začátek penalizace).</param>
/// <param name="ProdlouzenoOd">Žádost o prodloužení (U10).</param>
/// <param name="ProdlouzenoDo">Schválený termín (U11).</param>
/// <param name="PocetDni">Počet nově penalizovaných dnů v této obrátce.</param>
/// <param name="ZbyvaHodin">NES: zbývající SLA před obrátkou.</param>
/// <param name="SpotrebovanoHodin">NES: pracovní čas spotřebovaný obrátkou.</param>
public sealed record PenaltyReason(PenaltyReasonKod Kod, DateTime? ObratkaOd = null, DateTime? ObratkaDo = null,
    DateTime? Deadline = null, DateTime? ProdlouzenoOd = null, DateTime? ProdlouzenoDo = null,
    int? PocetDni = null, double? ZbyvaHodin = null, double? SpotrebovanoHodin = null);
