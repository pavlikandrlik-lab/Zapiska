using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Data;

namespace PmTracker.Web.Services.ServiceDesk.Prodleni;

/// <summary>
/// Skládá přehled prodlení IS projektu: načte synchronizované tickety, předávky a SLA katalog, spočítá penále
/// (<see cref="PenaltyCalculator"/>) a roztřídí tickety na v prodlení / hrozí / nelze vyhodnotit (spec 2026-10-15 §10).
/// Přehled se cachuje 10 min na (IS, den, období) — projekty se stejným IS sdílí výsledek; SLA katalog 1 h.
/// Výpadek databáze = <see cref="ProdleniStavDat.Nedostupne"/>, necachuje se.
/// </summary>
public sealed class ProdleniService : IProdleniService
{
    /// <summary>NES hrozí, když do vyčerpání SLA zbývá nejvýš tolik hodin (filtr starého SD GetCounterHodinyKReseni, spec R4).</summary>
    public const int HroziNesHodin = 48;

    /// <summary>PMP/PNF hrozí, když do termínu zbývá nejvýš tolik dní (filtr starého SD GetCounterDnyKReseni, spec R4).</summary>
    public const int HroziTerminDni = 5;

    private const string Nes = "NES";
    private const string ArchivStav = "archiv";
    private const string SlaKlic = "prodleni:sla";
    private static readonly TimeSpan PrehledTtl = TimeSpan.FromMinutes(10);
    /// <summary>
    /// Výpadek se pamatuje krátce: souhrnný pás, panel NES i Statistiky se ptají současně a každý by jinak
    /// čekal na timeout SQL. „Zkusit znovu" po minutě už databázi opravdu zkusí.
    /// </summary>
    private static readonly TimeSpan VypadekTtl = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan SlaTtl = TimeSpan.FromHours(1);

    private readonly PmTrackerDbContext _db;
    private readonly IProdleniQueryService _query;
    private readonly PenaltyCalculator _calc;
    private readonly TimeProvider _time;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ProdleniService> _log;

    public ProdleniService(PmTrackerDbContext db, IProdleniQueryService query, PenaltyCalculator calc, TimeProvider time,
        IMemoryCache cache, ILogger<ProdleniService> log)
    {
        _db = db;
        _query = query;
        _calc = calc;
        _time = time;
        _cache = cache;
        _log = log;
    }

    public Task<ProdleniPrehled> GetPrehledAsync(int projektId, CancellationToken ct) => SestavAsync(projektId, null, ct);

    public Task<ProdleniPrehled> GetPrehledProObdobiAsync(int projektId, DateTime obdobiOd, CancellationToken ct)
        => SestavAsync(projektId, obdobiOd.Date, ct);

