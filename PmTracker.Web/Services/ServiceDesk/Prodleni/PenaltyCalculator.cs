namespace PmTracker.Web.Services.ServiceDesk.Prodleni;

/// <summary>
/// Penalizované dny ticketu u dodavatele. Port 1:1 <c>PenaltyCalculation.TicketPenaltyCalculator</c>
/// ze starého ServiceDesku (Hotline.cs ř. 1327–1967), spec 2026-10-15 §5.
/// Technické změny bez vlivu na chování: „teď" z <see cref="TimeProvider"/> (lokální čas, jednou za výpočet),
/// strukturovaná odůvodnění místo textů, čisté návratové hodnoty místo <c>ref</c>,
/// deterministické řazení předávek (Datum, pak Id). Odchylky (spec §16): R6 chybějící SLA → chyba místo 0 h.
/// </summary>
public sealed class PenaltyCalculator
{
    private const string Nes = "NES", Pmp = "PMP", Pnf = "PNF";

    private readonly ICeskeSvatky _svatky;
    private readonly TimeProvider _time;

    public PenaltyCalculator(ICeskeSvatky svatky, TimeProvider time)
    {
        _svatky = svatky;
        _time = time;
    }

    /// <summary>Penále ticketu; při více dodavatelích jeden výsledek na dodavatele (T00). Port <c>CalculatePenalty</c> (ř. 1355–1393).</summary>
    public IReadOnlyList<PenaltyResult> Spocitej(PenaltyInput input)
    {
        var ted = Ted();
        var serazene = Serad(input.Udalosti);
        if (serazene.Count == 0)
            return [ChybovyVysledek(null, PenaltyChyba.ZadneUdalosti, false)];
        if (!serazene.Any(u => u.Typ == TransferTyp.U1))
            return [ChybovyVysledek(null, PenaltyChyba.ChybiIniciacniUdalost, false)];

        var dodavatele = Dodavatele(serazene);
        if (dodavatele.Count > 1)
            return dodavatele
                .Select(d => SpocitejWorkflow(input, RozdelPodleDodavatele(serazene, d), d, ted))
                .ToList();

        return [SpocitejWorkflow(input, serazene, null, ted)];
    }

    /// <summary>
    /// Pro „hrozí prodlení": kdy NES vyčerpá SLA v aktuální (otevřené) obrátce — <c>GetDeadline</c> nad zbývajícími
    /// sekundami po předchozích obrátkách; u PMP/PNF termín plnění aktuální obrátky, posunutý nejvyšším schváleným
    /// termínem U11 (stejně jako ho uplatní prominutí T14). Null, když ticket není u dodavatele nebo termín nejde určit.
    /// Pokud NES vyčerpal SLA už v dřívější obrátce, je deadline okamžik předání (obrátka se penalizuje celá, T04).
    /// </summary>
    public DateTime? AktualniDeadline(PenaltyInput input)
    {
        var ted = Ted();
        var wf = Serad(input.Udalosti);
        if (!wf.Any(u => u.Typ == TransferTyp.U1)) return null;

        var (otevrena, aktualni) = AktualniPredani(wf);
        if (otevrena is null) return null;

        if (Dodavatele(wf).Count > 1)
        {
            if (aktualni is null) return null;
            wf = RozdelPodleDodavatele(wf, aktualni);
        }

        var iOtevrene = wf.IndexOf(otevrena);
        if (iOtevrene < 0) return null;

        switch (NormalizujTyp(input.TypZaznamu))
        {
            case Nes:
            {
                if (input.SlaHodiny is null) return null;
                var zbyvaSekund = Convert.ToInt32(input.SlaHodiny.Value) * 60 * 60;
                for (var i = 0; i < iOtevrene; i++)
                {
                    if (wf[i].Typ is not (TransferTyp.U1 or TransferTyp.U2)) continue;
                    var mezi = PracovniSekundy(wf[i].Datum, KonecObratky(i, wf), ted);
                    if (zbyvaSekund - mezi < 0) return otevrena.Datum;
                    zbyvaSekund -= mezi;
                }
                return DeadlinePoSla(otevrena.Datum, zbyvaSekund);
            }
            case Pmp:
            case Pnf:
            {
                var prodlouzeno = wf.Where(u => u.Typ == TransferTyp.U11).Max(u => u.TerminParam);
                var termin = otevrena.TerminParam;
                if (termin is null) return prodlouzeno;
                if (prodlouzeno is null) return termin;
                return prodlouzeno > termin ? prodlouzeno : termin;
            }
            default:
                return null;
        }
    }

