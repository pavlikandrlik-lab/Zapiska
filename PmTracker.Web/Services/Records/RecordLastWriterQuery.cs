using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;

namespace PmTracker.Web.Services.Records;

public sealed record RecordLastWriter(string DisplayName, DateTime AtUtc);

/// <summary>
/// Spec 2026-09-17 §5.2 — verze záznamu pro detekci cizího zápisu.
///
/// Verzí je id posledního auditního zápisu nad záznamem. Audit s entitou „zaznam"
/// píší VÝHRADNĚ uživatelské cesty (uložení, smazání, doplnění identifikátoru,
/// schválení návrhu) — automatika (harvest, rebalance, sync harmonogramu) neaudituje
/// nic, takže její zásahy do kroků verzi neposunou. Právě na tom stojí celý návrh:
/// blokovat cizího ČLOVĚKA, ne automat.
///
/// Dotaz jede po indexu IX_authz_audit_log_entity_type_entity_id.
/// </summary>
public static class RecordVersionQuery
{
    private const string RecordEntityType = "zaznam";

    /// <summary>Vrací token verze, nebo prázdný řetězec, když záznam nemá žádný audit.</summary>
    public static async Task<string> ResolveVersionTokenAsync(
        PmTrackerDbContext dbContext,
        int recordId,
        CancellationToken ct)
    {
        var entityId = recordId.ToString(CultureInfo.InvariantCulture);
        var lastId = await dbContext.AuthzAuditLog.AsNoTracking()
            .Where(x => x.EntityType == RecordEntityType && x.EntityId == entityId)
            .MaxAsync(x => (long?)x.Id, ct);

        return lastId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }
}

/// <summary>
/// Spec 2026-09-17 §5.3 — poslední lidský zápis do záznamu z auditní tabulky.
/// Audit je jediný zdroj: projektove_zaznamy nemá sloupec „kdo naposledy upravil".
/// </summary>
public static class RecordLastWriterQuery
{
    private const string RecordEntityType = "zaznam";

    public static async Task<RecordLastWriter?> ResolveAsync(
        PmTrackerDbContext dbContext,
        int recordId,
        CancellationToken ct)
    {
        var entityId = recordId.ToString(CultureInfo.InvariantCulture);

        // Musí to být TENTÝŽ řádek, který <see cref="RecordVersionQuery"/> považuje za verzi,
        // tedy poslední podle id — jinak by hláška pojmenovala někoho, kdo s aktuální verzí
        // nemá nic společného (verzi posouvá i „assign“ identifikátoru z jednání, ne jen save).
        var last = await dbContext.AuthzAuditLog.AsNoTracking()
            .Where(x => x.EntityType == RecordEntityType && x.EntityId == entityId)
            .OrderByDescending(x => x.Id)
            .Select(x => new { x.ActorOsobaId, x.CreatedAt })
            .FirstOrDefaultAsync(ct);

        // Bez známého aktéra radši obecná hláška než cizí jméno — Build(null) ji umí.
        if (last?.ActorOsobaId is null)
        {
            return null;
        }

        var displayName = await PersonDisplayNameQuery.ResolveAsync(dbContext, last.ActorOsobaId.Value, ct);
        return new RecordLastWriter(displayName, last.CreatedAt);
    }
}

/// <summary>
/// Spec 2026-09-17 §5.3 — text hlášky. Oddělený od dotazu, aby šel testovat
/// bez databáze a aby nedohledaný autor nemohl shodit uložení.
/// </summary>
public static class RecordStaleMessageBuilder
{
    public static string Build(RecordLastWriter? writer)
    {
        if (writer is null)
        {
            return "Záznam mezitím uložil jiný uživatel. Vaše změny nelze uložit přes cizí verzi — načtěte záznam znovu.";
        }

        var stamp = writer.AtUtc.ToLocalTime().ToString("d.M.yyyy HH:mm", CultureInfo.GetCultureInfo("cs-CZ"));
        return $"Záznam mezitím uložil jiný uživatel: {writer.DisplayName} ({stamp}). "
             + "Vaše změny nelze uložit přes cizí verzi — načtěte záznam znovu.";
    }
}
