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

    public async Task<UserContextResolutionResult> ResolveAsync(HttpContext httpContext, CancellationToken ct = default)
    {
        int? osobaId = null;

        if (_environment.IsDevelopment())
        {
            var asUser = httpContext.Request.Query["asUser"].ToString();
            if (!string.IsNullOrWhiteSpace(asUser))
            {
                osobaId = await ResolveOsobaIdFromAsUserAsync(asUser, ct);
                if (!osobaId.HasValue)
                {
                    return UserContextResolutionResult.Forbidden($"Uživatel '{asUser}' nebyl nalezen v tabulce osoby.");
                }
            }
        }

        if (!osobaId.HasValue)
        {
            var principal = httpContext.User;
            var loginCandidates = BuildNormalizedLoginCandidates(principal);
            var isAuthenticated = principal?.Identity?.IsAuthenticated == true;

            WriteAuthTrace(
                $"ResolveAsync start | Path={httpContext.Request.Path.Value ?? string.Empty} | IsDevelopment={_environment.IsDevelopment()} | IsAuthenticated={isAuthenticated} | AuthenticationType={principal?.Identity?.AuthenticationType ?? "(null)"} | IdentityName={principal?.Identity?.Name ?? "(null)"} | LoginCandidates=[{string.Join(", ", loginCandidates.OrderBy(x => x, StringComparer.Ordinal))}] | LoginClaimTypesPresent={DescribePresentClaimTypes(principal, LoginClaimTypes)} | GuidClaimTypesPresent={DescribePresentClaimTypes(principal, GuidClaimTypes)}");

            _logger.LogInformation(
                "Resolving user context. Path={Path} IsDevelopment={IsDevelopment} IsAuthenticated={IsAuthenticated} AuthenticationType={AuthenticationType} IdentityNamePresent={IdentityNamePresent} LoginCandidateCount={LoginCandidateCount} LoginClaimTypesPresent={LoginClaimTypesPresent} GuidClaimTypesPresent={GuidClaimTypesPresent}",
                httpContext.Request.Path.Value ?? string.Empty,
                _environment.IsDevelopment(),
                isAuthenticated,
                principal?.Identity?.AuthenticationType ?? "(null)",
                !string.IsNullOrWhiteSpace(principal?.Identity?.Name),
                loginCandidates.Count,
                DescribePresentClaimTypes(principal, LoginClaimTypes),
                DescribePresentClaimTypes(principal, GuidClaimTypes));

            if (!isAuthenticated)
            {
                if (loginCandidates.Count > 0)
                {
                    WriteAuthTrace(
                        $"Unauthenticated principal branch | attempting AdLogin lookup from IIS login candidates | CandidateCount={loginCandidates.Count}");

                    _logger.LogInformation(
                        "Attempting user resolution from IIS login candidates without authenticated principal. Path={Path} LoginCandidateCount={LoginCandidateCount}",
                        httpContext.Request.Path.Value ?? string.Empty,
                        loginCandidates.Count);

                    osobaId = await ResolveOsobaIdFromLoginCandidatesAsync(loginCandidates, ct);
                }

                if (!osobaId.HasValue)
                {
                    if (_environment.IsDevelopment())
                    {
                        WriteAuthTrace("Unauthenticated principal branch | development fallback to first person in DB");

                        _logger.LogInformation(
                            "Falling back to development first-person resolution. Path={Path}",
                            httpContext.Request.Path.Value ?? string.Empty);

                        osobaId = await _dbContext.Osoby
                            .AsNoTracking()
                            .OrderBy(x => x.Id)
                            .Select(x => (int?)x.Id)
                            .FirstOrDefaultAsync(ct);

                        if (!osobaId.HasValue)
                        {
                            WriteAuthTrace("ResolveAsync failed | no people in DB for development fallback");

                            _logger.LogWarning(
                                "User context resolution failed. Path={Path} Reason=NoPeopleInDevelopmentDatabase",
                                httpContext.Request.Path.Value ?? string.Empty);

                            return UserContextResolutionResult.Forbidden("V databázi nejsou žádné osoby.");
                        }
                    }
                    else
                    {
                        WriteAuthTrace(
                            $"ResolveAsync failed | principal not authenticated | CandidateCount={loginCandidates.Count}");

                        _logger.LogWarning(
                            "User context resolution failed. Path={Path} Reason=PrincipalNotAuthenticated LoginCandidateCount={LoginCandidateCount}",
                            httpContext.Request.Path.Value ?? string.Empty,
                            loginCandidates.Count);

                        return UserContextResolutionResult.Unauthorized("Uživatel není autentizován.");
                    }
                }
            }
            else
            {
                var authenticatedPrincipal = principal!;
                var claimValue = GuidClaimTypes
                    .Select(type => authenticatedPrincipal.FindFirst(type)?.Value)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

                var guidParsed = Guid.TryParse(claimValue, out var objectGuid);
                WriteAuthTrace(
                    $"Authenticated principal branch | GuidClaimValue={(string.IsNullOrWhiteSpace(claimValue) ? "(none)" : claimValue)} | GuidParsed={guidParsed} | CandidateCount={loginCandidates.Count}");

                _logger.LogInformation(
                    "Authenticated principal detected. Path={Path} GuidClaimPresent={GuidClaimPresent} GuidParsed={GuidParsed} LoginCandidateCount={LoginCandidateCount}",
                    httpContext.Request.Path.Value ?? string.Empty,
                    !string.IsNullOrWhiteSpace(claimValue),
                    guidParsed,
                    loginCandidates.Count);

                if (guidParsed)
                {
                    osobaId = await _dbContext.Osoby
                        .AsNoTracking()
                        .Where(x => x.GuidAd == objectGuid)
                        .Select(x => (int?)x.Id)
                        .FirstOrDefaultAsync(ct);

                    WriteAuthTrace($"Guid lookup finished | MatchedOsobaId={osobaId?.ToString() ?? "(null)"}");

                    _logger.LogInformation(
                        "Guid-based person lookup completed. Path={Path} MatchedOsobaId={MatchedOsobaId}",
                        httpContext.Request.Path.Value ?? string.Empty,
                        osobaId);
                }

                if (!osobaId.HasValue)
                {
                    if (loginCandidates.Count > 0)
                    {
                        WriteAuthTrace(
                            $"Authenticated principal branch | Guid miss -> attempting AdLogin lookup | CandidateCount={loginCandidates.Count}");

                        _logger.LogInformation(
                            "Attempting authenticated login-candidate resolution after Guid lookup miss. Path={Path} LoginCandidateCount={LoginCandidateCount}",
                            httpContext.Request.Path.Value ?? string.Empty,
                            loginCandidates.Count);

                        osobaId = await ResolveOsobaIdFromLoginCandidatesAsync(loginCandidates, ct);
                    }
                }

                if (!osobaId.HasValue)
                {
                    WriteAuthTrace(
                        $"ResolveAsync failed | no Guid/AdLogin match | CandidateCount={loginCandidates.Count}");

                    _logger.LogWarning(
                        "User context resolution failed. Path={Path} Reason=NoGuidOrAdLoginMatch LoginCandidateCount={LoginCandidateCount}",
                        httpContext.Request.Path.Value ?? string.Empty,
                        loginCandidates.Count);

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
            .FirstOrDefaultAsync(ct);

        if (osoba is null)
        {
            WriteAuthTrace($"ResolveAsync failed | resolved OsobaId {osobaId} not found in DB");

            _logger.LogWarning(
                "User context resolution failed. Reason=ResolvedPersonMissingInDatabase OsobaId={OsobaId}",
                osobaId);

            return UserContextResolutionResult.Forbidden("Přihlášená osoba v databázi neexistuje.");
        }

        var organizationalUnit = await _dbContext.CiselnikOrganizacniCelky
            .AsNoTracking()
            .Where(x => x.Id == osoba.OrganizacniCelekId)
            .Select(x => new { x.Kod, x.Nazev })
            .FirstOrDefaultAsync(ct);

        var roleCodes = await (
                from ur in _dbContext.AuthzUserRoles.AsNoTracking()
                join role in _dbContext.AuthzRoles.AsNoTracking() on ur.RoleId equals role.Id
                where ur.OsobaId == osoba.Id && ur.IsActive && role.IsActive
                orderby role.Kod
                select role.Kod)
            .Distinct()
            .ToListAsync(ct);

        var isSuperAdmin = await _dbContext.AuthzSuperadmins
            .AsNoTracking()
            .AnyAsync(x => x.OsobaId == osoba.Id, ct);

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
            .ToListAsync(ct);

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
                ct);

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
            .ToListAsync(ct));
        grants.AddRange(implicitProjectRoleGrants);

        var implicitSubsystemRoleGrants = SubsystemRolePermissionGrantBuilder.BuildImplicitSubsystemRoleGrants(
            await (
                from assignment in _dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
                join role in _dbContext.CiselnikRoliSubsystemu.AsNoTracking() on assignment.RoleSubsystemuId equals role.Id
                join projectSubsystem in _dbContext.ProjektSubsystemy.AsNoTracking() on assignment.ProjektSubsystemId equals projectSubsystem.Id
                where assignment.OsobaId == osoba.Id
                    && !assignment.DatumOdebrani.HasValue
                    && !projectSubsystem.DatumOdebrani.HasValue
                select new SubsystemRoleAssignmentGrantSource
                {
                    RoleCode = role.Kod,
                    ProjectId = projectSubsystem.ProjektId
                })
            .ToListAsync(ct));
        grants.AddRange(implicitSubsystemRoleGrants);

        var projectRoleProjectIds = await _dbContext.ObsazeniProjektu
            .AsNoTracking()
            .Where(x => x.OsobaId == osoba.Id && !x.DatumOdebrani.HasValue)
            .Select(x => x.ProjektId)
            .ToListAsync(ct);

        var subsystemRoleProjectIds = await (
                from role in _dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
                join projectSubsystem in _dbContext.ProjektSubsystemy.AsNoTracking() on role.ProjektSubsystemId equals projectSubsystem.Id
                where role.OsobaId == osoba.Id
                    && !role.DatumOdebrani.HasValue
                    && !projectSubsystem.DatumOdebrani.HasValue
                select projectSubsystem.ProjektId)
            .ToListAsync(ct);

        var visibleProjectIds = projectRoleProjectIds
            .Concat(subsystemRoleProjectIds)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        var deletedProjectIds = await _dbContext.Projekty
            .AsNoTracking()
            .Join(
                _dbContext.CiselnikStavuProjektu.AsNoTracking(),
                project => project.StavId,
                status => status.Id,
                (project, status) => new
                {
                    project.Id,
                    status.Kod,
                    status.Nazev
                })
            .ToListAsync(ct);

        var resolvedDeletedProjectIds = deletedProjectIds
            .Where(x =>
                string.Equals(x.Kod, "DELETED", StringComparison.OrdinalIgnoreCase) ||
                _textNormalizer.Normalize(x.Nazev).Contains("smaz"))
            .Select(x => x.Id)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

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
            DeletedProjectIds = resolvedDeletedProjectIds,
            PermissionGrants = grants
        };

        _logger.LogInformation(
            "User context resolved successfully. OsobaId={OsobaId} IsSuperAdmin={IsSuperAdmin} RoleCount={RoleCount} VisibleProjectCount={VisibleProjectCount} PermissionGrantCount={PermissionGrantCount}",
            context.OsobaId,
            context.IsSuperAdmin,
            context.RoleKody.Count,
            context.VisibleProjectIds.Count,
            context.PermissionGrants.Count);

        WriteAuthTrace(
            $"ResolveAsync success | OsobaId={context.OsobaId} | IsSuperAdmin={context.IsSuperAdmin} | RoleCount={context.RoleKody.Count} | VisibleProjectCount={context.VisibleProjectIds.Count} | PermissionGrantCount={context.PermissionGrants.Count}");

        return UserContextResolutionResult.Success(context);
    }

    private async Task<int?> ResolveOsobaIdFromAsUserAsync(string asUser, CancellationToken ct)
    {
        if (int.TryParse(asUser, out var osobaId))
        {
            return await _dbContext.Osoby.AsNoTracking()
                .Where(x => x.Id == osobaId)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync(ct);
        }

        if (Guid.TryParse(asUser, out var guid))
        {
            return await _dbContext.Osoby.AsNoTracking()
                .Where(x => x.GuidAd == guid)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync(ct);
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
            .ToListAsync(ct);

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

    private async Task<int?> ResolveOsobaIdFromLoginCandidatesAsync(IReadOnlySet<string> loginCandidates, CancellationToken ct)
    {
        if (loginCandidates.Count == 0)
        {
            WriteAuthTrace("AdLogin lookup skipped | no login candidates");
            _logger.LogInformation("Skipping AdLogin lookup because there are no login candidates.");
            return null;
        }

        var people = await _dbContext.Osoby
            .AsNoTracking()
            .Where(x => x.AdLogin != null)
            .Select(x => new
            {
                x.Id,
                x.AdLogin
            })
            .ToListAsync(ct);

        var matchedPerson = people
            .FirstOrDefault(person => LoginEquals(person.AdLogin, loginCandidates))
            ?.Id;

        WriteAuthTrace(
            $"AdLogin lookup finished | CandidateCount={loginCandidates.Count} | Candidates=[{string.Join(", ", loginCandidates.OrderBy(x => x, StringComparer.Ordinal))}] | PeopleWithAdLoginCount={people.Count} | MatchedOsobaId={matchedPerson?.ToString() ?? "(null)"}");

        _logger.LogInformation(
            "AdLogin lookup completed. CandidateCount={CandidateCount} HasDomainQualifiedCandidate={HasDomainQualifiedCandidate} HasUpnCandidate={HasUpnCandidate} HasShortCandidate={HasShortCandidate} PeopleWithAdLoginCount={PeopleWithAdLoginCount} MatchedOsobaId={MatchedOsobaId}",
            loginCandidates.Count,
            loginCandidates.Any(candidate => candidate.Contains('\\')),
            loginCandidates.Any(candidate => candidate.Contains('@')),
            loginCandidates.Any(candidate => !candidate.Contains('\\') && !candidate.Contains('@')),
            people.Count,
            matchedPerson);

        return matchedPerson;
    }

    private static void WriteAuthTrace(string message)
    {
        Console.WriteLine($"[AUTH TRACE] {message}");
    }

    private static string DescribePresentClaimTypes(ClaimsPrincipal? principal, IEnumerable<string> claimTypes)
    {
        if (principal is null)
        {
            return "(none)";
        }

        var present = claimTypes
            .Where(type => !string.IsNullOrWhiteSpace(principal.FindFirst(type)?.Value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return present.Length == 0 ? "(none)" : string.Join(",", present);
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

        var storedCandidates = BuildNormalizedLoginCandidates(storedLogin);
        return storedCandidates.Overlaps(candidates);
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