    /// <summary>
    /// Je ticket právě u dodavatele? Ano, když po seřazení (Datum, Id) je poslední událost z {U1, U2, U3} typu U1/U2
    /// (spec §10). Vrací tu předávku a dodavatele platného v jejím okamžiku (poslední neprázdný dodavatel do ní včetně),
    /// jinak (null, null).
    /// </summary>
    public static (WorkflowUdalost? Predani, string? Dodavatel) AktualniPredani(IReadOnlyList<WorkflowUdalost> udalosti)
    {
        var wf = Serad(udalosti);
        var posledni = wf.LastOrDefault(u => u.Typ is TransferTyp.U1 or TransferTyp.U2 or TransferTyp.U3);
        if (posledni is null || posledni.Typ == TransferTyp.U3) return (null, null);

        string? dodavatel = null;
        foreach (var u in wf)
        {
            if (!string.IsNullOrEmpty(u.Dodavatel)) dodavatel = u.Dodavatel;
            if (ReferenceEquals(u, posledni)) break;
        }
        return (posledni, dodavatel);
    }

    private DateTime Ted() => _time.GetLocalNow().DateTime;

    private static string NormalizujTyp(string? typ) => (typ ?? string.Empty).Trim().ToUpperInvariant();

    private static List<WorkflowUdalost> Serad(IReadOnlyList<WorkflowUdalost> udalosti)
        => udalosti.OrderBy(u => u.Datum).ThenBy(u => u.Id).ToList();

    private static List<string> Dodavatele(List<WorkflowUdalost> wf)
        => wf.Select(u => u.Dodavatel).OfType<string>().Where(d => d.Length > 0).Distinct().ToList();

    private static PenaltyResult ChybovyVysledek(string? dodavatel, PenaltyChyba chyba, bool pracovniDny)
        => new(dodavatel, [], [], chyba, pracovniDny);

    /// <summary>Port <c>PrepareSortedWorkflowForMultipleSuppliers</c> (ř. 1395–1418): úseky workflow, kdy „chytá" daný dodavatel.</summary>
    private static List<WorkflowUdalost> RozdelPodleDodavatele(List<WorkflowUdalost> wf, string dodavatel)
    {
        var vysledek = new List<WorkflowUdalost>();
        var chytat = false;
        foreach (var u in wf)
        {
            if (!string.IsNullOrEmpty(u.Dodavatel))
                chytat = u.Dodavatel == dodavatel;
            if (chytat) vysledek.Add(u);
        }
        return vysledek;
    }

    /// <summary>Port <c>PrepareCalculationResult</c> (ř. 1420–1589) — rozcestí podle typu záznamu.</summary>
    private PenaltyResult SpocitejWorkflow(PenaltyInput input, List<WorkflowUdalost> wf, string? jinyDodavatel, DateTime ted)
    {
        var dodavatel = jinyDodavatel ?? input.Dodavatel;
        return NormalizujTyp(input.TypZaznamu) switch
        {
            Nes => SpocitejNes(input, wf, dodavatel, jinyDodavatel is not null, ted),
            Pmp => SpocitejSTerminem(wf, dodavatel, jinyDodavatel is not null, pracovniDny: false, ted),
            Pnf => SpocitejSTerminem(wf, dodavatel, jinyDodavatel is not null, pracovniDny: true, ted),
            _ => ChybovyVysledek(null, PenaltyChyba.NeznamyTyp, false),
        };
    }

