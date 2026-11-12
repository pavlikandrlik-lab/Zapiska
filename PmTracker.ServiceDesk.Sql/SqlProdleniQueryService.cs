using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PmTracker.ServiceDesk.Contracts;

namespace PmTracker.ServiceDesk.Sql;

/// <summary>
/// Dotazy pro výpočet prodlení přes dvě databáze (spec 2026-10-15 §6–7): tickety IS z intranetNEW,
/// <c>ticket_external*</c> z databáze ServiceDesku. Spojení běží v paměti, seznamy id jdou po kouscích
/// (≤ 1 000 parametrů v IN, limit SQL Serveru je 2 100).
/// </summary>
public sealed partial class SqlProdleniQueryService : IProdleniQueryService
{
    private const string ArchivStav = "archiv";
    private const int Chunk = 1000;

    private readonly TicketingReadOnlyDbContext _intranet;
    private readonly ServiceDeskExternalReadOnlyDbContext _sd;

    public SqlProdleniQueryService(TicketingReadOnlyDbContext intranet, ServiceDeskExternalReadOnlyDbContext sd)
    {
        _intranet = intranet;
        _sd = sd;
    }

    public bool Zapnuto => true;

    /// <summary>Stejné pravidlo jako <c>MOSD_External.GetSyncedTickets</c>: external_id musí být číslo.</summary>
    [GeneratedRegex(@"^\d+$")]
    private static partial Regex CiselneId();

    public async Task<IReadOnlyList<ProdleniTicketDto>> GetSynchronizovaneTicketyAsync(int isId, DateTime? aktivniOd, CancellationToken ct)
    {
        var moduly = await ModulyIsAsync(isId, ct);
        if (moduly.Count == 0) return [];

        var vcetneArchivu = aktivniOd is not null;
        // Ticket bez HOT_ZAZNAMY.id je mimo rozsah PM Trackeru (paměť feedback_sd_ticket_id_required).
        var zaznamy = await ZaznamyIs(_intranet.HotZaznamy, moduly, vcetneArchivu)
            .Select(z => new { z.Id, z.Pid, z.TypZaznamu, z.Strucne, z.Dulezitost, z.Modul, z.Dodavatel, z.Stav, z.Datum })
            .ToListAsync(ct);
        zaznamy.RemoveAll(z => string.IsNullOrEmpty(z.Id));
        if (zaznamy.Count == 0) return [];

        var externi = await TicketExternalPodleIdAsync(zaznamy.Select(z => z.Id).Distinct().ToList(), ct);

        HashSet<int> archivAktivni = [];
        if (vcetneArchivu)
        {
            var archivovaneExterniId = zaznamy
                .Where(z => z.Stav == ArchivStav && externi.ContainsKey(z.Id))
                .Select(z => externi[z.Id].Id)
                .ToList();
            archivAktivni = await ExterniIdSPredavkouOdAsync(archivovaneExterniId, aktivniOd!.Value, ct);
        }

        return zaznamy
            .Where(z => externi.ContainsKey(z.Id))
            .Where(z => z.Stav != ArchivStav || archivAktivni.Contains(externi[z.Id].Id))
            .Select(z =>
            {
                var e = externi[z.Id];
                return new ProdleniTicketDto(
                    Id: int.Parse(z.Id, System.Globalization.CultureInfo.InvariantCulture),
                    Pid: z.Pid ?? string.Empty,
                    TypZaznamu: (z.TypZaznamu ?? string.Empty).Trim().ToUpperInvariant(),
                    Strucne: z.Strucne,
                    Dulezitost: z.Dulezitost,
                    Modul: z.Modul,
                    Dodavatel: z.Dodavatel,
                    Stav: z.Stav,
                    Datum: z.Datum,
                    TicketExternalId: e.Id,
                    SupplierTag: e.SupplierTag);
            })
            .OrderBy(t => t.Id)
            .ToList();
    }

    public async Task<IReadOnlyDictionary<int, IReadOnlyList<WorkflowUdalostDto>>> GetWorkflowAsync(
        IReadOnlyCollection<int> ticketExternalIds, CancellationToken ct)
    {
        var vysledek = new Dictionary<int, IReadOnlyList<WorkflowUdalostDto>>();
        foreach (var chunk in ticketExternalIds.Distinct().Chunk(Chunk))
        {
            var radky = await _sd.TicketExternalWorkflow
                .Where(w => chunk.Contains(w.TicketExternalId))
                .Select(w => new { w.TicketExternalId, Dto = new WorkflowUdalostDto(w.Id, w.Date, w.TransferType, w.Supplier, w.DtParam) })
                .ToListAsync(ct);
            foreach (var g in radky.GroupBy(r => r.TicketExternalId))
                vysledek[g.Key] = g.Select(r => r.Dto).OrderBy(d => d.Datum).ThenBy(d => d.Id).ToList();
        }
        return vysledek;
    }

