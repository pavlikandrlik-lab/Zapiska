using Microsoft.EntityFrameworkCore;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.ServiceDesk.Sql.Entities;

namespace PmTracker.ServiceDesk.Sql;

public sealed class SqlTicketingQueryService : ITicketingQueryService
{
    /// <summary>
    /// Stavy, se kterými se kalkulace do výzvy NEDOSTANE (zadání uživatele 2026-09-09).
    /// Je to zákazový seznam, ne povolovací: akceptovaná je každá jiná hodnota.
    ///
    /// Do 2026-09-09 se filtrovalo na jedinou hodnotu „Akceptováno". Produkce jich ale
    /// používá víc — třeba „Fakturovat" u kalkulace, kterou projektový manažer prokazatelně
    /// akceptoval. Takové PNF se do výzvy vytisklo s nulovou cenou, bez chyby a bez varování.
    ///
    /// POZOR na směr selhání: neznámý stav se bere jako akceptovaný. Kdyby ServiceDesk
    /// zavedl nový rozpracovaný stav, vytiskl by se — proto ten seznam patří rozšířit
    /// hned, jak se takový stav objeví.
    ///
    /// Klíče jsou normalizované (bez bílých znaků, malými písmeny), viz NormalizovatStav.
    /// </summary>
    private static readonly HashSet<string> NeakceptovaneStavy = new(StringComparer.Ordinal)
    {
        "návrh",
        "neakceptováno",
        "akceptovat?",
    };

    private readonly TicketingReadOnlyDbContext _db;

    public SqlTicketingQueryService(TicketingReadOnlyDbContext db)
    {
        _db = db;
    }

    public async Task<HotZaznamDto?> GetZaznamAsync(string cislo, CancellationToken ct)
    {
        var z = await _db.HotZaznamy.FirstOrDefaultAsync(r => r.Id == cislo, ct);
        return z == null ? null : MapZaznam(z);
    }

    public async Task<IReadOnlyDictionary<string, HotZaznamDto>> GetZaznamyAsync(
        IReadOnlyCollection<string> cisla, CancellationToken ct)
    {
        if (cisla.Count == 0) return new Dictionary<string, HotZaznamDto>();

        var cislaArr = cisla.Distinct().ToArray();
        var raw = await _db.HotZaznamy
            .Where(r => cislaArr.Contains(r.Id))
            .ToListAsync(ct);
        return raw.ToDictionary(r => r.Id, MapZaznam);
    }

    public async Task<HotKalkulaceDto?> GetAkceptovanouKalkulaciAsync(string cislo, CancellationToken ct)
    {
        // Filtr akceptace běží v paměti, ne v SQL: normalizace stavu (bílé znaky, velikost
        // písmen) se nedá spolehlivě přeložit do dotazu a řádků na jedno pid je pár —
        // jsou to revize téže kalkulace.
        var kalkulace = await _db.HotKalkulace
            .Where(x => x.Pid == cislo)
            .ToListAsync(ct);

        var k = kalkulace
            .Where(x => JeAkceptovana(x.Akceptace))
            // Z akceptovaných vyhrává vyšší id (rozhodnutí uživatele 2026-09-08).
            // Sloupec verze nese označení technického zadání, ne pořadí revizí.
            .OrderByDescending(x => x.Id)
            .FirstOrDefault();
        return k == null ? null : MapKalkulace(k);
    }

    public async Task<IReadOnlyDictionary<string, HotKalkulaceDto>> GetAkceptovaneKalkulaceAsync(
        IReadOnlyCollection<string> cisla, CancellationToken ct)
    {
        if (cisla.Count == 0) return new Dictionary<string, HotKalkulaceDto>();

        var cislaArr = cisla.Distinct().ToArray();
        var raw = await _db.HotKalkulace
            .Where(x => x.Pid != null && cislaArr.Contains(x.Pid))
            .ToListAsync(ct);

        return raw
            .Where(x => JeAkceptovana(x.Akceptace))
            .GroupBy(x => x.Pid!)
            .ToDictionary(g => g.Key, g => MapKalkulace(g.OrderByDescending(k => k.Id).First()));
    }

    /// <summary>
    /// Akceptovaná je každá kalkulace, jejíž stav není na zákazovém seznamu. Prázdná
    /// hodnota se za akceptaci nepovažuje — ServiceDesk stav dopisuje průběžně, takže
    /// nevyplněno znamená „zatím se nic nestalo", ne souhlas.
    /// </summary>
    private static bool JeAkceptovana(string? akceptace)
    {
        if (string.IsNullOrWhiteSpace(akceptace))
        {
            return false;
        }

        return !NeakceptovaneStavy.Contains(NormalizovatStav(akceptace));
    }

    /// <summary>
    /// Sjednotí zápis stavu: zahodí všechny bílé znaky a převede na malá písmena.
    /// ServiceDesk je stará aplikace psaná postupně a stavy do ní chodí jako text,
    /// takže „Akceptovat ?" a „Akceptovat?" znamenají totéž a musí dopadnout stejně.
    /// </summary>
    private static string NormalizovatStav(string akceptace)
    {
        Span<char> buffer = stackalloc char[akceptace.Length];
        var delka = 0;
        foreach (var znak in akceptace)
        {
            if (!char.IsWhiteSpace(znak))
            {
                buffer[delka++] = char.ToLowerInvariant(znak);
            }
        }

        return new string(buffer[..delka]);
    }

    private static HotZaznamDto MapZaznam(HotZaznamEntity e)
        => new(e.Id, e.TypZaznamu, e.Strucne, e.Popis, e.Uzivatel, e.Subsystem, e.Modul, e.Pid);

    private static HotKalkulaceDto MapKalkulace(HotKalkulaceEntity k)
        => new(
            k.Id, k.Pid ?? string.Empty,
            k.PracnostA, k.SazbaA, k.CenaA,
            k.PracnostP, k.SazbaP, k.CenaP,
            k.PracnostT, k.SazbaT, k.CenaT,
            k.PracnostI, k.SazbaI, k.CenaI,
            k.Cena,
            k.PocetL, k.SazbaL, k.CenaL, k.RozpadLicence,
            k.TextTermin);
}
