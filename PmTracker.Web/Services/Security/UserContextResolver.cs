using System.Linq.Expressions;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
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

    private sealed record ActiveRoleRow(int RoleId, string RoleCode);
    private sealed record PermissionGrantRaw(string Klic, PermissionScopeLevel ScopeLevel, ScopeMode ScopeMode, bool IsAllowed, int RolePermissionId);
    private sealed record PermissionGrantProjectRow(string Klic, PermissionScopeLevel ScopeLevel, ScopeMode ScopeMode, bool IsAllowed, int RolePermissionId, int? ProjectId);
    private sealed record ResolvedPersonRow(
        int Id,
        string? Titul,
        string Jmeno,
        string Prijmeni,
        string? Email,
        string? OrganizacniCelekKod,
        string? OrganizacniCelekNazev,
        bool IsSuperAdmin);

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

    /// <summary>
    /// Načte projektové-role granty pro danou osobu z DB cesty:
    /// ObsazeniProjektu × CiselnikRoliProjektu (přes FK AuthzRoleId) × AuthzRolePermissions.
    /// Fáze B náhrada za <c>ProjectRolePermissionGrantBuilder</c>.
    /// </summary>
    /// <remarks>
    /// Sémantika: role s <see cref="RoleScope.Project"/> a mapping se <see cref="ScopeMode.All"/>
    /// znamená "všechny projekty, kde osoba má tuto roli". Výsledný VM emituje
    /// <c>ScopeMode="INCLUDE"</c> + <c>ProjectIds=[...]</c> (spec §3.3, VM kontrakt ze spec §3.1).
    /// </remarks>
    public static async Task<IReadOnlyList<PermissionGrantViewModel>> LoadDbDrivenProjectRoleGrantsAsync(
        PmTrackerDbContext dbContext,
        int osobaId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var rows = await (
                from assignment in dbContext.ObsazeniProjektu.AsNoTracking()
                where assignment.OsobaId == osobaId && !assignment.DatumOdebrani.HasValue
                join lookupRole in dbContext.CiselnikRoliProjektu.AsNoTracking()
                    on assignment.RoleId equals lookupRole.Id
                where lookupRole.AuthzRoleId != null
                join authzRole in dbContext.AuthzRoles.AsNoTracking()
                    on lookupRole.AuthzRoleId equals authzRole.Id
                where authzRole.IsActive
                join rp in dbContext.AuthzRolePermissions.AsNoTracking()
                    on authzRole.Id equals rp.RoleId
                where rp.IsAllowed
                join permission in dbContext.AuthzPermissions.AsNoTracking()
                    on rp.PermissionId equals permission.Id
                where permission.IsActive
                select new
                {
                    permission.Klic,
                    permission.ScopeLevel,
                    assignment.ProjektId
                })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => r.Klic)
            .Select(group => new PermissionGrantViewModel
            {
                PermissionKey = group.Key,
                ScopeLevel = group.First().ScopeLevel.ToString().ToUpperInvariant(),
                ScopeMode = "INCLUDE",
                IsAllowed = true,
                ProjectIds = group
                    .Select(r => r.ProjektId)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList(),
                SourceType = "PROJECT_ROLE"
            })
            .ToList();
    }

    /// <summary>
    /// Načte subsystémové-role granty pro danou osobu z DB cesty:
    /// ObsazeniSubsystemuProjektu × CiselnikRoliSubsystemu (přes FK AuthzRoleId)
    /// × AuthzRolePermissions. Výsledný VM používá ProjektId ze spojeného ProjektSubsystemy řádku.
    /// Fáze B náhrada za <c>SubsystemRolePermissionGrantBuilder</c>.
    /// </summary>
    public static async Task<IReadOnlyList<PermissionGrantViewModel>> LoadDbDrivenSubsystemRoleGrantsAsync(
        PmTrackerDbContext dbContext,
        int osobaId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var rows = await (
                from assignment in dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
                where assignment.OsobaId == osobaId && !assignment.DatumOdebrani.HasValue
                join lookupRole in dbContext.CiselnikRoliSubsystemu.AsNoTracking()
                    on assignment.RoleSubsystemuId equals lookupRole.Id
                where lookupRole.AuthzRoleId != null
                join projectSubsystem in dbContext.ProjektSubsystemy.AsNoTracking()
                    on assignment.ProjektSubsystemId equals projectSubsystem.Id
                where !projectSubsystem.DatumOdebrani.HasValue
                join authzRole in dbContext.AuthzRoles.AsNoTracking()
                    on lookupRole.AuthzRoleId equals authzRole.Id
                where authzRole.IsActive
                join rp in dbContext.AuthzRolePermissions.AsNoTracking()
                    on authzRole.Id equals rp.RoleId
                where rp.IsAllowed
                join permission in dbContext.AuthzPermissions.AsNoTracking()
                    on rp.PermissionId equals permission.Id
                where permission.IsActive
                select new
                {
                    permission.Klic,
                    permission.ScopeLevel,
                    projectSubsystem.ProjektId
                })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => r.Klic)
            .Select(group => new PermissionGrantViewModel
            {
                PermissionKey = group.Key,
                ScopeLevel = group.First().ScopeLevel.ToString().ToUpperInvariant(),
                ScopeMode = "INCLUDE",
                IsAllowed = true,
                ProjectIds = group
                    .Select(r => r.ProjektId)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList(),
                SourceType = "SUBSYSTEM_ROLE"
            })
            .ToList();
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
                            _logger.LogWarning(
                                "User context resolution failed. Path={Path} Reason=NoPeopleInDevelopmentDatabase",
                                httpContext.Request.Path.Value ?? string.Empty);

                            return UserContextResolutionResult.Forbidden("V databázi nejsou žádné osoby.");
                        }
                    }
                    else
                    {
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

                    _logger.LogInformation(
                        "Guid-based person lookup completed. Path={Path} MatchedOsobaId={MatchedOsobaId}",
                        httpContext.Request.Path.Value ?? string.Empty,
                        osobaId);
                }

                if (!osobaId.HasValue)
                {
                    if (loginCandidates.Count > 0)
                    {
                        _logger.LogInformation(
                            "Attempting authenticated login-candidate resolution after Guid lookup miss. Path={Path} LoginCandidateCount={LoginCandidateCount}",
                            httpContext.Request.Path.Value ?? string.Empty,
                            loginCandidates.Count);

                        osobaId = await ResolveOsobaIdFromLoginCandidatesAsync(loginCandidates, ct);
                    }
                }

                if (!osobaId.HasValue)
                {
                    _logger.LogWarning(
                        "User context resolution failed. Path={Path} Reason=NoGuidOrAdLoginMatch LoginCandidateCount={LoginCandidateCount}",
                        httpContext.Request.Path.Value ?? string.Empty,
                        loginCandidates.Count);

                    return UserContextResolutionResult.Forbidden("Nemáte přístup do aplikace. Chybí párování osoby.guid_ad nebo osoby.ad_login.");
                }
            }
        }

        var osoba = await (
                from person in _dbContext.Osoby.AsNoTracking()
                join organizationalUnit in _dbContext.CiselnikOrganizacniCelky.AsNoTracking()
                    on person.OrganizacniCelekId equals organizationalUnit.Id into organizationalUnitGroup
                from organizationalUnit in organizationalUnitGroup.DefaultIfEmpty()
                where person.Id == osobaId.Value
                select new ResolvedPersonRow(
                    person.Id,
                    person.Titul,
                    person.Jmeno,
                    person.Prijmeni,
                    person.Email,
                    organizationalUnit != null ? organizationalUnit.Kod : null,
                    organizationalUnit != null ? organizationalUnit.Nazev : null,
                    _dbContext.AuthzSuperadmins.AsNoTracking().Any(superadmin => superadmin.OsobaId == person.Id)))
            .FirstOrDefaultAsync(ct);

        if (osoba is null)
        {
            _logger.LogWarning(
                "User context resolution failed. Reason=ResolvedPersonMissingInDatabase OsobaId={OsobaId}",
                osobaId);

            return UserContextResolutionResult.Forbidden("Přihlášená osoba v databázi neexistuje.");
        }

        var activeRoleRows = await (
                from ur in _dbContext.AuthzUserRoles.AsNoTracking()
                join role in _dbContext.AuthzRoles.AsNoTracking() on ur.RoleId equals role.Id
                where ur.OsobaId == osoba.Id && ur.IsActive && role.IsActive
                select new ActiveRoleRow(ur.RoleId, role.Kod))
            .ToListAsync(ct);
        var roleCodes = activeRoleRows
            .Select(x => x.RoleCode)
            .Distinct()
            .OrderBy(x => x)
            .ToList();
        var activeRoleIds = activeRoleRows
            .Select(x => x.RoleId)
            .Distinct()
            .ToList();

        var isSuperAdmin = osoba.IsSuperAdmin;
        if (!isSuperAdmin)
        {
            isSuperAdmin = roleCodes.Any(code => string.Equals(code, "SUPERADMIN", StringComparison.OrdinalIgnoreCase));
        }

        var grantProjectRows = activeRoleIds.Count == 0
            ? new List<PermissionGrantProjectRow>()
            : await (
                    from rp in _dbContext.AuthzRolePermissions.AsNoTracking()
                    join p in _dbContext.AuthzPermissions.AsNoTracking() on rp.PermissionId equals p.Id
                    join includeProject in _dbContext.AuthzRolePermissionProjects.AsNoTracking()
                        on rp.Id equals includeProject.RolePermissionId into includeProjectGroup
                    from includeProject in includeProjectGroup.DefaultIfEmpty()
                    where activeRoleIds.Contains(rp.RoleId) && p.IsActive
                    select new PermissionGrantProjectRow(
                        p.Klic,
                        p.ScopeLevel,
                        rp.ScopeMode,
                        rp.IsAllowed,
                        rp.Id,
                        includeProject != null ? includeProject.ProjektId : null))
                .ToListAsync(ct);

        var grantsRaw = grantProjectRows
            .GroupBy(row => new PermissionGrantRaw(
                row.Klic,
                row.ScopeLevel,
                row.ScopeMode,
                row.IsAllowed,
                row.RolePermissionId))
            .Select(group => new
            {
                Raw = group.Key,
                ProjectIds = group
                    .Where(item => item.ProjectId.HasValue)
                    .Select(item => item.ProjectId!.Value)
                    .Distinct()
                    .ToList()
            })
            .ToList();

        var grants = grantsRaw
            .Select(item => new PermissionGrantViewModel
            {
                PermissionKey = item.Raw.Klic,
                ScopeLevel = item.Raw.ScopeLevel.ToString().ToUpperInvariant(),
                ScopeMode = item.Raw.ScopeMode.ToString().ToUpperInvariant(),
                IsAllowed = item.Raw.IsAllowed,
                ProjectIds = item.Raw.ScopeMode.ToString().ToUpperInvariant().Equals("INCLUDE", StringComparison.OrdinalIgnoreCase)
                    ? item.ProjectIds
                    : Array.Empty<int>()
            })
            .ToList();

        var activeProjectRoleAssignments = await (
                from assignment in _dbContext.ObsazeniProjektu.AsNoTracking()
                join role in _dbContext.CiselnikRoliProjektu.AsNoTracking() on assignment.RoleId equals role.Id
                where assignment.OsobaId == osoba.Id
                    && !assignment.DatumOdebrani.HasValue
                select new ProjectRoleAssignmentGrantSource
                {
                    RoleCode = role.Kod,
                    ProjectId = assignment.ProjektId
                })
            .ToListAsync(ct);
        var implicitProjectRoleGrants = ProjectRolePermissionGrantBuilder.BuildImplicitProjectRoleGrants(activeProjectRoleAssignments);
        grants.AddRange(implicitProjectRoleGrants);

        // Fáze B — Task B1: DB-driven granty jako doplněk builderu.
        // Builder stále aktivní; deduplikace zajistí, že stejný grant (klíč + scope mode + projectIds)
        // není přidán dvakrát. Po Fázi B3 se builder smaže a zůstane jen tato cesta.
        var dbDrivenProjectGrants = await LoadDbDrivenProjectRoleGrantsAsync(_dbContext, osoba.Id, ct);
        foreach (var dbGrant in dbDrivenProjectGrants)
        {
            var alreadyPresent = grants.Any(existing =>
                string.Equals(existing.PermissionKey, dbGrant.PermissionKey, StringComparison.OrdinalIgnoreCase)
                && string.Equals(existing.ScopeMode, dbGrant.ScopeMode, StringComparison.OrdinalIgnoreCase)
                && existing.ProjectIds.OrderBy(x => x).SequenceEqual(dbGrant.ProjectIds.OrderBy(x => x)));

            if (!alreadyPresent)
            {
                grants.Add(dbGrant);
            }
        }

        var activeSubsystemRoleAssignments = await (
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
            .ToListAsync(ct);
        var implicitSubsystemRoleGrants = SubsystemRolePermissionGrantBuilder.BuildImplicitSubsystemRoleGrants(activeSubsystemRoleAssignments);
        grants.AddRange(implicitSubsystemRoleGrants);

        // Fáze B — Task B2: DB-driven subsystémové granty jako doplněk builderu. Deduplikace stejná jako u projektových.
        var dbDrivenSubsystemGrants = await LoadDbDrivenSubsystemRoleGrantsAsync(_dbContext, osoba.Id, ct);
        foreach (var dbGrant in dbDrivenSubsystemGrants)
        {
            var alreadyPresent = grants.Any(existing =>
                string.Equals(existing.PermissionKey, dbGrant.PermissionKey, StringComparison.OrdinalIgnoreCase)
                && string.Equals(existing.ScopeMode, dbGrant.ScopeMode, StringComparison.OrdinalIgnoreCase)
                && existing.ProjectIds.OrderBy(x => x).SequenceEqual(dbGrant.ProjectIds.OrderBy(x => x)));

            if (!alreadyPresent)
            {
                grants.Add(dbGrant);
            }
        }

        var projectRoleProjectIds = activeProjectRoleAssignments
            .Select(x => x.ProjectId)
            .Distinct()
            .ToList();

        var subsystemRoleProjectIds = activeSubsystemRoleAssignments
            .Select(x => x.ProjectId)
            .Distinct()
            .ToList();

        var visibleProjectIds = projectRoleProjectIds
            .Concat(subsystemRoleProjectIds)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        var resolvedDeletedProjectIds = await ProjectAuthorizationQueryHelper.BuildDeletedProjectIdsAsync(
            _dbContext,
            _textNormalizer,
            ct);

        var displayName = BuildDisplayName(osoba.Titul, osoba.Jmeno, osoba.Prijmeni, osoba.Id);

        var context = new CurrentUserContextViewModel
        {
            OsobaId = osoba.Id,
            Jmeno = osoba.Jmeno,
            Prijmeni = osoba.Prijmeni,
            DisplayName = displayName,
            Email = osoba.Email?.Trim() ?? string.Empty,
            OrganizacniCelekKod = string.IsNullOrWhiteSpace(osoba.OrganizacniCelekKod) ? null : osoba.OrganizacniCelekKod.Trim(),
            OrganizacniCelek = osoba.OrganizacniCelekNazev ?? "-",
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
        var loginCandidates = BuildNormalizedLoginCandidates(asUser);
        var queryTokens = normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var candidates = await _dbContext.Osoby
            .AsNoTracking()
            .Where(BuildAsUserPrefilterPredicate(loginCandidates, normalized, queryTokens))
            .Select(x => new
            {
                x.Id,
                x.Titul,
                x.Jmeno,
                x.Prijmeni,
                x.Email,
                x.AdLogin
            })
            .Take(200)
            .ToListAsync(ct);
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
            _logger.LogInformation("Skipping AdLogin lookup because there are no login candidates.");
            return null;
        }

        var people = await _dbContext.Osoby
            .AsNoTracking()
            .Where(BuildAdLoginPrefilterPredicate(loginCandidates))
            .Select(x => new
            {
                x.Id,
                x.AdLogin
            })
            .ToListAsync(ct);

        var matchedPerson = people
            .FirstOrDefault(person => LoginEquals(person.AdLogin, loginCandidates))
            ?.Id;

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

    private static Expression<Func<OsobaEntity, bool>> BuildAdLoginPrefilterPredicate(IReadOnlySet<string> loginCandidates)
    {
        var normalizedCandidates = loginCandidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Select(candidate => candidate.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalizedCandidates.Length == 0)
        {
            return _ => false;
        }

        var shortCandidates = normalizedCandidates
            .Where(candidate => !candidate.Contains('\\') && !candidate.Contains('@'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var parameter = Expression.Parameter(typeof(OsobaEntity), "osoba");
        var adLoginProperty = Expression.Property(parameter, nameof(OsobaEntity.AdLogin));
        var notNull = Expression.NotEqual(adLoginProperty, Expression.Constant(null, typeof(string)));
        var lowerAdLogin = Expression.Call(adLoginProperty, typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!);

        Expression body = Expression.Constant(false);
        foreach (var candidate in normalizedCandidates)
        {
            body = Expression.OrElse(
                body,
                Expression.Equal(lowerAdLogin, Expression.Constant(candidate)));
        }

        foreach (var candidate in shortCandidates)
        {
            body = Expression.OrElse(
                body,
                Expression.Call(lowerAdLogin, typeof(string).GetMethod(nameof(string.EndsWith), [typeof(string)])!, Expression.Constant("\\" + candidate)));
            body = Expression.OrElse(
                body,
                Expression.Call(lowerAdLogin, typeof(string).GetMethod(nameof(string.StartsWith), [typeof(string)])!, Expression.Constant(candidate + "@")));
        }

        return Expression.Lambda<Func<OsobaEntity, bool>>(
            Expression.AndAlso(notNull, body),
            parameter);
    }

    private static Expression<Func<OsobaEntity, bool>> BuildAsUserPrefilterPredicate(
        IReadOnlySet<string> loginCandidates,
        string normalizedSearch,
        IReadOnlyCollection<string> queryTokens)
    {
        var loginPredicate = BuildAdLoginPrefilterPredicate(loginCandidates);
        var searchPredicate = BuildNameAndEmailPrefilterPredicate(normalizedSearch, queryTokens);

        return OrElse(loginPredicate, searchPredicate);
    }

    private static Expression<Func<OsobaEntity, bool>> BuildNameAndEmailPrefilterPredicate(
        string normalizedSearch,
        IReadOnlyCollection<string> queryTokens)
    {
        var normalized = normalizedSearch.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized) && queryTokens.Count == 0)
        {
            return _ => false;
        }

        var parameter = Expression.Parameter(typeof(OsobaEntity), "osoba");
        Expression body = Expression.Constant(false);

        if (!string.IsNullOrWhiteSpace(normalized))
        {
            body = Expression.OrElse(body, BuildContainsCondition(parameter, nameof(OsobaEntity.Email), normalized));
        }

        foreach (var token in queryTokens)
        {
            body = Expression.OrElse(body, BuildContainsCondition(parameter, nameof(OsobaEntity.Jmeno), token));
            body = Expression.OrElse(body, BuildContainsCondition(parameter, nameof(OsobaEntity.Prijmeni), token));
            body = Expression.OrElse(body, BuildContainsCondition(parameter, nameof(OsobaEntity.Titul), token));
        }

        return Expression.Lambda<Func<OsobaEntity, bool>>(body, parameter);
    }

    private static Expression<Func<OsobaEntity, bool>> OrElse(
        Expression<Func<OsobaEntity, bool>> left,
        Expression<Func<OsobaEntity, bool>> right)
    {
        var parameter = Expression.Parameter(typeof(OsobaEntity), "osoba");
        var leftBody = ReplaceParameter(left.Body, left.Parameters[0], parameter);
        var rightBody = ReplaceParameter(right.Body, right.Parameters[0], parameter);
        return Expression.Lambda<Func<OsobaEntity, bool>>(Expression.OrElse(leftBody, rightBody), parameter);
    }

    private static Expression ReplaceParameter(Expression body, ParameterExpression source, ParameterExpression target)
        => new ParameterReplaceVisitor(source, target).Visit(body)!;

    private static Expression BuildContainsCondition(ParameterExpression parameter, string propertyName, string value)
    {
        var property = Expression.Property(parameter, propertyName);
        var notNull = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
        var lowerProperty = Expression.Call(property, typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!);
        var contains = Expression.Call(
            lowerProperty,
            typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!,
            Expression.Constant(value));

        return Expression.AndAlso(notNull, contains);
    }

    private sealed class ParameterReplaceVisitor(ParameterExpression source, ParameterExpression target) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
            => node == source ? target : base.VisitParameter(node);
    }

    private string BuildDisplayName(string? titul, string jmeno, string prijmeni, int id)
    {
        var displayName = _personIdentityMatcher.BuildPersonName(titul, jmeno, prijmeni);
        return string.IsNullOrWhiteSpace(displayName) ? $"Uživatel #{id}" : displayName;
    }
}