    /// <summary>NES větev (ř. 1425–1505): řízení časem SLA, po vyčerpání dny.</summary>
    private PenaltyResult SpocitejNes(PenaltyInput input, List<WorkflowUdalost> wf, string? dodavatel, bool vicDodavatelu, DateTime ted)
    {
        // Odchylka R6: originál Convert.ToInt32(null) = 0 h → penále od prvního dne. Tady chyba „Nelze vyhodnotit".
        if (input.SlaHodiny is null)
            return ChybovyVysledek(dodavatel, PenaltyChyba.SlaChybiProDulezitost, true);

        var zbyvaSekund = Convert.ToInt32(input.SlaHodiny.Value) * 60 * 60;
        var rizenCasem = true;
        var duvody = new List<PenaltyReason>();
        var dny = new List<DateTime>();
        if (vicDodavatelu) duvody.Add(new PenaltyReason(PenaltyReasonKod.T00));

        for (var i = 0; i < wf.Count; i++)
        {
            var udalost = wf[i];
            if (udalost.Typ is not (TransferTyp.U1 or TransferTyp.U2)) continue;

            var start = udalost.Datum;
            var konec = KonecObratky(i, wf);

            if (rizenCasem)
            {
                var mezi = PracovniSekundy(start, konec, ted);
                if (zbyvaSekund - mezi < 0)
                {
                    // termín prošvihnut → dál řízení podle dní
                    rizenCasem = false;
                    var deadline = DeadlinePoSla(start, zbyvaSekund);
                    var (od, prodlDo) = NajdiProdlouzeni(i, wf, beforebound: true);
                    Pridej(PenalizujObratku(deadline, start, konec, od, prodlDo, dny, pracovniDny: true, ted), dny, duvody);
                }
                else
                {
                    duvody.Add(new PenaltyReason(PenaltyReasonKod.T01, start, konec,
                        ZbyvaHodin: zbyvaSekund / 3600.0, SpotrebovanoHodin: mezi / 3600.0));
                    zbyvaSekund -= mezi;
                }
            }
            else
            {
                var (od, prodlDo) = NajdiProdlouzeni(i, wf, beforebound: true);
                Pridej(PenalizujObratku(null, start, konec, od, prodlDo, dny, pracovniDny: true, ted), dny, duvody);
            }
        }

        return Uzavri(wf, dodavatel, dny, duvody, pracovniDny: true);
    }

    /// <summary>PMP (ř. 1507–1544, kalendářní dny) a PNF (ř. 1546–1584, pracovní dny) — deadline = termín plnění z U1/U2.</summary>
    private PenaltyResult SpocitejSTerminem(List<WorkflowUdalost> wf, string? dodavatel, bool vicDodavatelu, bool pracovniDny, DateTime ted)
    {
        var duvody = new List<PenaltyReason>();
        var dny = new List<DateTime>();
        if (vicDodavatelu) duvody.Add(new PenaltyReason(PenaltyReasonKod.T00));

        for (var i = 0; i < wf.Count; i++)
        {
            var udalost = wf[i];
            if (udalost.Typ is not (TransferTyp.U1 or TransferTyp.U2)) continue;

            var konec = KonecObratky(i, wf);
            var (od, prodlDo) = NajdiProdlouzeni(i, wf, beforebound: false);
            Pridej(PenalizujObratku(udalost.TerminParam, udalost.Datum, konec, od, prodlDo, dny, pracovniDny, ted), dny, duvody);
        }

        return Uzavri(wf, dodavatel, dny, duvody, pracovniDny);
    }

    private static void Pridej((List<DateTime> NoveDny, List<PenaltyReason> Duvody) obratka, List<DateTime> dny, List<PenaltyReason> duvody)
    {
        dny.AddRange(obratka.NoveDny);
        duvody.AddRange(obratka.Duvody);
    }

    private static PenaltyResult Uzavri(List<WorkflowUdalost> wf, string? dodavatel, List<DateTime> dny, List<PenaltyReason> duvody, bool pracovniDny)
    {
        var (zbyle, t14) = ProminDoSchvalenehoTerminu(wf, dny);
        if (t14 is not null) duvody.Add(t14);
        return new PenaltyResult(dodavatel, zbyle, duvody, null, pracovniDny);
    }

    /// <summary>Port <c>CheckProlongationPardon</c> (ř. 1591–1605): smaže dny ≤ nejvyššímu schválenému termínu U11.</summary>
    private static (List<DateTime> Dny, PenaltyReason? Duvod) ProminDoSchvalenehoTerminu(List<WorkflowUdalost> wf, List<DateTime> dny)
    {
        var prodlouzeni = wf.Where(u => u.Typ == TransferTyp.U11).ToList();
        if (prodlouzeni.Count == 0) return (dny, null);

        var prodlouzenoDo = prodlouzeni.Max(u => u.TerminParam);
        // d <= null je v C# false → při U11 bez termínu se nic nemaže (stejně jako originál).
        var zbyle = dny.Where(d => !(d <= prodlouzenoDo)).ToList();
        return (zbyle, new PenaltyReason(PenaltyReasonKod.T14, ProdlouzenoDo: prodlouzenoDo));
    }

