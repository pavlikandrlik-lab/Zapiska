using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Services.Security;

public sealed class UserContextResolver : IUserContextResolver
{
    private static readonly string[] GuidClaimTypes =
    {
        "http://schemas.microsoft.com/identity/claims/objectidentifier",
        "oid",
        ClaimTypes.NameIdentifier
    };

    private static readonly string[] LoginClaimTypes =
    {
        ClaimTypes.WindowsAccountName,
        ClaimTypes.Upn,
        ClaimTypes.Name,
        "preferred_username",
        "upn",
        "unique_name"
    };

    private readonly PmTrackerDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<UserContextResolver> _logger;
    private readonly ITextNormalizer _textNormalizer;
    private readonly IPersonIdentityMatcher _personIdentityMatcher;

    public UserContextResolver(
        PmTrackerDbContext dbContext,
        IWebHostEnvironment environment,
        ILogger<UserContextResolver> logger,
        ITextNormalizer textNormalizer,
        IPersonIdentityMatcher personIdentityMatcher)
    {
        _dbContext = dbContext;
        _environment = environment;
        _logger = logger;
        _textNormalizer = textNormalizer;
        _personIdentityMatcher = personIdentityMatcher;
    }

    public async Task<UserContextResolutionResult> ResolveAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        int? osobaId = null;

        if (_environment.IsDevelopment())
        {
            var asUser = httpContext.Request.Query["asUser"].ToString();
            if (!string.IsNullOrWhiteSpace(asUser))
            {
                osobaId = await ResolveOsobaIdFromAsUserAsync(asUser, cancellationToken);
                if (!osobaId.HasValue)
                {
                    return UserContextResolutionResult.Forbidden($"Uživatel '{asUser}' nebyl nalezen v tabulce osoby.");
                }
            }
        }

        if (!osobaId.HasValue)
        {
            var principal = httpContext.User;
            if (principal?.Identity?.IsAuthenticated != true)
            {
                if (_environment.IsDevelopment())
                {
                    osobaId = await _dbContext.Osoby
                        .AsNoTracking()
                        .OrderBy(x => x.Id)
                        .Select(x => (int?)x.Id)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (!osobaId.HasValue)
                    {
                        return UserContextResolutionResult.Forbidden("V databázi nejsou žádné osoby.");
                    }
                }
                else
                {
                    return UserContextResolutionResult.Unauthorized("Uživatel není autentizován.");
                }
            }
            else
            {
                var claimValue = GuidClaimTypes
                    .Select(type => principal.FindFirst(type)?.Value)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

                if (Guid.TryParse(claimValue, out var objectGuid))
                {
                    osobaId = await _dbContext.Osoby
                        .AsNoTracking()
                        .Where(x => x.GuidAd == objectGuid)
                        .Select(x => (int?)x.Id)
                        .FirstOrDefaultAsync(cancellationToken);
                }

                if (!osobaId.HasValue)
                {
                    var loginCandidates = BuildNormalizedLoginCandidates(principal);
                    if (loginCandidates.Count > 0)
                    {
                        var people = await _dbContext.Osoby
                            .AsNoTracking()
                            .Where(x => x.AdLogin != null)
                            .Select(x => new
                            {
                                x.Id,
                                x.AdLogin
                            })
                            .ToListAsync(cancellationToken);

                        osobaId = people
                            .FirstOrDefault(person => LoginEquals(person.AdLogin, loginCandidates))
                            ?.Id;
                    }
                }

                if (!osobaId.HasValue)
                {
                    return UserContextResolutionResult.Forbidden("Nemáte přístup do aplikace. Chybí párování osoby.guid_ad nebo osoby.ad_login.");
                }
            }
        }

