using PmTracker.Web.Services.ServiceDesk.Prodleni;

namespace PmTracker.Web.Services.ProjectDashboard;

/// <summary>Obrátka na ose: předání dodavateli → převzetí zpět (u otevřené do „teď").</summary>
/// <param name="JinyDodavatel">Obrátka u jiného dodavatele, než kterému patří řádek (ticket u více dodavatelů).</param>
public sealed record TimelineObratka(double OdPct, double DoPct, DateTime Od, DateTime? Do, string? Dodavatel,
    bool Otevrena, bool JinyDodavatel);

/// <summary>Bod na ose (termín, prodloužený termín).</summary>
public sealed record TimelineZnacka(double Pct, DateTime Datum);

/// <summary>Souvislý úsek penalizovaných dnů; <paramref name="Do"/> je půlnoc po posledním dni.</summary>
public sealed record TimelinePenale(double OdPct, double DoPct, DateTime Od, DateTime Do);

/// <summary>Časová osa obrátek jednoho řádku záložky NES v prodlení. Polohy jsou v % šířky osy.</summary>
public sealed record ProdleniTimelineViewModel(DateTime OsaOd, DateTime OsaDo,
    IReadOnlyList<TimelineObratka> Obratky, IReadOnlyList<TimelineZnacka> Prodlouzeni, TimelineZnacka? Termin,
    double DnesPct, DateTime Dnes, IReadOnlyList<TimelinePenale> Penale);

/// <summary>
/// Polohy na časové ose počítá server (spec 2026-10-15 §10; stejný princip jako serverové ticky harmonogramu) —
/// klient nic nedopočítává, takže osa, obrátky a značky vždy lícují.
/// </summary>
public static class ProdleniTimelineBuilder
{
    public static ProdleniTimelineViewModel Sestav(ProdleniPolozka polozka, DateTime nyni)
    {
        var udalosti = polozka.Udalosti;

        // Rozsah osy: od první předávky po nejzazší z {teď, termín, prodloužení, poslední událost}.
        var osaOd = udalosti.Count > 0 ? udalosti.Min(u => u.Datum) : polozka.UDodavateleOd;
        if (osaOd > nyni)
        {
            osaOd = nyni;
        }
        var prodlouzeneTerminy = udalosti
            .Where(u => u.Typ == TransferTyp.U11 && u.TerminParam.HasValue)
            .Select(u => u.TerminParam!.Value)
            .Distinct()
            .OrderBy(d => d)
            .ToList();
        var osaDo = new[] { nyni, polozka.Termin ?? nyni, polozka.ProdlouzenoDo ?? nyni }
            .Concat(udalosti.Select(u => u.Datum))
            .Concat(prodlouzeneTerminy)
            .Max();
        if (osaDo <= osaOd)
        {
            osaDo = osaOd.AddDays(1);
        }

        var delka = (osaDo - osaOd).TotalSeconds;
        double Pct(DateTime d) => Math.Round(Math.Clamp((d - osaOd).TotalSeconds / delka, 0, 1) * 100, 2);

        var obratky = new List<TimelineObratka>();
        WorkflowUdalost? otevrena = null;
        void Uzavri(DateTime? konec)
        {
            if (otevrena is null)
            {
                return;
            }
            var jiny = !string.Equals(otevrena.Dodavatel?.Trim(), polozka.Dodavatel?.Trim(), StringComparison.OrdinalIgnoreCase);
            obratky.Add(new TimelineObratka(Pct(otevrena.Datum), Pct(konec ?? nyni), otevrena.Datum, konec,
                otevrena.Dodavatel, konec is null, jiny));
            otevrena = null;
        }

        foreach (var u in udalosti)
        {
            switch (u.Typ)
            {
                case TransferTyp.U1 or TransferTyp.U2:
                    // Předání dalšímu dodavateli bez převzetí ukončí předchozí obrátku.
                    Uzavri(u.Datum);
                    otevrena = u;
                    break;
                case TransferTyp.U3:
                    Uzavri(u.Datum);
                    break;
            }
        }
        Uzavri(null);

        var penale = new List<TimelinePenale>();
        DateTime? behOd = null, behDo = null;
        foreach (var den in polozka.PenalizovaneDnyDatumy.Select(d => d.Date).Distinct().OrderBy(d => d))
        {
            if (behDo == den)
            {
                behDo = den.AddDays(1);
                continue;
            }
            PridejBeh();
            behOd = den;
            behDo = den.AddDays(1);
        }
        PridejBeh();

        return new ProdleniTimelineViewModel(osaOd, osaDo, obratky,
            prodlouzeneTerminy.Select(d => new TimelineZnacka(Pct(d), d)).ToList(),
            polozka.Termin is { } termin ? new TimelineZnacka(Pct(termin), termin) : null,
            Pct(nyni), nyni, penale);

        void PridejBeh()
        {
            if (behOd is not { } od || behDo is not { } @do)
            {
                return;
            }
            var odPct = Pct(od);
            var doPct = Pct(@do);
            if (doPct > odPct)
            {
                penale.Add(new TimelinePenale(odPct, doPct, od, @do));
            }
        }
    }
}
