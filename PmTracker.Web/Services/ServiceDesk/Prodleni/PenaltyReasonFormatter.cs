using System.Globalization;

namespace PmTracker.Web.Services.ServiceDesk.Prodleni;

/// <summary>Česká souvětí k odůvodnění penále (UI záložky NES v prodlení, komentáře v Excelu).
/// Na rozdíl od originálu ukazuje hodiny SLA správně na 2 desetinná místa (originál dělil celočíselně).</summary>
public static class PenaltyReasonFormatter
{
    private static readonly CultureInfo Cs = CultureInfo.GetCultureInfo("cs-CZ");

    public static string Text(PenaltyReason r)
    {
        var obratka = $"Obrátka {Cas(r.ObratkaOd)} – {Cas(r.ObratkaDo)}";
        return r.Kod switch
        {
            PenaltyReasonKod.T00 => "Ticket přešel mezi dodavateli, penále se počítá pro každého dodavatele zvlášť.",
            PenaltyReasonKod.T01 => $"{obratka} proběhla v rámci SLA: zbývalo {Hodiny(r.ZbyvaHodin)}, spotřebováno {Hodiny(r.SpotrebovanoHodin)}.",
            PenaltyReasonKod.T02 => $"Obrátka nedoběhla, ticket je u dodavatele od {Cas(r.ObratkaOd)}; počítá se do teď ({Cas(r.ObratkaDo)}).",
            PenaltyReasonKod.T03 => $"{obratka} proběhla včas (termín {Cas(r.Deadline)}).",
            PenaltyReasonKod.T04 => $"{obratka} nebyla včas, penalizováno od data předání: {Dny(r.PocetDni)}.",
            PenaltyReasonKod.T05 => $"{obratka} nebyla včas, penalizováno od termínu {Cas(r.Deadline)}: {Dny(r.PocetDni)}.",
            PenaltyReasonKod.T06 => $"{obratka} proběhla včas do prodlouženého termínu {Cas(r.ProdlouzenoDo)}.",
            PenaltyReasonKod.T07 => $"{obratka} nebyla včas ani do prodlouženého termínu {Cas(r.ProdlouzenoDo)}, penalizováno od {Cas(r.Deadline)}: {Dny(r.PocetDni)}.",
            PenaltyReasonKod.T08 => $"Termín pro výpočet prodloužení v obrátce {Cas(r.ObratkaOd)} – {Cas(r.ObratkaDo)} nastaven na datum předání {Cas(r.Deadline)} (žádost o prodloužení {Cas(r.ProdlouzenoOd)}, prodlouženo do {Cas(r.ProdlouzenoDo)}).",
            PenaltyReasonKod.T11 => $"{obratka} skončila včas do termínu prodlouženého na žádost dodavatele ({Cas(r.ProdlouzenoDo)}).",
            PenaltyReasonKod.T12 => $"{obratka} nebyla včas ani do termínu prodlouženého na žádost dodavatele ({Cas(r.ProdlouzenoDo)}), penalizováno: {Dny(r.PocetDni)}.",
            PenaltyReasonKod.T13 => $"{obratka}: neznámá kombinace údajů pro výpočet prodlení (chyba dat).",
            PenaltyReasonKod.T14 => r.ProdlouzenoDo is null
                ? "Termín byl prodloužen bez uvedeného data, žádné dny se nepromíjejí."
                : $"Termín byl schválen do {Cas(r.ProdlouzenoDo)}, dny do tohoto data včetně jsou prominuty.",
            _ => throw new ArgumentOutOfRangeException(nameof(r), r.Kod, "Neznámý kód odůvodnění."),
        };
    }

    /// <summary>„1 den", „2 dny", „5 dní".</summary>
    public static string Dny(int? pocet)
    {
        var n = pocet ?? 0;
        var tvar = n == 1 ? "den" : n is >= 2 and <= 4 ? "dny" : "dní";
        return $"{n} {tvar}";
    }

    private static string Cas(DateTime? d) => d?.ToString("dd.MM.yyyy HH:mm", Cs) ?? "teď";

    private static string Hodiny(double? h) => $"{(h ?? 0).ToString("0.00", Cs)} h";
}
