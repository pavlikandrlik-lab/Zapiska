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
