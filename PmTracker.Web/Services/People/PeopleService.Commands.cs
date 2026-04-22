using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.ActiveDirectory;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Audit;

namespace PmTracker.Web.Services.People;

public sealed partial class PeopleService
{
    public async Task<int> SaveManualPersonAsync(
        SaveManualPersonCommand command,
        CurrentUserContextViewModel currentUser,
        CancellationToken ct = default)
    {
        var organizationId = await ResolveOrganizationIdAsync(command.Organizace, currentUser.OsobaId, ct);
        var orgUnitId = await ResolveOrgUnitIdAsync(command.OrganizacniCelek, ct);
        var email = textNormalizer.NormalizeEmail(command.Email);

        if (command.Id.HasValue)
        {
            var existing = await dbContext.Osoby
                .FirstOrDefaultAsync(x => x.Id == command.Id.Value, ct)
                ?? throw new InvalidOperationException($"Osoba {command.Id.Value} nebyla nalezena.");

            var old = PersonAuditSnapshot.FromEntity(existing);
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

            await dbContext.SaveChangesAsync(ct);
            auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
                AuditActionType.Update,
                AuditEntityType.Person,
                existing.Id.ToString(CultureInfo.InvariantCulture),
                old,
                PersonAuditSnapshot.FromEntity(existing)));
            await dbContext.SaveChangesAsync(ct);
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
        await dbContext.SaveChangesAsync(ct);
        auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
            AuditActionType.Create,
            AuditEntityType.Person,
            entity.Id.ToString(CultureInfo.InvariantCulture),
            null,
            PersonAuditSnapshot.FromEntity(entity)));
        await dbContext.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task<int> SaveAdPersonAsync(
        SaveAdPersonCommand command,
        CurrentUserContextViewModel currentUser,
        CancellationToken ct = default)
    {
        if (!command.GuidAd.HasValue || command.GuidAd.Value == Guid.Empty)
        {
            throw new InvalidOperationException("Nejprve vyberte osobu z AD výsledků.");
        }

        var guidAd = command.GuidAd.Value;
        var adLogin = NormalizeAdLogin(command.AdLogin);
        var organizationId = await ResolveOrganizationForAdAsync(command.Organizace, command.AdCompany, currentUser.OsobaId, ct);
        var orgUnitId = await ResolveOrgUnitForAdAsync(command.OrganizacniCelek, command.AdDepartment, command.AdCompany, currentUser.OsobaId, ct);
        var titul = string.IsNullOrWhiteSpace(command.Titul) ? null : command.Titul.Trim();
        var email = textNormalizer.NormalizeEmail(command.Email)
            ?? throw new InvalidOperationException("AD osoba musí mít vyplněný email.");
        var existing = await dbContext.Osoby
            .FirstOrDefaultAsync(x => x.GuidAd == guidAd, ct);
        if (existing is not null)
        {
            var old = PersonAuditSnapshot.FromEntity(existing);
            existing.Jmeno = command.Jmeno.Trim();
            existing.Prijmeni = command.Prijmeni.Trim();
            existing.Titul = titul;
            existing.Email = email;
            existing.AdLogin = adLogin;
            existing.OrganizaceId = organizationId;
            existing.OrganizacniCelekId = orgUnitId;
            existing.LocationLocked = command.LocationLocked;
            await dbContext.SaveChangesAsync(ct);
            auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
                AuditActionType.Update,
                AuditEntityType.Person,
                existing.Id.ToString(CultureInfo.InvariantCulture),
                old,
                PersonAuditSnapshot.FromEntity(existing)));
            await dbContext.SaveChangesAsync(ct);

            // Reactive AD refresh — po pick existující osoby. Queue dedup
            // (§13.1) zajistí, že opakovaný save-stejné-osoby je no-op.
            await adReactiveQueue.EnqueueAsync(
                new AdReactiveSyncRequest(existing.Id, AdReactiveSource.PersonPicked), ct);
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
        await dbContext.SaveChangesAsync(ct);
        auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
            AuditActionType.Create,
            AuditEntityType.Person,
            entity.Id.ToString(CultureInfo.InvariantCulture),
            null,
            PersonAuditSnapshot.FromEntity(entity)));
        await dbContext.SaveChangesAsync(ct);

        // Reactive AD refresh — po prvním picku nové osoby z AD.
        await adReactiveQueue.EnqueueAsync(
            new AdReactiveSyncRequest(entity.Id, AdReactiveSource.PersonPicked), ct);
        return entity.Id;
    }

    public async Task DeletePersonAsync(
        DeletePersonCommand command,
        CurrentUserContextViewModel currentUser,
        CancellationToken ct = default)
    {
        var person = await dbContext.Osoby
            .FirstOrDefaultAsync(x => x.Id == command.Id, ct)
            ?? throw new InvalidOperationException("Osoba nebyla nalezena.");

        var hasProjectAssignments = await dbContext.ObsazeniProjektu
            .AsNoTracking()
            .AnyAsync(x => x.OsobaId == person.Id, ct);
        var hasOwnedRecords = await dbContext.ProjektoveZaznamy
            .AsNoTracking()
            .AnyAsync(x => x.VlastnikId == person.Id, ct);
        var hasCooperation = await dbContext.ZaznamSpoluprace
            .AsNoTracking()
            .AnyAsync(x => x.OsobaId == person.Id, ct);
        var hasComments = await dbContext.Vyjadreni
            .AsNoTracking()
            .AnyAsync(x => x.AutorOsobaId == person.Id, ct);
        var hasMeetingLocks = await dbContext.Jednani
            .AsNoTracking()
            .AnyAsync(x => x.UzamklOsobaId == person.Id, ct);
        var hasAttendance = await dbContext.Ucast
            .AsNoTracking()
            .AnyAsync(x => x.OsobaId == person.Id, ct);
        var hasSubsystemAssignments = await dbContext.ObsazeniSubsystemuProjektu
            .AsNoTracking()
            .AnyAsync(x => x.OsobaId == person.Id, ct);

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

        var old = PersonAuditSnapshot.FromEntity(person);

        var userRoles = await dbContext.AuthzUserRoles
            .Where(x => x.OsobaId == person.Id)
            .ToListAsync(ct);
        if (userRoles.Count > 0)
        {
            dbContext.AuthzUserRoles.RemoveRange(userRoles);
        }

        var superadminRows = await dbContext.AuthzSuperadmins
            .Where(x => x.OsobaId == person.Id)
            .ToListAsync(ct);
        if (superadminRows.Count > 0)
        {
            dbContext.AuthzSuperadmins.RemoveRange(superadminRows);
        }

        dbContext.Osoby.Remove(person);
        await dbContext.SaveChangesAsync(ct);
        auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
            AuditActionType.Delete,
            AuditEntityType.Person,
            command.Id.ToString(CultureInfo.InvariantCulture),
            old,
            null));
        await dbContext.SaveChangesAsync(ct);
    }

    private async Task<int> ResolveOrganizationIdAsync(string? organization, int? actorOsobaId, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(organization))
        {
            var match = await dbContext.CiselnikOrganizace
                .Where(x => x.Kod == organization || x.Nazev == organization)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync(ct);
            if (match.HasValue)
            {
                return match.Value;
            }
        }

        return await dbContext.CiselnikOrganizace
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Číselník organizací neobsahuje žádnou položku.");
    }

    private async Task<int> ResolveOrganizationForAdAsync(
        string? selectedOrganization,
        string? adCompany,
        int? actorOsobaId,
        CancellationToken ct)
    {
        var normalizedSelection = NormalizeCiselnikSelection(selectedOrganization);
        if (!string.IsNullOrWhiteSpace(normalizedSelection))
        {
            var resolved = await ResolveOrganizationIdOrNullAsync(normalizedSelection, ct);
            if (resolved.HasValue)
            {
                return resolved.Value;
            }

            return await EnsureOrganizationExistsAsync(normalizedSelection, null, actorOsobaId, ct);
        }

        var parsed = ParseAdCompanyToOrgData(adCompany);
        var derivedCode = DeriveOrganizationCodeFromCompany(adCompany ?? string.Empty, parsed.OrganizationCode);
        if (!string.IsNullOrWhiteSpace(derivedCode))
        {
            var byCode = await ResolveOrganizationIdOrNullAsync(derivedCode, ct);
            if (byCode.HasValue)
            {
                return byCode.Value;
            }
        }

        if (!string.IsNullOrWhiteSpace(parsed.OrganizationName))
        {
            var byName = await ResolveOrganizationIdOrNullAsync(parsed.OrganizationName, ct);
            if (byName.HasValue)
            {
                return byName.Value;
            }

            return await EnsureOrganizationExistsAsync(parsed.OrganizationName, derivedCode, actorOsobaId, ct);
        }

        var moByCode = await ResolveOrganizationIdOrNullAsync("MO", ct);
        if (moByCode.HasValue)
        {
            return moByCode.Value;
        }

        return await dbContext.CiselnikOrganizace
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Číselník organizací neobsahuje žádnou položku.");
    }

    private async Task<int?> ResolveOrgUnitForAdAsync(
        string? selectedOrgUnit,
        string? adDepartment,
        string? adCompany,
        int? actorOsobaId,
        CancellationToken ct)
    {
        var normalizedSelection = NormalizeCiselnikSelection(selectedOrgUnit);
        if (!string.IsNullOrWhiteSpace(normalizedSelection))
        {
            var parsedSelection = ParseOrgUnitSelectionToken(normalizedSelection);
            var resolved = await ResolveOrgUnitIdAsync(
                parsedSelection.Code ?? parsedSelection.Name ?? normalizedSelection,
                ct)
                ?? (!string.IsNullOrWhiteSpace(parsedSelection.Name)
                    ? await ResolveOrgUnitIdAsync(parsedSelection.Name, ct)
                    : null);
            if (resolved.HasValue)
            {
                return resolved.Value;
            }

            return await EnsureOrgUnitExistsAsync(
                parsedSelection.Name ?? normalizedSelection,
                parsedSelection.Code,
                actorOsobaId,
                ct);
        }

        var parsed = ParseOrgUnitFromAd(adDepartment, adCompany);
        if (string.IsNullOrWhiteSpace(parsed.Name) && string.IsNullOrWhiteSpace(parsed.Code))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(parsed.Code))
        {
            var byCode = await ResolveOrgUnitIdAsync(parsed.Code, ct);
            if (byCode.HasValue)
            {
                return byCode.Value;
            }
        }

        if (!string.IsNullOrWhiteSpace(parsed.Name))
        {
            var byName = await ResolveOrgUnitIdAsync(parsed.Name, ct);
            if (byName.HasValue)
            {
                return byName.Value;
            }
        }

        return await EnsureOrgUnitExistsAsync(parsed.Name ?? parsed.Code!, parsed.Code, actorOsobaId, ct);
    }

    private Task<int?> ResolveOrgUnitIdAsync(string? orgUnit, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(orgUnit))
        {
            return Task.FromResult<int?>(null);
        }

        return dbContext.CiselnikOrganizacniCelky
            .Where(x => x.Kod == orgUnit || x.Nazev == orgUnit)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<int?> ResolveOrganizationIdOrNullAsync(string? organizationValue, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(organizationValue))
        {
            return null;
        }

        var value = organizationValue.Trim();
        var normalized = textNormalizer.Normalize(value);
        var candidates = await dbContext.CiselnikOrganizace
            .AsNoTracking()
            .Select(x => new { x.Id, x.Kod, x.Nazev })
            .ToListAsync(ct);

        return candidates
            .Where(x =>
                string.Equals(x.Kod, value, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Nazev, value, StringComparison.OrdinalIgnoreCase)
                || textNormalizer.Normalize(x.Kod) == normalized
                || textNormalizer.Normalize(x.Nazev) == normalized)
            .Select(x => (int?)x.Id)
            .FirstOrDefault();
    }

    private async Task<int> EnsureOrganizationExistsAsync(
        string value,
        string? preferredCode,
        int? actorOsobaId,
        CancellationToken ct)
    {
        var trimmed = value.Trim();
        var existing = await ResolveOrganizationIdOrNullAsync(trimmed, ct);
        if (existing.HasValue)
        {
            return existing.Value;
        }

        var code = await BuildUniqueOrganizationCodeAsync(preferredCode, trimmed, ct);
        var entity = new CiselnikOrganizaceEntity
        {
            Kod = code,
            Nazev = trimmed,
            IsLocked = false
        };

        dbContext.CiselnikOrganizace.Add(entity);
        await dbContext.SaveChangesAsync(ct);
        auditWriteService.Add(actorOsobaId, new AuditWriteEntry(
            AuditActionType.Create,
            AuditEntityType.Dictionary,
            entity.Id.ToString(CultureInfo.InvariantCulture),
            null,
            new DictionaryAuditSnapshot("organizace", entity.Id, entity.Kod, entity.Nazev, entity.IsLocked)));
        await dbContext.SaveChangesAsync(ct);
        return entity.Id;
    }

    private async Task<int> EnsureOrgUnitExistsAsync(
        string value,
        string? preferredCode,
        int? actorOsobaId,
        CancellationToken ct)
    {
        var trimmed = value.Trim();
        var existing = await ResolveOrgUnitIdAsync(trimmed, ct);
        if (existing.HasValue)
        {
            return existing.Value;
        }

        var code = await BuildUniqueOrgUnitCodeAsync(preferredCode, trimmed, ct);
        var entity = new CiselnikOrganizacniCelekEntity
        {
            Kod = code,
            Nazev = trimmed,
            IsLocked = false
        };

        dbContext.CiselnikOrganizacniCelky.Add(entity);
        await dbContext.SaveChangesAsync(ct);
        auditWriteService.Add(actorOsobaId, new AuditWriteEntry(
            AuditActionType.Create,
            AuditEntityType.Dictionary,
            entity.Id.ToString(CultureInfo.InvariantCulture),
            null,
            new DictionaryAuditSnapshot("organizacni-celky", entity.Id, entity.Kod, entity.Nazev, entity.IsLocked)));
        await dbContext.SaveChangesAsync(ct);
        return entity.Id;
    }

    private Task<string> BuildUniqueOrganizationCodeAsync(
        string? preferredCode,
        string fallbackName,
        CancellationToken ct)
        => BuildUniqueCodeAsync(
            preferredCode,
            fallbackName,
            "ORG",
            (code, ct) => dbContext.CiselnikOrganizace.AsNoTracking().AnyAsync(x => x.Kod == code, ct),
            allowLeadingDigit: false,
            ct);

    private Task<string> BuildUniqueOrgUnitCodeAsync(
        string? preferredCode,
        string fallbackName,
        CancellationToken ct)
        => BuildUniqueCodeAsync(
            preferredCode,
            fallbackName,
            "CEL",
            (code, ct) => dbContext.CiselnikOrganizacniCelky.AsNoTracking().AnyAsync(x => x.Kod == code, ct),
            allowLeadingDigit: true,
            ct);

    private static async Task<string> BuildUniqueCodeAsync(
        string? preferredCode,
        string fallbackName,
        string prefix,
        Func<string, CancellationToken, Task<bool>> existsAsync,
        bool allowLeadingDigit,
        CancellationToken ct)
    {
        var baseCode = SanitizeCode(
            string.IsNullOrWhiteSpace(preferredCode) ? fallbackName : preferredCode,
            prefix,
            allowLeadingDigit);
        var candidate = baseCode;
        var counter = 1;
        while (await existsAsync(candidate, ct))
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

}