    /// <summary>Port <c>GetPenaltyDays</c> (ř. 1616–1722). Vrací nově penalizované dny a odůvodnění obrátky.</summary>
    private (List<DateTime> NoveDny, List<PenaltyReason> Duvody) PenalizujObratku(DateTime? deadline, DateTime start, DateTime? konec,
        DateTime? prodlouzenoOd, DateTime? prodlouzenoDo, IReadOnlyCollection<DateTime> uzPenalizovane, bool pracovniDny, DateTime ted)
    {
        var duvody = new List<PenaltyReason>();
        var noveDny = new List<DateTime>();

        if (konec is null)
        {
            // ticket je aktuálně u dodavatele → fiktivní návrat „teď"
            konec = ted;
            duvody.Add(new PenaltyReason(PenaltyReasonKod.T02, start, konec));
        }
        var konecObratky = konec.Value;

        if (deadline >= konecObratky)
        {
            duvody.Add(new PenaltyReason(PenaltyReasonKod.T03, start, konecObratky, Deadline: deadline));
        }
        else if (prodlouzenoDo is null || prodlouzenoDo < deadline)
        {
            // bez prodloužení → celá doba od deadlinu (bez deadlinu od předání)
            if (deadline is null)
            {
                noveDny = DnyMezi(start, konecObratky, uzPenalizovane, pracovniDny);
                duvody.Add(new PenaltyReason(PenaltyReasonKod.T04, start, konecObratky, PocetDni: noveDny.Count));
            }
            else
            {
                noveDny = DnyMezi(deadline.Value, konecObratky, uzPenalizovane, pracovniDny);
                duvody.Add(new PenaltyReason(PenaltyReasonKod.T05, start, konecObratky, Deadline: deadline, PocetDni: noveDny.Count));
            }
        }
        else if (prodlouzenoDo is not null && prodlouzenoOd is null)
        {
            // prodloužení bez předchozí žádosti dodavatele
            if (prodlouzenoDo > konecObratky)
            {
                duvody.Add(new PenaltyReason(PenaltyReasonKod.T06, start, konecObratky, ProdlouzenoDo: prodlouzenoDo));
            }
            else
            {
                var od = prodlouzenoDo < start ? start : prodlouzenoDo.Value;
                noveDny = DnyMezi(od, konecObratky, uzPenalizovane, pracovniDny);
                duvody.Add(new PenaltyReason(PenaltyReasonKod.T07, start, konecObratky, Deadline: od,
                    ProdlouzenoDo: prodlouzenoDo, PocetDni: noveDny.Count));
            }
        }
        else if (prodlouzenoDo is not null && prodlouzenoOd is not null)
        {
            // prodloužení po žádosti dodavatele (U10 → U11)
            if (deadline is null)
            {
                deadline = start;
                duvody.Add(new PenaltyReason(PenaltyReasonKod.T08, start, konecObratky, Deadline: deadline,
                    ProdlouzenoOd: prodlouzenoOd, ProdlouzenoDo: prodlouzenoDo));
            }

            // T09/T10 (dodavatel nestihl požádat o prodloužení včas) je v originálu zakomentované
            // (Hotline.cs ř. 1690–1703) a zůstává vypnuté — spec 2026-10-15 §5.3.

            if (prodlouzenoDo > konecObratky)
            {
                duvody.Add(new PenaltyReason(PenaltyReasonKod.T11, start, konecObratky, ProdlouzenoDo: prodlouzenoDo));
            }
            else
            {
                noveDny = DnyMezi(prodlouzenoDo.Value, konecObratky, uzPenalizovane, pracovniDny);
                duvody.Add(new PenaltyReason(PenaltyReasonKod.T12, start, konecObratky, ProdlouzenoDo: prodlouzenoDo, PocetDni: noveDny.Count));
            }
        }
        else
        {
            // T13 — v originálu poslední else; po větvích výše je fakticky nedosažitelná, ponechána 1:1.
            duvody.Add(new PenaltyReason(PenaltyReasonKod.T13, start, konecObratky));
        }

        return (noveDny, duvody);
    }