        var osoba = await _dbContext.Osoby
            .AsNoTracking()
            .Where(x => x.Id == osobaId.Value)
            .Select(x => new
            {
                x.Id,
                x.Titul,
                x.Jmeno,
                x.Prijmeni,
                x.Email,
                x.GuidAd,
                x.AdLogin,
                x.OrganizacniCelekId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (osoba is null)
        {
            return UserContextResolutionResult.Forbidden("Přihlášená osoba v databázi neexistuje.");
        }

        var organizationalUnit = await _dbContext.CiselnikOrganizacniCelky
            .AsNoTracking()
            .Where(x => x.Id == osoba.OrganizacniCelekId)
            .Select(x => new { x.Kod, x.Nazev })
            .FirstOrDefaultAsync(cancellationToken);

        var roleCodes = await (
                from ur in _dbContext.AuthzUserRoles.AsNoTracking()
                join role in _dbContext.AuthzRoles.AsNoTracking() on ur.RoleId equals role.Id
                where ur.OsobaId == osoba.Id && ur.IsActive && role.IsActive
                orderby role.Kod
                select role.Kod)
            .Distinct()
            .ToListAsync(cancellationToken);

        var isSuperAdmin = await _dbContext.AuthzSuperadmins
            .AsNoTracking()
            .AnyAsync(x => x.OsobaId == osoba.Id, cancellationToken);

        if (!isSuperAdmin)
        {
            isSuperAdmin = roleCodes.Any(code => string.Equals(code, "SUPERADMIN", StringComparison.OrdinalIgnoreCase));
        }

        var grantsRaw = await (
                from ur in _dbContext.AuthzUserRoles.AsNoTracking()
                join rp in _dbContext.AuthzRolePermissions.AsNoTracking() on ur.RoleId equals rp.RoleId
                join p in _dbContext.AuthzPermissions.AsNoTracking() on rp.PermissionId equals p.Id
                where ur.OsobaId == osoba.Id && ur.IsActive && p.IsActive
                select new
                {
                    p.Klic,
                    p.ScopeLevel,
                    rp.ScopeMode,
                    rp.IsAllowed,
                    RolePermissionId = rp.Id
                })
            .ToListAsync(cancellationToken);

        var rolePermissionIds = grantsRaw
            .Where(x => string.Equals(x.ScopeMode, "INCLUDE", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.RolePermissionId)
            .Distinct()
            .ToList();

        var includeProjectMap = await _dbContext.AuthzRolePermissionProjects
            .AsNoTracking()
            .Where(x => rolePermissionIds.Contains(x.RolePermissionId))
            .GroupBy(x => x.RolePermissionId)
            .ToDictionaryAsync(
                group => group.Key,
                group => (IReadOnlyList<int>)group.Select(item => item.ProjektId).Distinct().ToList(),
                cancellationToken);

        var grants = grantsRaw
            .Select(raw => new PermissionGrantViewModel
            {
                PermissionKey = raw.Klic,
                ScopeLevel = raw.ScopeLevel,
                ScopeMode = raw.ScopeMode,
                IsAllowed = raw.IsAllowed,
                ProjectIds = raw.ScopeMode.Equals("INCLUDE", StringComparison.OrdinalIgnoreCase)
                    ? includeProjectMap.GetValueOrDefault(raw.RolePermissionId, Array.Empty<int>())
                    : Array.Empty<int>()
            })
            .ToList();

        var implicitProjectRoleGrants = ProjectRolePermissionGrantBuilder.BuildImplicitProjectRoleGrants(
            await (
                from assignment in _dbContext.ObsazeniProjektu.AsNoTracking()
                join role in _dbContext.CiselnikRoliProjektu.AsNoTracking() on assignment.RoleId equals role.Id
                where assignment.OsobaId == osoba.Id
                    && !assignment.DatumOdebrani.HasValue
                select new ProjectRoleAssignmentGrantSource
                {
                    RoleCode = role.Kod,
                    ProjectId = assignment.ProjektId
                })
            .ToListAsync(cancellationToken));
        grants.AddRange(implicitProjectRoleGrants);

        var visibleProjectIds = await _dbContext.ObsazeniProjektu
            .AsNoTracking()
            .Where(x => x.OsobaId == osoba.Id && !x.DatumOdebrani.HasValue)
            .Select(x => x.ProjektId)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        var displayName = BuildDisplayName(osoba.Titul, osoba.Jmeno, osoba.Prijmeni, osoba.Id);

        var context = new CurrentUserContextViewModel
        {
            OsobaId = osoba.Id,
            Jmeno = osoba.Jmeno,
            Prijmeni = osoba.Prijmeni,
            DisplayName = displayName,
            Email = osoba.Email?.Trim() ?? string.Empty,
            OrganizacniCelekKod = string.IsNullOrWhiteSpace(organizationalUnit?.Kod) ? null : organizationalUnit.Kod.Trim(),
            OrganizacniCelek = organizationalUnit?.Nazev ?? "-",
            IsSuperAdmin = isSuperAdmin,
            RoleKody = roleCodes,
            VisibleProjectIds = visibleProjectIds,
            PermissionGrants = grants
        };

        return UserContextResolutionResult.Success(context);
    }

    private async Task<int?> ResolveOsobaIdFromAsUserAsync(string asUser, CancellationToken cancellationToken)
    {
        if (int.TryParse(asUser, out var osobaId))
        {
            return await _dbContext.Osoby.AsNoTracking()
                .Where(x => x.Id == osobaId)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (Guid.TryParse(asUser, out var guid))
        {
            return await _dbContext.Osoby.AsNoTracking()
                .Where(x => x.GuidAd == guid)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var normalized = _textNormalizer.Normalize(asUser);

        var candidates = await _dbContext.Osoby
            .AsNoTracking()
            .Select(x => new
            {
                x.Id,
                x.Titul,
                x.Jmeno,
                x.Prijmeni,
                x.Email,
                x.AdLogin
            })
            .ToListAsync(cancellationToken);

        var loginCandidates = BuildNormalizedLoginCandidates(asUser);
        var exact = candidates.FirstOrDefault(x =>
            _personIdentityMatcher.NameEquals(x.Titul, x.Jmeno, x.Prijmeni, normalized) ||
            _personIdentityMatcher.EmailEquals(x.Email, normalized) ||
            LoginEquals(x.AdLogin, loginCandidates));

        if (exact is not null)
        {
            return exact.Id;
        }

        var contains = candidates.FirstOrDefault(x =>
            _personIdentityMatcher.NameContains(x.Titul, x.Jmeno, x.Prijmeni, normalized) ||
            _personIdentityMatcher.EmailContains(x.Email, normalized) ||
            LoginContains(x.AdLogin, loginCandidates));

        if (contains is not null)
        {
            return contains.Id;
        }

        _logger.LogWarning("asUser '{AsUser}' nebyl nalezen mezi osobami.", asUser);
        return null;
    }

    private static HashSet<string> BuildNormalizedLoginCandidates(ClaimsPrincipal principal)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        addCandidate(principal.Identity?.Name, candidates);

        foreach (var claimType in LoginClaimTypes)
        {
            addCandidate(principal.FindFirst(claimType)?.Value, candidates);
        }

        return candidates;

        static void addCandidate(string? raw, HashSet<string> target)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            var trimmed = raw.Trim();
            tryAdd(trimmed, target);

            if (trimmed.Contains('\\'))
            {
                tryAdd(trimmed.Split('\\', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault(), target);
            }

            if (trimmed.Contains('@'))
            {
                tryAdd(trimmed.Split('@', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault(), target);
            }
        }

        static void tryAdd(string? value, HashSet<string> target)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            target.Add(value.Trim().ToLowerInvariant());
        }
    }

    private static HashSet<string> BuildNormalizedLoginCandidates(string? raw)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return candidates;
        }

        var trimmed = raw.Trim();
        candidates.Add(trimmed.ToLowerInvariant());

        if (trimmed.Contains('\\'))
        {
            var shortLogin = trimmed.Split('\\', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
            if (!string.IsNullOrWhiteSpace(shortLogin))
            {
                candidates.Add(shortLogin.Trim().ToLowerInvariant());
            }
        }

        if (trimmed.Contains('@'))
        {
            var upnLogin = trimmed.Split('@', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(upnLogin))
            {
                candidates.Add(upnLogin.Trim().ToLowerInvariant());
            }
        }

        return candidates;
    }

    private static bool LoginEquals(string? storedLogin, IReadOnlySet<string> candidates)
    {
        if (string.IsNullOrWhiteSpace(storedLogin) || candidates.Count == 0)
        {
            return false;
        }

        return candidates.Contains(storedLogin.Trim().ToLowerInvariant());
    }

    private static bool LoginContains(string? storedLogin, IReadOnlySet<string> candidates)
    {
        if (string.IsNullOrWhiteSpace(storedLogin) || candidates.Count == 0)
        {
            return false;
        }

        var normalized = storedLogin.Trim().ToLowerInvariant();
        return candidates.Any(candidate => normalized.Contains(candidate, StringComparison.Ordinal));
    }

    private string BuildDisplayName(string? titul, string jmeno, string prijmeni, int id)
    {
        var displayName = _personIdentityMatcher.BuildPersonName(titul, jmeno, prijmeni);
        return string.IsNullOrWhiteSpace(displayName) ? $"Uživatel #{id}" : displayName;
    }
}
