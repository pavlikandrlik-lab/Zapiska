using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;

namespace PmTracker.Web.Services.Records;

/// <summary>
/// Spec 2026-09-17 §4.3 / §5.3 — tvar jména v hláškách o souběhu: „Příjmení Jméno".
/// Záměrně BEZ titulu: hláška má kolegu identifikovat, ne titulovat. Jeden zdroj pravdy,
/// aby aplikace o téže osobě nemluvila na dvou místech dvěma způsoby.
/// </summary>
public static class PersonDisplayName
{
    public const string Unknown = "neznámý uživatel";

    public static string Format(string? prijmeni, string? jmeno)
    {
        var name = $"{prijmeni} {jmeno}".Trim();
        return string.IsNullOrWhiteSpace(name) ? Unknown : name;
    }
}

public static class PersonDisplayNameQuery
{
    public static async Task<string> ResolveAsync(
        PmTrackerDbContext dbContext,
        int osobaId,
        CancellationToken ct)
    {
        var osoba = await dbContext.Osoby.AsNoTracking()
            .Where(x => x.Id == osobaId)
            .Select(x => new { x.Jmeno, x.Prijmeni })
            .FirstOrDefaultAsync(ct);

        return osoba is null
            ? PersonDisplayName.Unknown
            : PersonDisplayName.Format(osoba.Prijmeni, osoba.Jmeno);
    }
}
