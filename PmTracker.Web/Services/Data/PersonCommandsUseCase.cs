using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Services.Data;

public sealed class PersonCommandsUseCase(
    PmTrackerDbContext dbContext,
    ITextNormalizer textNormalizer) : IPersonCommandsUseCase
{
    public int SaveManualPerson(SaveManualPersonCommand command, CurrentUserContextViewModel currentUser)
    {
        var organizationId = ResolveOrganizationId(command.Organizace);
        var orgUnitId = ResolveOrgUnitId(command.OrganizacniCelek);
        var email = textNormalizer.NormalizeEmail(command.Email);

        if (command.Id.HasValue)
        {
            var existing = dbContext.Osoby.FirstOrDefault(x => x.Id == command.Id.Value)
                ?? throw new InvalidOperationException($"Osoba {command.Id.Value} nebyla nalezena.");

            var old = JsonSerializer.Serialize(existing);
            existing.OrganizaceId = organizationId;
            existing.OrganizacniCelekId = orgUnitId;
            existing.LocationLocked = command.LocationLocked && existing.GuidAd.HasValue;

            if (!existing.GuidAd.HasValue)
            {
                existing.AdLogin = null;
                existing.Jmeno = command.Jmeno.Trim();
                existing.Prijmeni = command.Prijmeni.Trim();
                existing.Titul = string.IsNullOrWhiteSpace(command.Titul) ? null : command.Titul.Trim();
                existing.Email = email;
                existing.LocationLocked = false;
            }

            dbContext.SaveChanges();
            WriteAudit(
                currentUser.OsobaId,
                "osoby",
                existing.Id.ToString(CultureInfo.InvariantCulture),
                existing.GuidAd.HasValue ? "update_ad_location" : "update_manual",
                old,
                JsonSerializer.Serialize(existing));
            return existing.Id;
        }

        var entity = new OsobaEntity
        {
            Jmeno = command.Jmeno.Trim(),
            Prijmeni = command.Prijmeni.Trim(),
            Titul = string.IsNullOrWhiteSpace(command.Titul) ? null : command.Titul.Trim(),
            Email = email,
            AdLogin = null,
            OrganizaceId = organizationId,
            OrganizacniCelekId = orgUnitId,
            GuidAd = null,
            LocationLocked = false
        };
        dbContext.Osoby.Add(entity);
        dbContext.SaveChanges();
        WriteAudit(currentUser.OsobaId, "osoby", entity.Id.ToString(CultureInfo.InvariantCulture), "create_manual", null, JsonSerializer.Serialize(entity));
        return entity.Id;
    }

    public int SaveAdPerson(SaveAdPersonCommand command, CurrentUserContextViewModel currentUser)
    {
        if (!command.GuidAd.HasValue || command.GuidAd.Value == Guid.Empty)
        {
            throw new InvalidOperationException("Nejprve vyberte osobu z AD výsledků.");
        }

        var guidAd = command.GuidAd.Value;
        var adLogin = NormalizeAdLogin(command.AdLogin);
        var organizationId = ResolveOrganizationForAd(command.Organizace, command.AdCompany);
        var orgUnitId = ResolveOrgUnitForAd(command.OrganizacniCelek, command.AdDepartment, command.AdCompany);
        var titul = string.IsNullOrWhiteSpace(command.Titul) ? null : command.Titul.Trim();
        var email = textNormalizer.NormalizeEmail(command.Email)
            ?? throw new InvalidOperationException("AD osoba musí mít vyplněný email.");
        var existing = dbContext.Osoby.FirstOrDefault(x => x.GuidAd == guidAd);
        if (existing is not null)
        {
            existing.Jmeno = command.Jmeno.Trim();
            existing.Prijmeni = command.Prijmeni.Trim();
            existing.Titul = titul;
            existing.Email = email;
            existing.AdLogin = adLogin;
            existing.OrganizaceId = organizationId;
            existing.OrganizacniCelekId = orgUnitId;
            existing.LocationLocked = command.LocationLocked;
            dbContext.SaveChanges();
            WriteAudit(currentUser.OsobaId, "osoby", existing.Id.ToString(CultureInfo.InvariantCulture), "sync_ad", null, JsonSerializer.Serialize(existing));
            return existing.Id;
        }

        var entity = new OsobaEntity
        {
            Jmeno = command.Jmeno.Trim(),
            Prijmeni = command.Prijmeni.Trim(),
            Titul = titul,
            Email = email,
            AdLogin = adLogin,
            OrganizaceId = organizationId,
            OrganizacniCelekId = orgUnitId,
            GuidAd = guidAd,
            LocationLocked = command.LocationLocked
        };
        dbContext.Osoby.Add(entity);
        dbContext.SaveChanges();
        WriteAudit(currentUser.OsobaId, "osoby", entity.Id.ToString(CultureInfo.InvariantCulture), "create_ad", null, JsonSerializer.Serialize(entity));
        return entity.Id;
    }

    public void DeletePerson(DeletePersonCommand command, CurrentUserContextViewModel currentUser)
    {
        var person = dbContext.Osoby.FirstOrDefault(x => x.Id == command.Id)
            ?? throw new InvalidOperationException("Osoba nebyla nalezena.");

        var hasProjectAssignments = dbContext.ObsazeniProjektu.AsNoTracking().Any(x => x.OsobaId == person.Id);
        var hasOwnedRecords = dbContext.ProjektoveZaznamy.AsNoTracking().Any(x => x.VlastnikId == person.Id);
        var hasCooperation = dbContext.ZaznamSpoluprace.AsNoTracking().Any(x => x.OsobaId == person.Id);
        var hasComments = dbContext.Vyjadreni.AsNoTracking().Any(x => x.AutorOsobaId == person.Id);
        var hasMeetingLocks = dbContext.Jednani.AsNoTracking().Any(x => x.UzamklOsobaId == person.Id);
        var hasAttendance = dbContext.Ucast.AsNoTracking().Any(x => x.OsobaId == person.Id);
        var hasSubsystemAssignments = dbContext.ObsazeniSubsystemuProjektu.AsNoTracking().Any(x => x.OsobaId == person.Id);

        if (hasProjectAssignments
            || hasOwnedRecords
            || hasCooperation
            || hasComments
            || hasMeetingLocks
            || hasAttendance
            || hasSubsystemAssignments)
        {
            throw new InvalidOperationException("Osobu nelze odstranit, protože je navázaná na projektová data. Nejprve odeberte vazby.");
        }

        var old = JsonSerializer.Serialize(person);

        var userRoles = dbContext.AuthzUserRoles.Where(x => x.OsobaId == person.Id).ToList();
        if (userRoles.Count > 0)
        {
            dbContext.AuthzUserRoles.RemoveRange(userRoles);
        }

        var superadminRows = dbContext.AuthzSuperadmins.Where(x => x.OsobaId == person.Id).ToList();
        if (superadminRows.Count > 0)
        {
            dbContext.AuthzSuperadmins.RemoveRange(superadminRows);
        }

        var auditRows = dbContext.AuthzAuditLog.Where(x => x.ActorOsobaId == person.Id).ToList();
        foreach (var auditRow in auditRows)
        {
            auditRow.ActorOsobaId = null;
        }

        dbContext.Osoby.Remove(person);
        dbContext.SaveChanges();
        WriteAudit(currentUser.OsobaId, "osoby", command.Id.ToString(CultureInfo.InvariantCulture), "delete", old, null);
    }

    private int ResolveOrganizationId(string? organization)
    {
        if (!string.IsNullOrWhiteSpace(organization))
        {
            var match = dbContext.CiselnikOrganizace
                .Where(x => x.Kod == organization || x.Nazev == organization)
                .Select(x => (int?)x.Id)
                .FirstOrDefault();
            if (match.HasValue)
            {
                return match.Value;
            }
        }

        return dbContext.CiselnikOrganizace.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .First();
    }

    private int ResolveOrganizationForAd(string? selectedOrganization, string? adCompany)
    {
        var normalizedSelection = NormalizeCiselnikSelection(selectedOrganization);
        if (!string.IsNullOrWhiteSpace(normalizedSelection))
        {
            var resolved = ResolveOrganizationIdOrNull(normalizedSelection);
            if (resolved.HasValue)
            {
                return resolved.Value;
            }

            return EnsureOrganizationExists(normalizedSelection);
        }

        var parsed = ParseAdCompanyToOrgData(adCompany);
        var derivedCode = DeriveOrganizationCodeFromCompany(adCompany ?? string.Empty, parsed.OrganizationCode);
        if (!string.IsNullOrWhiteSpace(derivedCode))
        {
            var byCode = ResolveOrganizationIdOrNull(derivedCode);
            if (byCode.HasValue)
            {
                return byCode.Value;
            }
        }

        if (!string.IsNullOrWhiteSpace(parsed.OrganizationName))
        {
            var byName = ResolveOrganizationIdOrNull(parsed.OrganizationName);
            if (byName.HasValue)
            {
                return byName.Value;
            }

            return EnsureOrganizationExists(parsed.OrganizationName, derivedCode);
        }

        var moByCode = ResolveOrganizationIdOrNull("MO");
        if (moByCode.HasValue)
        {
            return moByCode.Value;
        }

        return dbContext.CiselnikOrganizace.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .First();
    }

    private int? ResolveOrgUnitForAd(string? selectedOrgUnit, string? adDepartment, string? adCompany)
    {
        var normalizedSelection = NormalizeCiselnikSelection(selectedOrgUnit);
        if (!string.IsNullOrWhiteSpace(normalizedSelection))
        {
            var parsedSelection = ParseOrgUnitSelectionToken(normalizedSelection);
            var resolved = ResolveOrgUnitId(parsedSelection.Code ?? parsedSelection.Name ?? normalizedSelection)
                ?? (!string.IsNullOrWhiteSpace(parsedSelection.Name) ? ResolveOrgUnitId(parsedSelection.Name) : null);
            if (resolved.HasValue)
            {
                return resolved.Value;
            }

            return EnsureOrgUnitExists(
                parsedSelection.Name ?? normalizedSelection,
                parsedSelection.Code);
        }

        var parsed = ParseOrgUnitFromAd(adDepartment, adCompany);
        if (string.IsNullOrWhiteSpace(parsed.Name) && string.IsNullOrWhiteSpace(parsed.Code))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(parsed.Code))
        {
            var byCode = ResolveOrgUnitId(parsed.Code);
            if (byCode.HasValue)
            {
                return byCode.Value;
            }
        }

        if (!string.IsNullOrWhiteSpace(parsed.Name))
        {
            var byName = ResolveOrgUnitId(parsed.Name);
            if (byName.HasValue)
            {
                return byName.Value;
            }
        }

        return EnsureOrgUnitExists(parsed.Name ?? parsed.Code!, parsed.Code);
    }

    private int? ResolveOrgUnitId(string? orgUnit)
    {
        if (string.IsNullOrWhiteSpace(orgUnit))
        {
            return null;
        }

        return dbContext.CiselnikOrganizacniCelky
            .Where(x => x.Kod == orgUnit || x.Nazev == orgUnit)
            .Select(x => (int?)x.Id)
            .FirstOrDefault();
    }

    private int? ResolveOrganizationIdOrNull(string? organizationValue)
    {
        if (string.IsNullOrWhiteSpace(organizationValue))
        {
            return null;
        }

        var value = organizationValue.Trim();
        var normalized = textNormalizer.Normalize(value);
        return dbContext.CiselnikOrganizace
            .AsNoTracking()
            .Select(x => new { x.Id, x.Kod, x.Nazev })
            .ToList()
            .Where(x =>
                string.Equals(x.Kod, value, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Nazev, value, StringComparison.OrdinalIgnoreCase)
                || textNormalizer.Normalize(x.Kod) == normalized
                || textNormalizer.Normalize(x.Nazev) == normalized)
            .Select(x => (int?)x.Id)
            .FirstOrDefault();
    }

    private int EnsureOrganizationExists(string value, string? preferredCode = null)
    {
        var trimmed = value.Trim();
        var existing = ResolveOrganizationIdOrNull(trimmed);
        if (existing.HasValue)
        {
            return existing.Value;
        }

        var code = BuildUniqueOrganizationCode(preferredCode, trimmed);
        var entity = new CiselnikOrganizaceEntity
        {
            Kod = code,
            Nazev = trimmed,
            IsLocked = false
        };

        dbContext.CiselnikOrganizace.Add(entity);
        dbContext.SaveChanges();
        return entity.Id;
    }

    private int EnsureOrgUnitExists(string value, string? preferredCode = null)
    {
        var trimmed = value.Trim();
        var existing = ResolveOrgUnitId(trimmed);
        if (existing.HasValue)
        {
            return existing.Value;
        }

        var code = BuildUniqueOrgUnitCode(preferredCode, trimmed);
        var entity = new CiselnikOrganizacniCelekEntity
        {
            Kod = code,
            Nazev = trimmed,
            IsLocked = false
        };

        dbContext.CiselnikOrganizacniCelky.Add(entity);
        dbContext.SaveChanges();
        return entity.Id;
    }

    private string BuildUniqueOrganizationCode(string? preferredCode, string fallbackName)
        => BuildUniqueCode(
            preferredCode,
            fallbackName,
            "ORG",
            code => dbContext.CiselnikOrganizace.AsNoTracking().Any(x => x.Kod == code),
            allowLeadingDigit: false);

    private string BuildUniqueOrgUnitCode(string? preferredCode, string fallbackName)
        => BuildUniqueCode(
            preferredCode,
            fallbackName,
            "CEL",
            code => dbContext.CiselnikOrganizacniCelky.AsNoTracking().Any(x => x.Kod == code),
            allowLeadingDigit: true);

    private static string BuildUniqueCode(string? preferredCode, string fallbackName, string prefix, Func<string, bool> exists, bool allowLeadingDigit)
    {
        var baseCode = SanitizeCode(string.IsNullOrWhiteSpace(preferredCode) ? fallbackName : preferredCode, prefix, allowLeadingDigit);
        var candidate = baseCode;
        var counter = 1;
        while (exists(candidate))
        {
            counter++;
            candidate = $"{baseCode}{counter}";
        }

        return candidate;
    }

    private static string SanitizeCode(string source, string fallbackPrefix, bool allowLeadingDigit)
    {
        var raw = new string((source ?? string.Empty).ToUpperInvariant().Where(ch => char.IsLetterOrDigit(ch)).ToArray());
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallbackPrefix;
        }

        if (raw.Length > 12)
        {
            raw = raw[..12];
        }

        if (!allowLeadingDigit && char.IsDigit(raw[0]))
        {
            raw = $"{fallbackPrefix}{raw}";
        }

        return raw;
    }

    private static string? NormalizeCiselnikSelection(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var trimmed = rawValue.Trim();
        const string newPrefix = "__new__:";
        if (trimmed.StartsWith(newPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[newPrefix.Length..].Trim();
        }

        return trimmed;
    }

    private static (string? Code, string? Name) ParseOrgUnitFromAd(string? adDepartment, string? adCompany)
    {
        var deptParsed = ParseOrgUnitSelectionToken(adDepartment);
        var companyParsed = ParseAdCompanyToOrgData(adCompany);

        var code = !string.IsNullOrWhiteSpace(deptParsed.Code) ? deptParsed.Code : companyParsed.OrgUnitCode;
        var name = !string.IsNullOrWhiteSpace(deptParsed.Name) ? deptParsed.Name : companyParsed.OrgUnitName;
        return (code, name);
    }

    private static (string? Code, string? Name) ParseOrgUnitSelectionToken(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return (null, null);
        }

        var trimmed = raw.Trim();
        var splitIndex = trimmed.IndexOf('|');
        if (splitIndex >= 1)
        {
            var left = trimmed[..splitIndex].Trim();
            var right = trimmed[(splitIndex + 1)..].Trim();
            return (
                string.IsNullOrWhiteSpace(left) ? null : left,
                string.IsNullOrWhiteSpace(right) ? null : right);
        }

        if (trimmed.All(char.IsDigit))
        {
            return (trimmed, null);
        }

        return (null, trimmed);
    }

    private static (string? OrganizationCode, string? OrganizationName, string? OrgUnitCode, string? OrgUnitName) ParseAdCompanyToOrgData(string? rawCompany)
    {
        if (string.IsNullOrWhiteSpace(rawCompany))
        {
            return (null, null, null, null);
        }

        var value = rawCompany.Trim();
        var slashIndex = value.IndexOf('/', StringComparison.Ordinal);
        var leftPart = slashIndex >= 0 ? value[..slashIndex].Trim() : value;
        var rightPart = slashIndex >= 0 ? value[(slashIndex + 1)..].Trim() : null;

        var code = default(string);
        var name = leftPart;
        var hyphenIndex = leftPart.IndexOf('-', StringComparison.Ordinal);
        if (hyphenIndex >= 2 && hyphenIndex <= 6)
        {
            var maybeCode = leftPart[..hyphenIndex].Trim().ToUpperInvariant();
            if (maybeCode.Length >= 2 && maybeCode.Length <= 8 && maybeCode.All(char.IsLetterOrDigit))
            {
                code = maybeCode;
                name = leftPart[(hyphenIndex + 1)..].Trim();
            }
        }

        var orgUnitCode = string.IsNullOrWhiteSpace(rightPart) ? null : rightPart;
        if (!string.IsNullOrWhiteSpace(orgUnitCode))
        {
            orgUnitCode = orgUnitCode.Trim();
        }

        return (
            string.IsNullOrWhiteSpace(code) ? null : code,
            string.IsNullOrWhiteSpace(name) ? null : name,
            orgUnitCode,
            string.IsNullOrWhiteSpace(name) ? null : name);
    }

    private static string? DeriveOrganizationCodeFromCompany(string company, string? parsedCode)
    {
        if (!string.IsNullOrWhiteSpace(parsedCode))
        {
            return parsedCode;
        }

        var parsedCompany = ParseAdCompanyToOrgData(company);
        if (!string.IsNullOrWhiteSpace(parsedCompany.OrganizationCode))
        {
            return parsedCompany.OrganizationCode;
        }

        if (string.IsNullOrWhiteSpace(company))
        {
            return null;
        }

        var normalized = company.Trim().ToLowerInvariant();
        if (normalized.Contains("ministerstvo obrany")
            || normalized.Contains("armáda české republiky")
            || normalized.Contains("armada ceske republiky")
            || normalized.Contains("acr"))
        {
            return "MO";
        }

        if (normalized.Contains("gordic"))
        {
            return "DOD";
        }

        return null;
    }

    private static string? NormalizeAdLogin(string? rawLogin)
    {
        if (string.IsNullOrWhiteSpace(rawLogin))
        {
            return null;
        }

        var value = rawLogin.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(value) ? null : value;
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