    public async Task<IReadOnlyList<SlaKatalogDto>> GetSlaKatalogAsync(CancellationToken ct)
        => await _sd.TicketExternalSla
            .OrderBy(s => s.Id)
            .Select(s => new SlaKatalogDto(s.Id, s.SlaName, s.SlaHours))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AkceptovanaKalkulaceDto>> GetAkceptovaneKalkulaceAsync(int isId, DateTime od, DateTime @do, CancellationToken ct)
    {
        var moduly = await ModulyIsAsync(isId, ct);
        if (moduly.Count == 0) return [];

        // I archivované tickety: akceptace v období patří do období bez ohledu na dnešní stav ticketu.
        var ticketIds = await ZaznamyIs(_intranet.HotZaznamy, moduly, vcetneArchivu: true)
            .Select(z => z.Id)
            .Distinct()
            .ToListAsync(ct);
        ticketIds.RemoveAll(string.IsNullOrEmpty);

        var kalkulace = new List<(long Id, string Pid, string? Akceptace, DateTime? DatumAkceptace, decimal? Cena)>();
        foreach (var chunk in ticketIds.Chunk(Chunk))
        {
            var radky = await _intranet.HotKalkulace
                .Where(k => k.Pid != null && chunk.Contains(k.Pid))
                .Select(k => new { k.Id, k.Pid, k.Akceptace, k.DatumAkceptace, k.Cena })
                .ToListAsync(ct);
            kalkulace.AddRange(radky.Select(k => (k.Id, k.Pid!, k.Akceptace, k.DatumAkceptace, k.Cena)));
        }

        // Akceptace = zákazový seznam (sdílený helper výzev), z akceptovaných vyhrává nejvyšší id
        // (rozhodnutí uživatele 2026-09-08). Teprve pak rozhoduje datum akceptace té jedné kalkulace.
        return kalkulace
            .Where(k => SqlTicketingQueryService.JeAkceptovana(k.Akceptace))
            .GroupBy(k => k.Pid)
            .Select(g => g.OrderByDescending(k => k.Id).First())
            .Where(k => k.DatumAkceptace is { } d && d >= od && d <= @do
                        && int.TryParse(k.Pid, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out _))
            .Select(k => new AkceptovanaKalkulaceDto(
                int.Parse(k.Pid, System.Globalization.CultureInfo.InvariantCulture), k.DatumAkceptace!.Value, k.Cena ?? 0m))
            .OrderBy(k => k.DatumAkceptace)
            .ThenBy(k => k.TicketId)
            .ToList();
    }

    /// <summary>
    /// Tickety modulů IS, které mají číslo (ticket bez HOT_ZAZNAMY.id je mimo rozsah PM Trackeru).
    /// Sloupec <c>id</c> je INT NULL a převodník ho čte jako text (NULL → ""). Do dotazu proto patří jen
    /// <c>Id != null</c>: porovnání s "" převodník přeloží na <c>[id] &lt;&gt; NULL</c>, což není nikdy pravda
    /// a dotaz by na SQL Serveru nevrátil nic. Prázdné číslo (InMemory testy) odfiltruje volající v paměti.
    /// </summary>
    internal static IQueryable<Entities.HotZaznamEntity> ZaznamyIs(
        IQueryable<Entities.HotZaznamEntity> zaznamy, IReadOnlyCollection<string> moduly, bool vcetneArchivu)
        => zaznamy.Where(z => z.Id != null
                           && z.Modul != null && moduly.Contains(z.Modul)
                           && (vcetneArchivu || z.Stav != ArchivStav));

    private Task<List<string>> ModulyIsAsync(int isId, CancellationToken ct)
        => _intranet.HotModuly.Where(m => m.IdIS == isId).Select(m => m.Zkratka).ToListAsync(ct);

    /// <summary>
    /// <c>ticket_external</c> spárované po Trim na čísla ticketů. Nečíselné <c>external_id</c> se přeskočí,
    /// duplicita → nižší id (originál bere FirstOrDefault).
    /// </summary>
    private async Task<Dictionary<string, (int Id, string? SupplierTag)>> TicketExternalPodleIdAsync(
        IReadOnlyList<string> cislaTicketu, CancellationToken ct)
    {
        var radky = new List<(int Id, string ExternalId, string? SupplierTag)>();
        foreach (var chunk in cislaTicketu.Chunk(Chunk))
        {
            var cast = await TicketExternalPodleCisel(_sd.TicketExternal, chunk)
                .Select(t => new { t.Id, t.ExternalId, t.SupplierTag })
                .ToListAsync(ct);
            radky.AddRange(cast.Select(t => (t.Id, t.ExternalId.Trim(), t.SupplierTag)));
        }

        return radky
            .Where(r => CiselneId().IsMatch(r.ExternalId))
            .GroupBy(r => r.ExternalId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g =>
            {
                var prvni = g.OrderBy(r => r.Id).First();
                return (prvni.Id, prvni.SupplierTag);
            }, StringComparer.Ordinal);
    }

    /// <summary><c>external_id</c> bývá doplněné mezerami → porovnává se po ořezu na straně serveru.</summary>
    internal static IQueryable<Entities.TicketExternalEntity> TicketExternalPodleCisel(
        IQueryable<Entities.TicketExternalEntity> externi, IReadOnlyCollection<string> cislaTicketu)
        => externi.Where(t => cislaTicketu.Contains(t.ExternalId.Trim()));

    private async Task<HashSet<int>> ExterniIdSPredavkouOdAsync(IReadOnlyList<int> externiIds, DateTime od, CancellationToken ct)
    {
        var vysledek = new HashSet<int>();
        foreach (var chunk in externiIds.Distinct().Chunk(Chunk))
        {
            var ids = await _sd.TicketExternalWorkflow
                .Where(w => chunk.Contains(w.TicketExternalId) && w.Date >= od)
                .Select(w => w.TicketExternalId)
                .Distinct()
                .ToListAsync(ct);
            vysledek.UnionWith(ids);
        }
        return vysledek;
    }
}
