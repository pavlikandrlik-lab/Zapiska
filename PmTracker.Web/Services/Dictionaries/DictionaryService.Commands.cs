using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Dictionaries;
using PmTracker.Web.Services.Audit;

namespace PmTracker.Web.Services.Dictionaries;

public sealed partial class DictionaryService
{

    public async Task SaveCiselnikRowAsync(
        SaveCiselnikRowCommand command,
        CurrentUserContextViewModel currentUser,
        CancellationToken ct = default)
    {
        var key = (command.Key ?? string.Empty).Trim().ToLowerInvariant();
        if (!DictionarySecurityPolicy.CanAccessDictionary(key, currentUser.IsSuperAdmin))
        {
            throw new InvalidOperationException("Číselník harmonogramu může upravovat pouze superadmin.");
        }

        var saveHandlers = BuildCiselnikSaveHandlers();
        if (!saveHandlers.TryGetValue(key, out var handler))
        {
            throw new InvalidOperationException($"Neznámý číselník '{command.Key}'.");
        }

        await NormalizeDictionaryLockStateAsync(key, command, currentUser, ct);
        var detailBefore = command.Id.HasValue
            ? await BuildCiselnikDetailAsync(key, currentUser, ct)
            : null;
        var oldRow = command.Id.HasValue
            ? detailBefore?.Polozky.FirstOrDefault(x => x.Id == command.Id.Value)
            : null;
        await handler(command, ct);

        await dbContext.SaveChangesAsync(ct);
        var detailAfter = await BuildCiselnikDetailAsync(key, currentUser, ct);
        var persistedRow = command.Id.HasValue
            ? detailAfter.Polozky.FirstOrDefault(x => x.Id == command.Id.Value)
            : detailAfter.Polozky.FirstOrDefault(x =>
                string.Equals(x.Kod, command.Kod, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Nazev, command.Nazev, StringComparison.OrdinalIgnoreCase));
        if (persistedRow is null)
        {
            throw new InvalidOperationException("Uloženou položku číselníku se nepodařilo dohledat pro audit.");
        }

        auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
            oldRow is null ? AuditActionType.Create : AuditActionType.Update,
            AuditEntityType.Dictionary,
            persistedRow.Id.ToString(CultureInfo.InvariantCulture),
            oldRow is null ? null : ToDictionaryAuditSnapshot(key, oldRow),
            ToDictionaryAuditSnapshot(key, persistedRow)));
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteCiselnikRowAsync(
        DeleteCiselnikRowCommand command,
        CurrentUserContextViewModel currentUser,
        CancellationToken ct = default)
    {
        var key = (command.Key ?? string.Empty).Trim().ToLowerInvariant();
        if (!DictionarySecurityPolicy.CanAccessDictionary(key, currentUser.IsSuperAdmin))
        {
            throw new InvalidOperationException("Číselník harmonogramu může upravovat pouze superadmin.");
        }

        if (!currentUser.IsSuperAdmin
            && command.Id > 0
            && await TryGetDictionaryRowLockStateAsync(key, command.Id, ct) == true)
        {
            throw new InvalidOperationException("Systémové položky číselníků může mazat pouze superadmin.");
        }

        var detail = await BuildCiselnikDetailAsync(key, currentUser, ct);
        var rowForAudit = detail.Polozky.FirstOrDefault(x => x.Id == command.Id)
            ?? throw new InvalidOperationException($"Položka {command.Id} v číselníku '{command.Key}' nebyla nalezena.");

        var deleteHandlers = BuildCiselnikDeleteHandlers();
        if (!deleteHandlers.TryGetValue(key, out var handler))
        {
            throw new InvalidOperationException($"Neznámý číselník '{command.Key}'.");
        }

        await handler(command, ct);

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException($"Položku '{rowForAudit.Nazev}' nelze smazat, protože je používána v aplikaci.", ex);
        }

        auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
            AuditActionType.Delete,
            AuditEntityType.Dictionary,
            command.Id.ToString(CultureInfo.InvariantCulture),
            ToDictionaryAuditSnapshot(key, rowForAudit),
            null));
        await dbContext.SaveChangesAsync(ct);
    }

    private IReadOnlyDictionary<string, Func<SaveCiselnikRowCommand, CancellationToken, Task>> BuildCiselnikSaveHandlers()
    {
        return new Dictionary<string, Func<SaveCiselnikRowCommand, CancellationToken, Task>>(StringComparer.OrdinalIgnoreCase)
        {
            ["stavy-projektu"] = SaveProjectStatusRowAsync,
            ["stavy-ukolu"] = SaveTaskStatusRowAsync,
            ["kategorie-zaznamu"] = (command, ct) => UpsertSimpleCiselnikAsync(
                command,
                dbContext.CiselnikKategoriiZaznamu,
                (CiselnikKategoriiZaznamuEntity row, SaveCiselnikRowCommand cmd) =>
                {
                    row.Kod = cmd.Kod.Trim();
                    row.Nazev = cmd.Nazev.Trim();
                    row.IsLocked = cmd.IsLocked;
                },
                cmd => new CiselnikKategoriiZaznamuEntity
                {
                    Kod = cmd.Kod.Trim(),
                    Nazev = cmd.Nazev.Trim(),
                    IsLocked = cmd.IsLocked
                },
                ct),
            ["typy-ukolu"] = (command, ct) => UpsertSimpleCiselnikAsync(
                command,
                dbContext.CiselnikTypuUkolu,
                (CiselnikTypuUkoluEntity row, SaveCiselnikRowCommand cmd) =>
                {
                    row.Kod = cmd.Kod.Trim();
                    row.Nazev = cmd.Nazev.Trim();
                    row.IsLocked = cmd.IsLocked;
                },
                cmd => new CiselnikTypuUkoluEntity
                {
                    Kod = cmd.Kod.Trim(),
                    Nazev = cmd.Nazev.Trim(),
                    IsLocked = cmd.IsLocked
                },
                ct),
            ["typy-externich-odkazu"] = (command, ct) => UpsertSimpleCiselnikAsync(
                command,
                dbContext.CiselnikTypuExternichOdkazu,
                (CiselnikTypuExternichOdkazuEntity row, SaveCiselnikRowCommand cmd) =>
                {
                    row.Kod = cmd.Kod.Trim();
                    row.Nazev = cmd.Nazev.Trim();
                    row.IsLocked = cmd.IsLocked;
                },
                cmd => new CiselnikTypuExternichOdkazuEntity
                {
                    Kod = cmd.Kod.Trim(),
                    Nazev = cmd.Nazev.Trim(),
                    IsLocked = cmd.IsLocked
                },
                ct),
            ["role-projektu"] = (command, ct) => UpsertSimpleCiselnikAsync(
                command,
                dbContext.CiselnikRoliProjektu,
                (CiselnikRoliProjektuEntity row, SaveCiselnikRowCommand cmd) =>
                {
                    row.Kod = cmd.Kod.Trim();
                    row.Nazev = cmd.Nazev.Trim();
                    row.IsLocked = cmd.IsLocked;
                },
                cmd => new CiselnikRoliProjektuEntity
                {
                    Kod = cmd.Kod.Trim(),
                    Nazev = cmd.Nazev.Trim(),
                    IsLocked = cmd.IsLocked
                },
                ct),
            ["role-subsystemu"] = (command, ct) => UpsertSimpleCiselnikAsync(
                command,
                dbContext.CiselnikRoliSubsystemu,
                (CiselnikRoleSubsystemuEntity row, SaveCiselnikRowCommand cmd) =>
                {
                    row.Kod = cmd.Kod.Trim();
                    row.Nazev = cmd.Nazev.Trim();
                    row.IsLocked = cmd.IsLocked;
                },
                cmd => new CiselnikRoleSubsystemuEntity
                {
                    Kod = cmd.Kod.Trim(),
                    Nazev = cmd.Nazev.Trim(),
                    IsLocked = cmd.IsLocked
                },
                ct),
            ["stavy-ucasti"] = (command, ct) => UpsertSimpleCiselnikAsync(
                command,
                dbContext.CiselnikStavuUcasti,
                (CiselnikStavuUcastiEntity row, SaveCiselnikRowCommand cmd) =>
                {
                    row.Kod = cmd.Kod.Trim();
                    row.Nazev = cmd.Nazev.Trim();
                    row.IsLocked = cmd.IsLocked;
                },
                cmd => new CiselnikStavuUcastiEntity
                {
                    Kod = cmd.Kod.Trim(),
                    Nazev = cmd.Nazev.Trim(),
                    IsLocked = cmd.IsLocked
                },
                ct),
            ["organizace"] = (command, ct) => UpsertSimpleCiselnikAsync(
                command,
                dbContext.CiselnikOrganizace,
                (CiselnikOrganizaceEntity row, SaveCiselnikRowCommand cmd) =>
                {
                    row.Kod = cmd.Kod.Trim();
                    row.Nazev = cmd.Nazev.Trim();
                    row.IsLocked = cmd.IsLocked;
                },
                cmd => new CiselnikOrganizaceEntity
                {
                    Kod = cmd.Kod.Trim(),
                    Nazev = cmd.Nazev.Trim(),
                    IsLocked = cmd.IsLocked
                },
                ct),
            ["organizacni-celky"] = (command, ct) => UpsertSimpleCiselnikAsync(
                command,
                dbContext.CiselnikOrganizacniCelky,
                (CiselnikOrganizacniCelekEntity row, SaveCiselnikRowCommand cmd) =>
                {
                    row.Kod = cmd.Kod.Trim();
                    row.Nazev = cmd.Nazev.Trim();
                    row.IsLocked = cmd.IsLocked;
                },
                cmd => new CiselnikOrganizacniCelekEntity
                {
                    Kod = cmd.Kod.Trim(),
                    Nazev = cmd.Nazev.Trim(),
                    IsLocked = cmd.IsLocked
                },
                ct),
            ["subsystemy"] = SaveSubsystemRowAsync,
            ["stavy-jednani"] = (command, ct) => UpsertSimpleCiselnikAsync(
                command,
                dbContext.CiselnikStavuJednani,
                (CiselnikStavuJednaniEntity row, SaveCiselnikRowCommand cmd) =>
                {
                    row.Kod = cmd.Kod.Trim();
                    row.Nazev = cmd.Nazev.Trim();
                    row.IsLocked = cmd.IsLocked;
                },
                cmd => new CiselnikStavuJednaniEntity
                {
                    Kod = cmd.Kod.Trim(),
                    Nazev = cmd.Nazev.Trim(),
                    IsLocked = cmd.IsLocked
                },
                ct),
            ["vyzvy"] = SaveVyzvaRowAsync
        };
    }

    private IReadOnlyDictionary<string, Func<DeleteCiselnikRowCommand, CancellationToken, Task>> BuildCiselnikDeleteHandlers()
    {
        return new Dictionary<string, Func<DeleteCiselnikRowCommand, CancellationToken, Task>>(StringComparer.OrdinalIgnoreCase)
        {
            ["stavy-projektu"] = (command, ct) => RemoveCiselnikRowAsync(dbContext.CiselnikStavuProjektu, command.Id, "stavy-projektu", ct),
            ["stavy-ukolu"] = (command, ct) => RemoveCiselnikRowAsync(dbContext.CiselnikStavuUkolu, command.Id, "stavy-ukolu", ct),
            ["kategorie-zaznamu"] = (command, ct) => RemoveCiselnikRowAsync(dbContext.CiselnikKategoriiZaznamu, command.Id, "kategorie-zaznamu", ct),
            ["typy-ukolu"] = (command, ct) => RemoveCiselnikRowAsync(dbContext.CiselnikTypuUkolu, command.Id, "typy-ukolu", ct),
            ["typy-externich-odkazu"] = (command, ct) => RemoveCiselnikRowAsync(dbContext.CiselnikTypuExternichOdkazu, command.Id, "typy-externich-odkazu", ct),
            ["role-projektu"] = (command, ct) => RemoveCiselnikRowAsync(dbContext.CiselnikRoliProjektu, command.Id, "role-projektu", ct),
            ["role-subsystemu"] = (command, ct) => RemoveCiselnikRowAsync(dbContext.CiselnikRoliSubsystemu, command.Id, "role-subsystemu", ct),
            ["stavy-ucasti"] = (command, ct) => RemoveCiselnikRowAsync(dbContext.CiselnikStavuUcasti, command.Id, "stavy-ucasti", ct),
            ["organizace"] = (command, ct) => RemoveCiselnikRowAsync(dbContext.CiselnikOrganizace, command.Id, "organizace", ct),
            ["organizacni-celky"] = (command, ct) => RemoveCiselnikRowAsync(dbContext.CiselnikOrganizacniCelky, command.Id, "organizacni-celky", ct),
            ["subsystemy"] = (command, ct) => RemoveCiselnikRowAsync(dbContext.Subsystemy, command.Id, "subsystemy", ct),
            ["stavy-jednani"] = (command, ct) => RemoveCiselnikRowAsync(dbContext.CiselnikStavuJednani, command.Id, "stavy-jednani", ct),
            ["vyzvy"] = (command, ct) => RemoveCiselnikRowAsync(dbContext.Vyzvy, command.Id, "vyzvy", ct)
        };
    }

    private async Task NormalizeDictionaryLockStateAsync(
        string key,
        SaveCiselnikRowCommand command,
        CurrentUserContextViewModel currentUser,
        CancellationToken ct)
    {
        if (!currentUser.IsSuperAdmin
            && command.Id.HasValue
            && await TryGetDictionaryRowLockStateAsync(key, command.Id.Value, ct) == true)
        {
            throw new InvalidOperationException("Systémové položky číselníků může upravovat nebo odemykat pouze superadmin.");
        }

        command.IsLocked = DictionarySecurityPolicy.NormalizeRequestedLockState(key, command.IsLocked, currentUser.IsSuperAdmin);
    }

    private Task<bool?> TryGetDictionaryRowLockStateAsync(string key, int id, CancellationToken ct)
    {
        return key switch
        {
            "stavy-projektu" => dbContext.CiselnikStavuProjektu.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsLocked).FirstOrDefaultAsync(ct),
            "stavy-ukolu" => dbContext.CiselnikStavuUkolu.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsLocked).FirstOrDefaultAsync(ct),
            "kategorie-zaznamu" => dbContext.CiselnikKategoriiZaznamu.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsLocked).FirstOrDefaultAsync(ct),
            "typy-ukolu" => dbContext.CiselnikTypuUkolu.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsLocked).FirstOrDefaultAsync(ct),
            "typy-externich-odkazu" => dbContext.CiselnikTypuExternichOdkazu.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsLocked).FirstOrDefaultAsync(ct),
            "role-projektu" => dbContext.CiselnikRoliProjektu.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsLocked).FirstOrDefaultAsync(ct),
            "stavy-ucasti" => dbContext.CiselnikStavuUcasti.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsLocked).FirstOrDefaultAsync(ct),
            "organizace" => dbContext.CiselnikOrganizace.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsLocked).FirstOrDefaultAsync(ct),
            "organizacni-celky" => dbContext.CiselnikOrganizacniCelky.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsLocked).FirstOrDefaultAsync(ct),
            "vyzvy" => dbContext.Vyzvy.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)(x.Stav == VyzvaStav.Odeslano)).FirstOrDefaultAsync(ct),
            "stavy-jednani" => dbContext.CiselnikStavuJednani.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsLocked).FirstOrDefaultAsync(ct),
            _ => Task.FromResult<bool?>(null)
        };
    }

    private async Task SaveProjectStatusRowAsync(SaveCiselnikRowCommand command, CancellationToken ct)
    {
        if (command.Id.HasValue)
        {
            var row = await dbContext.CiselnikStavuProjektu.FirstOrDefaultAsync(x => x.Id == command.Id.Value, ct)
                ?? throw new InvalidOperationException($"Řádek {command.Id.Value} nebyl nalezen.");
            row.Kod = command.Kod.Trim();
            row.Nazev = command.Nazev.Trim();
            row.IsLocked = command.IsLocked;
            return;
        }

        dbContext.CiselnikStavuProjektu.Add(new CiselnikStavuProjektuEntity
        {
            Kod = command.Kod.Trim(),
            Nazev = command.Nazev.Trim(),
            IsLocked = command.IsLocked
        });
    }

    private async Task SaveTaskStatusRowAsync(SaveCiselnikRowCommand command, CancellationToken ct)
    {
        var isFinal = command.HodnotyNavic.FirstOrDefault()?.Equals("ano", StringComparison.OrdinalIgnoreCase) == true
            || command.HodnotyNavic.FirstOrDefault() == "1"
            || command.HodnotyNavic.FirstOrDefault()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

        if (command.Id.HasValue)
        {
            var row = await dbContext.CiselnikStavuUkolu.FirstOrDefaultAsync(x => x.Id == command.Id.Value, ct)
                ?? throw new InvalidOperationException($"Řádek {command.Id.Value} nebyl nalezen.");
            row.Kod = command.Kod.Trim();
            row.Nazev = command.Nazev.Trim();
            row.IsLocked = command.IsLocked;
            row.IsFinal = isFinal;
            return;
        }

        dbContext.CiselnikStavuUkolu.Add(new CiselnikStavuUkoluEntity
        {
            Kod = command.Kod.Trim(),
            Nazev = command.Nazev.Trim(),
            IsLocked = command.IsLocked,
            IsFinal = isFinal
        });
    }

    private async Task SaveSubsystemRowAsync(SaveCiselnikRowCommand command, CancellationToken ct)
    {
        var kod = command.Kod.Trim();
        var nazev = command.Nazev.Trim();
        if (string.IsNullOrWhiteSpace(kod))
        {
            throw new InvalidOperationException("Kód subsystému je povinný.");
        }

        if (string.IsNullOrWhiteSpace(nazev))
        {
            throw new InvalidOperationException("Název subsystému je povinný.");
        }

        var duplicateCodeExists = await dbContext.Subsystemy
            .AsNoTracking()
            .AnyAsync(x => x.Kod == kod && (!command.Id.HasValue || x.Id != command.Id.Value), ct);
        if (duplicateCodeExists)
        {
            throw new InvalidOperationException($"Subsystém s kódem '{kod}' už existuje.");
        }

        if (command.Id.HasValue)
        {
            var row = await dbContext.Subsystemy.FirstOrDefaultAsync(x => x.Id == command.Id.Value, ct)
                ?? throw new InvalidOperationException($"Subsystém {command.Id.Value} nebyl nalezen.");
            row.Kod = kod;
            row.Nazev = nazev;
            return;
        }

        dbContext.Subsystemy.Add(new SubsystemEntity
        {
            Kod = kod,
            Nazev = nazev
        });
    }

    private async Task SaveVyzvaRowAsync(SaveCiselnikRowCommand command, CancellationToken ct)
    {
        var year = timeProvider.GetLocalNow().Year;
        var yearRaw = command.HodnotyNavic.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(yearRaw))
        {
            if (int.TryParse(yearRaw, out var y) && y > 1900 && y < 9999)
            {
                year = y;
            }
            else if (DateTime.TryParse(yearRaw, out var parsed))
            {
                year = parsed.Year;
            }
        }

        if (command.Id.HasValue)
        {
            var row = await dbContext.Vyzvy.FirstOrDefaultAsync(x => x.Id == command.Id.Value, ct)
                ?? throw new InvalidOperationException($"Řádek {command.Id.Value} nebyl nalezen.");
            row.Kod = command.Kod.Trim();
            row.Rok = year;
            return;
        }

        // TODO (fáze 2): fallback číselníkový upsert přes dictionary UI; povinná pole jsou placeholder.
        dbContext.Vyzvy.Add(new VyzvaEntity
        {
            ProjektId = 1,
            Kod = command.Kod.Trim(),
            PoradoveVRoce = 0,
            Rok = year,
            Stav = VyzvaStav.Priprava,
            DatumZalozeni = timeProvider.GetUtcNow().UtcDateTime,
            ZalozilOsobaId = 0,
            MistoPlneniSnapshot = string.Empty,
            CisloRamcoveSmlouvySnapshot = string.Empty,
        });
    }

    private static async Task UpsertSimpleCiselnikAsync<T>(
        SaveCiselnikRowCommand command,
        DbSet<T> set,
        Action<T, SaveCiselnikRowCommand> assign,
        Func<SaveCiselnikRowCommand, T> create,
        CancellationToken ct)
        where T : class
    {
        if (command.Id.HasValue)
        {
            var row = await set.FindAsync([command.Id.Value], ct);
            if (row is null)
            {
                throw new InvalidOperationException($"Řádek {command.Id.Value} nebyl nalezen.");
            }

            assign(row, command);
            return;
        }

        set.Add(create(command));
    }

    private static async Task RemoveCiselnikRowAsync<T>(DbSet<T> set, int id, string key, CancellationToken ct)
        where T : class
    {
        var row = await set.FirstOrDefaultAsync(x => EF.Property<int>(x, "Id") == id, ct);
        if (row is null)
        {
            throw new InvalidOperationException($"Položka {id} v číselníku '{key}' nebyla nalezena.");
        }

        set.Remove(row);
    }

    private static DictionaryAuditSnapshot ToDictionaryAuditSnapshot(string key, CiselnikRadekViewModel row)
        => new(
            key,
            row.Id,
            row.Kod,
            row.Nazev,
            row.IsLocked,
            null,
            row.HodnotyNavicRaw.FirstOrDefault() ?? row.HodnotyNavic.FirstOrDefault());
}
