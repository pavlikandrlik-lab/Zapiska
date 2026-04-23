using Microsoft.EntityFrameworkCore;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.ServiceDesk.Sql.Entities;

namespace PmTracker.ServiceDesk.Sql;

/// <summary>
/// Read-only dotazy nad <c>HOT_IS</c>/<c>HOT_MODULY</c>/<c>HOT_ZAZNAMY</c> pro panel
/// "NES v prodlení" a IS rozpočet v projektovém dashboardu.
/// </summary>
public sealed class SqlInformacniSystemQueryService : IInformacniSystemQueryService
{
    private const string AktivniPrefix = "Aktivní";
    private const string ArchivStav = "archiv";

    private readonly TicketingReadOnlyDbContext _db;

    public SqlInformacniSystemQueryService(TicketingReadOnlyDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<InformacniSystemDto>> GetAktivniIsAsync(CancellationToken ct)
    {
        // aktivita je char(10) → StartsWith("Aktivní") spolehlivě matchne s trailing spaces.
        // SQL provider přeloží na LIKE 'Aktivní%', InMemory provider vyhodnotí klientsky.
        var raw = await _db.HotIs
            .Where(i => i.Aktivita != null && i.Aktivita.StartsWith(AktivniPrefix))
            .OrderBy(i => i.Id)
            .ToListAsync(ct);

        return raw
            .Select(i => new InformacniSystemDto(
                Id: i.Id,
                Nazev: i.Nazev ?? string.Empty,
                Zkratka: (i.Zkratka ?? string.Empty).Trim(),
                JeAktivni: true,
                Limit: i.Limit,
                Cerpani: i.Cerpani))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProdlenyTicketDto>> GetProdleneAsync(
        int isId, DateTime reference, CancellationToken ct)
    {
        // Nejdřív zjistit všechny modul zkratky pod daným IS.
        var modulyProIs = await _db.HotModuly
            .Where(m => m.IdIS == isId)
            .Select(m => m.Zkratka)
            .ToListAsync(ct);

        if (modulyProIs.Count == 0)
            return Array.Empty<ProdlenyTicketDto>();

        // Memory feedback (Kontext §11): ticket bez z.Id je mimo scope PM Trackeru.
        // Dva samostatné dotazy — jeden pro NES (sla_deadline), druhý pro PMP+PNF (dat_res_t).
        // InMemory provider nemá DATEDIFF, proto počítáme dny v C#.

        var nesRaw = await _db.HotZaznamy
            .Where(z => z.TypZaznamu == "NES"
                     && z.Stav != ArchivStav
                     && z.Id != null && z.Id != ""
                     && z.Modul != null
                     && modulyProIs.Contains(z.Modul)
                     && z.SlaDeadline != null
                     && z.SlaDeadline < reference)
            .OrderBy(z => z.SlaDeadline)
            .Select(z => new
            {
                z.Id, z.Pid, z.TypZaznamu, z.Strucne, z.Dulezitost, z.Zavaznost,
                z.Modul, z.Dodavatel, z.Stav, Termin = z.SlaDeadline!.Value
            })
            .ToListAsync(ct);

        var pmpPnfRaw = await _db.HotZaznamy
            .Where(z => (z.TypZaznamu == "PMP" || z.TypZaznamu == "PNF")
                     && z.Stav != ArchivStav
                     && z.Id != null && z.Id != ""
                     && z.Modul != null
                     && modulyProIs.Contains(z.Modul)
                     && z.DatResT != null
                     && z.DatResT < reference)
            .OrderBy(z => z.DatResT)
            .Select(z => new
            {
                z.Id, z.Pid, z.TypZaznamu, z.Strucne, z.Dulezitost, z.Zavaznost,
                z.Modul, z.Dodavatel, z.Stav, Termin = z.DatResT!.Value
            })
            .ToListAsync(ct);

        var all = nesRaw.Concat(pmpPnfRaw)
            .OrderBy(x => x.Termin)
            .Select(x => new ProdlenyTicketDto(
                // z.Id != null && != "" garantuje filtr výše; int.Parse záměrně bez fallbacku —
                // non-numeric id v DB by byl strukturální nesoulad, ať rupne hlasitě.
                Id: int.Parse(x.Id!),
                Pid: x.Pid ?? string.Empty,
                TypZaznamu: x.TypZaznamu ?? string.Empty,
                Strucne: x.Strucne,
                Dulezitost: x.Dulezitost,
                Zavaznost: x.Zavaznost,
                Modul: x.Modul,
                Dodavatel: x.Dodavatel,
                Stav: x.Stav,
                Termin: x.Termin,
                DniProdleni: (int)Math.Floor((reference.Date - x.Termin.Date).TotalDays)))
            .ToList();

        return all;
    }

    /// <inheritdoc />
    public async Task<IsRozpocetDto?> GetRozpocetAsync(int isId, CancellationToken ct)
    {
        var i = await _db.HotIs.FirstOrDefaultAsync(x => x.Id == isId, ct);
        if (i is null) return null;

        return new IsRozpocetDto(
            IsId: i.Id,
            IsZkratka: (i.Zkratka ?? string.Empty).Trim(),
            Limit: i.Limit,
            Cerpani: i.Cerpani);
    }
}