    private async Task<ProdleniPrehled> SestavAsync(int projektId, DateTime? obdobiOd, CancellationToken ct)
    {
        var isId = await _db.Projekty.AsNoTracking()
            .Where(p => p.Id == projektId)
            .Select(p => p.ServiceDeskInfoSystemId)
            .FirstOrDefaultAsync(ct);
        if (isId is not int id) return ProdleniPrehled.Prazdny(ProdleniStavDat.BezNapojeni, null, null);

        var zkratka = SdInfoSystemy.ById(id)?.Zkratka;
        if (!_query.Zapnuto) return ProdleniPrehled.Prazdny(ProdleniStavDat.BezNapojeni, id, zkratka);

        var ted = _time.GetLocalNow().DateTime;
        var klic = $"prodleni:{id}:{ted:yyyyMMdd}:{(obdobiOd is { } od ? od.ToString("yyyyMMdd") : "-")}";
        if (_cache.TryGetValue(klic, out ProdleniPrehled? hotovy) && hotovy is not null) return hotovy;

        try
        {
            var tickety = await _query.GetSynchronizovaneTicketyAsync(id, obdobiOd, ct);
            var workflow = await _query.GetWorkflowAsync(tickety.Select(t => t.TicketExternalId).Distinct().ToList(), ct);
            var katalog = await SlaKatalogAsync(ct);

            var prehled = Spocitej(id, zkratka, tickety, workflow, katalog, ted);
            _cache.Set(klic, prehled, PrehledTtl);
            return prehled;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "Prodlení ticketů nejde spočítat, ServiceDesk neodpovídá (isId={IsId}).", id);
            var vypadek = ProdleniPrehled.Prazdny(ProdleniStavDat.Nedostupne, id, zkratka);
            _cache.Set(klic, vypadek, VypadekTtl);
            return vypadek;
        }
    }

    private ProdleniPrehled Spocitej(int isId, string? zkratka, IReadOnlyList<ProdleniTicketDto> tickety,
        IReadOnlyDictionary<int, IReadOnlyList<WorkflowUdalostDto>> workflow, SlaKatalog katalog, DateTime ted)
    {
        var vProdleni = new List<ProdleniPolozka>();
        var hrozi = new List<HroziPolozka>();
        var nelze = new List<NelzeVyhodnotitPolozka>();
        var otevrene = new List<SynchronizovanyTicket>();
        var penalizace = new List<PenalizaceVObdobi>();
        var chybejiciSla = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var t in tickety)
        {
            var udalosti = (workflow.TryGetValue(t.TicketExternalId, out var wf) ? wf : [])
                .Select(d => new WorkflowUdalost(d.Id, d.Datum, d.TransferType, d.Supplier, d.DtParam))
                .OrderBy(u => u.Datum).ThenBy(u => u.Id)
                .ToList();

            // Spec R8: ořez na obou stranách, velikost písmen se rozlišuje.
            double? sla = t.Dulezitost?.Trim() is { } klic && katalog.Hodiny.TryGetValue(klic, out var h) ? h : null;
            if (sla is null && t.TypZaznamu == Nes) chybejiciSla.Add(t.Dulezitost ?? string.Empty);

            var input = new PenaltyInput(t.Id, t.TypZaznamu, t.Dulezitost, sla, t.Dodavatel, udalosti);
            var vysledky = _calc.Spocitej(input);
            var (predani, aktualniDodavatel) = PenaltyCalculator.AktualniPredani(udalosti);
            // Archivovaný ticket (jen ve Statistikách za období, spec R3) u dodavatele není, i když archivace
            // přišla bez převzetí U3. Jeho otevřená obrátka by jinak sbírala penále až do dneška.
            var archiv = JeArchiv(t.Stav);
            var uDodavatele = predani is not null && !archiv;
            var konecArchivu = archiv && udalosti.Count > 0 ? udalosti[^1].Datum.Date : DateTime.MaxValue;

            if (!archiv) otevrene.Add(new SynchronizovanyTicket(t.Id, t.TypZaznamu, uDodavatele));

            var chyba = vysledky.FirstOrDefault(r => r.MaChybu);
            if (chyba is not null)
            {
                // Chyba se ukazuje jen u ticketu, který je u dodavatele (spec §10 Nelze vyhodnotit).
                if (uDodavatele) nelze.Add(new NelzeVyhodnotitPolozka(t.Id, t.TypZaznamu, t.Strucne, t.Dulezitost, chyba.Chyba!.Value));
                continue;
            }

            var viceDodavatelu = vysledky.Count > 1;
            foreach (var r in vysledky)
            {
                var dny = archiv ? r.PenalizovaneDny.Where(d => d.Date <= konecArchivu).ToList() : r.PenalizovaneDny;
                if (dny.Count > 0) penalizace.Add(new PenalizaceVObdobi(t.Id, r.Dodavatel, dny));

                // V prodlení je jen dodavatel, u kterého ticket právě je.
                var jeAktualni = uDodavatele && (!viceDodavatelu || r.Dodavatel == aktualniDodavatel);
                if (!jeAktualni) continue;

                var deadline = _calc.AktualniDeadline(input);
                if (r.PocetDni > 0)
                {
                    vProdleni.Add(new ProdleniPolozka(t.Id, t.Pid, t.TypZaznamu, t.Dulezitost, t.Strucne, r.Dodavatel,
                        viceDodavatelu, predani!.Datum,
                        Termin: t.TypZaznamu == Nes ? deadline : predani.TerminParam,
                        ProdlouzenoDo: udalosti.Where(u => u.Typ == TransferTyp.U11).Max(u => u.TerminParam),
                        r.PocetDni, r.PenalizovaneDny, r.PracovniDny, udalosti, r.Duvody));
                }
                else if (deadline is { } termin)
                {
                    var zbyva = termin - ted;
                    var prah = t.TypZaznamu == Nes ? TimeSpan.FromHours(HroziNesHodin) : TimeSpan.FromDays(HroziTerminDni);
                    if (zbyva <= prah) hrozi.Add(new HroziPolozka(t.Id, t.TypZaznamu, t.Strucne, r.Dodavatel, termin, zbyva));
                }
            }
        }

        ZalogujChybejiciSla(katalog, chybejiciSla);

        return new ProdleniPrehled(ProdleniStavDat.Ok, isId, zkratka,
            vProdleni.OrderByDescending(p => p.PenalizovaneDny).ThenBy(p => p.TicketId).ToList(),
            hrozi.OrderBy(p => p.Zbyva).ThenBy(p => p.TicketId).ToList(),
            nelze.OrderBy(p => p.TicketId).ToList(),
            otevrene.OrderBy(p => p.TicketId).ToList(),
            penalizace);
    }

    private static bool JeArchiv(string? stav) => string.Equals(stav?.Trim(), ArchivStav, StringComparison.OrdinalIgnoreCase);

    /// <summary>Každá chybějící důležitost se zaloguje jednou za načtení katalogu (spec §13), s přesnou hodnotou.</summary>
    private void ZalogujChybejiciSla(SlaKatalog katalog, IEnumerable<string> chybejici)
    {
        var nove = chybejici.Where(d => katalog.ZalogovaneChybejici.TryAdd(d, 0)).ToList();
        if (nove.Count == 0) return;
        _log.LogWarning(
            "SLA katalog ticket_external_sla nemá řádek pro důležitost {Dulezitosti}; tyto NES tickety jsou v „Nelze vyhodnotit“.",
            string.Join(", ", nove.Select(d => $"'{d}'")));
    }

    private async Task<SlaKatalog> SlaKatalogAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(SlaKlic, out SlaKatalog? hotovy) && hotovy is not null) return hotovy;

        var radky = await _query.GetSlaKatalogAsync(ct);
        var hodiny = new Dictionary<string, double>(StringComparer.Ordinal);
        var idPodleKlice = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var r in radky.OrderBy(r => r.Id))
        {
            var klic = r.SlaName.Trim();
            if (idPodleKlice.TryGetValue(klic, out var pouzite))
            {
                _log.LogWarning(
                    "SLA katalog: po ořezu mezer koliduje '{SlaName}' (id {IdPouzite} a {IdVynechane}), použije se id {IdPouzite}.",
                    klic, pouzite, r.Id, pouzite);
                continue;
            }
            hodiny[klic] = r.SlaHours;
            idPodleKlice[klic] = r.Id;
        }

        var katalog = new SlaKatalog(hodiny);
        _cache.Set(SlaKlic, katalog, SlaTtl);
        return katalog;
    }

    /// <summary>SLA hodiny podle oříznuté důležitosti + které chybějící hodnoty už jsou zalogované (na dobu života katalogu).</summary>
    private sealed class SlaKatalog(IReadOnlyDictionary<string, double> hodiny)
    {
        public IReadOnlyDictionary<string, double> Hodiny { get; } = hodiny;
        public ConcurrentDictionary<string, byte> ZalogovaneChybejici { get; } = new(StringComparer.Ordinal);
    }
}
