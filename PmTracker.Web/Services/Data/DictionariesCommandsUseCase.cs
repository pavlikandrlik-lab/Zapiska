using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Dictionaries;

namespace PmTracker.Web.Services.Data;

public sealed class DictionariesCommandsUseCase(
    PmTrackerDbContext dbContext) : IDictionariesCommandsUseCase
{
    private const string HarmonogramKrokyCiselnikKey = "harmonogram-kroky";

    public void SaveCiselnikRow(SaveCiselnikRowCommand command, CurrentUserContextViewModel currentUser, IDictionariesCommandsComposition composition)
    {
        var key = (command.Key ?? string.Empty).Trim().ToLowerInvariant();
        if (!DictionarySecurityPolicy.CanAccessDictionary(key, currentUser.IsSuperAdmin))
        {
            throw new InvalidOperationException("Číselník harmonogramu může upravovat pouze superadmin.");
        }

        var saveHandlers = BuildCiselnikSaveHandlers(composition);
        if (!saveHandlers.TryGetValue(key, out var handler))
        {
            throw new InvalidOperationException($"Neznámý číselník '{command.Key}'.");
        }

        NormalizeDictionaryLockState(key, command, currentUser);
        handler(command);

        dbContext.SaveChanges();
        WriteAudit(currentUser.OsobaId, $"ciselnik:{command.Key}", command.Id?.ToString(CultureInfo.InvariantCulture) ?? "new", "upsert", null, JsonSerializer.Serialize(command));
    }

    public void DeleteCiselnikRow(DeleteCiselnikRowCommand command, CurrentUserContextViewModel currentUser, IDictionariesCommandsComposition composition)
    {
        var key = (command.Key ?? string.Empty).Trim().ToLowerInvariant();
        if (!DictionarySecurityPolicy.CanAccessDictionary(key, currentUser.IsSuperAdmin))
        {
            throw new InvalidOperationException("Číselník harmonogramu může upravovat pouze superadmin.");
        }

        if (!currentUser.IsSuperAdmin && command.Id > 0 && TryGetDictionaryRowLockState(key, command.Id) == true)
        {
            throw new InvalidOperationException("Systémové položky číselníků může mazat pouze superadmin.");
        }

        var detail = composition.BuildCiselnikDetail(key, currentUser);
        var rowForAudit = detail.Polozky.FirstOrDefault(x => x.Id == command.Id)
            ?? throw new InvalidOperationException($"Položka {command.Id} v číselníku '{command.Key}' nebyla nalezena.");

        var deleteHandlers = BuildCiselnikDeleteHandlers(composition);
        if (!deleteHandlers.TryGetValue(key, out var handler))
        {
            throw new InvalidOperationException($"Neznámý číselník '{command.Key}'.");
        }

        handler(command);

        try
        {
            dbContext.SaveChanges();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException($"Položku '{rowForAudit.Nazev}' nelze smazat, protože je používána v aplikaci.", ex);
        }

        WriteAudit(
            currentUser.OsobaId,
            $"ciselnik:{command.Key}",
            command.Id.ToString(CultureInfo.InvariantCulture),
            "delete",
            JsonSerializer.Serialize(rowForAudit),
            null);
    }

    private IReadOnlyDictionary<string, Action<SaveCiselnikRowCommand>> BuildCiselnikSaveHandlers(IDictionariesCommandsComposition composition)
    {
        return new Dictionary<string, Action<SaveCiselnikRowCommand>>(StringComparer.OrdinalIgnoreCase)
        {
            ["stavy-projektu"] = SaveProjectStatusRow,
            ["stavy-ukolu"] = SaveTaskStatusRow,
            ["kategorie-zaznamu"] = SaveKategorieRow,
            ["typy-ukolu"] = SaveTypUkoluRow,
            ["typy-externich-odkazu"] = SaveTypExternihoOdkazuRow,
            ["role-projektu"] = SaveRoleProjektuRow,
            ["role-subsystemu"] = SaveRoleSubsystemuRow,
            ["stavy-ucasti"] = SaveStavUcastiRow,
            ["organizace"] = SaveOrganizaceRow,
            ["organizacni-celky"] = SaveOrgUnitRow,
            ["subsystemy"] = SaveSubsystemRow,
            [HarmonogramKrokyCiselnikKey] = composition.SaveHarmonogramStepRow,
            ["stavy-jednani"] = SaveStavJednaniRow,
            ["vyzvy"] = SaveVyzvaRow
        };
    }

    private IReadOnlyDictionary<string, Action<DeleteCiselnikRowCommand>> BuildCiselnikDeleteHandlers(IDictionariesCommandsComposition composition)
    {
        return new Dictionary<string, Action<DeleteCiselnikRowCommand>>(StringComparer.OrdinalIgnoreCase)
        {
            ["stavy-projektu"] = command => RemoveCiselnikRow(dbContext.CiselnikStavuProjektu, command.Id, "stavy-projektu"),
            ["stavy-ukolu"] = command => RemoveCiselnikRow(dbContext.CiselnikStavuUkolu, command.Id, "stavy-ukolu"),
            ["kategorie-zaznamu"] = command => RemoveCiselnikRow(dbContext.CiselnikKategoriiZaznamu, command.Id, "kategorie-zaznamu"),
            ["typy-ukolu"] = command => RemoveCiselnikRow(dbContext.CiselnikTypuUkolu, command.Id, "typy-ukolu"),
            ["typy-externich-odkazu"] = command => RemoveCiselnikRow(dbContext.CiselnikTypuExternichOdkazu, command.Id, "typy-externich-odkazu"),
            ["role-projektu"] = command => RemoveCiselnikRow(dbContext.CiselnikRoliProjektu, command.Id, "role-projektu"),
            ["role-subsystemu"] = command => RemoveCiselnikRow(dbContext.CiselnikRoliSubsystemu, command.Id, "role-subsystemu"),
            ["stavy-ucasti"] = command => RemoveCiselnikRow(dbContext.CiselnikStavuUcasti, command.Id, "stavy-ucasti"),
            ["organizace"] = command => RemoveCiselnikRow(dbContext.CiselnikOrganizace, command.Id, "organizace"),
            ["organizacni-celky"] = command => RemoveCiselnikRow(dbContext.CiselnikOrganizacniCelky, command.Id, "organizacni-celky"),
            ["subsystemy"] = command => RemoveCiselnikRow(dbContext.Subsystemy, command.Id, "subsystemy"),
            [HarmonogramKrokyCiselnikKey] = composition.DeleteHarmonogramStepRow,
            ["stavy-jednani"] = command => RemoveCiselnikRow(dbContext.CiselnikStavuJednani, command.Id, "stavy-jednani"),
            ["vyzvy"] = command => RemoveCiselnikRow(dbContext.CiselnikVyzvy, command.Id, "vyzvy")
        };
    }

    private void NormalizeDictionaryLockState(string key, SaveCiselnikRowCommand command, CurrentUserContextViewModel currentUser)
    {
        if (!currentUser.IsSuperAdmin && command.Id.HasValue && TryGetDictionaryRowLockState(key, command.Id.Value) == true)
        {
            throw new InvalidOperationException("Systémové položky číselníků může upravovat nebo odemykat pouze superadmin.");
        }

        command.IsLocked = DictionarySecurityPolicy.NormalizeRequestedLockState(key, command.IsLocked, currentUser.IsSuperAdmin);
    }

    private bool? TryGetDictionaryRowLockState(string key, int id)
    {
        return key switch
        {
            "stavy-projektu" => dbContext.CiselnikStavuProjektu.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsLocked).FirstOrDefault(),
            "stavy-ukolu" => dbContext.CiselnikStavuUkolu.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsLocked).FirstOrDefault(),
            "kategorie-zaznamu" => dbContext.CiselnikKategoriiZaznamu.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsLocked).FirstOrDefault(),
            "typy-ukolu" => dbContext.CiselnikTypuUkolu.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsLocked).FirstOrDefault(),
            "typy-externich-odkazu" => dbContext.CiselnikTypuExternichOdkazu.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsLocked).FirstOrDefault(),
            "role-projektu" => dbContext.CiselnikRoliProjektu.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsLocked).FirstOrDefault(),
            "stavy-ucasti" => dbContext.CiselnikStavuUcasti.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsLocked).FirstOrDefault(),
            "organizace" => dbContext.CiselnikOrganizace.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsLocked).FirstOrDefault(),
            "organizacni-celky" => dbContext.CiselnikOrganizacniCelky.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsLocked).FirstOrDefault(),
            "vyzvy" => dbContext.CiselnikVyzvy.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsLocked).FirstOrDefault(),
            "stavy-jednani" => dbContext.CiselnikStavuJednani.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsLocked).FirstOrDefault(),
            HarmonogramKrokyCiselnikKey => true,
            _ => null
        };
    }

    private void SaveProjectStatusRow(SaveCiselnikRowCommand command)
    {
        if (command.Id.HasValue)
        {
            var row = dbContext.CiselnikStavuProjektu.First(x => x.Id == command.Id.Value);
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

    private void SaveTaskStatusRow(SaveCiselnikRowCommand command)
    {
        var isFinal = command.HodnotyNavic.FirstOrDefault()?.Equals("ano", StringComparison.OrdinalIgnoreCase) == true
            || command.HodnotyNavic.FirstOrDefault() == "1"
            || command.HodnotyNavic.FirstOrDefault()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

        if (command.Id.HasValue)
        {
            var row = dbContext.CiselnikStavuUkolu.First(x => x.Id == command.Id.Value);
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

    private void SaveKategorieRow(SaveCiselnikRowCommand command)
        => UpsertSimpleCiselnik(
            command,
            dbContext.CiselnikKategoriiZaznamu,
            (row, cmd) =>
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
            });

    private void SaveTypUkoluRow(SaveCiselnikRowCommand command)
        => UpsertSimpleCiselnik(
            command,
            dbContext.CiselnikTypuUkolu,
            (row, cmd) =>
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
            });

    private void SaveTypExternihoOdkazuRow(SaveCiselnikRowCommand command)
        => UpsertSimpleCiselnik(
            command,
            dbContext.CiselnikTypuExternichOdkazu,
            (row, cmd) =>
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
            });

    private void SaveRoleProjektuRow(SaveCiselnikRowCommand command)
        => UpsertSimpleCiselnik(
            command,
            dbContext.CiselnikRoliProjektu,
            (row, cmd) =>
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
            });

    private void SaveRoleSubsystemuRow(SaveCiselnikRowCommand command)
        => UpsertSimpleCiselnik(
            command,
            dbContext.CiselnikRoliSubsystemu,
            (row, cmd) =>
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
            });

    private void SaveStavUcastiRow(SaveCiselnikRowCommand command)
        => UpsertSimpleCiselnik(
            command,
            dbContext.CiselnikStavuUcasti,
            (row, cmd) =>
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
            });

    private void SaveOrganizaceRow(SaveCiselnikRowCommand command)
        => UpsertSimpleCiselnik(
            command,
            dbContext.CiselnikOrganizace,
            (row, cmd) =>
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
            });

    private void SaveOrgUnitRow(SaveCiselnikRowCommand command)
        => UpsertSimpleCiselnik(
            command,
            dbContext.CiselnikOrganizacniCelky,
            (row, cmd) =>
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
            });

    private void SaveSubsystemRow(SaveCiselnikRowCommand command)
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

        var duplicateCodeExists = dbContext.Subsystemy.AsNoTracking().Any(x =>
            x.Kod == kod && (!command.Id.HasValue || x.Id != command.Id.Value));
        if (duplicateCodeExists)
        {
            throw new InvalidOperationException($"Subsystém s kódem '{kod}' už existuje.");
        }

        if (command.Id.HasValue)
        {
            var row = dbContext.Subsystemy.FirstOrDefault(x => x.Id == command.Id.Value)
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

    private void SaveStavJednaniRow(SaveCiselnikRowCommand command)
        => UpsertSimpleCiselnik(
            command,
            dbContext.CiselnikStavuJednani,
            (row, cmd) =>
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
            });

    private void SaveVyzvaRow(SaveCiselnikRowCommand command)
    {
        var year = DateTime.Today;
        var yearRaw = command.HodnotyNavic.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(yearRaw))
        {
            if (int.TryParse(yearRaw, out var y) && y > 1900 && y < 9999)
            {
                year = new DateTime(y, 1, 1);
            }
            else if (DateTime.TryParse(yearRaw, out var parsed))
            {
                year = new DateTime(parsed.Year, 1, 1);
            }
        }

        if (command.Id.HasValue)
        {
            var row = dbContext.CiselnikVyzvy.First(x => x.Id == command.Id.Value);
            row.Kod = command.Kod.Trim();
            row.Nazev = command.Nazev.Trim();
            row.Rok = year;
            row.IsLocked = command.IsLocked;
            return;
        }

        dbContext.CiselnikVyzvy.Add(new CiselnikVyzvaEntity
        {
            Kod = command.Kod.Trim(),
            Nazev = command.Nazev.Trim(),
            Rok = year,
            IsLocked = command.IsLocked
        });
    }

    private static void UpsertSimpleCiselnik<T>(
        SaveCiselnikRowCommand command,
        DbSet<T> set,
        Action<T, SaveCiselnikRowCommand> assign,
        Func<SaveCiselnikRowCommand, T> create)
        where T : class
    {
        if (command.Id.HasValue)
        {
            var row = set.Find(command.Id.Value);
            if (row is null)
            {
                throw new InvalidOperationException($"Řádek {command.Id.Value} nebyl nalezen.");
            }

            assign(row, command);
            return;
        }

        var created = create(command);
        set.Add(created);
    }

    private static void RemoveCiselnikRow<T>(DbSet<T> set, int id, string key)
        where T : class
    {
        var row = set.FirstOrDefault(x => EF.Property<int>(x, "Id") == id);
        if (row is null)
        {
            throw new InvalidOperationException($"Položka {id} v číselníku '{key}' nebyla nalezena.");
        }

        set.Remove(row);
    }

    private void WriteAudit(int? actorOsobaId, string entityType, string entityId, string action, string? oldValue, string? newValue)
    {
        dbContext.AuthzAuditLog.Add(new AuthzAuditLogEntity
        {
            ActorOsobaId = actorOsobaId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            OldValue = oldValue,
            NewValue = newValue,
            CreatedAt = DateTime.UtcNow
        });
        dbContext.SaveChanges();
    }
}