    /// <summary>
    /// Port <c>GetPenaltyDaysBetweenTwoDates</c> (ř. 1740–1784): dny od data <paramref name="od"/> (i započatý den;
    /// při 23:59:59 až od dalšího dne) do data <paramref name="do"/> včetně, bez již penalizovaných, u pracovních jen pracovní.
    /// </summary>
    private List<DateTime> DnyMezi(DateTime od, DateTime @do, IReadOnlyCollection<DateTime> uzPenalizovane, bool pracovniDny)
    {
        var vysledek = new List<DateTime>();
        var den = od.Date;
        if (od.Hour == 23 && od.Minute == 59 && od.Second == 59) den = den.AddDays(1);

        while (den <= @do.Date)
        {
            if (!uzPenalizovane.Contains(den) && (!pracovniDny || _svatky.JePracovniDen(den)))
                vysledek.Add(den);
            den = den.AddDays(1);
        }
        return vysledek;
    }

    /// <summary>
    /// Port <c>GetProlongationSegment</c> (ř. 1795–1832). U11 se hledá jen když po obrátce existuje U3;
    /// <paramref name="beforebound"/> (NES) dovolí hledat U11 i před aktuální obrátkou (až po index 1).
    /// U10 se hledá jen uvnitř obrátky.
    /// </summary>
    private static (DateTime? Od, DateTime? Do) NajdiProdlouzeni(int x, List<WorkflowUdalost> wf, bool beforebound)
    {
        int? posledniPrevzeti = null;
        for (var i = x + 1; i < wf.Count; i++)
        {
            if (wf[i].Typ == TransferTyp.U3)
            {
                posledniPrevzeti = i;
                break;
            }
        }
        if (posledniPrevzeti is null) return (null, null);

        DateTime? od = null, prodlDo = null;
        var dolniMez = beforebound ? 0 : x;
        for (var i = posledniPrevzeti.Value; i > dolniMez; i--)
        {
            if (wf[i].Typ != TransferTyp.U11) continue;

            prodlDo = wf[i].TerminParam;
            for (var z = i; z > x; z--)
            {
                if (wf[z].Typ == TransferTyp.U10)
                {
                    od = wf[z].Datum;
                    break;
                }
            }
            break;
        }
        return (od, prodlDo);
    }

    /// <summary>Port <c>GetDeadline</c> (ř. 1841–1892): přičte SLA sekundy jen v pracovní dny (celý den 00–24 h).</summary>
    private DateTime DeadlinePoSla(DateTime start, int zbyvaSekund)
    {
        if (zbyvaSekund <= 0) return start;

        var cil = start;
        double zbyva = zbyvaSekund;
        while (zbyva > 0)
        {
            var den = cil.Date;
            if (_svatky.JePracovniDen(den))
            {
                var konecDne = den.AddDays(1);
                var dostupne = (konecDne - cil).TotalSeconds;
                if (dostupne >= zbyva)
                {
                    cil = cil.AddSeconds(zbyva);
                    zbyva = 0;
                }
                else
                {
                    cil = konecDne;
                    zbyva -= dostupne;
                }
            }
            else
            {
                cil = den.AddDays(1);
            }
        }
        return cil;
    }

    /// <summary>Port <c>GetWorkflowTransferReturnDate</c> (ř. 1894–1905): první U3 po události, jinak null (u dodavatele).</summary>
    private static DateTime? KonecObratky(int x, List<WorkflowUdalost> wf)
    {
        for (var i = x + 1; i < wf.Count; i++)
            if (wf[i].Typ == TransferTyp.U3) return wf[i].Datum;
        return null;
    }

    /// <summary>Port <c>GetWorkingSecondsBetweenDates</c> (ř. 1907–1957): pracovní sekundy (celé pracovní dny), oříznuto na int.</summary>
    private int PracovniSekundy(DateTime start, DateTime? konec, DateTime ted)
    {
        var end = konec ?? ted;
        if (end <= start) return 0;

        double celkem = 0;
        for (var den = start.Date; den <= end.Date; den = den.AddDays(1))
        {
            if (!_svatky.JePracovniDen(den)) continue;
            var zacatekDne = den;
            var konecDne = den.AddDays(1);
            var efektivniStart = start > zacatekDne ? start : zacatekDne;
            var efektivniKonec = end < konecDne ? end : konecDne;
            if (efektivniKonec > efektivniStart)
                celkem += (efektivniKonec - efektivniStart).TotalSeconds;
        }
        return (int)celkem;
    }
}
