using System.Globalization;
using System.Data;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Dictionaries;
using PmTracker.Web.Services.Schedules;

namespace PmTracker.Web.Services.Data;

public sealed class SqlServerDataStore : IPmTrackerDataStore
{
    private static readonly StringComparer Ci = StringComparer.OrdinalIgnoreCase;
    private const string HarmonogramKrokyCiselnikKey = "harmonogram-kroky";
    private const int HarmonogramDelayColorPseudoRowId = 0;
    private const string HarmonogramDelayColorPseudoKod = "DELAY_COLOR";
    private const string DefaultDelayBarvaHex = "#DC2626";
    private const byte RecordDisplayNumberTypeIncrement = 0;
    private const byte RecordDisplayNumberTypeMeeting = 1;
    private static readonly (int Poradi, string Kod, string Nazev, string BarvaHex)[] DefaultHarmonogramKroky =
    [
        (1, "HS01_DURATION", "1. příprava zadání dodavateli", "#EF4444"),
        (2, "HS02_DURATION", "2. konzultace termínů s dodavatelem před vytvořením zadání", "#F97316"),
        (3, "HS03_DURATION", "3. odeslání zadání dodavateli", "#F59E0B"),
        (4, "HS04_DURATION", "4. dodání návrhu řešení", "#84CC16"),
        (5, "HS05_DURATION", "5. vypořádání připomínek", "#22C55E"),
        (6, "HS06_DURATION", "6. odeslání požadavku na výrobu", "#14B8A6"),
        (7, "HS07_DURATION", "7. dodání funkcionality dodavatelem", "#06B6D4"),
        (8, "HS08_DURATION", "8. připomínkování", "#3B82F6"),
        (9, "HS09_DURATION", "9. testování", "#6366F1"),
        (10, "HS10_DURATION", "10. nasazení do provozu", "#8B5CF6"),
        (11, "HS11_DURATION", "11. fakturace", "#D946EF")
    ];

    private readonly PmTrackerDbContext _dbContext;
    private readonly ITextNormalizer _textNormalizer;
    private readonly IPersonIdentityMatcher _personIdentityMatcher;
    private readonly ICommentAuthorizationPolicy _commentAuthorizationPolicy;
    private readonly IReadOnlyDictionary<string, Action<SaveCiselnikRowCommand>> _ciselnikSaveHandlers;
    private readonly IReadOnlyDictionary<string, Action<DeleteCiselnikRowCommand>> _ciselnikDeleteHandlers;

    private sealed record HarmonogramTypPar(
        int KrokIndex,
        string Kod,
        string Nazev,
        string BarvaHex,
        int TrvaniTypId,
        int ZpozdeniTypId);

    private sealed record HarmonogramSchemaDefinition(
        int Verze,
        string DelayBarvaHex,
        IReadOnlyList<HarmonogramTypPar> Kroky);

    private sealed record HarmonogramSchemaCloneResult(
        HarmonogramSablonaEntity SourceSchema,
        HarmonogramSablonaEntity NewSchema,
        IReadOnlyDictionary<int, HarmonogramTypEntity> ClonedBySourceId,
        IReadOnlyList<HarmonogramTypEntity> ClonedRows);
    private sealed record HarmonogramVypocetKroku(
        int KrokIndex,
        string Kod,
        string Nazev,
        string BarvaHex,
        int TrvaniTypId,
        int ZpozdeniTypId,
        int TrvaniDni,
        int ZpozdeniDni,
        DateTime PlanStartDatum,
        DateTime BaselineDatum,
        DateTime RealStartDatum,
        DateTime PosunuteDatum);

    public SqlServerDataStore(
        PmTrackerDbContext dbContext,
        ITextNormalizer textNormalizer,
        IPersonIdentityMatcher personIdentityMatcher,
        ICommentAuthorizationPolicy commentAuthorizationPolicy)
    {
        _dbContext = dbContext;
        _textNormalizer = textNormalizer;
        _personIdentityMatcher = personIdentityMatcher;
        _commentAuthorizationPolicy = commentAuthorizationPolicy;
        _ciselnikSaveHandlers = BuildCiselnikSaveHandlers();
        _ciselnikDeleteHandlers = BuildCiselnikDeleteHandlers();
    }

    public CurrentUserContextViewModel BuildCurrentUserContext(string? asProfile)
    {
        var osobaId = ResolveAsProfileToOsobaId(asProfile);

        var osoba = _dbContext.Osoby
            .AsNoTracking()
            .Where(x => x.Id == osobaId)
            .Select(x => new
            {
                x.Id,
                x.Titul,
                x.Jmeno,
                x.Prijmeni,
                x.Email,
                x.GuidAd,
                x.OrganizacniCelekId
            })
            .FirstOrDefault();

        if (osoba is null)
        {
            throw new InvalidOperationException($"Osoba '{asProfile}' nebyla v DB nalezena.");
        }

        var roles = BuildUserRoleCodes(osoba.Id);
        var grants = BuildUserPermissionGrants(osoba.Id);
        var teamProjectIds = _dbContext.ObsazeniProjektu.AsNoTracking()
            .Where(x => x.OsobaId == osoba.Id)
            .Select(x => x.ProjektId)
            .Distinct()
            .ToList();
        var isSuperadmin = _dbContext.AuthzSuperadmins.AsNoTracking().Any(x => x.OsobaId == osoba.Id)
            || roles.Any(x => Ci.Equals(x, "SUPERADMIN"));

        var orgUnit = _dbContext.CiselnikOrganizacniCelky.AsNoTracking()
            .Where(x => x.Id == osoba.OrganizacniCelekId)
            .Select(x => new { x.Kod, x.Nazev })
            .FirstOrDefault();

        return new CurrentUserContextViewModel
        {
            OsobaId = osoba.Id,
            Jmeno = osoba.Jmeno,
            Prijmeni = osoba.Prijmeni,
            DisplayName = BuildDisplayName(osoba.Titul, osoba.Jmeno, osoba.Prijmeni, osoba.Id),
            Email = osoba.Email?.Trim() ?? string.Empty,
            OrganizacniCelekKod = string.IsNullOrWhiteSpace(orgUnit?.Kod) ? null : orgUnit.Kod.Trim(),
            OrganizacniCelek = orgUnit?.Nazev ?? "-",
            IsSuperAdmin = isSuperadmin,
            RoleKody = roles,
            PermissionGrants = grants,
            TeamProjectIds = teamProjectIds
        };
    }

    public bool ProjektExists(int id)
        => _dbContext.Projekty.AsNoTracking().Any(x => x.Id == id);

    public IReadOnlyList<ProjektListItemViewModel> BuildProjektyList()
    {
        var projects = _dbContext.Projekty.AsNoTracking().ToList();
        var statuses = _dbContext.CiselnikStavuProjektu.AsNoTracking().ToDictionary(x => x.Id);

        return projects
            .OrderBy(x => x.Zkratka, StringComparer.CurrentCultureIgnoreCase)
            .Select(project =>
            {
                return new ProjektListItemViewModel
                {
                    Id = project.Id,
                    Zkratka = project.Zkratka,
                    Nazev = project.CelyNazev,
                    StavKod = statuses.GetValueOrDefault(project.StavId)?.Kod,
                    Stav = statuses.GetValueOrDefault(project.StavId)?.Nazev ?? "-",
                    PouzivatIdentJednani = project.PouzivatIdentJednani,
                    CanEdit = true,
                    CanDelete = true
                };
            })
            .ToList();
    }

    public ProjektDetailViewModel BuildProjektDetail(int id)
    {
        var project = _dbContext.Projekty.AsNoTracking().FirstOrDefault(x => x.Id == id)
            ?? throw new InvalidOperationException($"Projekt {id} nebyl nalezen.");

        var statusById = _dbContext.CiselnikStavuProjektu.AsNoTracking().ToDictionary(x => x.Id);
        var teamRoleOptions = _dbContext.CiselnikRoliProjektu.AsNoTracking()
            .OrderBy(x => x.Nazev)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Kod,
                Label = x.Nazev
            })
            .ToList();
        var subsystemFilterOptions = _dbContext.Subsystemy.AsNoTracking()
            .OrderBy(x => x.Kod)
            .ThenBy(x => x.Nazev)
            .Select(x => new LookupOptionViewModel
            {
                Value = string.IsNullOrWhiteSpace(x.Kod) ? x.Nazev : x.Kod,
                Label = string.IsNullOrWhiteSpace(x.Kod) ? x.Nazev : $"{x.Kod} - {x.Nazev}"
            })
            .ToList();
        var categoryFilterOptions = _dbContext.CiselnikKategoriiZaznamu.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Kod,
                Label = x.Nazev
            })
            .ToList();
        var taskStateFilterOptions = _dbContext.CiselnikStavuUkolu.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Kod,
                Label = x.Nazev
            })
            .ToList();
        var taskTypeFilterOptions = _dbContext.CiselnikTypuUkolu.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Kod,
                Label = x.Nazev
            })
            .ToList();
        var meetingStatusOptions = _dbContext.CiselnikStavuJednani.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Kod,
                Label = x.Nazev
            })
            .ToList();
        var meetingStatusFilterOptions = _dbContext.CiselnikStavuJednani.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Id.ToString(CultureInfo.InvariantCulture),
                Label = x.Nazev
            })
            .ToList();

        var records = BuildRecordCardsForProject(id);
        var harmonogramUkoly = BuildProjectScheduleRows(records);
        var meetings = BuildJednaniList(id);
        var team = BuildTeamMembers(id);
        var teamCandidates = BuildTeamCandidates();
        var ownerFilterOptions = records
            .Where(x => x.AktualniVlastnikId > 0)
            .GroupBy(x => x.AktualniVlastnikId)
            .Select(group => new LookupOptionViewModel
            {
                Value = group.Key.ToString(CultureInfo.InvariantCulture),
                Label = group.First().AktualniVlastnik
            })
            .OrderBy(x => x.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new ProjektDetailViewModel
        {
            Projekt = new ProjektHeaderViewModel
            {
                Id = project.Id,
                Nazev = project.CelyNazev,
                Zkratka = project.Zkratka,
                Stav = statusById.GetValueOrDefault(project.StavId)?.Nazev ?? "-",
                PouzivatIdentJednani = project.PouzivatIdentJednani
            },
            DleSubsystemu = true,
            SkupinySubsystemu = records
                .GroupBy(x => x.AktualniSubsystem)
                .OrderBy(x => x.Key, StringComparer.CurrentCultureIgnoreCase)
                .Select(group => new SubsystemGroupViewModel
                {
                    Nazev = group.Key,
                    Zaznamy = group.ToList()
                })
                .ToList(),
            Zaznamy = records,
            Jednani = meetings,
            Tym = team,
            DostupniClenoveTymu = teamCandidates,
            HarmonogramUkoly = harmonogramUkoly,
            RoleProjektu = teamRoleOptions,
            OtevrenaJednani = BuildOpenMeetingOptions(meetings),
            StavyJednani = meetingStatusOptions,
            Filtry = new ProjektFiltryViewModel
            {
                Subsystemy = records.Select(x => x.AktualniSubsystem).Distinct(Ci).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList(),
                SubsystemyMoznosti = subsystemFilterOptions,
                Kategorie = records.Select(x => x.KategorieNazev).Distinct(Ci).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList(),
                KategorieMoznosti = categoryFilterOptions,
                StavyUkolu = records.Select(x => x.Stav).Distinct(Ci).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList(),
                StavyUkoluMoznosti = taskStateFilterOptions,
                TypyUkolu = records.Where(x => !string.IsNullOrWhiteSpace(x.TypUkolu)).Select(x => x.TypUkolu!).Distinct(Ci).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList(),
                TypyUkoluMoznosti = taskTypeFilterOptions,
                Vlastnici = records.Select(x => x.AktualniVlastnik).Distinct(Ci).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList(),
                VlastniciMoznosti = ownerFilterOptions,
                StavyJednaniVyjadreni = meetingStatusFilterOptions
            }
        };
    }

    public ZaznamEditViewModel BuildZaznamEdit(int id)
    {
        var record = _dbContext.ProjektoveZaznamy.AsNoTracking().FirstOrDefault(x => x.Id == id)
            ?? throw new InvalidOperationException($"Záznam {id} nebyl nalezen.");

        return BuildZaznamEditForEntity(record, isCreate: false);
    }

    public ZaznamEditViewModel BuildZaznamCreate(int projektId)
    {
        var project = _dbContext.Projekty.AsNoTracking()
            .FirstOrDefault(x => x.Id == projektId)
            ?? throw new InvalidOperationException($"Projekt {projektId} nebyl nalezen.");

        var defaultCategory = _dbContext.CiselnikKategoriiZaznamu.AsNoTracking().OrderBy(x => x.Nazev).FirstOrDefault();
        var defaultStatus = _dbContext.CiselnikStavuUkolu.AsNoTracking().OrderBy(x => x.Nazev).FirstOrDefault();
        var defaultSubsystem = _dbContext.Subsystemy.AsNoTracking().OrderBy(x => x.Nazev).FirstOrDefault();
        var openMeetingOptions = BuildOpenMeetingOptions(BuildJednaniList(projektId));
        var selectedMeetingIdForNumber = project.PouzivatIdentJednani
            ? openMeetingOptions.FirstOrDefault()?.Id
            : null;
        var defaultSubsystemOwnerId = defaultSubsystem is null
            ? null
            : _dbContext.Osoby.AsNoTracking()
                .Where(x => x.Id == defaultSubsystem.VedouciOsobaId)
                .Select(x => (int?)x.Id)
                .FirstOrDefault();

        var defaultOwnerId = defaultSubsystemOwnerId
            ?? _dbContext.ObsazeniProjektu.AsNoTracking()
                .Where(x => x.ProjektId == projektId)
                .OrderBy(x => x.Id)
                .Select(x => (int?)x.OsobaId)
            .FirstOrDefault()
            ?? _dbContext.Osoby.AsNoTracking().OrderBy(x => x.Id).Select(x => x.Id).First();
        var activeSchema = GetActiveHarmonogramSchema();
        var activeSchemaVersion = activeSchema.Verze > 0 ? activeSchema.Verze : 1;
        var nextRecordNumber = GetNextCisloZaznamu(projektId);
        var createVisibleNumber = project.PouzivatIdentJednani
            ? string.Empty
            : nextRecordNumber.ToString(CultureInfo.InvariantCulture);

        var draft = new ProjektovyZaznamEntity
        {
            Id = 0,
            ProjektId = projektId,
            KategorieId = defaultCategory?.Id ?? 0,
            AktualniTypUkoluId = null,
            StavUkoluId = defaultStatus?.Id,
            CisloZaznamu = nextRecordNumber,
            CisloViditelne = createVisibleNumber,
            CisloViditelneTyp = RecordDisplayNumberTypeIncrement,
            CisloViditelneA = nextRecordNumber,
            CisloViditelneB = 0,
            CisloJednaniZdrojId = null,
            Nazev = string.Empty,
            Popis = string.Empty,
            VlastnikId = defaultOwnerId,
            DatumZalozeni = DateTime.Today,
            DatumUkonceni = DateTime.Today,
            SubsystemId = defaultSubsystem?.Id ?? 0,
            HarmonogramSablonaVerze = activeSchemaVersion
        };

        return BuildZaznamEditForEntity(draft, isCreate: true, forceMeetingIdForNumber: selectedMeetingIdForNumber, projectUsesMeetingIdentifier: project.PouzivatIdentJednani, openMeetingOptions: openMeetingOptions);
    }

    public int GetNextCisloZaznamu(int projektId)
    {
        var maxValue = _dbContext.ProjektoveZaznamy
            .AsNoTracking()
            .Where(x => x.ProjektId == projektId)
            .Select(x => (int?)x.CisloZaznamu)
            .Max();

        return (maxValue ?? 0) + 1;
    }

    public IReadOnlyList<JednaniProjektListItemViewModel> BuildJednaniOverview()
    {
        var projects = _dbContext.Projekty.AsNoTracking()
            .OrderBy(x => x.Zkratka)
            .Select(x => new { x.Id, x.CelyNazev })
            .ToList();

        return projects
            .Select(project => new JednaniProjektListItemViewModel
            {
                ProjektId = project.Id,
                ProjektNazev = project.CelyNazev,
                Jednani = BuildJednaniList(project.Id)
            })
            .Where(x => x.Jednani.Count > 0)
            .ToList();
    }

    public IReadOnlyList<JednaniListItemViewModel> BuildJednaniList(int projektId)
    {
        var meetings = _dbContext.Jednani.AsNoTracking()
            .Where(x => x.ProjektId == projektId)
            .ToList();

        var statusById = _dbContext.CiselnikStavuJednani.AsNoTracking().ToDictionary(x => x.Id);
        var personsById = _dbContext.Osoby.AsNoTracking().ToDictionary(x => x.Id);

        return meetings
            .OrderByDescending(x => x.CisloJednani)
            .ThenByDescending(x => x.DatumPlanovane)
            .ThenByDescending(x => x.CasZacatek)
            .Select(x => new JednaniListItemViewModel
            {
                Id = x.Id,
                CisloJednani = x.CisloJednani,
                Datum = x.DatumPlanovane,
                CasZacatek = x.CasZacatek,
                Misto = string.IsNullOrWhiteSpace(x.Misto) ? "-" : x.Misto,
                StavKod = statusById.GetValueOrDefault(x.StavJednaniId)?.Kod,
                Stav = statusById.GetValueOrDefault(x.StavJednaniId)?.Nazev ?? "-",
                UzamklOsoba = x.UzamklOsobaId.HasValue ? BuildInlinePersonLabelFromOsoba(personsById.GetValueOrDefault(x.UzamklOsobaId.Value)) : null
            })
            .ToList();
    }

    public JednaniDetailViewModel BuildJednaniDetail(int id)
    {
        var meeting = _dbContext.Jednani.AsNoTracking().FirstOrDefault(x => x.Id == id)
            ?? throw new InvalidOperationException($"Jednání {id} nebylo nalezeno.");

        var project = _dbContext.Projekty.AsNoTracking().First(x => x.Id == meeting.ProjektId);
        var meetings = BuildJednaniList(project.Id);
        var currentMeeting = meetings.First(x => x.Id == id);

        var attendance = BuildMeetingAttendance(id, project.Id);
        var taskRows = BuildMeetingTasks(id, project.Id);
        var meetingStatusesRaw = _dbContext.CiselnikStavuJednani.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Kod, x.Nazev })
            .ToList();
        var meetingStatuses = meetingStatusesRaw
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Kod,
                Label = x.Nazev
            })
            .ToList();
        var attendanceStatuses = _dbContext.CiselnikStavuUcasti.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Kod,
                Label = x.Nazev
            })
            .ToList();

        var openStatusCode = meetingStatusesRaw
            .Where(x => Ci.Equals(x.Kod, "OPEN") || x.Nazev.Contains("otev", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Kod)
            .FirstOrDefault();
        var closedStatusCode = meetingStatusesRaw
            .Where(x => Ci.Equals(x.Kod, "CLOSED") || x.Nazev.Contains("uzav", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Kod)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(closedStatusCode) && meeting.UzamklOsobaId.HasValue)
        {
            closedStatusCode = currentMeeting.StavKod;
        }

        return new JednaniDetailViewModel
        {
            ProjektId = project.Id,
            ProjektNazev = project.CelyNazev,
            Jednani = currentMeeting,
            OtevrenyStavKod = openStatusCode,
            UzavrenyStavKod = closedStatusCode,
            Ucast = attendance,
            Ukoly = taskRows,
            StavyJednani = meetingStatuses,
            StavyUcasti = attendanceStatuses
        };
    }

    public OsobyIndexViewModel BuildOsoby()
    {
        var organizations = _dbContext.CiselnikOrganizace.AsNoTracking().ToDictionary(x => x.Id);
        var orgUnits = _dbContext.CiselnikOrganizacniCelky.AsNoTracking().ToDictionary(x => x.Id);
        var organizationOptions = organizations.Values
            .OrderBy(x => x.Nazev)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Kod,
                Label = string.IsNullOrWhiteSpace(x.Kod) ? x.Nazev : $"{x.Kod} - {x.Nazev}"
            })
            .ToList();
        var orgUnitOptions = orgUnits.Values
            .OrderBy(x => x.Nazev)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Kod,
                Label = string.IsNullOrWhiteSpace(x.Kod) ? x.Nazev : $"{x.Kod} - {x.Nazev}"
            })
            .ToList();
        var peopleRows = _dbContext.Osoby.AsNoTracking()
            .OrderBy(x => x.Prijmeni)
            .ThenBy(x => x.Jmeno)
            .ToList();

        var people = peopleRows
            .Select(x => new OsobaListItemViewModel
            {
                Id = x.Id,
                Jmeno = x.Jmeno,
                Prijmeni = x.Prijmeni,
                Titul = x.Titul,
                Email = x.Email?.Trim() ?? string.Empty,
                OrganizaceKod = organizations.GetValueOrDefault(x.OrganizaceId)?.Kod,
                Organizace = organizations.GetValueOrDefault(x.OrganizaceId)?.Nazev,
                OrganizacniCelekKod = x.OrganizacniCelekId.HasValue ? orgUnits.GetValueOrDefault(x.OrganizacniCelekId.Value)?.Kod : null,
                OrganizacniCelek = x.OrganizacniCelekId.HasValue ? orgUnits.GetValueOrDefault(x.OrganizacniCelekId.Value)?.Nazev : null,
                JeAdUcet = x.GuidAd.HasValue,
                LocationLocked = x.LocationLocked
            })
            .ToList();

        return new OsobyIndexViewModel
        {
            Osoby = people,
            Organizace = organizationOptions,
            OrganizacniCelky = orgUnitOptions
        };
    }

    public ProfilPageViewModel BuildProfilPage(CurrentUserContextViewModel currentUser, int? projektId)
    {
        return new ProfilPageViewModel
        {
            Uzivatel = currentUser,
            MojeRole = BuildProfilRolePrava(currentUser.OsobaId)
        };
    }

    public CiselnikyDashboardViewModel BuildCiselnikyDashboard(string? id, CurrentUserContextViewModel currentUser)
    {
        var items = BuildCiselnikItems();
        var selected = string.IsNullOrWhiteSpace(id)
            ? items.FirstOrDefault()?.Key ?? "stavy-projektu"
            : id.Trim();

        return new CiselnikyDashboardViewModel
        {
            Ciselniky = items,
            VybranyCiselnik = BuildCiselnikDetail(selected, currentUser)
        };
    }

    public CiselnikDetailViewModel BuildCiselnikDetail(string id, CurrentUserContextViewModel currentUser)
    {
        var key = (id ?? string.Empty).Trim().ToLowerInvariant();
        var canChangeLockState = currentUser.IsSuperAdmin;

        return key switch
        {
            "stavy-projektu" => new CiselnikDetailViewModel
            {
                Key = key,
                Nazev = "Stavy projektů",
                CanChangeLockState = canChangeLockState,
                SloupceNavic = Array.Empty<string>(),
                Polozky = _dbContext.CiselnikStavuProjektu.AsNoTracking().OrderBy(x => x.Nazev).Select(x => new CiselnikRadekViewModel
                {
                    Id = x.Id,
                    Kod = x.Kod,
                    Nazev = x.Nazev,
                    IsLocked = x.IsLocked,
                    CanChangeLockState = canChangeLockState,
                    HodnotyNavic = Array.Empty<string>()
                }).ToList()
            },
            "stavy-ukolu" => new CiselnikDetailViewModel
            {
                Key = key,
                Nazev = "Stavy úkolů",
                CanChangeLockState = canChangeLockState,
                SloupceNavic = new[] { "Finální" },
                Polozky = _dbContext.CiselnikStavuUkolu.AsNoTracking().OrderBy(x => x.Nazev).Select(x => new CiselnikRadekViewModel
                {
                    Id = x.Id,
                    Kod = x.Kod,
                    Nazev = x.Nazev,
                    IsLocked = x.IsLocked,
                    CanChangeLockState = canChangeLockState,
                    HodnotyNavic = new[] { x.IsFinal ? "Ano" : "Ne" }
                }).ToList()
            },
            "kategorie-zaznamu" => BuildSimpleCiselnikDetail(key, "Kategorie záznamů", _dbContext.CiselnikKategoriiZaznamu.AsNoTracking().Select(x => new CiselnikRadekViewModel
            {
                Id = x.Id,
                Kod = x.Kod,
                Nazev = x.Nazev,
                IsLocked = x.IsLocked,
                CanChangeLockState = canChangeLockState,
                HodnotyNavic = Array.Empty<string>()
            }), canChangeLockState),
            "typy-ukolu" => BuildSimpleCiselnikDetail(key, "Typy úkolů", _dbContext.CiselnikTypuUkolu.AsNoTracking().Select(x => new CiselnikRadekViewModel
            {
                Id = x.Id,
                Kod = x.Kod,
                Nazev = x.Nazev,
                IsLocked = x.IsLocked,
                CanChangeLockState = canChangeLockState,
                HodnotyNavic = Array.Empty<string>()
            }), canChangeLockState),
            "typy-externich-odkazu" => BuildSimpleCiselnikDetail(key, "Typy externích odkazů", _dbContext.CiselnikTypuExternichOdkazu.AsNoTracking().Select(x => new CiselnikRadekViewModel
            {
                Id = x.Id,
                Kod = x.Kod,
                Nazev = x.Nazev,
                IsLocked = x.IsLocked,
                CanChangeLockState = canChangeLockState,
                HodnotyNavic = Array.Empty<string>()
            }), canChangeLockState),
            "role-projektu" => BuildSimpleCiselnikDetail(key, "Role projektu", _dbContext.CiselnikRoliProjektu.AsNoTracking().Select(x => new CiselnikRadekViewModel
            {
                Id = x.Id,
                Kod = x.Kod,
                Nazev = x.Nazev,
                IsLocked = x.IsLocked,
                CanChangeLockState = canChangeLockState,
                HodnotyNavic = Array.Empty<string>()
            }), canChangeLockState),
            "stavy-ucasti" => BuildSimpleCiselnikDetail(key, "Stavy účasti", _dbContext.CiselnikStavuUcasti.AsNoTracking().Select(x => new CiselnikRadekViewModel
            {
                Id = x.Id,
                Kod = x.Kod,
                Nazev = x.Nazev,
                IsLocked = x.IsLocked,
                CanChangeLockState = canChangeLockState,
                HodnotyNavic = Array.Empty<string>()
            }), canChangeLockState),
            "organizace" => BuildSimpleCiselnikDetail(key, "Organizace", _dbContext.CiselnikOrganizace.AsNoTracking().Select(x => new CiselnikRadekViewModel
            {
                Id = x.Id,
                Kod = x.Kod,
                Nazev = x.Nazev,
                IsLocked = x.IsLocked,
                CanChangeLockState = canChangeLockState,
                HodnotyNavic = Array.Empty<string>()
            }), canChangeLockState),
            "organizacni-celky" => BuildSimpleCiselnikDetail(key, "Organizační celky", _dbContext.CiselnikOrganizacniCelky.AsNoTracking().Select(x => new CiselnikRadekViewModel
            {
                Id = x.Id,
                Kod = x.Kod,
                Nazev = x.Nazev,
                IsLocked = x.IsLocked,
                CanChangeLockState = canChangeLockState,
                HodnotyNavic = Array.Empty<string>()
            }), canChangeLockState),
            "subsystemy" => BuildSubsystemyCiselnikDetail(key, canChangeLockState),
            HarmonogramKrokyCiselnikKey => BuildHarmonogramKrokyCiselnikDetail(key, canChangeLockState),
            "vyzvy" => new CiselnikDetailViewModel
            {
                Key = key,
                Nazev = "Výzvy",
                CanChangeLockState = canChangeLockState,
                SloupceNavic = new[] { "Rok" },
                Polozky = _dbContext.CiselnikVyzvy.AsNoTracking().OrderBy(x => x.Kod).Select(x => new CiselnikRadekViewModel
                {
                    Id = x.Id,
                    Kod = x.Kod,
                    Nazev = x.Nazev,
                    IsLocked = x.IsLocked,
                    CanChangeLockState = canChangeLockState,
                    HodnotyNavic = new[] { x.Rok.ToString("yyyy", CultureInfo.InvariantCulture) }
                }).ToList()
            },
            "stavy-jednani" => BuildSimpleCiselnikDetail(key, "Stavy jednání", _dbContext.CiselnikStavuJednani.AsNoTracking().Select(x => new CiselnikRadekViewModel
            {
                Id = x.Id,
                Kod = x.Kod,
                Nazev = x.Nazev,
                IsLocked = x.IsLocked,
                CanChangeLockState = canChangeLockState,
                HodnotyNavic = Array.Empty<string>()
            }), canChangeLockState),
            _ => BuildSimpleCiselnikDetail("stavy-projektu", "Stavy projektů", _dbContext.CiselnikStavuProjektu.AsNoTracking().Select(x => new CiselnikRadekViewModel
            {
                Id = x.Id,
                Kod = x.Kod,
                Nazev = x.Nazev,
                IsLocked = x.IsLocked,
                CanChangeLockState = canChangeLockState,
                HodnotyNavic = Array.Empty<string>()
            }), canChangeLockState)
        };
    }

    public NastaveniDashboardViewModel BuildNastaveniDashboard(string? section, CurrentUserContextViewModel currentUser, int? userId, int? projektId)
    {
        var panel = BuildNastaveniPanel(section, currentUser, userId, projektId);
        var sections = BuildNastaveniSections(currentUser, panel);

        return new NastaveniDashboardViewModel
        {
            Sekce = sections,
            AktivniPanel = panel,
            SelectedUserId = panel.EffectivePermissions.SelectedUserId,
            SelectedProjektId = panel.EffectivePermissions.SelectedProjectId
        };
    }

    public NastaveniPanelViewModel BuildNastaveniPanel(string? section, CurrentUserContextViewModel currentUser, int? userId, int? projektId)
    {
        var canManage = currentUser.HasPermission(PermissionKeys.SettingsManage);
        var normalized = NormalizeSettingsSection(section, canManage);

        var categories = _dbContext.AuthzPermissionCategories.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Kod)
            .Select(x => new PermissionCategoryViewModel
            {
                Id = x.Id,
                Kod = x.Kod,
                Nazev = x.Nazev,
                SortOrder = x.SortOrder
            })
            .ToList();

        var permissionRows = _dbContext.AuthzPermissions.AsNoTracking().OrderBy(x => x.Klic).ToList();
        var permissions = permissionRows
            .Select(x => new PermissionViewModel
            {
                Id = x.Id,
                Klic = x.Klic,
                Nazev = x.Nazev,
                CategoryKod = categories.FirstOrDefault(c => c.Id == x.CategoryId)?.Kod ?? "-",
                ScopeLevel = x.ScopeLevel,
                IsActive = x.IsActive,
                IsSystem = x.IsSystem
            })
            .ToList();

        var roles = _dbContext.AuthzRoles.AsNoTracking().OrderBy(x => x.Kod)
            .Select(x => new RoleViewModel
            {
                Id = x.Id,
                Kod = x.Kod,
                Nazev = x.Nazev,
                Popis = x.Popis ?? string.Empty,
                IsSystem = x.IsSystem,
                IsActive = x.IsActive
            })
            .ToList();

        var rolePermissionProjects = _dbContext.AuthzRolePermissionProjects.AsNoTracking().ToList();

        var rolePermissionScopes = _dbContext.AuthzRolePermissions.AsNoTracking()
            .OrderBy(x => x.RoleId)
            .ThenBy(x => x.PermissionId)
            .ToList()
            .Select(x => new RolePermissionScopeViewModel
            {
                Id = x.Id,
                RoleId = x.RoleId,
                RoleKod = roles.FirstOrDefault(role => role.Id == x.RoleId)?.Kod ?? "-",
                PermissionId = x.PermissionId,
                PermissionKlic = permissions.FirstOrDefault(permission => permission.Id == x.PermissionId)?.Klic ?? "-",
                ScopeMode = x.ScopeMode,
                IsAllowed = x.IsAllowed,
                ProjektIds = rolePermissionProjects.Where(p => p.RolePermissionId == x.Id).Select(p => p.ProjektId).Distinct().ToList()
            })
            .ToList();

        var users = _dbContext.Osoby.AsNoTracking()
            .OrderBy(x => x.Prijmeni)
            .ThenBy(x => x.Jmeno)
            .Select(x => new { x.Id, x.Titul, x.Jmeno, x.Prijmeni, x.Email })
            .ToList();

        var userRoleRows = _dbContext.AuthzUserRoles.AsNoTracking()
            .Where(x => x.IsActive)
            .Join(
                _dbContext.AuthzRoles.AsNoTracking(),
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new
                {
                    userRole.OsobaId,
                    userRole.RoleId,
                    RoleKod = role.Kod,
                    RoleIsActive = role.IsActive
                })
            .Where(x => x.RoleIsActive)
            .ToList();

        var userRoleByOsobaId = userRoleRows
            .GroupBy(x => x.OsobaId)
            .ToDictionary(group => group.Key, group => group
                .OrderBy(x => x.RoleKod, StringComparer.CurrentCultureIgnoreCase)
                .Select(x => new UserRoleItemViewModel
                {
                    RoleId = x.RoleId,
                    RoleKod = x.RoleKod
                })
                .ToList());

        var userRoleAssignments = users.Select(user =>
        {
            var userRoles = userRoleByOsobaId.TryGetValue(user.Id, out var assignedRoles)
                ? assignedRoles
                : new List<UserRoleItemViewModel>();
            return new UserRoleAssignmentViewModel
            {
                OsobaId = user.Id,
                Osoba = BuildDisplayName(user.Titul, user.Jmeno, user.Prijmeni, user.Id),
                Email = user.Email?.Trim() ?? string.Empty,
                RoleKody = userRoles.Select(x => x.RoleKod).ToList(),
                RoleAssignments = userRoles
            };
        }).ToList();

        var projects = _dbContext.Projekty.AsNoTracking().OrderBy(x => x.Zkratka)
            .Select(x => new NastaveniProjektItemViewModel { Id = x.Id, Nazev = x.CelyNazev })
            .ToList();

        var effectivePermissions = BuildEffectivePermissionPreview(currentUser, userId, projektId);

        var (title, description) = normalized switch
        {
            "akce" => ("Akce", "Katalog akcí aplikace."),
            "role-akce" => ("Mapování rolí na akce", "Nastavení oprávnění a rozsahů ALL / INCLUDE."),
            "uzivatele-role" => ("Přiřazení rolí uživatelům", "Mapování rolí na osoby."),
            "efektivni-prava" => ("Kontrola efektivních práv", "Diagnostický pohled na finální práva uživatele."),
            _ => ("Role", "Správa rolí administrátorů.")
        };

        return new NastaveniPanelViewModel
        {
            SectionKey = normalized,
            Nazev = title,
            Popis = description,
            Role = roles,
            PermissionCategories = categories,
            Permissions = permissions,
            RolePermissionScopes = rolePermissionScopes,
            UserRoles = userRoleAssignments,
            EffectivePermissions = effectivePermissions,
            Projekty = projects
        };
    }

    public PdfExportTemplateViewModel BuildProjectPrintTemplate(int projektId, CurrentUserContextViewModel currentUser, bool autoPrint)
    {
        if (!ProjektExists(projektId))
        {
            throw new InvalidOperationException($"Projekt {projektId} nebyl nalezen.");
        }

        var project = _dbContext.Projekty.AsNoTracking().First(x => x.Id == projektId);
        var records = BuildExportRecords(projektId, null, null, 0, false);

        return new PdfExportTemplateViewModel
        {
            ExportVariant = "project_all",
            AutoPrint = autoPrint,
            ProjektId = project.Id,
            ProjektZkratka = project.Zkratka,
            ProjektNazev = project.CelyNazev,
            JednaniId = null,
            JednaniCislo = null,
            JednaniDatum = null,
            JednaniMisto = null,
            JednaniStav = "Projekt",
            Vytvoril = currentUser.DisplayName,
            VytvorenoDne = DateTime.Now,
            SnapshotSummary = "Tisk kompletního projektu bez filtru.",
            PreparationSummary = null,
            VedeniProjektu = BuildProjectLeadershipRoles(project.Id),
            AppliedRuleSummary = new[] { "Bez omezení" },
            Legenda = Array.Empty<PdfLegendItemViewModel>(),
            Zaznamy = records,
            Dochazka = Array.Empty<PdfAttendanceGroupViewModel>()
        };
    }

    public PdfExportTemplateViewModel BuildMeetingPrintTemplate(int jednaniId, CurrentUserContextViewModel currentUser, bool autoPrint)
    {
        var meeting = _dbContext.Jednani.AsNoTracking().FirstOrDefault(x => x.Id == jednaniId)
            ?? throw new InvalidOperationException($"Jednání {jednaniId} nebylo nalezeno.");

        var project = _dbContext.Projekty.AsNoTracking().First(x => x.Id == meeting.ProjektId);
        var status = _dbContext.CiselnikStavuJednani.AsNoTracking().FirstOrDefault(x => x.Id == meeting.StavJednaniId);

        var records = BuildExportRecords(project.Id, meeting.Id, null, meeting.CisloJednani, true);

        return new PdfExportTemplateViewModel
        {
            ExportVariant = "meeting",
            AutoPrint = autoPrint,
            ProjektId = project.Id,
            ProjektZkratka = project.Zkratka,
            ProjektNazev = project.CelyNazev,
            JednaniId = meeting.Id,
            JednaniCislo = meeting.CisloJednani,
            JednaniDatum = meeting.DatumPlanovane,
            JednaniMisto = meeting.Misto,
            JednaniStav = status?.Nazev ?? "-",
            Vytvoril = currentUser.DisplayName,
            VytvorenoDne = DateTime.Now,
            SnapshotSummary = string.Empty,
            PreparationSummary = null,
            VedeniProjektu = Array.Empty<PdfProjectRoleMemberViewModel>(),
            AppliedRuleSummary = new[] { "Automatický meeting výstup" },
            Legenda = Array.Empty<PdfLegendItemViewModel>(),
            Zaznamy = records,
            Dochazka = BuildAttendanceGroups(meeting.Id, project.Id)
        };
    }

    public PdfExportTemplateViewModel BuildTaskPrintTemplate(int projektId, int zaznamId, CurrentUserContextViewModel currentUser, bool autoPrint)
    {
        var project = _dbContext.Projekty.AsNoTracking().FirstOrDefault(x => x.Id == projektId)
            ?? throw new InvalidOperationException($"Projekt {projektId} nebyl nalezen.");

        var lastMeeting = _dbContext.Jednani.AsNoTracking()
            .Where(x => x.ProjektId == projektId)
            .OrderByDescending(x => x.CisloJednani)
            .FirstOrDefault();

        var records = BuildExportRecords(projektId, lastMeeting?.Id, zaznamId, lastMeeting?.CisloJednani ?? 0, true);

        return new PdfExportTemplateViewModel
        {
            ExportVariant = "task_single",
            AutoPrint = autoPrint,
            ProjektId = project.Id,
            ProjektZkratka = project.Zkratka,
            ProjektNazev = project.CelyNazev,
            JednaniId = lastMeeting?.Id,
            JednaniCislo = lastMeeting?.CisloJednani,
            JednaniDatum = lastMeeting?.DatumPlanovane,
            JednaniMisto = lastMeeting?.Misto,
            JednaniStav = "Úkol",
            Vytvoril = currentUser.DisplayName,
            VytvorenoDne = DateTime.Now,
            SnapshotSummary = "Tisk jednoho úkolu.",
            PreparationSummary = null,
            VedeniProjektu = Array.Empty<PdfProjectRoleMemberViewModel>(),
            AppliedRuleSummary = new[] { "Automatický task výstup" },
            Legenda = Array.Empty<PdfLegendItemViewModel>(),
            Zaznamy = records,
            Dochazka = Array.Empty<PdfAttendanceGroupViewModel>()
        };
    }

    public int SaveProject(SaveProjectCommand command, CurrentUserContextViewModel currentUser)
    {
        var statusId = ResolveProjectStatusId(command.Stav);
        if (command.Id.HasValue)
        {
            var existing = _dbContext.Projekty.FirstOrDefault(x => x.Id == command.Id.Value)
                ?? throw new InvalidOperationException($"Projekt {command.Id.Value} nebyl nalezen.");

            var old = JsonSerializer.Serialize(existing);
            existing.CelyNazev = command.Nazev.Trim();
            existing.Zkratka = command.Zkratka.Trim();
            existing.StavId = statusId;
            existing.PouzivatIdentJednani = command.PouzivatIdentJednani;
            _dbContext.SaveChanges();
            WriteAudit(currentUser.OsobaId, "projekty", existing.Id.ToString(CultureInfo.InvariantCulture), "update", old, JsonSerializer.Serialize(existing));
            return existing.Id;
        }

        var created = new ProjektEntity
        {
            CelyNazev = command.Nazev.Trim(),
            Zkratka = command.Zkratka.Trim(),
            StavId = statusId,
            PouzivatIdentJednani = command.PouzivatIdentJednani
        };
        _dbContext.Projekty.Add(created);
        _dbContext.SaveChanges();

        WriteAudit(currentUser.OsobaId, "projekty", created.Id.ToString(CultureInfo.InvariantCulture), "create", null, JsonSerializer.Serialize(created));
        return created.Id;
    }

    public void SoftDeleteProject(SoftDeleteProjectCommand command, CurrentUserContextViewModel currentUser)
    {
        var deletedStatusId = _dbContext.CiselnikStavuProjektu
            .AsNoTracking()
            .Select(x => new { x.Id, x.Kod, x.Nazev })
            .ToList()
            .FirstOrDefault(x =>
                string.Equals(x.Kod, "DELETED", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(x.Nazev) && x.Nazev.Contains("smaz", StringComparison.OrdinalIgnoreCase)))
            ?.Id
            ?? throw new InvalidOperationException("V číselníku stavu projektu chybí stav DELETED/Smazáno.");

        var project = _dbContext.Projekty.FirstOrDefault(x => x.Id == command.ProjektId)
            ?? throw new InvalidOperationException($"Projekt {command.ProjektId} nebyl nalezen.");

        var old = JsonSerializer.Serialize(project);
        project.StavId = deletedStatusId;
        _dbContext.SaveChanges();
        WriteAudit(currentUser.OsobaId, "projekty", project.Id.ToString(CultureInfo.InvariantCulture), "soft_delete", old, JsonSerializer.Serialize(project));
    }

    public int SaveRecord(SaveRecordCommand command, CurrentUserContextViewModel currentUser)
    {
        var canEditRecord = currentUser.HasPermission(PermissionKeys.RecordsEdit, command.ProjektId);
        var canEditScheduleFull = currentUser.HasPermission(PermissionKeys.RecordsScheduleEdit, command.ProjektId);
        var canAddSchedule = currentUser.HasPermission(PermissionKeys.RecordsScheduleAdd, command.ProjektId);
        if (!canEditRecord)
        {
            if (!canEditScheduleFull && !canAddSchedule)
            {
                throw new InvalidOperationException("Nemáte oprávnění upravovat tento záznam.");
            }

            return SaveRecordScheduleOnly(command, currentUser, canEditScheduleFull);
        }

        if (!command.VlastnikId.HasValue || command.VlastnikId.Value <= 0)
        {
            throw new InvalidOperationException("Vyberte vlastníka z nabídky osob.");
        }

        var ownerIdValue = command.VlastnikId.Value;
        var categoryId = ResolveKategorieId(command.Kategorie);
        var category = _dbContext.CiselnikKategoriiZaznamu.AsNoTracking()
            .Where(x => x.Id == categoryId)
            .Select(x => new { x.Kod, x.Nazev })
            .FirstOrDefault();
        var isTaskCategory = IsTaskCategory(category?.Kod, category?.Nazev);
        var statusId = ResolveStavUkoluId(command.Stav);
        var typeId = ResolveTypUkoluId(command.TypUkolu);
        var subsystemId = ResolveSubsystemId(command.Subsystem);
        var activeSchema = GetActiveHarmonogramSchema();
        var defaultSchemaVersion = activeSchema.Verze > 0 ? activeSchema.Verze : 1;
        var ownerId = _dbContext.Osoby.AsNoTracking()
            .Where(x => x.Id == ownerIdValue)
            .Select(x => (int?)x.Id)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Vlastník s ID '{ownerIdValue}' nebyl nalezen.");
        var project = _dbContext.Projekty.AsNoTracking()
            .Where(x => x.Id == command.ProjektId)
            .Select(x => new
            {
                x.Id,
                x.PouzivatIdentJednani
            })
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Projekt {command.ProjektId} nebyl nalezen.");

        using var tx = _dbContext.Database.BeginTransaction(IsolationLevel.Serializable);

        ProjektovyZaznamEntity entity;
        if (command.Id.HasValue)
        {
            entity = _dbContext.ProjektoveZaznamy.FirstOrDefault(x => x.Id == command.Id.Value)
                ?? throw new InvalidOperationException($"Záznam {command.Id.Value} nebyl nalezen.");

            var oldOwner = entity.VlastnikId;
            var oldDate = entity.DatumUkonceni;
            var oldSubsystem = entity.SubsystemId;
            var oldType = entity.AktualniTypUkoluId;

            entity.KategorieId = categoryId;
            entity.StavUkoluId = statusId;
            entity.AktualniTypUkoluId = typeId;
            entity.Nazev = command.Nazev.Trim();
            entity.Popis = string.IsNullOrWhiteSpace(command.Popis) ? null : command.Popis.Trim();
            entity.VlastnikId = ownerId;
            entity.DatumUkonceni = command.TerminUkonceni.Date;
            entity.SubsystemId = subsystemId;
            if (string.IsNullOrWhiteSpace(entity.CisloViditelne))
            {
                entity.CisloViditelne = entity.CisloZaznamu.ToString(CultureInfo.InvariantCulture);
                entity.CisloViditelneTyp = RecordDisplayNumberTypeIncrement;
                entity.CisloViditelneA = entity.CisloZaznamu;
                entity.CisloViditelneB = 0;
                entity.CisloJednaniZdrojId = null;
            }
            if (entity.HarmonogramSablonaVerze <= 0)
            {
                entity.HarmonogramSablonaVerze = defaultSchemaVersion;
            }

            _dbContext.SaveChanges();

            if (oldOwner != entity.VlastnikId)
            {
                _dbContext.ZaznamHistorieVlastnik.Add(new ZaznamHistorieVlastnikEntity
                {
                    ZaznamId = entity.Id,
                    PuvodniVlastnik = oldOwner,
                    NovyVlastnik = entity.VlastnikId,
                    DatumZmeny = DateTime.Now
                });
            }

            if (oldDate.Date != entity.DatumUkonceni.Date)
            {
                _dbContext.ZaznamHistorieTerminu.Add(new ZaznamHistorieTerminuEntity
                {
                    ZaznamId = entity.Id,
                    PuvodniDatum = oldDate.Date,
                    NoveDatum = entity.DatumUkonceni.Date,
                    DatumZmeny = DateTime.Now,
                    Duvod = "Úprava záznamu"
                });
            }

            if (oldSubsystem != entity.SubsystemId)
            {
                _dbContext.ZaznamHistorieSubsystem.Add(new ZaznamHistorieSubsystemEntity
                {
                    ZaznamId = entity.Id,
                    PuvodniSubsystem = oldSubsystem,
                    NovySubsystem = entity.SubsystemId,
                    DatumZmeny = DateTime.Now
                });
            }

            if (oldType != entity.AktualniTypUkoluId && oldType.HasValue && entity.AktualniTypUkoluId.HasValue)
            {
                _dbContext.ZaznamHistorieZmenTypu.Add(new ZaznamHistorieZmenTypuEntity
                {
                    ZaznamId = entity.Id,
                    PuvodniTypId = oldType.Value,
                    NovyTypId = entity.AktualniTypUkoluId.Value,
                    DatumZmeny = DateTime.Now,
                    ZmenilOsobaId = currentUser.OsobaId
                });
            }
        }
        else
        {
            var requestedCislo = command.CisloZaznamu > 0 ? command.CisloZaznamu : 0;
            var cislo = requestedCislo;
            if (cislo <= 0
                || _dbContext.ProjektoveZaznamy.Any(x => x.ProjektId == command.ProjektId && x.CisloZaznamu == cislo))
            {
                cislo = GetNextCisloZaznamuTransactional(command.ProjektId);
            }

            var cisloViditelneTyp = RecordDisplayNumberTypeIncrement;
            var cisloViditelneA = cislo;
            var cisloViditelneB = 0;
            int? cisloJednaniZdrojId = null;
            var cisloViditelne = cislo.ToString(CultureInfo.InvariantCulture);

            if (project.PouzivatIdentJednani)
            {
                var meetingRows = _dbContext.Jednani.AsNoTracking()
                    .Where(x => x.ProjektId == command.ProjektId)
                    .ToList();
                var meetingStateById = _dbContext.CiselnikStavuJednani.AsNoTracking()
                    .ToDictionary(x => x.Id);
                var openMeetingIds = meetingRows
                    .Where(x => !IsMeetingReadOnly(x, meetingStateById.GetValueOrDefault(x.StavJednaniId)))
                    .Select(x => x.Id)
                    .ToHashSet();
                if (openMeetingIds.Count == 0)
                {
                    throw new InvalidOperationException("Není dostupné žádné neuzavřené jednání.");
                }

                if (!command.JednaniIdProCislo.HasValue || command.JednaniIdProCislo.Value <= 0)
                {
                    throw new InvalidOperationException("Pro tento režim vyberte jednání.");
                }

                var meeting = meetingRows
                    .FirstOrDefault(x => x.Id == command.JednaniIdProCislo.Value)
                    ?? throw new InvalidOperationException("Vybrané jednání neexistuje.");
                if (!openMeetingIds.Contains(meeting.Id))
                {
                    throw new InvalidOperationException("Vybrané jednání je uzavřené. Vyberte neuzavřené jednání.");
                }

                var nextOrder = AllocateMeetingOrderTransactional(command.ProjektId, meeting.CisloJednani);
                cisloViditelneTyp = RecordDisplayNumberTypeMeeting;
                cisloViditelneA = meeting.CisloJednani;
                cisloViditelneB = nextOrder;
                cisloJednaniZdrojId = meeting.Id;
                cisloViditelne = $"{meeting.CisloJednani}-{nextOrder}";
            }

            entity = new ProjektovyZaznamEntity
            {
                ProjektId = command.ProjektId,
                KategorieId = categoryId,
                StavUkoluId = statusId,
                AktualniTypUkoluId = typeId,
                CisloZaznamu = cislo,
                CisloViditelne = cisloViditelne,
                CisloViditelneTyp = cisloViditelneTyp,
                CisloViditelneA = cisloViditelneA,
                CisloViditelneB = cisloViditelneB,
                CisloJednaniZdrojId = cisloJednaniZdrojId,
                Nazev = command.Nazev.Trim(),
                Popis = string.IsNullOrWhiteSpace(command.Popis) ? null : command.Popis.Trim(),
                VlastnikId = ownerId,
                DatumZalozeni = command.DatumZalozeni.Date,
                DatumUkonceni = command.TerminUkonceni.Date,
                SubsystemId = subsystemId,
                HarmonogramSablonaVerze = defaultSchemaVersion
            };
            _dbContext.ProjektoveZaznamy.Add(entity);
        }

        _dbContext.SaveChanges();
        ReplaceRecordCollaboration(entity.Id, command.VybraniSpolupracovniciIds);
        ReplaceRecordExternalLinks(entity.Id, command.ExterniVazby);
        List<SaveRecordHarmonogramValueCommand>? normalizedScheduleValues = null;
        if (isTaskCategory && command.HarmonogramHodnoty.Count > 0)
        {
            var scheduleSchema = GetSchemaForRecord(entity);
            normalizedScheduleValues = ReplaceRecordScheduleValues(entity.Id, command.HarmonogramHodnoty, scheduleSchema.Kroky);
        }
        _dbContext.SaveChanges();
        tx.Commit();

        WriteAudit(currentUser.OsobaId, "projektove_zaznamy", entity.Id.ToString(CultureInfo.InvariantCulture), command.Id.HasValue ? "update" : "create", null, JsonSerializer.Serialize(entity));
        if (normalizedScheduleValues is not null)
        {
            WriteAudit(
                currentUser.OsobaId,
                "zaznam_harmonogram_hodnoty",
                entity.Id.ToString(CultureInfo.InvariantCulture),
                "upsert",
                null,
                JsonSerializer.Serialize(normalizedScheduleValues));
        }

        return entity.Id;
    }

    public void AssignMeetingIdentifier(AssignMeetingIdentifierCommand command, CurrentUserContextViewModel currentUser)
    {
        using var tx = _dbContext.Database.BeginTransaction(IsolationLevel.Serializable);

        var record = _dbContext.ProjektoveZaznamy
            .FromSqlRaw("SELECT * FROM projektove_zaznamy WITH (UPDLOCK, HOLDLOCK) WHERE id = {0}", command.ZaznamId)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Záznam {command.ZaznamId} nebyl nalezen.");
        if (record.ProjektId != command.ProjektId)
        {
            throw new InvalidOperationException("Záznam nepatří do vybraného projektu.");
        }

        if (record.CisloViditelneTyp == RecordDisplayNumberTypeMeeting)
        {
            throw new InvalidOperationException("Záznam už má identifikátor podle jednání.");
        }

        var meetingRows = _dbContext.Jednani.AsNoTracking()
            .Where(x => x.ProjektId == command.ProjektId)
            .ToList();
        var meetingStateById = _dbContext.CiselnikStavuJednani.AsNoTracking()
            .ToDictionary(x => x.Id);
        var openMeetingIds = meetingRows
            .Where(x => !IsMeetingReadOnly(x, meetingStateById.GetValueOrDefault(x.StavJednaniId)))
            .Select(x => x.Id)
            .ToHashSet();
        if (openMeetingIds.Count == 0)
        {
            throw new InvalidOperationException("Není dostupné žádné neuzavřené jednání.");
        }

        var meeting = meetingRows
            .FirstOrDefault(x => x.Id == command.JednaniId)
            ?? throw new InvalidOperationException("Vybrané jednání neexistuje.");
        if (!openMeetingIds.Contains(meeting.Id))
        {
            throw new InvalidOperationException("Vybrané jednání je uzavřené. Vyberte neuzavřené jednání.");
        }

        var nextOrder = AllocateMeetingOrderTransactional(command.ProjektId, meeting.CisloJednani);
        var old = JsonSerializer.Serialize(record);
        record.CisloViditelneTyp = RecordDisplayNumberTypeMeeting;
        record.CisloViditelneA = meeting.CisloJednani;
        record.CisloViditelneB = nextOrder;
        record.CisloJednaniZdrojId = meeting.Id;
        record.CisloViditelne = $"{meeting.CisloJednani}-{nextOrder}";
        _dbContext.SaveChanges();
        tx.Commit();

        WriteAudit(
            currentUser.OsobaId,
            "projektove_zaznamy",
            record.Id.ToString(CultureInfo.InvariantCulture),
            "assign_meeting_identifier",
            old,
            JsonSerializer.Serialize(record));
    }

    public void AddComment(AddCommentCommand command, CurrentUserContextViewModel currentUser)
    {
        var normalizedText = NormalizeCommentText(command.Text);
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            throw new InvalidOperationException("Vyjádření nesmí být prázdné.");
        }

        var record = _dbContext.ProjektoveZaznamy
            .AsNoTracking()
            .FirstOrDefault(x => x.Id == command.ZaznamId)
            ?? throw new InvalidOperationException($"Záznam {command.ZaznamId} neexistuje.");

        var meeting = _dbContext.Jednani
            .AsNoTracking()
            .FirstOrDefault(x => x.Id == command.JednaniId);
        if (meeting is null)
        {
            throw new InvalidOperationException($"Jednání {command.JednaniId} neexistuje.");
        }

        if (meeting.ProjektId != record.ProjektId)
        {
            throw new InvalidOperationException("Vybrané jednání nepatří k tomuto záznamu.");
        }

        EnsureMeetingAllowsCommentChanges(meeting);

        var subsystemLeaderId = ResolveSubsystemLeaderId(record.SubsystemId);
        var canAdd = _commentAuthorizationPolicy.CanAddComment(currentUser, record.ProjektId, subsystemLeaderId);
        if (!canAdd)
        {
            throw new InvalidOperationException("Nemáte oprávnění přidat vyjádření k tomuto záznamu.");
        }

        var note = new VyjadreniEntity
        {
            ZaznamId = command.ZaznamId,
            JednaniId = command.JednaniId,
            AutorOsobaId = currentUser.OsobaId,
            TextVyjadreni = normalizedText,
            DatumVyjadreni = DateTime.Now
        };

        _dbContext.Vyjadreni.Add(note);
        _dbContext.SaveChanges();
        WriteAudit(currentUser.OsobaId, "vyjadreni", note.Id.ToString(CultureInfo.InvariantCulture), "create", null, JsonSerializer.Serialize(note));
    }

    public void UpdateComment(UpdateCommentCommand command, CurrentUserContextViewModel currentUser)
    {
        var normalizedText = NormalizeCommentText(command.Text);
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            throw new InvalidOperationException("Vyjádření nesmí být prázdné.");
        }

        var comment = _dbContext.Vyjadreni.FirstOrDefault(x => x.Id == command.Id)
            ?? throw new InvalidOperationException($"Vyjádření {command.Id} nebylo nalezeno.");

        var meeting = _dbContext.Jednani
            .AsNoTracking()
            .FirstOrDefault(x => x.Id == comment.JednaniId)
            ?? throw new InvalidOperationException($"Jednání {comment.JednaniId} neexistuje.");

        var record = _dbContext.ProjektoveZaznamy
            .AsNoTracking()
            .FirstOrDefault(x => x.Id == comment.ZaznamId)
            ?? throw new InvalidOperationException($"Záznam {comment.ZaznamId} neexistuje.");

        if (meeting.ProjektId != record.ProjektId)
        {
            throw new InvalidOperationException("Vyjádření je navázáno na neplatnou kombinaci záznamu a jednání.");
        }

        EnsureMeetingAllowsCommentChanges(meeting);

        if (!CanModifyComment(comment, record, currentUser))
        {
            throw new InvalidOperationException("Nemáte oprávnění upravit toto vyjádření.");
        }

        var old = JsonSerializer.Serialize(comment);
        comment.TextVyjadreni = normalizedText;
        comment.DatumVyjadreni = DateTime.Now;
        _dbContext.SaveChanges();
        WriteAudit(currentUser.OsobaId, "vyjadreni", comment.Id.ToString(CultureInfo.InvariantCulture), "update", old, JsonSerializer.Serialize(comment));
    }

    private static string NormalizeCommentText(string? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        return value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
    }

    public void DeleteComment(DeleteCommentCommand command, CurrentUserContextViewModel currentUser)
    {
        var comment = _dbContext.Vyjadreni.FirstOrDefault(x => x.Id == command.Id)
            ?? throw new InvalidOperationException($"Vyjádření {command.Id} nebylo nalezeno.");

        var meeting = _dbContext.Jednani
            .AsNoTracking()
            .FirstOrDefault(x => x.Id == comment.JednaniId)
            ?? throw new InvalidOperationException($"Jednání {comment.JednaniId} neexistuje.");

        var record = _dbContext.ProjektoveZaznamy
            .AsNoTracking()
            .FirstOrDefault(x => x.Id == comment.ZaznamId)
            ?? throw new InvalidOperationException($"Záznam {comment.ZaznamId} neexistuje.");

        if (meeting.ProjektId != record.ProjektId)
        {
            throw new InvalidOperationException("Vyjádření je navázáno na neplatnou kombinaci záznamu a jednání.");
        }

        EnsureMeetingAllowsCommentChanges(meeting);

        if (!CanModifyComment(comment, record, currentUser))
        {
            throw new InvalidOperationException("Nemáte oprávnění smazat toto vyjádření.");
        }

        var old = JsonSerializer.Serialize(comment);
        _dbContext.Vyjadreni.Remove(comment);
        _dbContext.SaveChanges();
        WriteAudit(currentUser.OsobaId, "vyjadreni", comment.Id.ToString(CultureInfo.InvariantCulture), "delete", old, null);
    }

    public int SaveMeeting(SaveMeetingCommand command, CurrentUserContextViewModel currentUser)
    {
        var duplicateMeetingNumberExists = _dbContext.Jednani
            .AsNoTracking()
            .Any(x => x.ProjektId == command.ProjektId
                      && x.CisloJednani == command.CisloJednani
                      && (!command.Id.HasValue || x.Id != command.Id.Value));
        if (duplicateMeetingNumberExists)
        {
            throw new InvalidOperationException($"Jednání č. {command.CisloJednani} už v tomto projektu existuje.");
        }

        var statusId = ResolveMeetingStatusId(command.StavJednani);
        if (command.Id.HasValue)
        {
            var existing = _dbContext.Jednani.FirstOrDefault(x => x.Id == command.Id.Value)
                ?? throw new InvalidOperationException($"Jednání {command.Id.Value} nebylo nalezeno.");

            existing.CisloJednani = command.CisloJednani;
            existing.DatumPlanovane = command.DatumPlanovane.Date;
            existing.CasZacatek = command.CasZacatek;
            existing.Misto = command.Misto?.Trim();
            existing.StavJednaniId = statusId;
            _dbContext.SaveChanges();
            WriteAudit(currentUser.OsobaId, "jednani", existing.Id.ToString(CultureInfo.InvariantCulture), "update", null, JsonSerializer.Serialize(existing));
            return existing.Id;
        }

        var created = new JednaniEntity
        {
            ProjektId = command.ProjektId,
            CisloJednani = command.CisloJednani,
            DatumPlanovane = command.DatumPlanovane.Date,
            CasZacatek = command.CasZacatek,
            Misto = command.Misto?.Trim(),
            StavJednaniId = statusId,
            UzamklOsobaId = null
        };
        _dbContext.Jednani.Add(created);
        _dbContext.SaveChanges();
        WriteAudit(currentUser.OsobaId, "jednani", created.Id.ToString(CultureInfo.InvariantCulture), "create", null, JsonSerializer.Serialize(created));
        return created.Id;
    }

    public void DeleteMeeting(DeleteMeetingCommand command, CurrentUserContextViewModel currentUser)
    {
        var meeting = _dbContext.Jednani.FirstOrDefault(x => x.Id == command.JednaniId)
            ?? throw new InvalidOperationException($"Jednání {command.JednaniId} nebylo nalezeno.");

        if (meeting.ProjektId != command.ProjektId)
        {
            throw new InvalidOperationException("Jednání nepatří do zvoleného projektu.");
        }

        var attendanceRows = _dbContext.Ucast
            .Where(x => x.JednaniId == command.JednaniId)
            .ToList();
        var commentRows = _dbContext.Vyjadreni
            .Where(x => x.JednaniId == command.JednaniId)
            .ToList();

        var oldMeeting = JsonSerializer.Serialize(meeting);
        if (attendanceRows.Count > 0)
        {
            _dbContext.Ucast.RemoveRange(attendanceRows);
        }

        if (commentRows.Count > 0)
        {
            _dbContext.Vyjadreni.RemoveRange(commentRows);
        }

        if (attendanceRows.Count > 0 || commentRows.Count > 0)
        {
            // Persist dependent row deletion first to avoid FK ordering issues in providers
            // where relationships are not fully modeled in EF metadata.
            _dbContext.SaveChanges();
        }

        _dbContext.Jednani.Remove(meeting);
        _dbContext.SaveChanges();

        var meta = JsonSerializer.Serialize(new
        {
            Meeting = meeting,
            DeletedAttendance = attendanceRows.Count,
            DeletedComments = commentRows.Count
        });
        WriteAudit(currentUser.OsobaId, "jednani", command.JednaniId.ToString(CultureInfo.InvariantCulture), "delete", oldMeeting, meta);
    }

    public void SaveMeetingStatus(SaveMeetingStatusCommand command, CurrentUserContextViewModel currentUser)
    {
        var meeting = _dbContext.Jednani.FirstOrDefault(x => x.Id == command.JednaniId)
            ?? throw new InvalidOperationException($"Jednání {command.JednaniId} nebylo nalezeno.");

        var statusId = ResolveMeetingStatusId(command.Stav);
        meeting.StavJednaniId = statusId;
        if (command.UzavritJednani)
        {
            meeting.UzamklOsobaId = currentUser.OsobaId;
        }
        else if (command.OtevritJednani)
        {
            meeting.UzamklOsobaId = null;
        }

        _dbContext.SaveChanges();
        WriteAudit(currentUser.OsobaId, "jednani", meeting.Id.ToString(CultureInfo.InvariantCulture), "status_update", null, JsonSerializer.Serialize(meeting));
    }

    public void SaveMeetingNote(SaveMeetingNoteCommand command, CurrentUserContextViewModel currentUser)
    {
        AddComment(new AddCommentCommand
        {
            JednaniId = command.JednaniId,
            ZaznamId = command.ZaznamId,
            Text = command.Text
        }, currentUser);
    }

    public void SaveAttendance(SaveAttendanceCommand command, CurrentUserContextViewModel currentUser)
    {
        var stateId = ResolveAttendanceStatusId(command.StavUcasti);
        var entity = _dbContext.Ucast.FirstOrDefault(x => x.JednaniId == command.JednaniId && x.OsobaId == command.OsobaId);
        if (entity is null)
        {
            entity = new UcastEntity
            {
                JednaniId = command.JednaniId,
                OsobaId = command.OsobaId,
                StavUcastiId = stateId
            };
            _dbContext.Ucast.Add(entity);
        }
        else
        {
            entity.StavUcastiId = stateId;
        }

        _dbContext.SaveChanges();
        WriteAudit(currentUser.OsobaId, "ucast", $"{command.JednaniId}:{command.OsobaId}", "upsert", null, JsonSerializer.Serialize(entity));
    }

    public void SaveTeamMember(SaveTeamMemberCommand command, CurrentUserContextViewModel currentUser)
    {
        if (!command.OsobaId.HasValue || command.OsobaId.Value <= 0)
        {
            throw new InvalidOperationException("Vyberte osobu z nabídky.");
        }

        var osobaId = command.OsobaId.Value;
        var roleId = ResolveProjectRoleId(command.Role)
            ?? throw new InvalidOperationException($"Role '{command.Role}' nebyla nalezena.");

        var existing = _dbContext.ObsazeniProjektu
            .FirstOrDefault(x => x.ProjektId == command.ProjektId && x.OsobaId == osobaId);
        if (existing is null)
        {
            _dbContext.ObsazeniProjektu.Add(new ObsazeniProjektuEntity
            {
                ProjektId = command.ProjektId,
                OsobaId = osobaId,
                RoleId = roleId
            });
        }
        else
        {
            existing.RoleId = roleId;
        }

        _dbContext.SaveChanges();
        WriteAudit(currentUser.OsobaId, "obsazeni_projektu", $"{command.ProjektId}:{osobaId}", "upsert", null, JsonSerializer.Serialize(command));
    }

    public void RemoveTeamMember(RemoveTeamMemberCommand command, CurrentUserContextViewModel currentUser)
    {
        var rows = _dbContext.ObsazeniProjektu
            .Where(x => x.ProjektId == command.ProjektId && x.OsobaId == command.OsobaId)
            .ToList();
        if (rows.Count == 0)
        {
            return;
        }

        _dbContext.ObsazeniProjektu.RemoveRange(rows);
        _dbContext.SaveChanges();
        WriteAudit(currentUser.OsobaId, "obsazeni_projektu", $"{command.ProjektId}:{command.OsobaId}", "delete", null, null);
    }

    public int SaveManualPerson(SaveManualPersonCommand command, CurrentUserContextViewModel currentUser)
    {
        var organizationId = ResolveOrganizationId(command.Organizace);
        var orgUnitId = ResolveOrgUnitId(command.OrganizacniCelek);
        var email = _textNormalizer.NormalizeEmail(command.Email);

        if (command.Id.HasValue)
        {
            var existing = _dbContext.Osoby.FirstOrDefault(x => x.Id == command.Id.Value)
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

            _dbContext.SaveChanges();
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
        _dbContext.Osoby.Add(entity);
        _dbContext.SaveChanges();
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
        var email = _textNormalizer.NormalizeEmail(command.Email)
            ?? throw new InvalidOperationException("AD osoba musí mít vyplněný email.");
        var existing = _dbContext.Osoby.FirstOrDefault(x => x.GuidAd == guidAd);
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
            _dbContext.SaveChanges();
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
        _dbContext.Osoby.Add(entity);
        _dbContext.SaveChanges();
        WriteAudit(currentUser.OsobaId, "osoby", entity.Id.ToString(CultureInfo.InvariantCulture), "create_ad", null, JsonSerializer.Serialize(entity));
        return entity.Id;
    }

    public void DeletePerson(DeletePersonCommand command, CurrentUserContextViewModel currentUser)
    {
        var person = _dbContext.Osoby.FirstOrDefault(x => x.Id == command.Id)
            ?? throw new InvalidOperationException("Osoba nebyla nalezena.");

        var hasProjectAssignments = _dbContext.ObsazeniProjektu.AsNoTracking().Any(x => x.OsobaId == person.Id);
        var hasOwnedRecords = _dbContext.ProjektoveZaznamy.AsNoTracking().Any(x => x.VlastnikId == person.Id);
        var hasCooperation = _dbContext.ZaznamSpoluprace.AsNoTracking().Any(x => x.OsobaId == person.Id);
        var hasComments = _dbContext.Vyjadreni.AsNoTracking().Any(x => x.AutorOsobaId == person.Id);
        var hasMeetingLocks = _dbContext.Jednani.AsNoTracking().Any(x => x.UzamklOsobaId == person.Id);
        var hasAttendance = _dbContext.Ucast.AsNoTracking().Any(x => x.OsobaId == person.Id);
        var hasSubsystemOwnership = _dbContext.Subsystemy.AsNoTracking().Any(x => x.VedouciOsobaId == person.Id);

        if (hasProjectAssignments
            || hasOwnedRecords
            || hasCooperation
            || hasComments
            || hasMeetingLocks
            || hasAttendance
            || hasSubsystemOwnership)
        {
            throw new InvalidOperationException("Osobu nelze odstranit, protože je navázaná na projektová data. Nejprve odeberte vazby.");
        }

        var old = JsonSerializer.Serialize(person);

        var userRoles = _dbContext.AuthzUserRoles.Where(x => x.OsobaId == person.Id).ToList();
        if (userRoles.Count > 0)
        {
            _dbContext.AuthzUserRoles.RemoveRange(userRoles);
        }

        var superadminRows = _dbContext.AuthzSuperadmins.Where(x => x.OsobaId == person.Id).ToList();
        if (superadminRows.Count > 0)
        {
            _dbContext.AuthzSuperadmins.RemoveRange(superadminRows);
        }

        var auditRows = _dbContext.AuthzAuditLog.Where(x => x.ActorOsobaId == person.Id).ToList();
        foreach (var auditRow in auditRows)
        {
            auditRow.ActorOsobaId = null;
        }

        _dbContext.Osoby.Remove(person);
        _dbContext.SaveChanges();
        WriteAudit(currentUser.OsobaId, "osoby", command.Id.ToString(CultureInfo.InvariantCulture), "delete", old, null);
    }

    private IReadOnlyDictionary<string, Action<SaveCiselnikRowCommand>> BuildCiselnikSaveHandlers()
    {
        return new Dictionary<string, Action<SaveCiselnikRowCommand>>(StringComparer.OrdinalIgnoreCase)
        {
            ["stavy-projektu"] = SaveProjectStatusRow,
            ["stavy-ukolu"] = SaveTaskStatusRow,
            ["kategorie-zaznamu"] = SaveKategorieRow,
            ["typy-ukolu"] = SaveTypUkoluRow,
            ["typy-externich-odkazu"] = SaveTypExternihoOdkazuRow,
            ["role-projektu"] = SaveRoleProjektuRow,
            ["stavy-ucasti"] = SaveStavUcastiRow,
            ["organizace"] = SaveOrganizaceRow,
            ["organizacni-celky"] = SaveOrgUnitRow,
            ["subsystemy"] = SaveSubsystemRow,
            [HarmonogramKrokyCiselnikKey] = SaveHarmonogramStepRow,
            ["stavy-jednani"] = SaveStavJednaniRow,
            ["vyzvy"] = SaveVyzvaRow
        };
    }

    private IReadOnlyDictionary<string, Action<DeleteCiselnikRowCommand>> BuildCiselnikDeleteHandlers()
    {
        return new Dictionary<string, Action<DeleteCiselnikRowCommand>>(StringComparer.OrdinalIgnoreCase)
        {
            ["stavy-projektu"] = command => RemoveCiselnikRow(_dbContext.CiselnikStavuProjektu, command.Id, "stavy-projektu"),
            ["stavy-ukolu"] = command => RemoveCiselnikRow(_dbContext.CiselnikStavuUkolu, command.Id, "stavy-ukolu"),
            ["kategorie-zaznamu"] = command => RemoveCiselnikRow(_dbContext.CiselnikKategoriiZaznamu, command.Id, "kategorie-zaznamu"),
            ["typy-ukolu"] = command => RemoveCiselnikRow(_dbContext.CiselnikTypuUkolu, command.Id, "typy-ukolu"),
            ["typy-externich-odkazu"] = command => RemoveCiselnikRow(_dbContext.CiselnikTypuExternichOdkazu, command.Id, "typy-externich-odkazu"),
            ["role-projektu"] = command => RemoveCiselnikRow(_dbContext.CiselnikRoliProjektu, command.Id, "role-projektu"),
            ["stavy-ucasti"] = command => RemoveCiselnikRow(_dbContext.CiselnikStavuUcasti, command.Id, "stavy-ucasti"),
            ["organizace"] = command => RemoveCiselnikRow(_dbContext.CiselnikOrganizace, command.Id, "organizace"),
            ["organizacni-celky"] = command => RemoveCiselnikRow(_dbContext.CiselnikOrganizacniCelky, command.Id, "organizacni-celky"),
            ["subsystemy"] = command => RemoveCiselnikRow(_dbContext.Subsystemy, command.Id, "subsystemy"),
            [HarmonogramKrokyCiselnikKey] = DeleteHarmonogramStepRow,
            ["stavy-jednani"] = command => RemoveCiselnikRow(_dbContext.CiselnikStavuJednani, command.Id, "stavy-jednani"),
            ["vyzvy"] = command => RemoveCiselnikRow(_dbContext.CiselnikVyzvy, command.Id, "vyzvy")
        };
    }

    public void SaveCiselnikRow(SaveCiselnikRowCommand command, CurrentUserContextViewModel currentUser)
    {
        var key = (command.Key ?? string.Empty).Trim().ToLowerInvariant();
        if (Ci.Equals(key, HarmonogramKrokyCiselnikKey) && !currentUser.IsSuperAdmin)
        {
            throw new InvalidOperationException("Číselník harmonogramu může upravovat pouze superadmin.");
        }

        if (!_ciselnikSaveHandlers.TryGetValue(key, out var handler))
        {
            throw new InvalidOperationException($"Neznámý číselník '{command.Key}'.");
        }

        NormalizeDictionaryLockState(key, command, currentUser);
        handler(command);

        _dbContext.SaveChanges();
        WriteAudit(currentUser.OsobaId, $"ciselnik:{command.Key}", command.Id?.ToString(CultureInfo.InvariantCulture) ?? "new", "upsert", null, JsonSerializer.Serialize(command));
    }

    public void DeleteCiselnikRow(DeleteCiselnikRowCommand command, CurrentUserContextViewModel currentUser)
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

        var detail = BuildCiselnikDetail(key, currentUser);
        var rowForAudit = detail.Polozky.FirstOrDefault(x => x.Id == command.Id)
            ?? throw new InvalidOperationException($"Položka {command.Id} v číselníku '{command.Key}' nebyla nalezena.");

        if (!_ciselnikDeleteHandlers.TryGetValue(key, out var handler))
        {
            throw new InvalidOperationException($"Neznámý číselník '{command.Key}'.");
        }

        handler(command);

        try
        {
            _dbContext.SaveChanges();
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
            "stavy-projektu" => _dbContext.CiselnikStavuProjektu.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsLocked).FirstOrDefault(),
            "stavy-ukolu" => _dbContext.CiselnikStavuUkolu.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsLocked).FirstOrDefault(),
            "kategorie-zaznamu" => _dbContext.CiselnikKategoriiZaznamu.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsLocked).FirstOrDefault(),
            "typy-ukolu" => _dbContext.CiselnikTypuUkolu.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsLocked).FirstOrDefault(),
            "typy-externich-odkazu" => _dbContext.CiselnikTypuExternichOdkazu.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsLocked).FirstOrDefault(),
            "role-projektu" => _dbContext.CiselnikRoliProjektu.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsLocked).FirstOrDefault(),
            "stavy-ucasti" => _dbContext.CiselnikStavuUcasti.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsLocked).FirstOrDefault(),
            "organizace" => _dbContext.CiselnikOrganizace.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsLocked).FirstOrDefault(),
            "organizacni-celky" => _dbContext.CiselnikOrganizacniCelky.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsLocked).FirstOrDefault(),
            "vyzvy" => _dbContext.CiselnikVyzvy.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsLocked).FirstOrDefault(),
            "stavy-jednani" => _dbContext.CiselnikStavuJednani.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsLocked).FirstOrDefault(),
            HarmonogramKrokyCiselnikKey => true,
            _ => null
        };
    }

    public void SaveUserRoleAssignment(SaveUserRoleAssignmentCommand command, CurrentUserContextViewModel currentUser)
    {
        if (!_dbContext.Osoby.AsNoTracking().Any(x => x.Id == command.OsobaId))
        {
            throw new InvalidOperationException("Vybraná osoba neexistuje.");
        }

        var role = _dbContext.AuthzRoles.FirstOrDefault(x => x.Id == command.RoleId);
        if (role is null)
        {
            throw new InvalidOperationException("Vybraná role neexistuje.");
        }

        var row = _dbContext.AuthzUserRoles.FirstOrDefault(x => x.OsobaId == command.OsobaId && x.RoleId == command.RoleId);
        var oldValue = row is null ? null : JsonSerializer.Serialize(row);
        if (row is null)
        {
            row = new AuthzUserRoleEntity
            {
                OsobaId = command.OsobaId,
                RoleId = command.RoleId,
                IsActive = command.IsActive,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.AuthzUserRoles.Add(row);
        }
        else
        {
            row.IsActive = command.IsActive;
        }

        _dbContext.SaveChanges();
        WriteAudit(currentUser.OsobaId, "authz.user_roles", row.Id.ToString(CultureInfo.InvariantCulture), "upsert", oldValue, JsonSerializer.Serialize(row));
    }

    public void SaveUserRolesForUser(SaveUserRolesForUserCommand command, CurrentUserContextViewModel currentUser)
    {
        if (!_dbContext.Osoby.AsNoTracking().Any(x => x.Id == command.OsobaId))
        {
            throw new InvalidOperationException("Vybraná osoba neexistuje.");
        }

        var requestedRoleIds = (command.RoleIds ?? new List<int>())
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        var roleRows = _dbContext.AuthzRoles.AsNoTracking()
            .Where(x => requestedRoleIds.Contains(x.Id))
            .Select(x => new { x.Id, x.IsActive })
            .ToList();

        if (roleRows.Count != requestedRoleIds.Count)
        {
            throw new InvalidOperationException("Vybraná role neexistuje.");
        }

        if (roleRows.Any(x => !x.IsActive))
        {
            throw new InvalidOperationException("Nelze přiřadit neaktivní roli.");
        }

        var oldRows = _dbContext.AuthzUserRoles.AsNoTracking()
            .Where(x => x.OsobaId == command.OsobaId)
            .OrderBy(x => x.RoleId)
            .ToList();

        var existingRows = _dbContext.AuthzUserRoles
            .Where(x => x.OsobaId == command.OsobaId)
            .ToList();

        var requestedSet = requestedRoleIds.ToHashSet();
        var existingByRoleId = existingRows.ToDictionary(x => x.RoleId);
        foreach (var existing in existingRows)
        {
            existing.IsActive = requestedSet.Contains(existing.RoleId);
        }

        foreach (var roleId in requestedRoleIds)
        {
            if (existingByRoleId.ContainsKey(roleId))
            {
                continue;
            }

            _dbContext.AuthzUserRoles.Add(new AuthzUserRoleEntity
            {
                OsobaId = command.OsobaId,
                RoleId = roleId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        _dbContext.SaveChanges();

        var newRows = _dbContext.AuthzUserRoles.AsNoTracking()
            .Where(x => x.OsobaId == command.OsobaId)
            .OrderBy(x => x.RoleId)
            .ToList();

        WriteAudit(
            currentUser.OsobaId,
            "authz.user_roles",
            command.OsobaId.ToString(CultureInfo.InvariantCulture),
            "replace",
            JsonSerializer.Serialize(oldRows),
            JsonSerializer.Serialize(newRows));
    }

    public void SaveAuthzRole(SaveAuthzRoleCommand command, CurrentUserContextViewModel currentUser)
    {
        var kod = command.Kod.Trim();
        var nazev = command.Nazev.Trim();
        var popis = string.IsNullOrWhiteSpace(command.Popis) ? null : command.Popis.Trim();
        if (string.IsNullOrWhiteSpace(kod) || string.IsNullOrWhiteSpace(nazev))
        {
            throw new InvalidOperationException("Vyplňte kód i název role.");
        }

        var duplicateExists = _dbContext.AuthzRoles.AsNoTracking()
            .Where(x => !command.Id.HasValue || x.Id != command.Id.Value)
            .Select(x => x.Kod)
            .ToList()
            .Any(existingCode => Ci.Equals(existingCode, kod));
        if (duplicateExists)
        {
            throw new InvalidOperationException($"Role s kódem '{kod}' už existuje.");
        }

        AuthzRoleEntity role;
        string action;
        string? oldValue = null;

        if (command.Id is int roleId)
        {
            role = _dbContext.AuthzRoles.FirstOrDefault(x => x.Id == roleId)
                ?? throw new InvalidOperationException("Role nebyla nalezena.");

            if (role.IsSystem)
            {
                throw new InvalidOperationException("Systémovou roli nelze upravit.");
            }

            oldValue = JsonSerializer.Serialize(role);
            role.Kod = kod;
            role.Nazev = nazev;
            role.Popis = popis;
            action = "update";
        }
        else
        {
            role = new AuthzRoleEntity
            {
                Kod = kod,
                Nazev = nazev,
                Popis = popis,
                IsSystem = false,
                IsActive = true
            };
            _dbContext.AuthzRoles.Add(role);
            action = "create";
        }

        _dbContext.SaveChanges();

        WriteAudit(
            currentUser.OsobaId,
            "authz.roles",
            role.Id.ToString(CultureInfo.InvariantCulture),
            action,
            oldValue,
            JsonSerializer.Serialize(role));
    }

    public void ToggleAuthzRole(ToggleAuthzRoleCommand command, CurrentUserContextViewModel currentUser)
    {
        var role = _dbContext.AuthzRoles.FirstOrDefault(x => x.Id == command.Id)
            ?? throw new InvalidOperationException("Role nebyla nalezena.");

        if (role.IsSystem)
        {
            throw new InvalidOperationException("Systémovou roli nelze deaktivovat.");
        }

        var oldValue = JsonSerializer.Serialize(role);
        role.IsActive = command.IsActive;
        _dbContext.SaveChanges();

        WriteAudit(
            currentUser.OsobaId,
            "authz.roles",
            role.Id.ToString(CultureInfo.InvariantCulture),
            command.IsActive ? "activate" : "deactivate",
            oldValue,
            JsonSerializer.Serialize(role));
    }

    public void SaveAuthzPermission(SaveAuthzPermissionCommand command, CurrentUserContextViewModel currentUser)
    {
        var klic = command.Klic.Trim();
        var nazev = command.Nazev.Trim();
        var scopeLevel = (command.ScopeLevel ?? string.Empty).Trim().ToUpperInvariant();
        var categoryId = command.CategoryId;
        if (string.IsNullOrWhiteSpace(klic) || string.IsNullOrWhiteSpace(nazev))
        {
            throw new InvalidOperationException("Vyplňte klíč i název akce.");
        }

        if (!PermissionKeys.IsSupported(klic))
        {
            throw new InvalidOperationException($"Klíč '{klic}' není v seznamu podporovaných akcí. Vyberte klíč z nabídky.");
        }

        var catalogEntry = PermissionKeys.BuildCatalog()
            .FirstOrDefault(x => Ci.Equals(x.Key, klic));
        if (catalogEntry is not null)
        {
            var categoryCode = catalogEntry.CategoryKod.Trim();
            var mappedCategoryId = _dbContext.AuthzPermissionCategories.AsNoTracking()
                .Where(x => x.IsActive && x.Kod == categoryCode)
                .Select(x => (int?)x.Id)
                .FirstOrDefault();

            if (!mappedCategoryId.HasValue)
            {
                throw new InvalidOperationException($"Katalog akcí odkazuje na neexistující kategorii '{categoryCode}'.");
            }

            categoryId = mappedCategoryId.Value;
            scopeLevel = catalogEntry.ScopeLevel;
        }

        if (!Ci.Equals(scopeLevel, "GLOBAL") && !Ci.Equals(scopeLevel, "PROJECT"))
        {
            throw new InvalidOperationException("Neplatný rozsah akce.");
        }

        if (!_dbContext.AuthzPermissionCategories.AsNoTracking().Any(x => x.Id == categoryId && x.IsActive))
        {
            throw new InvalidOperationException("Vybraná kategorie akcí neexistuje.");
        }

        var duplicateExists = _dbContext.AuthzPermissions.AsNoTracking()
            .Where(x => !command.Id.HasValue || x.Id != command.Id.Value)
            .Select(x => x.Klic)
            .ToList()
            .Any(existingKey => Ci.Equals(existingKey, klic));
        if (duplicateExists)
        {
            throw new InvalidOperationException($"Akce s klíčem '{klic}' už existuje.");
        }

        AuthzPermissionEntity permission;
        string action;
        string? oldValue = null;

        if (command.Id is int permissionId)
        {
            permission = _dbContext.AuthzPermissions.FirstOrDefault(x => x.Id == permissionId)
                ?? throw new InvalidOperationException("Akce nebyla nalezena.");

            if (permission.IsSystem)
            {
                throw new InvalidOperationException("Systémovou akci nelze upravit.");
            }

            oldValue = JsonSerializer.Serialize(permission);
            permission.Klic = klic;
            permission.Nazev = nazev;
            permission.CategoryId = categoryId;
            permission.ScopeLevel = scopeLevel;
            action = "update";
        }
        else
        {
            permission = new AuthzPermissionEntity
            {
                Klic = klic,
                Nazev = nazev,
                CategoryId = categoryId,
                ScopeLevel = scopeLevel,
                IsActive = true,
                IsSystem = false
            };
            _dbContext.AuthzPermissions.Add(permission);
            action = "create";
        }

        _dbContext.SaveChanges();

        WriteAudit(
            currentUser.OsobaId,
            "authz.permissions",
            permission.Id.ToString(CultureInfo.InvariantCulture),
            action,
            oldValue,
            JsonSerializer.Serialize(permission));
    }

    public void ToggleAuthzPermission(ToggleAuthzPermissionCommand command, CurrentUserContextViewModel currentUser)
    {
        var permission = _dbContext.AuthzPermissions.FirstOrDefault(x => x.Id == command.Id)
            ?? throw new InvalidOperationException("Akce nebyla nalezena.");

        if (permission.IsSystem)
        {
            throw new InvalidOperationException("Systémovou akci nelze deaktivovat.");
        }

        var oldValue = JsonSerializer.Serialize(permission);
        permission.IsActive = command.IsActive;
        _dbContext.SaveChanges();

        WriteAudit(
            currentUser.OsobaId,
            "authz.permissions",
            permission.Id.ToString(CultureInfo.InvariantCulture),
            command.IsActive ? "activate" : "deactivate",
            oldValue,
            JsonSerializer.Serialize(permission));
    }

    public void SaveRolePermission(SaveRolePermissionCommand command, CurrentUserContextViewModel currentUser)
    {
        var scopeMode = string.Equals(command.ScopeMode, "INCLUDE", StringComparison.OrdinalIgnoreCase)
            ? "INCLUDE"
            : string.Equals(command.ScopeMode, "ALL", StringComparison.OrdinalIgnoreCase)
                ? "ALL"
                : throw new InvalidOperationException("Neplatný rozsah mapování role/akce.");

        if (!_dbContext.AuthzRoles.AsNoTracking().Any(x => x.Id == command.RoleId))
        {
            throw new InvalidOperationException("Vybraná role neexistuje.");
        }

        if (!_dbContext.AuthzPermissions.AsNoTracking().Any(x => x.Id == command.PermissionId))
        {
            throw new InvalidOperationException("Vybraná akce neexistuje.");
        }

        AuthzRolePermissionEntity? row = null;
        if (command.Id.HasValue)
        {
            row = _dbContext.AuthzRolePermissions.FirstOrDefault(x => x.Id == command.Id.Value)
                ?? throw new InvalidOperationException("Mapování role/akce nebylo nalezeno.");
        }
        else
        {
            row = _dbContext.AuthzRolePermissions.FirstOrDefault(x => x.RoleId == command.RoleId && x.PermissionId == command.PermissionId);
        }

        var currentRolePermissionId = row?.Id;
        var duplicateExists = _dbContext.AuthzRolePermissions.AsNoTracking()
            .Any(x => x.RoleId == command.RoleId
                      && x.PermissionId == command.PermissionId
                      && (!currentRolePermissionId.HasValue || x.Id != currentRolePermissionId.Value));
        if (duplicateExists)
        {
            throw new InvalidOperationException("Pro zvolenou roli a akci už mapování existuje.");
        }

        var oldValue = row is null ? null : JsonSerializer.Serialize(row);
        if (row is null)
        {
            row = new AuthzRolePermissionEntity
            {
                RoleId = command.RoleId,
                PermissionId = command.PermissionId,
                ScopeMode = scopeMode,
                IsAllowed = command.IsAllowed
            };
            _dbContext.AuthzRolePermissions.Add(row);
        }
        else
        {
            row.RoleId = command.RoleId;
            row.PermissionId = command.PermissionId;
            row.ScopeMode = scopeMode;
            row.IsAllowed = command.IsAllowed;
        }

        _dbContext.SaveChanges();

        var currentProjects = _dbContext.AuthzRolePermissionProjects.Where(x => x.RolePermissionId == row.Id).ToList();
        _dbContext.AuthzRolePermissionProjects.RemoveRange(currentProjects);
        if (scopeMode == "INCLUDE")
        {
            var projectIds = (command.ProjektIds ?? new List<int>()).Where(x => x > 0).Distinct().ToList();
            var validProjectIds = _dbContext.Projekty.AsNoTracking()
                .Where(x => projectIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToHashSet();
            var missingProjectIds = projectIds.Where(x => !validProjectIds.Contains(x)).ToList();
            if (missingProjectIds.Count > 0)
            {
                throw new InvalidOperationException("Vybrané projekty pro INCLUDE mapování neexistují.");
            }

            foreach (var projectId in projectIds)
            {
                _dbContext.AuthzRolePermissionProjects.Add(new AuthzRolePermissionProjectEntity
                {
                    RolePermissionId = row.Id,
                    ProjektId = projectId
                });
            }
        }

        _dbContext.SaveChanges();
        WriteAudit(currentUser.OsobaId, "authz.role_permissions", row.Id.ToString(CultureInfo.InvariantCulture), "upsert", oldValue, JsonSerializer.Serialize(command));
    }

    private IReadOnlyList<string> BuildUserRoleCodes(int osobaId)
    {
        return _dbContext.AuthzUserRoles.AsNoTracking()
            .Where(x => x.OsobaId == osobaId && x.IsActive)
            .Join(_dbContext.AuthzRoles.AsNoTracking(), ur => ur.RoleId, role => role.Id, (ur, role) => role)
            .Where(role => role.IsActive)
            .Select(role => role.Kod)
            .Distinct()
            .OrderBy(x => x)
            .ToList();
    }

    private IReadOnlyList<PermissionGrantViewModel> BuildUserPermissionGrants(int osobaId)
    {
        var rolePermissions = (
            from ur in _dbContext.AuthzUserRoles.AsNoTracking()
            join rp in _dbContext.AuthzRolePermissions.AsNoTracking() on ur.RoleId equals rp.RoleId
            join p in _dbContext.AuthzPermissions.AsNoTracking() on rp.PermissionId equals p.Id
            where ur.OsobaId == osobaId && ur.IsActive && p.IsActive
            select new
            {
                RolePermissionId = rp.Id,
                p.Klic,
                p.ScopeLevel,
                rp.ScopeMode,
                rp.IsAllowed
            }).ToList();

        var rolePermissionIds = rolePermissions
            .Where(x => Ci.Equals(x.ScopeMode, "INCLUDE"))
            .Select(x => x.RolePermissionId)
            .Distinct()
            .ToList();

        var includeMap = _dbContext.AuthzRolePermissionProjects.AsNoTracking()
            .Where(x => rolePermissionIds.Contains(x.RolePermissionId))
            .GroupBy(x => x.RolePermissionId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<int>)group.Select(item => item.ProjektId).Distinct().ToList());

        return rolePermissions
            .Select(item => new PermissionGrantViewModel
            {
                PermissionKey = item.Klic,
                ScopeLevel = item.ScopeLevel,
                ScopeMode = item.ScopeMode,
                IsAllowed = item.IsAllowed,
                ProjectIds = Ci.Equals(item.ScopeMode, "INCLUDE")
                    ? includeMap.GetValueOrDefault(item.RolePermissionId, Array.Empty<int>())
                    : Array.Empty<int>()
            })
            .ToList();
    }

    private static string ResolveVisibleRecordNumber(ProjektovyZaznamEntity record)
    {
        if (!string.IsNullOrWhiteSpace(record.CisloViditelne))
        {
            return record.CisloViditelne.Trim();
        }

        return record.CisloZaznamu.ToString(CultureInfo.InvariantCulture);
    }

    private static int ResolveVisibleNumberPartA(ProjektovyZaznamEntity record)
    {
        if (record.CisloViditelneA > 0)
        {
            return record.CisloViditelneA;
        }

        return Math.Max(0, record.CisloZaznamu);
    }

    private static int ResolveVisibleNumberPartB(ProjektovyZaznamEntity record)
    {
        if (record.CisloViditelneTyp == RecordDisplayNumberTypeMeeting)
        {
            return Math.Max(1, record.CisloViditelneB);
        }

        return 0;
    }

    private static IEnumerable<ProjektovyZaznamEntity> OrderRecordsByVisibleNumber(IEnumerable<ProjektovyZaznamEntity> rows)
        => rows
            .OrderBy(ResolveVisibleNumberPartA)
            .ThenBy(ResolveVisibleNumberPartB)
            .ThenBy(x => x.CisloZaznamu);

    private List<ZaznamCardViewModel> BuildRecordCardsForProject(int projectId)
    {
        var records = _dbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.ProjektId == projectId)
            .ToList();
        records = OrderRecordsByVisibleNumber(records).ToList();

        var categories = _dbContext.CiselnikKategoriiZaznamu.AsNoTracking().ToDictionary(x => x.Id);
        var taskTypes = _dbContext.CiselnikTypuUkolu.AsNoTracking().ToDictionary(x => x.Id);
        var taskStates = _dbContext.CiselnikStavuUkolu.AsNoTracking().ToDictionary(x => x.Id);
        var subsystems = _dbContext.Subsystemy.AsNoTracking().ToDictionary(x => x.Id);
        var people = _dbContext.Osoby.AsNoTracking().ToDictionary(x => x.Id);

        var ownerHistoryByRecord = _dbContext.ZaznamHistorieVlastnik.AsNoTracking()
            .Where(x => records.Select(r => r.Id).Contains(x.ZaznamId))
            .OrderBy(x => x.DatumZmeny)
            .ToList()
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var termHistoryByRecord = _dbContext.ZaznamHistorieTerminu.AsNoTracking()
            .Where(x => records.Select(r => r.Id).Contains(x.ZaznamId))
            .OrderBy(x => x.DatumZmeny)
            .ToList()
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var subsystemHistoryByRecord = _dbContext.ZaznamHistorieSubsystem.AsNoTracking()
            .Where(x => records.Select(r => r.Id).Contains(x.ZaznamId))
            .OrderBy(x => x.DatumZmeny)
            .ToList()
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var taskTypeHistoryByRecord = _dbContext.ZaznamHistorieZmenTypu.AsNoTracking()
            .Where(x => records.Select(r => r.Id).Contains(x.ZaznamId))
            .OrderBy(x => x.DatumZmeny)
            .ToList()
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var extTypeById = _dbContext.CiselnikTypuExternichOdkazu.AsNoTracking().ToDictionary(x => x.Id);
        var vyzvaById = _dbContext.CiselnikVyzvy.AsNoTracking().ToDictionary(x => x.Id);
        var externalByRecord = _dbContext.ZaznamExterniOdkazy.AsNoTracking()
            .Where(x => records.Select(r => r.Id).Contains(x.ZaznamId))
            .OrderBy(x => x.Id)
            .ToList()
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var organizations = _dbContext.CiselnikOrganizace.AsNoTracking().ToDictionary(x => x.Id);
        var orgUnits = _dbContext.CiselnikOrganizacniCelky.AsNoTracking().ToDictionary(x => x.Id);
        var collaborationByRecord = _dbContext.ZaznamSpoluprace.AsNoTracking()
            .Where(x => records.Select(r => r.Id).Contains(x.ZaznamId))
            .ToList()
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var comments = _dbContext.Vyjadreni.AsNoTracking()
            .Where(x => records.Select(r => r.Id).Contains(x.ZaznamId))
            .OrderBy(x => x.Id)
            .ToList();

        var meetings = _dbContext.Jednani.AsNoTracking().ToDictionary(x => x.Id);
        var meetingStates = _dbContext.CiselnikStavuJednani.AsNoTracking().ToDictionary(x => x.Id);

        var commentByRecord = comments
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => group.ToList());

        return records.Select(record =>
        {
            var ownerHistory = ownerHistoryByRecord.GetValueOrDefault(record.Id, new List<ZaznamHistorieVlastnikEntity>());
            var termHistory = termHistoryByRecord.GetValueOrDefault(record.Id, new List<ZaznamHistorieTerminuEntity>());
            var subsystemHistory = subsystemHistoryByRecord.GetValueOrDefault(record.Id, new List<ZaznamHistorieSubsystemEntity>());
            var taskTypeHistory = taskTypeHistoryByRecord.GetValueOrDefault(record.Id, new List<ZaznamHistorieZmenTypuEntity>());
            var commentsForRecord = commentByRecord.GetValueOrDefault(record.Id, new List<VyjadreniEntity>());
            var category = categories.GetValueOrDefault(record.KategorieId);
            var isTask = IsTaskCategory(category?.Kod, category?.Nazev);

            var commentMeetingStateCodes = commentsForRecord
                .Select(comment =>
                {
                    if (!meetings.TryGetValue(comment.JednaniId, out var meeting))
                    {
                        return null;
                    }

                    return meeting.StavJednaniId.ToString(CultureInfo.InvariantCulture);
                })
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code!.Trim())
                .Distinct(Ci)
                .ToList();

            var vmComments = commentsForRecord.Select(comment =>
            {
                meetings.TryGetValue(comment.JednaniId, out var meeting);
                var meetingState = meeting is null ? null : meetingStates.GetValueOrDefault(meeting.StavJednaniId);
                var author = people.GetValueOrDefault(comment.AutorOsobaId) ?? people.GetValueOrDefault(record.VlastnikId);
                return new VyjadreniViewModel
                {
                    Id = comment.Id,
                    AutorOsobaId = comment.AutorOsobaId,
                    Autor = BuildInlinePersonLabelFromOsoba(author),
                    Datum = comment.DatumVyjadreni,
                    Text = comment.TextVyjadreni,
                    JednaniCislo = meeting?.CisloJednani,
                    JednaniDatum = meeting?.DatumPlanovane,
                    LzeUpravit = !IsMeetingReadOnly(meeting, meetingState)
                };
            })
            .OrderBy(x => x.JednaniCislo ?? int.MaxValue)
            .ThenBy(x => x.Id)
            .ToList();

            var hasPreparationComment = commentsForRecord.Any(comment =>
            {
                if (!meetings.TryGetValue(comment.JednaniId, out var meeting))
                {
                    return false;
                }

                return meetingStates.TryGetValue(meeting.StavJednaniId, out var state)
                    && state.Kod.Equals("DRAFT", StringComparison.OrdinalIgnoreCase);
            });

            var externalLinks = externalByRecord.GetValueOrDefault(record.Id, new List<ZaznamExterniOdkazEntity>())
                .Select(link =>
                {
                    var ticketId = ExtractServiceDeskTicketId(link.Cislo);
                    return new ExterniOdkazViewModel
                    {
                        Typ = extTypeById.GetValueOrDefault(link.TypOdkazuId)?.Kod ?? "-",
                        TypNazev = extTypeById.GetValueOrDefault(link.TypOdkazuId)?.Nazev,
                        Cislo = link.Cislo,
                        ServiceDeskTicketId = ticketId,
                        ServiceDeskUrl = BuildServiceDeskUrl(ticketId),
                        Vyzva = link.Vyzva.HasValue ? vyzvaById.GetValueOrDefault(link.Vyzva.Value)?.Kod : null,
                        DatumObjednani = link.DatumObjednani,
                        DatumPlanDodani = link.PlanDodani,
                        DatumDodani = link.DatumDodani,
                        DatumPrevzeti = link.DatumPrevzeti
                    };
                })
                .ToList();

            var collaboration = collaborationByRecord.GetValueOrDefault(record.Id, new List<ZaznamSpolupraceEntity>())
                .Select(x =>
                {
                    var person = people.GetValueOrDefault(x.OsobaId);
                    return new SpolupracovnikViewModel
                    {
                        OsobaId = x.OsobaId,
                        Osoba = BuildDisplayNameFromOsoba(person),
                        Email = person?.Email?.Trim(),
                        Organizace = person is null ? null : organizations.GetValueOrDefault(person.OrganizaceId)?.Nazev,
                        OrganizacniCelek = person?.OrganizacniCelekId is int cel ? orgUnits.GetValueOrDefault(cel)?.Nazev : null
                    };
                })
                .ToList();

            var currentOwner = people.GetValueOrDefault(record.VlastnikId);
            var currentSubsystem = subsystems.GetValueOrDefault(record.SubsystemId);
            var currentState = record.StavUkoluId.HasValue ? taskStates.GetValueOrDefault(record.StavUkoluId.Value) : null;
            var currentTaskType = record.AktualniTypUkoluId.HasValue ? taskTypes.GetValueOrDefault(record.AktualniTypUkoluId.Value) : null;

            return new ZaznamCardViewModel
            {
                Id = record.Id,
                HarmonogramSablonaVerze = record.HarmonogramSablonaVerze,
                CisloZaznamu = record.CisloZaznamu,
                CisloViditelne = ResolveVisibleRecordNumber(record),
                Nazev = record.Nazev,
                KategorieKod = category?.Kod ?? "-",
                KategorieNazev = category?.Nazev ?? "-",
                TypUkoluKod = currentTaskType?.Kod,
                TypUkolu = currentTaskType?.Nazev,
                StavKod = currentState?.Kod,
                Stav = currentState?.Nazev ?? "-",
                IsAktivniStav = record.StavUkoluId.HasValue
                    ? !(currentState?.IsFinal ?? false)
                    : true,
                JeUkol = isTask,
                VyjadreniJednaniStavyKody = commentMeetingStateCodes,
                Popis = record.Popis ?? string.Empty,
                HistorieVlastniku = ownerHistory.Select(x => BuildInlinePersonLabelFromOsoba(people.GetValueOrDefault(x.PuvodniVlastnik))).Distinct(Ci).ToList(),
                AktualniVlastnik = BuildInlinePersonLabelFromOsoba(currentOwner),
                HistorieTerminu = termHistory.Select(x => x.PuvodniDatum).Distinct().ToList(),
                AktualniTermin = record.DatumUkonceni,
                HistorieSubsystemu = subsystemHistory.Select(x => subsystems.GetValueOrDefault(x.PuvodniSubsystem)?.Nazev ?? "-").Distinct(Ci).ToList(),
                HistorieTypuUkolu = taskTypeHistory
                    .Select(x => taskTypes.GetValueOrDefault(x.PuvodniTypId)?.Nazev)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Cast<string>()
                    .Distinct(Ci)
                    .ToList(),
                AktualniSubsystemKod = string.IsNullOrWhiteSpace(currentSubsystem?.Kod) ? (currentSubsystem?.Nazev ?? string.Empty) : currentSubsystem.Kod,
                AktualniSubsystem = currentSubsystem?.Nazev ?? "-",
                AktualniSubsystemVedouciOsobaId = currentSubsystem?.VedouciOsobaId ?? 0,
                ExterniOdkazy = externalLinks,
                Spoluprace = collaboration,
                Vyjadreni = vmComments,
                MaVyjadreniProPripravuJednani = hasPreparationComment,
                DatumZalozeni = record.DatumZalozeni,
                AktualniVlastnikId = record.VlastnikId
            };
        }).ToList();
    }

    private List<ProjektHarmonogramUkolViewModel> BuildProjectScheduleRows(IReadOnlyList<ZaznamCardViewModel> records)
    {
        var taskRecords = records
            .Where(x => x.JeUkol)
            .ToList();

        if (taskRecords.Count == 0)
        {
            return new List<ProjektHarmonogramUkolViewModel>();
        }

        var taskRecordIds = taskRecords.Select(x => x.Id).ToList();
        var harmonogramByRecord = _dbContext.ZaznamHarmonogramHodnoty.AsNoTracking()
            .Where(x => taskRecordIds.Contains(x.ZaznamId))
            .ToList()
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(item => item.TypId, item => item.HodnotaInt));
        var schemaCache = new Dictionary<int, HarmonogramSchemaDefinition>();
        var ownerRows = _dbContext.Osoby.AsNoTracking()
            .Where(x => taskRecords.Select(r => r.AktualniVlastnikId).Contains(x.Id))
            .Select(x => new { x.Id, x.Titul, x.Jmeno, x.Prijmeni, x.OrganizacniCelekId })
            .ToList();
        var ownerById = ownerRows.ToDictionary(x => x.Id);
        var ownerOrgIds = ownerRows
            .Where(x => x.OrganizacniCelekId.HasValue)
            .Select(x => x.OrganizacniCelekId!.Value)
            .Distinct()
            .ToList();
        var ownerOrgCodes = ownerOrgIds.Count == 0
            ? new Dictionary<int, string>()
            : _dbContext.CiselnikOrganizacniCelky.AsNoTracking()
                .Where(x => ownerOrgIds.Contains(x.Id))
                .ToDictionary(x => x.Id, x => string.IsNullOrWhiteSpace(x.Kod) ? x.Nazev : x.Kod);

        return taskRecords
            .Select(record =>
            {
                var deadline = (record.AktualniTermin ?? record.DatumZalozeni).Date;
                var schema = GetSchemaForRecord(record.HarmonogramSablonaVerze, schemaCache);
                var harmonogramHodnoty = harmonogramByRecord.GetValueOrDefault(record.Id, new Dictionary<int, int>());
                var vypocet = BuildHarmonogramVypocet(record.DatumZalozeni, schema.Kroky, harmonogramHodnoty);
                var souhrn = BuildHarmonogramSouhrn(vypocet, deadline);
                var owner = ownerById.GetValueOrDefault(record.AktualniVlastnikId);
                var ownerOrgCode = owner?.OrganizacniCelekId is int orgId ? ownerOrgCodes.GetValueOrDefault(orgId) : null;
                var ownerDisplay = owner is null
                    ? record.AktualniVlastnik
                    : BuildDisplayName(owner.Titul, owner.Jmeno, owner.Prijmeni, owner.Id);
                var hasVisualDuration = souhrn.CelkoveTrvaniDni > 0;

                if (!hasVisualDuration)
                {
                    return null;
                }

                return new ProjektHarmonogramUkolViewModel
                {
                    ZaznamId = record.Id,
                    CisloZaznamu = record.CisloZaznamu,
                    CisloViditelne = record.CisloViditelne,
                    Nazev = record.Nazev,
                    KategorieKod = record.KategorieKod,
                    KategorieNazev = record.KategorieNazev,
                    TypUkoluKod = record.TypUkoluKod,
                    TypUkolu = record.TypUkolu,
                    Stav = record.Stav,
                    StavKod = record.StavKod,
                    SubsystemKod = record.AktualniSubsystemKod,
                    Subsystem = record.AktualniSubsystem,
                    Vlastnik = ownerDisplay,
                    VlastnikOrgKod = ownerOrgCode,
                    VlastnikId = record.AktualniVlastnikId,
                    IsAktivniStav = record.IsAktivniStav,
                    DatumZalozeni = record.DatumZalozeni.Date,
                    TerminUkonceni = deadline,
                    BaselineDokonceni = souhrn.BaselineDokonceni,
                    SkutecneDokonceni = souhrn.SkutecneDokonceni,
                    CelkoveTrvaniDni = souhrn.CelkoveTrvaniDni,
                    CelkovaOdchylkaDni = souhrn.CelkovaOdchylkaDni,
                    DelkaDoTerminuDni = Math.Max(0, (deadline - record.DatumZalozeni.Date).Days),
                    Stihame = souhrn.Stihame,
                    PrekroceniDni = souhrn.PrekroceniDni,
                    DelayBarvaHex = schema.DelayBarvaHex,
                    MaVizualniTrvani = hasVisualDuration,
                    Kroky = vypocet
                        .Select(krok => new ProjektHarmonogramKrokViewModel
                        {
                            KrokIndex = krok.KrokIndex,
                            Nazev = krok.Nazev,
                            TrvaniDni = krok.TrvaniDni,
                            OdchylkaDni = krok.ZpozdeniDni,
                            BarvaHex = krok.BarvaHex,
                            PlanStart = krok.PlanStartDatum.Date,
                            PlanEnd = krok.BaselineDatum.Date,
                            RealStart = krok.RealStartDatum.Date,
                            RealEnd = krok.PosunuteDatum.Date
                        })
                        .ToList()
                };
            })
            .Where(x => x is not null)
            .Cast<ProjektHarmonogramUkolViewModel>()
            .ToList();
    }

    private HarmonogramSchemaDefinition GetSchemaForRecord(
        ProjektovyZaznamEntity record,
        IDictionary<int, HarmonogramSchemaDefinition>? cache = null)
        => GetSchemaForRecord(record.HarmonogramSablonaVerze, cache);

    private HarmonogramSchemaDefinition GetSchemaForRecord(
        int schemaVersion,
        IDictionary<int, HarmonogramSchemaDefinition>? cache = null)
    {
        if (schemaVersion <= 0)
        {
            var activeSchema = GetActiveHarmonogramSchema();
            if (cache is not null && activeSchema.Verze > 0 && !cache.ContainsKey(activeSchema.Verze))
            {
                cache[activeSchema.Verze] = activeSchema;
            }

            return activeSchema;
        }

        if (cache is not null && cache.TryGetValue(schemaVersion, out var cached))
        {
            return cached;
        }

        var loaded = LoadHarmonogramSchema(schemaVersion);
        if (cache is not null && loaded.Verze > 0)
        {
            cache[loaded.Verze] = loaded;
        }

        return loaded;
    }

    private HarmonogramSchemaDefinition GetActiveHarmonogramSchema()
    {
        try
        {
            var schema = _dbContext.HarmonogramSablony.AsNoTracking()
                .OrderByDescending(x => x.IsAktivni)
                .ThenByDescending(x => x.Verze)
                .FirstOrDefault();
            if (schema is null)
            {
                return BuildFallbackSchemaDefinition();
            }

            var steps = LoadHarmonogramTypy(schema.Verze);
            if (steps.Count == 0)
            {
                var fallback = BuildFallbackSchemaDefinition(schema.Verze, schema.DelayBarvaHex);
                return fallback;
            }

            return new HarmonogramSchemaDefinition(
                schema.Verze,
                NormalizeHexColor(schema.DelayBarvaHex, DefaultDelayBarvaHex),
                steps);
        }
        catch (Exception ex) when (IsMissingHarmonogramCatalogSchema(ex))
        {
            return BuildFallbackSchemaDefinition();
        }
    }

    private HarmonogramSchemaDefinition LoadHarmonogramSchema(int schemaVersion)
    {
        if (schemaVersion <= 0)
        {
            return GetActiveHarmonogramSchema();
        }

        try
        {
            var schema = _dbContext.HarmonogramSablony.AsNoTracking()
                .FirstOrDefault(x => x.Verze == schemaVersion);
            if (schema is null)
            {
                return GetActiveHarmonogramSchema();
            }

            var steps = LoadHarmonogramTypy(schemaVersion);
            if (steps.Count == 0)
            {
                return BuildFallbackSchemaDefinition(schema.Verze, schema.DelayBarvaHex);
            }

            return new HarmonogramSchemaDefinition(
                schema.Verze,
                NormalizeHexColor(schema.DelayBarvaHex, DefaultDelayBarvaHex),
                steps);
        }
        catch (Exception ex) when (IsMissingHarmonogramCatalogSchema(ex))
        {
            return BuildFallbackSchemaDefinition(schemaVersion);
        }
    }

    private List<HarmonogramTypPar> LoadHarmonogramTypy(int schemaVersion)
    {
        List<HarmonogramTypEntity> typRows;
        try
        {
            typRows = _dbContext.CiselnikHarmonogramTypu.AsNoTracking()
                .Where(x => x.SablonaVerze == schemaVersion)
                .OrderBy(x => x.KrokPoradi)
                .ThenBy(x => x.JeZpozdeni)
                .ThenBy(x => x.Id)
                .ToList();
        }
        catch (Exception ex) when (IsMissingHarmonogramCatalogSchema(ex))
        {
            return new List<HarmonogramTypPar>();
        }

        if (typRows.Count == 0)
        {
            return new List<HarmonogramTypPar>();
        }

        var durationRows = typRows
            .Where(x => !x.JeZpozdeni)
            .OrderBy(x => x.KrokPoradi)
            .ThenBy(x => x.Id)
            .ToList();
        var delayRows = typRows
            .Where(x => x.JeZpozdeni)
            .OrderBy(x => x.KrokPoradi)
            .ThenBy(x => x.Id)
            .ToList();

        var result = new List<HarmonogramTypPar>(durationRows.Count);
        var krokIndex = 1;
        foreach (var duration in durationRows)
        {
            var delayType = delayRows.FirstOrDefault(x => x.KrokKey == duration.KrokKey)
                ?? delayRows.FirstOrDefault(x => x.KrokPoradi == duration.KrokPoradi);

            result.Add(new HarmonogramTypPar(
                krokIndex,
                string.IsNullOrWhiteSpace(duration.Kod) ? $"STEP_{krokIndex:00}" : duration.Kod.Trim(),
                string.IsNullOrWhiteSpace(duration.Nazev) ? $"Krok {krokIndex}" : duration.Nazev.Trim(),
                NormalizeHexColor(duration.BarvaHex, ResolveDefaultStepColor(krokIndex)),
                duration.Id,
                delayType?.Id ?? 0));
            krokIndex += 1;
        }

        return result;
    }

    private static HarmonogramSchemaDefinition BuildFallbackSchemaDefinition(int version = 0, string? delayBarvaHex = null)
    {
        return new HarmonogramSchemaDefinition(
            version,
            NormalizeHexColor(delayBarvaHex, DefaultDelayBarvaHex),
            DefaultHarmonogramKroky
                .Select(step => new HarmonogramTypPar(step.Poradi, step.Kod, step.Nazev, step.BarvaHex, 0, 0))
                .ToList());
    }

    private static string ResolveDefaultStepColor(int stepIndex)
    {
        var matched = DefaultHarmonogramKroky.FirstOrDefault(step => step.Poradi == stepIndex);
        if (matched.Poradi > 0 && !string.IsNullOrWhiteSpace(matched.BarvaHex))
        {
            return matched.BarvaHex;
        }

        return DefaultHarmonogramKroky.Length > 0 ? DefaultHarmonogramKroky[0].BarvaHex : "#94A3B8";
    }

    private static bool IsValidHexColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (normalized.Length != 7 || normalized[0] != '#')
        {
            return false;
        }

        for (var i = 1; i < normalized.Length; i += 1)
        {
            var c = normalized[i];
            var isDigit = c >= '0' && c <= '9';
            var isLowerHex = c >= 'a' && c <= 'f';
            var isUpperHex = c >= 'A' && c <= 'F';
            if (!isDigit && !isLowerHex && !isUpperHex)
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizeHexColor(string? value, string fallback)
    {
        if (!IsValidHexColor(value))
        {
            return fallback.ToUpperInvariant();
        }

        return value!.Trim().ToUpperInvariant();
    }

    private static bool IsMissingHarmonogramCatalogSchema(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is SqlException sqlException
                && (sqlException.Number == 208 || sqlException.Number == 207))
            {
                return true;
            }
        }

        return false;
    }

    private static List<HarmonogramVypocetKroku> BuildHarmonogramVypocet(
        DateTime datumZalozeni,
        IReadOnlyList<HarmonogramTypPar> harmonogramTypy,
        IReadOnlyDictionary<int, int>? harmonogramHodnoty)
    {
        var definitions = (harmonogramTypy.Count == 0
                ? DefaultHarmonogramKroky
                    .Select(step => new HarmonogramTypPar(step.Poradi, step.Kod, step.Nazev, step.BarvaHex, 0, 0))
                    .ToList()
                : harmonogramTypy.OrderBy(x => x.KrokIndex).ToList())
            .Select(type => new ScheduleTimelineStepDefinition
            {
                StepIndex = type.KrokIndex,
                Code = type.Kod,
                Name = type.Nazev,
                ColorHex = NormalizeHexColor(type.BarvaHex, ResolveDefaultStepColor(type.KrokIndex)),
                DurationTypeId = type.TrvaniTypId,
                OffsetTypeId = type.ZpozdeniTypId
            })
            .ToList();
        var computation = ScheduleTimelineCalculator.Compute(datumZalozeni, definitions, harmonogramHodnoty);

        return computation.Steps
            .Select(step => new HarmonogramVypocetKroku(
                step.StepIndex,
                step.Code,
                step.Name,
                step.ColorHex,
                step.DurationTypeId,
                step.OffsetTypeId,
                step.DurationDays,
                step.OffsetDays,
                step.PlanStartDate,
                step.PlanEndDate,
                step.ActualStartDate,
                step.ActualEndDate))
            .ToList();
    }

    private static HarmonogramSouhrnViewModel BuildHarmonogramSouhrn(
        IReadOnlyList<HarmonogramVypocetKroku> kroky,
        DateTime terminUkolu)
    {
        var summary = ScheduleTimelineCalculator.Summarize(
            new ScheduleTimelineComputation
            {
                Steps = kroky
                    .Select(step => new ScheduleTimelineStepResult
                    {
                        StepIndex = step.KrokIndex,
                        Code = step.Kod,
                        Name = step.Nazev,
                        ColorHex = step.BarvaHex,
                        DurationTypeId = step.TrvaniTypId,
                        OffsetTypeId = step.ZpozdeniTypId,
                        DurationDays = step.TrvaniDni,
                        OffsetDays = step.ZpozdeniDni,
                        PlanStartDate = step.PlanStartDatum,
                        PlanEndDate = step.BaselineDatum,
                        ActualStartDate = step.RealStartDatum,
                        ActualEndDate = step.PosunuteDatum
                    })
                    .ToList(),
                TotalDurationDays = kroky.Sum(x => x.TrvaniDni),
                TotalOffsetDays = kroky.Sum(x => x.ZpozdeniDni)
            },
            terminUkolu);

        return new HarmonogramSouhrnViewModel
        {
            BaselineDokonceni = summary.BaselineCompletion,
            SkutecneDokonceni = summary.ActualCompletion,
            TerminUkolu = summary.Deadline,
            CelkoveTrvaniDni = summary.TotalDurationDays,
            CelkovaOdchylkaDni = summary.TotalOffsetDays,
            Stihame = summary.IsOnTrack,
            PrekroceniDni = Math.Max(0, summary.OverrunDays)
        };
    }

    private List<TeamMemberViewModel> BuildTeamMembers(int projectId)
    {
        var assignments = _dbContext.ObsazeniProjektu.AsNoTracking().Where(x => x.ProjektId == projectId).ToList();
        var roles = _dbContext.CiselnikRoliProjektu.AsNoTracking().ToDictionary(x => x.Id);
        var people = _dbContext.Osoby.AsNoTracking().ToDictionary(x => x.Id);
        var organizations = _dbContext.CiselnikOrganizace.AsNoTracking().ToDictionary(x => x.Id);
        var orgUnits = _dbContext.CiselnikOrganizacniCelky.AsNoTracking().ToDictionary(x => x.Id);

        return assignments
            .OrderBy(x => roles.GetValueOrDefault(x.RoleId)?.Nazev ?? string.Empty)
            .ThenBy(x => x.Id)
            .Select(x =>
            {
                var person = people.GetValueOrDefault(x.OsobaId);
                return new TeamMemberViewModel
                {
                    OsobaId = x.OsobaId,
                    Osoba = BuildDisplayNameFromOsoba(person),
                    Email = person?.Email?.Trim(),
                    Role = roles.GetValueOrDefault(x.RoleId)?.Nazev ?? "-",
                    Organizace = person is null ? null : organizations.GetValueOrDefault(person.OrganizaceId)?.Nazev,
                    OrganizacniCelek = person?.OrganizacniCelekId is int cel ? orgUnits.GetValueOrDefault(cel)?.Nazev : null
                };
            })
            .ToList();
    }

    private List<PdfProjectRoleMemberViewModel> BuildProjectLeadershipRoles(int projectId)
    {
        return BuildTeamMembers(projectId)
            .Where(member => !string.IsNullOrWhiteSpace(member.Osoba))
            .Select(member => new PdfProjectRoleMemberViewModel
            {
                Osoba = member.Osoba,
                Role = string.IsNullOrWhiteSpace(member.Role) ? "-" : member.Role
            })
            .OrderBy(member => member.Role, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(member => member.Osoba, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private List<TeamCandidateViewModel> BuildTeamCandidates()
    {
        var organizations = _dbContext.CiselnikOrganizace.AsNoTracking().ToDictionary(x => x.Id);
        var orgUnits = _dbContext.CiselnikOrganizacniCelky.AsNoTracking().ToDictionary(x => x.Id);
        var people = _dbContext.Osoby.AsNoTracking()
            .OrderBy(x => x.Prijmeni)
            .ThenBy(x => x.Jmeno)
            .ToList();

        return people.Select(x => new TeamCandidateViewModel
            {
                Id = x.Id,
                Osoba = BuildDisplayName(x.Titul, x.Jmeno, x.Prijmeni, x.Id),
                Email = x.Email?.Trim(),
                Organizace = organizations.GetValueOrDefault(x.OrganizaceId)?.Nazev,
                OrganizacniCelek = x.OrganizacniCelekId.HasValue ? orgUnits.GetValueOrDefault(x.OrganizacniCelekId.Value)?.Nazev : null
            })
            .ToList();
    }

    private ZaznamEditViewModel BuildZaznamEditForEntity(
        ProjektovyZaznamEntity record,
        bool isCreate,
        int? forceMeetingIdForNumber = null,
        bool? projectUsesMeetingIdentifier = null,
        IReadOnlyList<JednaniOptionViewModel>? openMeetingOptions = null)
    {
        var categories = _dbContext.CiselnikKategoriiZaznamu.AsNoTracking().OrderBy(x => x.Nazev).ToList();
        var taskStates = _dbContext.CiselnikStavuUkolu.AsNoTracking().OrderBy(x => x.Nazev).ToList();
        var taskTypes = _dbContext.CiselnikTypuUkolu.AsNoTracking().OrderBy(x => x.Nazev).ToList();
        var subsystems = _dbContext.Subsystemy.AsNoTracking().OrderBy(x => x.Nazev).ToList();
        var people = _dbContext.Osoby.AsNoTracking().OrderBy(x => x.Prijmeni).ThenBy(x => x.Jmeno).ToList();

        var extTypes = _dbContext.CiselnikTypuExternichOdkazu.AsNoTracking().OrderBy(x => x.Kod).ToList();
        var vyzvy = _dbContext.CiselnikVyzvy.AsNoTracking().OrderBy(x => x.Kod).ToList();

        var organizations = _dbContext.CiselnikOrganizace.AsNoTracking().ToDictionary(x => x.Id);
        var orgUnits = _dbContext.CiselnikOrganizacniCelky.AsNoTracking().ToDictionary(x => x.Id);
        var projectUsesMeetingNumbering = projectUsesMeetingIdentifier
            ?? _dbContext.Projekty.AsNoTracking()
                .Where(x => x.Id == record.ProjektId)
                .Select(x => (bool?)x.PouzivatIdentJednani)
                .FirstOrDefault()
            ?? false;
        var meetingOptions = openMeetingOptions ?? BuildOpenMeetingOptions(BuildJednaniList(record.ProjektId));
        var selectedMeetingId = forceMeetingIdForNumber
            ?? (record.CisloViditelneTyp == RecordDisplayNumberTypeMeeting ? record.CisloJednaniZdrojId : null);

        var externalLinks = isCreate
            ? new List<ExterniOdkazEditViewModel>()
            : _dbContext.ZaznamExterniOdkazy.AsNoTracking().Where(x => x.ZaznamId == record.Id)
                .OrderBy(x => x.Id)
                .ToList()
                .Select(x => new ExterniOdkazEditViewModel
                {
                    Id = x.Id,
                    Typ = extTypes.FirstOrDefault(et => et.Id == x.TypOdkazuId)?.Kod ?? string.Empty,
                    Cislo = x.Cislo,
                    Vyzva = x.Vyzva.HasValue ? vyzvy.FirstOrDefault(v => v.Id == x.Vyzva.Value)?.Kod : null,
                    DatumObjednani = x.DatumObjednani,
                    PlanDodani = x.PlanDodani,
                    DatumDodani = x.DatumDodani,
                    DatumPrevzeti = x.DatumPrevzeti
                })
                .ToList();

        var selectedCollaborationIds = isCreate
            ? new List<int>()
            : _dbContext.ZaznamSpoluprace.AsNoTracking().Where(x => x.ZaznamId == record.Id).Select(x => x.OsobaId).ToList();

        var selectedCategory = categories.FirstOrDefault(x => x.Id == record.KategorieId);
        var isTaskCategory = IsTaskCategory(selectedCategory?.Kod, selectedCategory?.Nazev);
        var harmonogramSchema = isCreate
            ? GetActiveHarmonogramSchema()
            : GetSchemaForRecord(record);
        var harmonogramTypy = harmonogramSchema.Kroky;
        var allowedTypeIds = harmonogramTypy
            .SelectMany(x => new[] { x.TrvaniTypId, x.ZpozdeniTypId })
            .Where(x => x > 0)
            .Distinct()
            .ToList();
        var harmonogramValues = (!isCreate && isTaskCategory && allowedTypeIds.Count > 0)
            ? _dbContext.ZaznamHarmonogramHodnoty.AsNoTracking()
                .Where(x => x.ZaznamId == record.Id && allowedTypeIds.Contains(x.TypId))
                .ToDictionary(x => x.TypId, x => x.HodnotaInt)
            : new Dictionary<int, int>();
        var harmonogramKroky = BuildHarmonogramVypocet(record.DatumZalozeni, harmonogramTypy, harmonogramValues);
        var harmonogramSouhrn = BuildHarmonogramSouhrn(harmonogramKroky, record.DatumUkonceni);

        return new ZaznamEditViewModel
        {
            Id = record.Id,
            CisloZaznamu = record.CisloZaznamu,
            CisloViditelne = ResolveVisibleRecordNumber(record),
            ProjektId = record.ProjektId,
            IsCreate = isCreate,
            PouzivatIdentJednani = projectUsesMeetingNumbering,
            MaDostupneJednaniProCislo = meetingOptions.Count > 0,
            MuzeDoplnitIdentifikatorJednani = !isCreate && projectUsesMeetingNumbering && record.CisloViditelneTyp != RecordDisplayNumberTypeMeeting,
            JednaniIdProCislo = selectedMeetingId,
            JednaniProCisloOptions = meetingOptions,
            Nazev = record.Nazev,
            Kategorie = selectedCategory?.Nazev ?? string.Empty,
            Popis = record.Popis ?? string.Empty,
            TypUkolu = record.AktualniTypUkoluId.HasValue ? taskTypes.FirstOrDefault(x => x.Id == record.AktualniTypUkoluId.Value)?.Nazev : null,
            Stav = record.StavUkoluId.HasValue ? taskStates.FirstOrDefault(x => x.Id == record.StavUkoluId.Value)?.Nazev ?? string.Empty : string.Empty,
            KategorieZaznamu = categories.Select(x => x.Nazev).ToList(),
            StavyUkolu = taskStates.Select(x => x.Nazev).ToList(),
            TypyUkolu = taskTypes.Select(x => x.Nazev).ToList(),
            DatumZalozeni = record.DatumZalozeni,
            TerminUkonceni = record.DatumUkonceni,
            Subsystemy = subsystems.Select(x => new SubsystemOptionViewModel
            {
                Kod = x.Kod,
                Nazev = x.Nazev,
                VedouciOsobaId = x.VedouciOsobaId
            }).ToList(),
            Subsystem = subsystems.FirstOrDefault(x => x.Id == record.SubsystemId)?.Nazev ?? string.Empty,
            Vlastnici = people.Select(x => new LookupOptionViewModel
            {
                Value = x.Id.ToString(CultureInfo.InvariantCulture),
                Label = BuildInlinePersonLabel(x.Titul, x.Jmeno, x.Prijmeni, x.Email, x.Id)
            }).ToList(),
            VlastnikId = record.VlastnikId,
            JeUkolKategorie = isTaskCategory,
            HarmonogramDelayBarvaHex = harmonogramSchema.DelayBarvaHex,
            HarmonogramKroky = harmonogramKroky.Select(krok => new HarmonogramKrokEditViewModel
            {
                KrokIndex = krok.KrokIndex,
                Nazev = krok.Nazev,
                BarvaHex = krok.BarvaHex,
                TrvaniTypId = krok.TrvaniTypId,
                ZpozdeniTypId = krok.ZpozdeniTypId,
                TrvaniDni = krok.TrvaniDni,
                OdchylkaDni = krok.ZpozdeniDni,
                BaselineDatum = krok.BaselineDatum,
                SkutecneDatum = krok.PosunuteDatum
            }).ToList(),
            HarmonogramSouhrn = harmonogramSouhrn,
            DostupniSpolupracovnici = people.Select(x => new SpolupracovnikOptionViewModel
            {
                OsobaId = x.Id,
                Osoba = BuildDisplayName(x.Titul, x.Jmeno, x.Prijmeni, x.Id),
                Email = x.Email?.Trim(),
                Organizace = organizations.GetValueOrDefault(x.OrganizaceId)?.Nazev,
                OrganizacniCelek = x.OrganizacniCelekId.HasValue ? orgUnits.GetValueOrDefault(x.OrganizacniCelekId.Value)?.Nazev : null
            }).ToList(),
            VybraniSpolupracovniciIds = selectedCollaborationIds,
            ExterniVazby = externalLinks,
            TypyExternichOdkazu = extTypes.Select(x => x.Kod).ToList(),
            Vyzvy = vyzvy.Select(x => x.Kod).ToList()
        };
    }

    private List<JednaniOptionViewModel> BuildOpenMeetingOptions(IReadOnlyList<JednaniListItemViewModel> meetings)
    {
        return meetings
            .Where(x =>
                !string.IsNullOrWhiteSpace(x.StavKod)
                && !Ci.Equals(x.StavKod, "CLOSED")
                && !x.Stav.Contains("uzav", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(x.UzamklOsoba))
            .OrderByDescending(x => x.CisloJednani)
            .Select(x => new JednaniOptionViewModel
            {
                Id = x.Id,
                Label = $"Jednání č. {x.CisloJednani} ({x.Datum:dd.MM.yyyy})"
            })
            .ToList();
    }

    private List<UcastViewModel> BuildMeetingAttendance(int meetingId, int projectId)
    {
        var attendances = _dbContext.Ucast.AsNoTracking().Where(x => x.JednaniId == meetingId).ToList();
        var attendanceByPerson = attendances.ToDictionary(x => x.OsobaId);
        var stateRows = _dbContext.CiselnikStavuUcasti.AsNoTracking().OrderBy(x => x.Id).ToList();
        var states = stateRows.ToDictionary(x => x.Id);
        var defaultState = stateRows
            .FirstOrDefault(x => Ci.Equals(x.Kod, "PRESENT") || x.Nazev.Contains("přít", StringComparison.OrdinalIgnoreCase))
            ?? stateRows.FirstOrDefault();

        var teamMemberIds = _dbContext.ObsazeniProjektu.AsNoTracking()
            .Where(x => x.ProjektId == projectId)
            .OrderBy(x => x.Id)
            .Select(x => x.OsobaId)
            .ToList();

        var participantIds = teamMemberIds
            .Concat(attendances.Select(x => x.OsobaId))
            .Distinct()
            .ToList();

        if (participantIds.Count == 0)
        {
            return new List<UcastViewModel>();
        }

        var people = _dbContext.Osoby.AsNoTracking()
            .Where(x => participantIds.Contains(x.Id))
            .ToDictionary(x => x.Id);

        return participantIds
            .OrderBy(osobaId => BuildDisplayNameFromOsoba(people.GetValueOrDefault(osobaId)), StringComparer.CurrentCultureIgnoreCase)
            .Select(osobaId =>
            {
                var attendance = attendanceByPerson.GetValueOrDefault(osobaId);
                var attendanceState = attendance is null
                    ? defaultState
                    : states.GetValueOrDefault(attendance.StavUcastiId);

                return new UcastViewModel
                {
                    OsobaId = osobaId,
                    Osoba = BuildDisplayNameFromOsoba(people.GetValueOrDefault(osobaId)),
                    Email = people.GetValueOrDefault(osobaId)?.Email?.Trim(),
                    StavUcastiKod = attendanceState?.Kod,
                    StavUcasti = attendanceState?.Nazev ?? "-"
                };
            })
            .ToList();
    }

    private List<JednaniUkolViewModel> BuildMeetingTasks(int meetingId, int projectId)
    {
        var projectRecords = _dbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.ProjektId == projectId)
            .ToList();
        projectRecords = OrderRecordsByVisibleNumber(projectRecords).ToList();
        var subsystems = _dbContext.Subsystemy.AsNoTracking().ToDictionary(x => x.Id);

        var commentsByRecord = _dbContext.Vyjadreni.AsNoTracking()
            .Where(x => x.JednaniId == meetingId)
            .OrderBy(x => x.Id)
            .ToList()
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var people = _dbContext.Osoby.AsNoTracking().ToDictionary(x => x.Id);
        var meeting = _dbContext.Jednani.AsNoTracking().FirstOrDefault(x => x.Id == meetingId);
        var meetingState = meeting is null
            ? null
            : _dbContext.CiselnikStavuJednani.AsNoTracking().FirstOrDefault(x => x.Id == meeting.StavJednaniId);
        var canModify = !IsMeetingReadOnly(meeting, meetingState);

        return projectRecords.Select(record => new JednaniUkolViewModel
        {
            ZaznamId = record.Id,
            CisloZaznamu = record.CisloZaznamu,
            CisloViditelne = ResolveVisibleRecordNumber(record),
            Popis = record.Nazev,
            Zapis = string.Empty,
            SubsystemVedouciOsobaId = subsystems.GetValueOrDefault(record.SubsystemId)?.VedouciOsobaId ?? 0,
            Vyjadreni = commentsByRecord
                .GetValueOrDefault(record.Id, new List<VyjadreniEntity>())
                .Select(comment => new JednaniVyjadreniViewModel
                {
                    Id = comment.Id,
                    AutorOsobaId = comment.AutorOsobaId,
                    Autor = BuildInlinePersonLabelFromOsoba(people.GetValueOrDefault(comment.AutorOsobaId)),
                    Datum = comment.DatumVyjadreni,
                    Text = comment.TextVyjadreni,
                    LzeUpravit = canModify
                })
                .ToList()
        }).ToList();
    }

    private CiselnikDetailViewModel BuildSimpleCiselnikDetail(string key, string name, IQueryable<CiselnikRadekViewModel> rows, bool canChangeLockState)
    {
        return new CiselnikDetailViewModel
        {
            Key = key,
            Nazev = name,
            CanChangeLockState = canChangeLockState,
            SloupceNavic = Array.Empty<string>(),
            Polozky = rows.OrderBy(x => x.Nazev).ToList()
        };
    }

    private CiselnikDetailViewModel BuildSubsystemyCiselnikDetail(string key, bool canChangeLockState)
    {
        _ = canChangeLockState;
        var people = _dbContext.Osoby.AsNoTracking()
            .OrderBy(x => x.Prijmeni)
            .ThenBy(x => x.Jmeno)
            .ToList();
        var peopleById = people.ToDictionary(x => x.Id);
        var subsystemRows = _dbContext.Subsystemy.AsNoTracking()
            .OrderBy(x => x.Nazev)
            .ToList();

        return new CiselnikDetailViewModel
        {
            Key = key,
            Nazev = "Subsystémy",
            CanChangeLockState = false,
            SloupceNavic = new[] { "Vedoucí subsystému" },
            HodnotaNavicVolby = BuildSubsystemLeaderOptions(people),
            IsHodnotaNavicSelect = true,
            IsHodnotaNavicRequired = true,
            Polozky = subsystemRows
                .Select(x => new CiselnikRadekViewModel
                {
                    Id = x.Id,
                    Kod = x.Kod,
                    Nazev = x.Nazev,
                    IsLocked = false,
                    CanChangeLockState = false,
                    HodnotyNavic = new[]
                    {
                        BuildInlinePersonLabelFromOsoba(peopleById.GetValueOrDefault(x.VedouciOsobaId))
                    },
                    HodnotyNavicRaw = new[]
                    {
                        x.VedouciOsobaId.ToString(CultureInfo.InvariantCulture)
                    }
                })
                .ToList()
        };
    }

    private CiselnikDetailViewModel BuildHarmonogramKrokyCiselnikDetail(string key, bool canChangeLockState)
    {
        var schema = GetActiveHarmonogramSchema();
        var rows = schema.Kroky
            .OrderBy(x => x.KrokIndex)
            .Select(step => new CiselnikRadekViewModel
            {
                Id = step.TrvaniTypId,
                Kod = step.Kod,
                Nazev = step.Nazev,
                IsLocked = false,
                CanEdit = true,
                CanDelete = true,
                CanChangeLockState = false,
                HodnotyNavic = new[] { step.BarvaHex },
                HodnotyNavicRaw = new[] { step.BarvaHex }
            })
            .ToList();

        rows.Add(new CiselnikRadekViewModel
        {
            Id = HarmonogramDelayColorPseudoRowId,
            Kod = HarmonogramDelayColorPseudoKod,
            Nazev = "Globální barva skutečnosti",
            IsLocked = true,
            CanEdit = true,
            CanDelete = false,
            CanChangeLockState = false,
            HodnotyNavic = new[] { schema.DelayBarvaHex },
            HodnotyNavicRaw = new[] { schema.DelayBarvaHex }
        });

        return new CiselnikDetailViewModel
        {
            Key = key,
            Nazev = "Harmonogramové kroky",
            CanCreate = true,
            CanChangeLockState = canChangeLockState,
            SloupceNavic = new[] { "Barva" },
            IsHodnotaNavicSelect = false,
            IsHodnotaNavicRequired = true,
            Polozky = rows
        };
    }

    private List<LookupOptionViewModel> BuildSubsystemLeaderOptions(IReadOnlyList<OsobaEntity> people)
    {
        return people
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Id.ToString(CultureInfo.InvariantCulture),
                Label = BuildInlinePersonLabel(x.Titul, x.Jmeno, x.Prijmeni, x.Email, x.Id)
            })
            .ToList();
    }

    private List<CiselnikListItemViewModel> BuildCiselnikItems()
    {
        return new List<CiselnikListItemViewModel>
        {
            new() { Key = "stavy-projektu", Nazev = "Stavy projektů", PocetPolozek = _dbContext.CiselnikStavuProjektu.Count() },
            new() { Key = "stavy-ukolu", Nazev = "Stavy úkolů", PocetPolozek = _dbContext.CiselnikStavuUkolu.Count() },
            new() { Key = "kategorie-zaznamu", Nazev = "Kategorie záznamů", PocetPolozek = _dbContext.CiselnikKategoriiZaznamu.Count() },
            new() { Key = "typy-ukolu", Nazev = "Typy úkolů", PocetPolozek = _dbContext.CiselnikTypuUkolu.Count() },
            new() { Key = "typy-externich-odkazu", Nazev = "Typy externích odkazů", PocetPolozek = _dbContext.CiselnikTypuExternichOdkazu.Count() },
            new() { Key = "role-projektu", Nazev = "Role projektu", PocetPolozek = _dbContext.CiselnikRoliProjektu.Count() },
            new() { Key = "stavy-ucasti", Nazev = "Stavy účasti", PocetPolozek = _dbContext.CiselnikStavuUcasti.Count() },
            new() { Key = "organizace", Nazev = "Organizace", PocetPolozek = _dbContext.CiselnikOrganizace.Count() },
            new() { Key = "organizacni-celky", Nazev = "Organizační celky", PocetPolozek = _dbContext.CiselnikOrganizacniCelky.Count() },
            new() { Key = "subsystemy", Nazev = "Subsystémy", PocetPolozek = _dbContext.Subsystemy.Count() },
            new() { Key = HarmonogramKrokyCiselnikKey, Nazev = "Harmonogramové kroky", PocetPolozek = CountHarmonogramCatalogRows() },
            new() { Key = "vyzvy", Nazev = "Výzvy", PocetPolozek = _dbContext.CiselnikVyzvy.Count() },
            new() { Key = "stavy-jednani", Nazev = "Stavy jednání", PocetPolozek = _dbContext.CiselnikStavuJednani.Count() }
        };
    }

    private int CountHarmonogramCatalogRows()
    {
        var schema = GetActiveHarmonogramSchema();
        return Math.Max(0, schema.Kroky.Count + 1);
    }

    private List<NastaveniSectionItemViewModel> BuildNastaveniSections(CurrentUserContextViewModel currentUser, NastaveniPanelViewModel panel)
    {
        var sections = new List<NastaveniSectionItemViewModel>
        {
            new() { Key = "role", Nazev = "Role", Popis = "Správa rolí", Pocet = panel.Role.Count },
            new() { Key = "akce", Nazev = "Akce", Popis = "Katalog akcí", Pocet = panel.Permissions.Count },
            new() { Key = "role-akce", Nazev = "Role -> Akce", Popis = "Mapování role/akce", Pocet = panel.RolePermissionScopes.Count },
            new() { Key = "uzivatele-role", Nazev = "Uživatelé -> Role", Popis = "Přiřazení rolí", Pocet = panel.UserRoles.Count }
        };

        if (currentUser.HasPermission(PermissionKeys.SettingsManage))
        {
            sections.Add(new NastaveniSectionItemViewModel
            {
                Key = "efektivni-prava",
                Nazev = "Efektivní práva",
                Popis = "Kontrola výsledných práv",
                Pocet = panel.EffectivePermissions.Rows.Count
            });
        }

        return sections;
    }

    private string NormalizeSettingsSection(string? section, bool canManageSettings)
    {
        var normalized = (section ?? "role").Trim().ToLowerInvariant();
        if (normalized == "efektivni-prava" && !canManageSettings)
        {
            return "role";
        }

        var valid = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "role", "akce", "role-akce", "uzivatele-role", "efektivni-prava"
        };

        return valid.Contains(normalized) ? normalized : "role";
    }

    private EffectivePermissionsPreviewViewModel BuildEffectivePermissionPreview(CurrentUserContextViewModel currentUser, int? selectedUserId, int? selectedProjectId)
    {
        var users = _dbContext.Osoby.AsNoTracking().OrderBy(x => x.Prijmeni).ThenBy(x => x.Jmeno).ToList();
        var projects = _dbContext.Projekty.AsNoTracking().OrderBy(x => x.Zkratka).ToList();

        var userId = selectedUserId ?? currentUser.OsobaId;
        var selectedUser = users.FirstOrDefault(x => x.Id == userId) ?? users.FirstOrDefault();
        if (selectedUser is null)
        {
            return new EffectivePermissionsPreviewViewModel
            {
                SelectedUserId = currentUser.OsobaId,
                SelectedProjectId = selectedProjectId,
                OsobaId = currentUser.OsobaId,
                Osoba = currentUser.DisplayName,
                ProjektId = selectedProjectId,
                ProjektNazev = selectedProjectId.HasValue
                    ? projects.FirstOrDefault(x => x.Id == selectedProjectId.Value)?.CelyNazev ?? "-"
                    : "Všechny projekty",
                AvailableUsers = Array.Empty<EffectiveRightsFilterOptionViewModel>(),
                AvailableProjects = Array.Empty<EffectiveRightsFilterOptionViewModel>(),
                Rows = Array.Empty<EffectivePermissionRowViewModel>()
            };
        }

        var previewContext = new CurrentUserContextViewModel
        {
            OsobaId = selectedUser.Id,
            Jmeno = selectedUser.Jmeno,
            Prijmeni = selectedUser.Prijmeni,
            DisplayName = BuildDisplayName(selectedUser.Titul, selectedUser.Jmeno, selectedUser.Prijmeni, selectedUser.Id),
            Email = selectedUser.Email?.Trim() ?? string.Empty,
            OrganizacniCelekKod = null,
            OrganizacniCelek = "-",
            IsSuperAdmin = _dbContext.AuthzSuperadmins.AsNoTracking().Any(x => x.OsobaId == selectedUser.Id),
            RoleKody = BuildUserRoleCodes(selectedUser.Id),
            PermissionGrants = BuildUserPermissionGrants(selectedUser.Id)
        };

        var permissions = _dbContext.AuthzPermissions.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Klic).ToList();

        var rows = permissions.Select(permission =>
        {
            var allowed = previewContext.HasPermission(permission.Klic, selectedProjectId);
            var grants = previewContext.PermissionGrants.Where(x => Ci.Equals(x.PermissionKey, permission.Klic)).ToList();
            var scopeSummary = grants.Count == 0
                ? "-"
                : string.Join(", ", grants.Select(x =>
                {
                    if (Ci.Equals(x.ScopeMode, "ALL"))
                    {
                        return "ALL";
                    }

                    if (x.ProjectIds.Count == 0)
                    {
                        return "INCLUDE (prázdné)";
                    }

                    var names = projects.Where(p => x.ProjectIds.Contains(p.Id)).Select(p => p.Zkratka).ToList();
                    return "INCLUDE: " + string.Join("; ", names);
                }));

            return new EffectivePermissionRowViewModel
            {
                PermissionKlic = permission.Klic,
                PermissionNazev = permission.Nazev,
                IsAllowed = allowed,
                ScopeSummary = scopeSummary
            };
        }).ToList();

        return new EffectivePermissionsPreviewViewModel
        {
            SelectedUserId = selectedUser.Id,
            SelectedProjectId = selectedProjectId,
            OsobaId = selectedUser.Id,
            Osoba = BuildInlinePersonLabel(selectedUser.Titul, selectedUser.Jmeno, selectedUser.Prijmeni, selectedUser.Email, selectedUser.Id),
            ProjektId = selectedProjectId,
            ProjektNazev = selectedProjectId.HasValue
                ? projects.FirstOrDefault(x => x.Id == selectedProjectId.Value)?.CelyNazev ?? "-"
                : "Všechny projekty",
            AvailableUsers = users.Select(user => new EffectiveRightsFilterOptionViewModel
            {
                Id = user.Id,
                Label = BuildInlinePersonLabel(user.Titul, user.Jmeno, user.Prijmeni, user.Email, user.Id)
            }).ToList(),
            AvailableProjects = projects.Select(project => new EffectiveRightsFilterOptionViewModel
            {
                Id = project.Id,
                Label = project.CelyNazev
            }).ToList(),
            Rows = rows
        };
    }

    private IReadOnlyList<ProfilRolePravaViewModel> BuildProfilRolePrava(int osobaId)
    {
        var roles = (
            from userRole in _dbContext.AuthzUserRoles.AsNoTracking()
            join role in _dbContext.AuthzRoles.AsNoTracking() on userRole.RoleId equals role.Id
            where userRole.OsobaId == osobaId
                && userRole.IsActive
                && role.IsActive
            orderby role.Kod, role.Nazev
            select new
            {
                role.Id,
                role.Kod,
                role.Nazev,
                role.Popis
            })
            .Distinct()
            .ToList();

        if (roles.Count == 0)
        {
            return [];
        }

        var roleIds = roles.Select(x => x.Id).ToList();
        var rolePermissions = (
            from rolePermission in _dbContext.AuthzRolePermissions.AsNoTracking()
            join permission in _dbContext.AuthzPermissions.AsNoTracking() on rolePermission.PermissionId equals permission.Id
            where roleIds.Contains(rolePermission.RoleId)
                && permission.IsActive
            select new
            {
                rolePermission.Id,
                rolePermission.RoleId,
                permission.Klic,
                permission.Nazev,
                rolePermission.ScopeMode,
                rolePermission.IsAllowed
            })
            .ToList();

        var includeRolePermissionIds = rolePermissions
            .Where(x => Ci.Equals(x.ScopeMode, "INCLUDE"))
            .Select(x => x.Id)
            .Distinct()
            .ToList();

        var includedProjectIdsByRolePermissionId = _dbContext.AuthzRolePermissionProjects.AsNoTracking()
            .Where(x => includeRolePermissionIds.Contains(x.RolePermissionId))
            .GroupBy(x => x.RolePermissionId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<int>)group.Select(x => x.ProjektId).Distinct().ToList());

        var projectCodesById = _dbContext.Projekty.AsNoTracking()
            .OrderBy(x => x.Zkratka)
            .Select(x => new
            {
                x.Id,
                x.Zkratka
            })
            .ToDictionary(x => x.Id, x => x.Zkratka);

        return roles
            .Select(role => new ProfilRolePravaViewModel
            {
                RoleId = role.Id,
                RoleKod = role.Kod,
                RoleNazev = role.Nazev,
                Popis = string.IsNullOrWhiteSpace(role.Popis) ? null : role.Popis.Trim(),
                Akce = rolePermissions
                    .Where(x => x.RoleId == role.Id)
                    .OrderBy(x => x.Klic, StringComparer.CurrentCultureIgnoreCase)
                    .Select(x => new ProfilRoleAkceViewModel
                    {
                        PermissionKlic = x.Klic,
                        PermissionNazev = x.Nazev,
                        IsAllowed = x.IsAllowed,
                        ScopeSummary = BuildPermissionScopeSummary(
                            x.ScopeMode,
                            includedProjectIdsByRolePermissionId.GetValueOrDefault(x.Id, Array.Empty<int>()),
                            projectCodesById)
                    })
                    .ToList()
            })
            .ToList();
    }

    private static string BuildPermissionScopeSummary(
        string scopeMode,
        IReadOnlyList<int> projectIds,
        IReadOnlyDictionary<int, string> projectCodesById)
    {
        if (string.Equals(scopeMode, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            return "ALL";
        }

        if (!string.Equals(scopeMode, "INCLUDE", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(scopeMode) ? "-" : scopeMode.Trim().ToUpperInvariant();
        }

        if (projectIds.Count == 0)
        {
            return "INCLUDE (prázdné)";
        }

        var projectCodes = projectIds
            .Select(projectId => projectCodesById.GetValueOrDefault(projectId))
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return projectCodes.Count == 0
            ? "INCLUDE (prázdné)"
            : "INCLUDE: " + string.Join("; ", projectCodes);
    }

    private List<PdfAttendanceGroupViewModel> BuildAttendanceGroups(int meetingId, int projectId)
    {
        var attendanceStates = _dbContext.CiselnikStavuUcasti.AsNoTracking().ToDictionary(x => x.Id);
        var people = _dbContext.Osoby.AsNoTracking().ToDictionary(x => x.Id);
        var projectMemberIds = _dbContext.ObsazeniProjektu.AsNoTracking()
            .Where(x => x.ProjektId == projectId)
            .Select(x => x.OsobaId)
            .Distinct()
            .ToHashSet();

        var attendanceRows = _dbContext.Ucast.AsNoTracking()
            .Where(x => x.JednaniId == meetingId)
            .ToList()
            .Where(x => projectMemberIds.Count == 0 || projectMemberIds.Contains(x.OsobaId))
            .ToList();

        return attendanceRows
            .GroupBy(x => x.StavUcastiId)
            .Select(group =>
            {
                var state = attendanceStates.GetValueOrDefault(group.Key);
                var names = group
                    .Select(item => BuildDisplayNameFromOsoba(people.GetValueOrDefault(item.OsobaId)))
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(Ci)
                    .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                return new
                {
                    State = state,
                    Names = names
                };
            })
            .Where(group => group.Names.Count > 0)
            .OrderBy(group => AttendancePrintOrder(group.State))
            .ThenBy(group => group.State?.Nazev ?? "Bez stavu", StringComparer.CurrentCultureIgnoreCase)
            .Select(group => new PdfAttendanceGroupViewModel
            {
                Stav = group.State?.Nazev ?? "Bez stavu",
                Osoby = group.Names
            })
            .ToList();
    }

    private int AttendancePrintOrder(CiselnikStavuUcastiEntity? state)
    {
        if (state is null)
        {
            return 100;
        }

        if (Ci.Equals(state.Kod, "PRESENT"))
        {
            return 1;
        }

        if (Ci.Equals(state.Kod, "ONLINE"))
        {
            return 2;
        }

        if (Ci.Equals(state.Kod, "EXCUSED"))
        {
            return 3;
        }

        if (Ci.Equals(state.Kod, "MISSING") || Ci.Equals(state.Kod, "ABSENT"))
        {
            return 4;
        }

        var normalizedName = _textNormalizer.Normalize(state.Nazev);
        if (normalizedName.Contains("videokonference", StringComparison.OrdinalIgnoreCase)
            || normalizedName.Contains("online", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (normalizedName.Contains("neomluven", StringComparison.OrdinalIgnoreCase)
            || normalizedName.Contains("nepritomen", StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        if (normalizedName.Contains("omluven", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (normalizedName.Contains("pritomen", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 100;
    }

    private List<PdfExportRecordViewModel> BuildExportRecords(int projectId, int? anchorMeetingId, int? specificRecordId, int anchorMeetingNumber, bool limitComments)
    {
        var records = _dbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.ProjektId == projectId)
            .Where(x => !specificRecordId.HasValue || x.Id == specificRecordId.Value)
            .ToList();
        records = OrderRecordsByVisibleNumber(records).ToList();

        var categories = _dbContext.CiselnikKategoriiZaznamu.AsNoTracking().ToDictionary(x => x.Id);
        var taskTypes = _dbContext.CiselnikTypuUkolu.AsNoTracking().ToDictionary(x => x.Id);
        var taskStates = _dbContext.CiselnikStavuUkolu.AsNoTracking().ToDictionary(x => x.Id);
        var subsystems = _dbContext.Subsystemy.AsNoTracking().ToDictionary(x => x.Id);
        var people = _dbContext.Osoby.AsNoTracking().ToDictionary(x => x.Id);

        var comments = _dbContext.Vyjadreni.AsNoTracking()
            .Where(x => records.Select(r => r.Id).Contains(x.ZaznamId))
            .OrderByDescending(x => x.DatumVyjadreni)
            .ToList();

        var meetings = _dbContext.Jednani.AsNoTracking()
            .Where(x => x.ProjektId == projectId)
            .OrderByDescending(x => x.CisloJednani)
            .ToList();

        var meetingStateMap = _dbContext.CiselnikStavuJednani.AsNoTracking().ToDictionary(x => x.Id);
        var externalTypeMap = _dbContext.CiselnikTypuExternichOdkazu.AsNoTracking().ToDictionary(x => x.Id);

        var externalLinksByRecord = _dbContext.ZaznamExterniOdkazy.AsNoTracking()
            .Where(x => records.Select(r => r.Id).Contains(x.ZaznamId))
            .ToList()
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var collaboration = _dbContext.ZaznamSpoluprace.AsNoTracking()
            .Where(x => records.Select(r => r.Id).Contains(x.ZaznamId))
            .ToList()
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var termHistory = _dbContext.ZaznamHistorieTerminu.AsNoTracking()
            .Where(x => records.Select(r => r.Id).Contains(x.ZaznamId))
            .OrderByDescending(x => x.DatumZmeny)
            .ToList()
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var meetingById = meetings.ToDictionary(x => x.Id);
        var anchorMeeting = anchorMeetingId.HasValue ? meetings.FirstOrDefault(x => x.Id == anchorMeetingId.Value) : null;
        var anchorState = anchorMeeting is null ? null : meetingStateMap.GetValueOrDefault(anchorMeeting.StavJednaniId);
        var previousMeetingNumber = anchorMeeting is null
            ? null
            : meetings.Where(x => x.CisloJednani < anchorMeeting.CisloJednani)
                .OrderByDescending(x => x.CisloJednani)
                .Select(x => (int?)x.CisloJednani)
                .FirstOrDefault();

        var filteredComments = anchorMeeting is null
            ? comments
            : comments
                .Where(comment =>
                    meetingById.TryGetValue(comment.JednaniId, out var commentMeeting)
                    && commentMeeting.CisloJednani <= anchorMeeting.CisloJednani)
                .ToList();

        var commentGroups = filteredComments
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var exportRows = records.Select(record =>
        {
            var commentsForRecord = commentGroups.GetValueOrDefault(record.Id, new List<VyjadreniEntity>());
            var selectedComments = limitComments
                ? ApplyCommentLimit(commentsForRecord, records.Count)
                : commentsForRecord;

            var commentRows = selectedComments.Select(comment =>
            {
                meetingById.TryGetValue(comment.JednaniId, out var commentMeeting);
                var commentState = commentMeeting is null ? null : meetingStateMap.GetValueOrDefault(commentMeeting.StavJednaniId);
                var author = people.GetValueOrDefault(comment.AutorOsobaId) ?? people.GetValueOrDefault(record.VlastnikId);

                var highlightColor = ResolveHighlightColor(anchorMeeting, anchorState, previousMeetingNumber, commentMeeting);

                return new PdfExportCommentViewModel
                {
                    Autor = BuildDisplayNameFromOsoba(author),
                    Datum = comment.DatumVyjadreni,
                    Text = comment.TextVyjadreni,
                    Delka = comment.TextVyjadreni?.Length ?? 0,
                    JednaniCislo = commentMeeting?.CisloJednani,
                    JednaniDatum = commentMeeting?.DatumPlanovane,
                    JednaniStav = commentState?.Nazev,
                    IsNew = !string.IsNullOrWhiteSpace(highlightColor),
                    HighlightColor = highlightColor
                };
            }).ToList();

            var external = externalLinksByRecord.GetValueOrDefault(record.Id, new List<ZaznamExterniOdkazEntity>())
                .Select(link =>
                {
                    var typeCode = externalTypeMap.GetValueOrDefault(link.TypOdkazuId)?.Kod ?? "-";
                    return $"{typeCode} {link.Cislo}";
                })
                .ToList();

            var peopleCollab = collaboration.GetValueOrDefault(record.Id, new List<ZaznamSpolupraceEntity>())
                .Select(item => BuildDisplayNameFromOsoba(people.GetValueOrDefault(item.OsobaId)))
                .Distinct(Ci)
                .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            var historyDates = termHistory.GetValueOrDefault(record.Id, new List<ZaznamHistorieTerminuEntity>())
                .Select(x => x.PuvodniDatum)
                .Distinct()
                .OrderByDescending(x => x)
                .ToList();

            var category = categories.GetValueOrDefault(record.KategorieId);
            var taskType = record.AktualniTypUkoluId.HasValue
                ? taskTypes.GetValueOrDefault(record.AktualniTypUkoluId.Value)
                : null;
            var subsystem = subsystems.GetValueOrDefault(record.SubsystemId);

            return new PdfExportRecordViewModel
            {
                ZaznamId = record.Id,
                CisloZaznamu = record.CisloZaznamu,
                CisloViditelne = ResolveVisibleRecordNumber(record),
                CisloViditelneA = ResolveVisibleNumberPartA(record),
                CisloViditelneB = ResolveVisibleNumberPartB(record),
                Nazev = record.Nazev,
                Popis = record.Popis,
                KategorieKod = category?.Kod ?? "-",
                Kategorie = category?.Nazev ?? "-",
                TypUkoluKod = taskType?.Kod,
                TypUkolu = taskType?.Nazev,
                Stav = record.StavUkoluId.HasValue ? taskStates.GetValueOrDefault(record.StavUkoluId.Value)?.Nazev ?? "-" : "-",
                Vlastnik = BuildDisplayNameFromOsoba(people.GetValueOrDefault(record.VlastnikId)),
                SubsystemKod = subsystem?.Kod ?? "-",
                Subsystem = subsystem?.Nazev ?? "-",
                DatumZalozeni = record.DatumZalozeni,
                HistorieTerminu = historyDates,
                Termin = record.DatumUkonceni,
                ExterniVazby = external,
                Spoluprace = peopleCollab,
                Vyjadreni = commentRows
            };
        }).ToList();

        return exportRows;
    }

    private static List<VyjadreniEntity> ApplyCommentLimit(List<VyjadreniEntity> comments, int taskCount)
    {
        const int maxCommentsPerTask = 5;
        const int totalBudget = 39;
        var budgetPerTask = Math.Max(10, (int)Math.Floor(totalBudget / Math.Max(taskCount, 1d)));

        var selected = new List<VyjadreniEntity>();
        var usedLines = 0;
        foreach (var comment in comments.OrderByDescending(x => x.DatumVyjadreni))
        {
            if (selected.Count >= maxCommentsPerTask)
            {
                break;
            }

            var estimatedLines = 2 + (int)Math.Ceiling((comment.TextVyjadreni?.Length ?? 0) / 115d);
            if (selected.Count > 0 && usedLines + estimatedLines > budgetPerTask)
            {
                break;
            }

            selected.Add(comment);
            usedLines += estimatedLines;
        }

        return selected;
    }

    private static string? ResolveHighlightColor(
        JednaniEntity? anchorMeeting,
        CiselnikStavuJednaniEntity? anchorState,
        int? previousMeetingNumber,
        JednaniEntity? commentMeeting)
    {
        if (anchorMeeting is null || commentMeeting is null || anchorState is null)
        {
            return null;
        }

        var anchorIsPreparation = anchorState.Kod.Equals("DRAFT", StringComparison.OrdinalIgnoreCase)
            || anchorState.Nazev.Contains("příprav", StringComparison.OrdinalIgnoreCase);

        if (!anchorIsPreparation)
        {
            return commentMeeting.Id == anchorMeeting.Id ? "#8ec6e9" : null;
        }

        if (commentMeeting.Id == anchorMeeting.Id)
        {
            return "#f1978c";
        }

        if (previousMeetingNumber.HasValue && commentMeeting.CisloJednani == previousMeetingNumber.Value)
        {
            return "#8ec6e9";
        }

        return null;
    }

    private bool CanModifyComment(VyjadreniEntity comment, ProjektovyZaznamEntity record, CurrentUserContextViewModel currentUser)
    {
        var subsystemLeaderId = ResolveSubsystemLeaderId(record.SubsystemId);
        return _commentAuthorizationPolicy.CanModifyComment(
            currentUser,
            record.ProjektId,
            subsystemLeaderId,
            comment.AutorOsobaId);
    }

    private bool CanCommentAsSubsystemLeader(ProjektovyZaznamEntity record, CurrentUserContextViewModel currentUser)
    {
        var subsystemLeaderId = ResolveSubsystemLeaderId(record.SubsystemId);
        return _commentAuthorizationPolicy.CanCommentAsSubsystemLeader(
            currentUser,
            record.ProjektId,
            subsystemLeaderId);
    }

    private int ResolveSubsystemLeaderId(int subsystemId)
    {
        return _dbContext.Subsystemy
            .AsNoTracking()
            .Where(x => x.Id == subsystemId)
            .Select(x => (int?)x.VedouciOsobaId)
            .FirstOrDefault() ?? 0;
    }

    private void EnsureMeetingAllowsCommentChanges(JednaniEntity meeting)
    {
        var status = _dbContext.CiselnikStavuJednani
            .AsNoTracking()
            .FirstOrDefault(x => x.Id == meeting.StavJednaniId);

        if (IsMeetingReadOnly(meeting, status))
        {
            throw new InvalidOperationException("Vyjádření u uzavřeného jednání nelze upravovat ani mazat. Nejprve jednání otevřete.");
        }
    }

    private static bool IsMeetingReadOnly(JednaniEntity? meeting, CiselnikStavuJednaniEntity? status)
    {
        if (meeting is null)
        {
            return true;
        }

        if (meeting.UzamklOsobaId.HasValue)
        {
            return true;
        }

        if (status is null)
        {
            return false;
        }

        return Ci.Equals(status.Kod, "CLOSED")
            || status.Nazev.Contains("uzav", StringComparison.OrdinalIgnoreCase);
    }

    private int ResolveProjectStatusId(string value)
    {
        var input = value?.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new InvalidOperationException("Stav projektu není vyplněn.");
        }

        var direct = _dbContext.CiselnikStavuProjektu
            .Where(x => x.Kod == input || x.Nazev == input)
            .Select(x => (int?)x.Id)
            .FirstOrDefault();
        if (direct.HasValue)
        {
            return direct.Value;
        }

        var normalized = _textNormalizer.Normalize(input);
        var match = _dbContext.CiselnikStavuProjektu
            .AsNoTracking()
            .Select(x => new { x.Id, x.Kod, x.Nazev })
            .AsEnumerable()
            .FirstOrDefault(x =>
                _textNormalizer.Normalize(x.Kod) == normalized ||
                _textNormalizer.Normalize(x.Nazev) == normalized);

        if (match is not null)
        {
            return match.Id;
        }

        throw new InvalidOperationException($"Stav projektu '{value}' neexistuje.");
    }

    private int ResolveKategorieId(string value)
        => _dbContext.CiselnikKategoriiZaznamu
            .Where(x => x.Kod == value || x.Nazev == value)
            .Select(x => (int?)x.Id)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Kategorie '{value}' neexistuje.");

    private static bool IsTaskCategory(string? categoryCode, string? categoryName)
    {
        if (!string.IsNullOrWhiteSpace(categoryCode)
            && Ci.Equals(categoryCode.Trim(), "UKOL"))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(categoryName))
        {
            return false;
        }

        return categoryName.Contains("úkol", StringComparison.OrdinalIgnoreCase)
            || categoryName.Contains("ukol", StringComparison.OrdinalIgnoreCase);
    }

    private int ResolveStavUkoluId(string value)
        => _dbContext.CiselnikStavuUkolu
            .Where(x => x.Kod == value || x.Nazev == value)
            .Select(x => (int?)x.Id)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Stav úkolu '{value}' neexistuje.");

    private int ResolveSubsystemId(string value)
        => _dbContext.Subsystemy
            .Where(x => x.Kod == value || x.Nazev == value)
            .Select(x => (int?)x.Id)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Subsystém '{value}' neexistuje.");

    private int ResolveMeetingStatusId(string value)
        => _dbContext.CiselnikStavuJednani
            .Where(x => x.Kod == value || x.Nazev == value)
            .Select(x => (int?)x.Id)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Stav jednání '{value}' neexistuje.");

    private int ResolveAttendanceStatusId(string value)
        => _dbContext.CiselnikStavuUcasti
            .Where(x => x.Kod == value || x.Nazev == value)
            .Select(x => (int?)x.Id)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Stav účasti '{value}' neexistuje.");

    private int? ResolveProjectRoleId(string value)
        => _dbContext.CiselnikRoliProjektu
            .Where(x => x.Kod == value || x.Nazev == value)
            .Select(x => (int?)x.Id)
            .FirstOrDefault();

    private int ResolveTypOdkazuId(string value)
        => _dbContext.CiselnikTypuExternichOdkazu
            .Where(x => x.Kod == value || x.Nazev == value)
            .Select(x => (int?)x.Id)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Typ externí vazby '{value}' neexistuje.");

    private int? ResolveTypUkoluId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return _dbContext.CiselnikTypuUkolu
            .Where(x => x.Kod == value || x.Nazev == value)
            .Select(x => (int?)x.Id)
            .FirstOrDefault();
    }

    private int ResolveOsobaId(string value)
    {
        if (int.TryParse(value, out var id))
        {
            return id;
        }

        var normalized = _textNormalizer.Normalize(value);
        var osoby = _dbContext.Osoby.AsNoTracking().ToList();
        var exact = osoby.FirstOrDefault(x =>
            _personIdentityMatcher.NameEquals(x.Titul, x.Jmeno, x.Prijmeni, normalized) ||
            _personIdentityMatcher.EmailEquals(x.Email, normalized));
        if (exact is not null)
        {
            return exact.Id;
        }

        var contains = osoby.FirstOrDefault(x =>
            _personIdentityMatcher.NameContains(x.Titul, x.Jmeno, x.Prijmeni, normalized) ||
            _personIdentityMatcher.EmailContains(x.Email, normalized));
        if (contains is not null)
        {
            return contains.Id;
        }

        throw new InvalidOperationException($"Osoba '{value}' nebyla nalezena.");
    }

    private int ResolveOrganizationId(string? organization)
    {
        if (!string.IsNullOrWhiteSpace(organization))
        {
            var match = _dbContext.CiselnikOrganizace
                .Where(x => x.Kod == organization || x.Nazev == organization)
                .Select(x => (int?)x.Id)
                .FirstOrDefault();
            if (match.HasValue)
            {
                return match.Value;
            }
        }

        return _dbContext.CiselnikOrganizace.AsNoTracking()
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

        return _dbContext.CiselnikOrganizace.AsNoTracking()
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

        return _dbContext.CiselnikOrganizacniCelky
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
        var normalized = _textNormalizer.Normalize(value);
        return _dbContext.CiselnikOrganizace
            .AsNoTracking()
            .Select(x => new { x.Id, x.Kod, x.Nazev })
            .ToList()
            .Where(x =>
                string.Equals(x.Kod, value, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Nazev, value, StringComparison.OrdinalIgnoreCase)
                || _textNormalizer.Normalize(x.Kod) == normalized
                || _textNormalizer.Normalize(x.Nazev) == normalized)
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

        _dbContext.CiselnikOrganizace.Add(entity);
        _dbContext.SaveChanges();
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

        _dbContext.CiselnikOrganizacniCelky.Add(entity);
        _dbContext.SaveChanges();
        return entity.Id;
    }

    private string BuildUniqueOrganizationCode(string? preferredCode, string fallbackName)
        => BuildUniqueCode(
            preferredCode,
            fallbackName,
            "ORG",
            code => _dbContext.CiselnikOrganizace.AsNoTracking().Any(x => x.Kod == code),
            allowLeadingDigit: false);

    private string BuildUniqueOrgUnitCode(string? preferredCode, string fallbackName)
        => BuildUniqueCode(
            preferredCode,
            fallbackName,
            "CEL",
            code => _dbContext.CiselnikOrganizacniCelky.AsNoTracking().Any(x => x.Kod == code),
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

    private int? ResolveVyzvaId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return _dbContext.CiselnikVyzvy
            .Where(x => x.Kod == value || x.Nazev == value)
            .Select(x => (int?)x.Id)
            .FirstOrDefault();
    }

    private int GetNextCisloZaznamuTransactional(int projektId)
    {
        var maxValue = _dbContext.ProjektoveZaznamy
            .FromSqlRaw("SELECT * FROM projektove_zaznamy WITH (UPDLOCK, HOLDLOCK) WHERE projekt_id = {0}", projektId)
            .Select(x => (int?)x.CisloZaznamu)
            .Max();

        return (maxValue ?? 0) + 1;
    }

    private int AllocateMeetingOrderTransactional(int projektId, int cisloJednani)
    {
        var maxOrder = _dbContext.ProjektoveZaznamy
            .FromSqlRaw(
                "SELECT * FROM projektove_zaznamy WITH (UPDLOCK, HOLDLOCK) WHERE projekt_id = {0} AND cislo_viditelne_typ = {1} AND cislo_viditelne_a = {2}",
                projektId,
                RecordDisplayNumberTypeMeeting,
                cisloJednani)
            .Select(x => (int?)x.CisloViditelneB)
            .Max();

        return (maxOrder ?? 0) + 1;
    }

    private void ReplaceRecordCollaboration(int zaznamId, IReadOnlyList<int> selectedPersonIds)
    {
        var existing = _dbContext.ZaznamSpoluprace.Where(x => x.ZaznamId == zaznamId).ToList();
        _dbContext.ZaznamSpoluprace.RemoveRange(existing);

        foreach (var personId in selectedPersonIds.Distinct())
        {
            _dbContext.ZaznamSpoluprace.Add(new ZaznamSpolupraceEntity
            {
                ZaznamId = zaznamId,
                OsobaId = personId
            });
        }
    }

    private void ReplaceRecordExternalLinks(int zaznamId, IReadOnlyList<SaveRecordExterniVazbaCommand> externalLinks)
    {
        var existing = _dbContext.ZaznamExterniOdkazy.Where(x => x.ZaznamId == zaznamId).ToList();
        _dbContext.ZaznamExterniOdkazy.RemoveRange(existing);

        foreach (var link in externalLinks)
        {
            if (string.IsNullOrWhiteSpace(link.Typ) || string.IsNullOrWhiteSpace(link.Cislo))
            {
                continue;
            }

            var typeId = ResolveTypOdkazuId(link.Typ);
            _dbContext.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
            {
                ZaznamId = zaznamId,
                TypOdkazuId = typeId,
                Cislo = link.Cislo.Trim(),
                DatumObjednani = link.DatumObjednani,
                PlanDodani = link.PlanDodani,
                DatumDodani = link.DatumDodani,
                DatumPrevzeti = link.DatumPrevzeti,
                Vyzva = ResolveVyzvaId(link.Vyzva)
            });
        }
    }

    private int SaveRecordScheduleOnly(
        SaveRecordCommand command,
        CurrentUserContextViewModel currentUser,
        bool canEditScheduleFull)
    {
        if (!command.Id.HasValue || command.Id.Value <= 0)
        {
            throw new InvalidOperationException("Bez oprávnění records.edit nelze zakládat nový záznam.");
        }

        if (!string.Equals(command.EditorTab, "schedule", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Bez oprávnění records.edit lze ukládat pouze záložku Harmonogram.");
        }

        var entity = _dbContext.ProjektoveZaznamy.AsNoTracking()
            .FirstOrDefault(x => x.Id == command.Id.Value)
            ?? throw new InvalidOperationException($"Záznam {command.Id.Value} nebyl nalezen.");
        if (entity.ProjektId != command.ProjektId)
        {
            throw new InvalidOperationException("Záznam nepatří do zvoleného projektu.");
        }

        var category = _dbContext.CiselnikKategoriiZaznamu.AsNoTracking()
            .Where(x => x.Id == entity.KategorieId)
            .Select(x => new { x.Kod, x.Nazev })
            .FirstOrDefault();
        if (!IsTaskCategory(category?.Kod, category?.Nazev))
        {
            throw new InvalidOperationException("Harmonogram lze upravovat pouze u záznamů kategorie úkol.");
        }

        var schema = GetSchemaForRecord(entity);
        var harmonogramTypy = schema.Kroky;
        var valuesToPersist = canEditScheduleFull
            ? command.HarmonogramHodnoty
            : BuildScheduleValuesForAddOnly(entity.Id, command.HarmonogramHodnoty, harmonogramTypy);

        using var tx = _dbContext.Database.BeginTransaction(IsolationLevel.Serializable);
        var normalizedScheduleValues = ReplaceRecordScheduleValues(entity.Id, valuesToPersist, harmonogramTypy);
        _dbContext.SaveChanges();
        tx.Commit();

        WriteAudit(
            currentUser.OsobaId,
            "zaznam_harmonogram_hodnoty",
            entity.Id.ToString(CultureInfo.InvariantCulture),
            "upsert",
            null,
            JsonSerializer.Serialize(normalizedScheduleValues));

        return entity.Id;
    }

    private List<SaveRecordHarmonogramValueCommand> BuildScheduleValuesForAddOnly(
        int zaznamId,
        IReadOnlyList<SaveRecordHarmonogramValueCommand> submittedValues,
        IReadOnlyList<HarmonogramTypPar> harmonogramTypy)
    {
        if (harmonogramTypy.Count == 0)
        {
            return new List<SaveRecordHarmonogramValueCommand>();
        }

        var durationTypeIds = harmonogramTypy
            .Select(x => x.TrvaniTypId)
            .Where(x => x > 0)
            .Distinct()
            .ToList();
        var delayTypeIds = harmonogramTypy
            .Select(x => x.ZpozdeniTypId)
            .Where(x => x > 0)
            .Distinct()
            .ToList();
        var allowedTypeIds = durationTypeIds
            .Concat(delayTypeIds)
            .ToHashSet();
        if (allowedTypeIds.Count == 0)
        {
            return new List<SaveRecordHarmonogramValueCommand>();
        }

        var submittedByType = submittedValues
            .Where(x => allowedTypeIds.Contains(x.TypId))
            .GroupBy(x => x.TypId)
            .ToDictionary(group => group.Key, group => Math.Max(0, group.Last().Hodnota));

        var existingByType = _dbContext.ZaznamHarmonogramHodnoty.AsNoTracking()
            .Where(x => x.ZaznamId == zaznamId && allowedTypeIds.Contains(x.TypId))
            .ToDictionary(x => x.TypId, x => Math.Max(0, x.HodnotaInt));

        var desired = new Dictionary<int, int>();

        foreach (var typeId in delayTypeIds)
        {
            if (submittedByType.TryGetValue(typeId, out var delayValue))
            {
                desired[typeId] = delayValue;
            }
            else if (existingByType.TryGetValue(typeId, out var existingDelayValue))
            {
                desired[typeId] = existingDelayValue;
            }
        }

        foreach (var typeId in durationTypeIds)
        {
            var existingDuration = existingByType.GetValueOrDefault(typeId, 0);
            if (existingDuration > 0)
            {
                desired[typeId] = existingDuration;
                continue;
            }

            if (submittedByType.TryGetValue(typeId, out var submittedDuration))
            {
                desired[typeId] = submittedDuration;
            }
        }

        return desired
            .Select(item => new SaveRecordHarmonogramValueCommand
            {
                TypId = item.Key,
                Hodnota = item.Value
            })
            .OrderBy(x => x.TypId)
            .ToList();
    }

    private List<SaveRecordHarmonogramValueCommand> ReplaceRecordScheduleValues(
        int zaznamId,
        IReadOnlyList<SaveRecordHarmonogramValueCommand> harmonogramValues,
        IReadOnlyList<HarmonogramTypPar> harmonogramTypy)
    {
        var allowedTypeIds = harmonogramTypy
            .SelectMany(x => new[] { x.TrvaniTypId, x.ZpozdeniTypId })
            .Where(x => x > 0)
            .Distinct()
            .ToHashSet();

        if (allowedTypeIds.Count == 0)
        {
            return new List<SaveRecordHarmonogramValueCommand>();
        }

        var normalized = harmonogramValues
            .Where(x => allowedTypeIds.Contains(x.TypId))
            .GroupBy(x => x.TypId)
            .Select(group => new
            {
                TypId = group.Key,
                Hodnota = Math.Max(0, group.Last().Hodnota)
            })
            .Where(x => x.Hodnota > 0)
            .ToDictionary(x => x.TypId, x => x.Hodnota);
        var normalizedResult = normalized
            .Select(item => new SaveRecordHarmonogramValueCommand
            {
                TypId = item.Key,
                Hodnota = item.Value
            })
            .OrderBy(x => x.TypId)
            .ToList();

        var existing = _dbContext.ZaznamHarmonogramHodnoty
            .Where(x => x.ZaznamId == zaznamId && allowedTypeIds.Contains(x.TypId))
            .ToList();

        var now = DateTime.UtcNow;
        foreach (var row in existing)
        {
            if (normalized.TryGetValue(row.TypId, out var value))
            {
                row.HodnotaInt = value;
                row.UpdatedAt = now;
                normalized.Remove(row.TypId);
            }
            else
            {
                _dbContext.ZaznamHarmonogramHodnoty.Remove(row);
            }
        }

        foreach (var item in normalized)
        {
            _dbContext.ZaznamHarmonogramHodnoty.Add(new ZaznamHarmonogramHodnotaEntity
            {
                ZaznamId = zaznamId,
                TypId = item.Key,
                HodnotaInt = item.Value,
                UpdatedAt = now
            });
        }

        return normalizedResult;
    }

    private void SaveHarmonogramStepRow(SaveCiselnikRowCommand command)
    {
        var rawColor = command.HodnotyNavic.FirstOrDefault()?.Trim();
        if (!IsValidHexColor(rawColor))
        {
            throw new InvalidOperationException("Barva kroku musí být ve formátu #RRGGBB.");
        }

        var colorHex = rawColor!.ToUpperInvariant();
        var clone = CloneActiveHarmonogramSchema();
        var rows = clone.ClonedRows.ToList();
        var durationRows = rows.Where(x => !x.JeZpozdeni).OrderBy(x => x.KrokPoradi).ThenBy(x => x.Id).ToList();

        if (command.Id.GetValueOrDefault() == HarmonogramDelayColorPseudoRowId)
        {
            clone.NewSchema.DelayBarvaHex = colorHex;
            FinalizeClonedHarmonogramSchema(clone.NewSchema, normalizeRows: false);
            return;
        }

        var kod = (command.Kod ?? string.Empty).Trim();
        var nazev = (command.Nazev ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(kod))
        {
            throw new InvalidOperationException("Kód kroku harmonogramu je povinný.");
        }

        if (string.IsNullOrWhiteSpace(nazev))
        {
            throw new InvalidOperationException("Název kroku harmonogramu je povinný.");
        }

        if (command.Id.HasValue)
        {
            if (!clone.ClonedBySourceId.TryGetValue(command.Id.Value, out var targetStep) || targetStep.JeZpozdeni)
            {
                throw new InvalidOperationException("Upravovat lze pouze hlavní kroky harmonogramu.");
            }

            var duplicateCodeExists = durationRows.Any(x => x.Id != targetStep.Id && Ci.Equals(x.Kod, kod));
            if (duplicateCodeExists)
            {
                throw new InvalidOperationException($"Krok s kódem '{kod}' už existuje.");
            }

            targetStep.Kod = kod;
            targetStep.Nazev = nazev;
            targetStep.BarvaHex = colorHex;

            var pairedDelay = rows.FirstOrDefault(x => x.JeZpozdeni && x.KrokKey == targetStep.KrokKey);
            if (pairedDelay is not null && string.IsNullOrWhiteSpace(pairedDelay.Nazev))
            {
                pairedDelay.Nazev = $"{nazev} - skutečnost";
            }
        }
        else
        {
            var duplicateCodeExists = durationRows.Any(x => Ci.Equals(x.Kod, kod));
            if (duplicateCodeExists)
            {
                throw new InvalidOperationException($"Krok s kódem '{kod}' už existuje.");
            }

            var nextOrder = durationRows.Count == 0 ? 1 : durationRows.Max(x => x.KrokPoradi) + 1;
            var stepKey = Guid.NewGuid();
            var usedCodes = rows.Select(x => x.Kod).Where(x => !string.IsNullOrWhiteSpace(x));
            var delayKod = BuildUniqueDelayCode($"{kod}_DELAY", usedCodes);

            _dbContext.CiselnikHarmonogramTypu.Add(new HarmonogramTypEntity
            {
                Kod = kod,
                Nazev = nazev,
                Hodnota = nextOrder,
                IsLocked = true,
                SablonaVerze = clone.NewSchema.Verze,
                KrokKey = stepKey,
                KrokPoradi = nextOrder,
                JeZpozdeni = false,
                BarvaHex = colorHex
            });

            _dbContext.CiselnikHarmonogramTypu.Add(new HarmonogramTypEntity
            {
                Kod = delayKod,
                Nazev = $"{nazev} - skutečnost",
                Hodnota = 100 + nextOrder,
                IsLocked = true,
                SablonaVerze = clone.NewSchema.Verze,
                KrokKey = stepKey,
                KrokPoradi = nextOrder,
                JeZpozdeni = true,
                BarvaHex = null
            });
        }

        _dbContext.SaveChanges();
        FinalizeClonedHarmonogramSchema(clone.NewSchema);
    }

    private void DeleteHarmonogramStepRow(DeleteCiselnikRowCommand command)
    {
        if (command.Id == HarmonogramDelayColorPseudoRowId)
        {
            throw new InvalidOperationException("Globální barvu skutečnosti nelze smazat.");
        }

        var clone = CloneActiveHarmonogramSchema();
        if (!clone.ClonedBySourceId.TryGetValue(command.Id, out var targetStep) || targetStep.JeZpozdeni)
        {
            throw new InvalidOperationException("Smazat lze pouze hlavní kroky harmonogramu.");
        }

        var remainingMainSteps = clone.ClonedRows.Count(x => !x.JeZpozdeni && x.Id != targetStep.Id);
        if (remainingMainSteps <= 0)
        {
            throw new InvalidOperationException("Harmonogram musí obsahovat alespoň jeden krok.");
        }

        var pairedDelay = clone.ClonedRows.FirstOrDefault(x => x.JeZpozdeni && x.KrokKey == targetStep.KrokKey);
        _dbContext.CiselnikHarmonogramTypu.Remove(targetStep);
        if (pairedDelay is not null)
        {
            _dbContext.CiselnikHarmonogramTypu.Remove(pairedDelay);
        }

        _dbContext.SaveChanges();
        FinalizeClonedHarmonogramSchema(clone.NewSchema);
    }

    private HarmonogramSchemaCloneResult CloneActiveHarmonogramSchema()
    {
        var sourceSchema = _dbContext.HarmonogramSablony
            .OrderByDescending(x => x.IsAktivni)
            .ThenByDescending(x => x.Verze)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("Aktivní harmonogramová šablona není dostupná.");

        var sourceRows = _dbContext.CiselnikHarmonogramTypu.AsNoTracking()
            .Where(x => x.SablonaVerze == sourceSchema.Verze)
            .OrderBy(x => x.KrokPoradi)
            .ThenBy(x => x.JeZpozdeni)
            .ThenBy(x => x.Id)
            .ToList();
        if (sourceRows.Count == 0)
        {
            throw new InvalidOperationException("Aktivní harmonogramová šablona neobsahuje žádné kroky.");
        }

        var newSchema = new HarmonogramSablonaEntity
        {
            DelayBarvaHex = NormalizeHexColor(sourceSchema.DelayBarvaHex, DefaultDelayBarvaHex),
            IsAktivni = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = null
        };
        _dbContext.HarmonogramSablony.Add(newSchema);
        _dbContext.SaveChanges();

        var mapBySourceId = new Dictionary<int, HarmonogramTypEntity>();
        var clonedRows = new List<HarmonogramTypEntity>(sourceRows.Count);
        foreach (var sourceRow in sourceRows)
        {
            var clone = new HarmonogramTypEntity
            {
                Kod = sourceRow.Kod,
                Nazev = sourceRow.Nazev,
                Hodnota = sourceRow.Hodnota,
                IsLocked = sourceRow.IsLocked,
                SablonaVerze = newSchema.Verze,
                KrokKey = sourceRow.KrokKey,
                KrokPoradi = sourceRow.KrokPoradi,
                JeZpozdeni = sourceRow.JeZpozdeni,
                BarvaHex = sourceRow.BarvaHex
            };
            _dbContext.CiselnikHarmonogramTypu.Add(clone);
            mapBySourceId[sourceRow.Id] = clone;
            clonedRows.Add(clone);
        }
        _dbContext.SaveChanges();

        return new HarmonogramSchemaCloneResult(sourceSchema, newSchema, mapBySourceId, clonedRows);
    }

    private void ActivateClonedHarmonogramSchema(HarmonogramSablonaEntity newSchema)
    {
        var activeSchemas = _dbContext.HarmonogramSablony
            .Where(x => x.IsAktivni && x.Verze != newSchema.Verze)
            .ToList();
        foreach (var schema in activeSchemas)
        {
            schema.IsAktivni = false;
        }

        newSchema.IsAktivni = true;
    }

    private void FinalizeClonedHarmonogramSchema(HarmonogramSablonaEntity newSchema, bool normalizeRows = true)
    {
        if (normalizeRows)
        {
            NormalizeHarmonogramSchemaRows(newSchema.Verze);
        }

        ActivateClonedHarmonogramSchema(newSchema);
    }

    private void NormalizeHarmonogramSchemaRows(int schemaVersion)
    {
        var rows = _dbContext.CiselnikHarmonogramTypu
            .Where(x => x.SablonaVerze == schemaVersion)
            .OrderBy(x => x.KrokPoradi)
            .ThenBy(x => x.JeZpozdeni)
            .ThenBy(x => x.Id)
            .ToList();

        var durationRows = rows
            .Where(x => !x.JeZpozdeni)
            .OrderBy(x => x.KrokPoradi)
            .ThenBy(x => x.Id)
            .ToList();

        var durationKeySet = durationRows.Select(x => x.KrokKey).ToHashSet();
        var orphanDelayRows = rows.Where(x => x.JeZpozdeni && !durationKeySet.Contains(x.KrokKey)).ToList();
        if (orphanDelayRows.Count > 0)
        {
            _dbContext.CiselnikHarmonogramTypu.RemoveRange(orphanDelayRows);
        }

        for (var index = 0; index < durationRows.Count; index += 1)
        {
            var order = index + 1;
            var duration = durationRows[index];
            duration.KrokPoradi = order;
            duration.Hodnota = order;
            duration.BarvaHex = NormalizeHexColor(duration.BarvaHex, ResolveDefaultStepColor(order));

            var delayRows = rows
                .Where(x => x.JeZpozdeni && x.KrokKey == duration.KrokKey)
                .OrderBy(x => x.Id)
                .ToList();
            foreach (var delay in delayRows)
            {
                delay.KrokPoradi = order;
                delay.Hodnota = 100 + order;
                delay.BarvaHex = null;
            }

            if (delayRows.Count == 0)
            {
                var fallbackDelayCode = BuildUniqueDelayCode($"{duration.Kod}_DELAY", rows.Select(x => x.Kod));
                _dbContext.CiselnikHarmonogramTypu.Add(new HarmonogramTypEntity
                {
                    Kod = fallbackDelayCode,
                    Nazev = $"{duration.Nazev} - skutečnost",
                    Hodnota = 100 + order,
                    IsLocked = true,
                    SablonaVerze = schemaVersion,
                    KrokKey = duration.KrokKey,
                    KrokPoradi = order,
                    JeZpozdeni = true,
                    BarvaHex = null
                });
            }
        }
    }

    private static string BuildUniqueDelayCode(string baseCode, IEnumerable<string?> usedCodes)
    {
        var codeBase = string.IsNullOrWhiteSpace(baseCode) ? "STEP_DELAY" : baseCode.Trim();
        var used = new HashSet<string>(usedCodes.Where(x => !string.IsNullOrWhiteSpace(x))!.Select(x => x!.Trim()), Ci);
        if (!used.Contains(codeBase))
        {
            return codeBase;
        }

        var index = 2;
        while (used.Contains($"{codeBase}_{index}"))
        {
            index += 1;
        }

        return $"{codeBase}_{index}";
    }

    private void SaveProjectStatusRow(SaveCiselnikRowCommand command)
    {
        if (command.Id.HasValue)
        {
            var row = _dbContext.CiselnikStavuProjektu.First(x => x.Id == command.Id.Value);
            row.Kod = command.Kod.Trim();
            row.Nazev = command.Nazev.Trim();
            row.IsLocked = command.IsLocked;
            return;
        }

        _dbContext.CiselnikStavuProjektu.Add(new CiselnikStavuProjektuEntity
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
            var row = _dbContext.CiselnikStavuUkolu.First(x => x.Id == command.Id.Value);
            row.Kod = command.Kod.Trim();
            row.Nazev = command.Nazev.Trim();
            row.IsLocked = command.IsLocked;
            row.IsFinal = isFinal;
            return;
        }

        _dbContext.CiselnikStavuUkolu.Add(new CiselnikStavuUkoluEntity
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
            _dbContext.CiselnikKategoriiZaznamu,
            (row, cmd) =>
            {
                row.Kod = cmd.Kod.Trim();
                row.Nazev = cmd.Nazev.Trim();
                row.IsLocked = cmd.IsLocked;
            },
            command => new CiselnikKategoriiZaznamuEntity
            {
                Kod = command.Kod.Trim(),
                Nazev = command.Nazev.Trim(),
                IsLocked = command.IsLocked
            });

    private void SaveTypUkoluRow(SaveCiselnikRowCommand command)
        => UpsertSimpleCiselnik(
            command,
            _dbContext.CiselnikTypuUkolu,
            (row, cmd) =>
            {
                row.Kod = cmd.Kod.Trim();
                row.Nazev = cmd.Nazev.Trim();
                row.IsLocked = cmd.IsLocked;
            },
            command => new CiselnikTypuUkoluEntity
            {
                Kod = command.Kod.Trim(),
                Nazev = command.Nazev.Trim(),
                IsLocked = command.IsLocked
            });

    private void SaveTypExternihoOdkazuRow(SaveCiselnikRowCommand command)
        => UpsertSimpleCiselnik(
            command,
            _dbContext.CiselnikTypuExternichOdkazu,
            (row, cmd) =>
            {
                row.Kod = cmd.Kod.Trim();
                row.Nazev = cmd.Nazev.Trim();
                row.IsLocked = cmd.IsLocked;
            },
            command => new CiselnikTypuExternichOdkazuEntity
            {
                Kod = command.Kod.Trim(),
                Nazev = command.Nazev.Trim(),
                IsLocked = command.IsLocked
            });

    private void SaveRoleProjektuRow(SaveCiselnikRowCommand command)
        => UpsertSimpleCiselnik(
            command,
            _dbContext.CiselnikRoliProjektu,
            (row, cmd) =>
            {
                row.Kod = cmd.Kod.Trim();
                row.Nazev = cmd.Nazev.Trim();
                row.IsLocked = cmd.IsLocked;
            },
            command => new CiselnikRoliProjektuEntity
            {
                Kod = command.Kod.Trim(),
                Nazev = command.Nazev.Trim(),
                IsLocked = command.IsLocked
            });

    private void SaveStavUcastiRow(SaveCiselnikRowCommand command)
        => UpsertSimpleCiselnik(
            command,
            _dbContext.CiselnikStavuUcasti,
            (row, cmd) =>
            {
                row.Kod = cmd.Kod.Trim();
                row.Nazev = cmd.Nazev.Trim();
                row.IsLocked = cmd.IsLocked;
            },
            command => new CiselnikStavuUcastiEntity
            {
                Kod = command.Kod.Trim(),
                Nazev = command.Nazev.Trim(),
                IsLocked = command.IsLocked
            });

    private void SaveOrganizaceRow(SaveCiselnikRowCommand command)
        => UpsertSimpleCiselnik(
            command,
            _dbContext.CiselnikOrganizace,
            (row, cmd) =>
            {
                row.Kod = cmd.Kod.Trim();
                row.Nazev = cmd.Nazev.Trim();
                row.IsLocked = cmd.IsLocked;
            },
            command => new CiselnikOrganizaceEntity
            {
                Kod = command.Kod.Trim(),
                Nazev = command.Nazev.Trim(),
                IsLocked = command.IsLocked
            });

    private void SaveOrgUnitRow(SaveCiselnikRowCommand command)
        => UpsertSimpleCiselnik(
            command,
            _dbContext.CiselnikOrganizacniCelky,
            (row, cmd) =>
            {
                row.Kod = cmd.Kod.Trim();
                row.Nazev = cmd.Nazev.Trim();
                row.IsLocked = cmd.IsLocked;
            },
            command => new CiselnikOrganizacniCelekEntity
            {
                Kod = command.Kod.Trim(),
                Nazev = command.Nazev.Trim(),
                IsLocked = command.IsLocked
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

        var ownerRaw = command.HodnotyNavic
            .Select(x => x?.Trim())
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        if (string.IsNullOrWhiteSpace(ownerRaw))
        {
            throw new InvalidOperationException("Vyberte vedoucího subsystému.");
        }

        if (!int.TryParse(ownerRaw, out var vedouciOsobaId))
        {
            throw new InvalidOperationException("Neplatná hodnota vedoucího subsystému.");
        }

        var existsLeader = _dbContext.Osoby.AsNoTracking().Any(x => x.Id == vedouciOsobaId);
        if (!existsLeader)
        {
            throw new InvalidOperationException("Vybraný vedoucí subsystému neexistuje.");
        }

        var duplicateCodeExists = _dbContext.Subsystemy.AsNoTracking().Any(x =>
            x.Kod == kod && (!command.Id.HasValue || x.Id != command.Id.Value));
        if (duplicateCodeExists)
        {
            throw new InvalidOperationException($"Subsystém s kódem '{kod}' už existuje.");
        }

        if (command.Id.HasValue)
        {
            var row = _dbContext.Subsystemy.FirstOrDefault(x => x.Id == command.Id.Value)
                ?? throw new InvalidOperationException($"Subsystém {command.Id.Value} nebyl nalezen.");
            row.Kod = kod;
            row.Nazev = nazev;
            row.VedouciOsobaId = vedouciOsobaId;
            return;
        }

        _dbContext.Subsystemy.Add(new SubsystemEntity
        {
            Kod = kod,
            Nazev = nazev,
            VedouciOsobaId = vedouciOsobaId
        });
    }

    private void SaveStavJednaniRow(SaveCiselnikRowCommand command)
        => UpsertSimpleCiselnik(
            command,
            _dbContext.CiselnikStavuJednani,
            (row, cmd) =>
            {
                row.Kod = cmd.Kod.Trim();
                row.Nazev = cmd.Nazev.Trim();
                row.IsLocked = cmd.IsLocked;
            },
            command => new CiselnikStavuJednaniEntity
            {
                Kod = command.Kod.Trim(),
                Nazev = command.Nazev.Trim(),
                IsLocked = command.IsLocked
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
            var row = _dbContext.CiselnikVyzvy.First(x => x.Id == command.Id.Value);
            row.Kod = command.Kod.Trim();
            row.Nazev = command.Nazev.Trim();
            row.Rok = year;
            row.IsLocked = command.IsLocked;
            return;
        }

        _dbContext.CiselnikVyzvy.Add(new CiselnikVyzvaEntity
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
        _dbContext.AuthzAuditLog.Add(new AuthzAuditLogEntity
        {
            ActorOsobaId = actorOsobaId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            OldValue = oldValue,
            NewValue = newValue,
            CreatedAt = DateTime.UtcNow
        });
        _dbContext.SaveChanges();
    }

    private int ResolveAsProfileToOsobaId(string? asProfile)
    {
        if (string.IsNullOrWhiteSpace(asProfile))
        {
            var firstId = _dbContext.Osoby.AsNoTracking().OrderBy(x => x.Id).Select(x => (int?)x.Id).FirstOrDefault();
            if (!firstId.HasValue)
            {
                throw new InvalidOperationException("Tabulka osoby je prázdná.");
            }

            return firstId.Value;
        }

        if (int.TryParse(asProfile, out var id))
        {
            return id;
        }

        if (Guid.TryParse(asProfile, out var guid))
        {
            var guidMatch = _dbContext.Osoby.AsNoTracking().Where(x => x.GuidAd == guid).Select(x => (int?)x.Id).FirstOrDefault();
            if (guidMatch.HasValue)
            {
                return guidMatch.Value;
            }
        }

        var normalized = _textNormalizer.Normalize(asProfile);
        var normalizedLoginCandidates = BuildNormalizedLoginCandidates(asProfile);
        var people = _dbContext.Osoby.AsNoTracking().ToList();
        var match = people.FirstOrDefault(x =>
            _personIdentityMatcher.NameEquals(x.Titul, x.Jmeno, x.Prijmeni, normalized) ||
            _personIdentityMatcher.EmailEquals(x.Email, normalized) ||
            LoginEquals(x.AdLogin, normalizedLoginCandidates));
        if (match is not null)
        {
            return match.Id;
        }

        var containsMatch = people.FirstOrDefault(x =>
            _personIdentityMatcher.NameContains(x.Titul, x.Jmeno, x.Prijmeni, normalized) ||
            _personIdentityMatcher.EmailContains(x.Email, normalized) ||
            LoginContains(x.AdLogin, normalizedLoginCandidates));
        if (containsMatch is not null)
        {
            return containsMatch.Id;
        }

        throw new InvalidOperationException($"Uživatel '{asProfile}' nebyl nalezen.");
    }

    private string BuildDisplayName(string? titul, string jmeno, string prijmeni, int id)
    {
        var displayName = _personIdentityMatcher.BuildPersonName(titul, jmeno, prijmeni);
        return string.IsNullOrWhiteSpace(displayName) ? $"Uživatel #{id}" : displayName;
    }

    private string BuildDisplayNameFromOsoba(OsobaEntity? osoba)
    {
        if (osoba is null)
        {
            return "-";
        }

        return BuildDisplayName(osoba.Titul, osoba.Jmeno, osoba.Prijmeni, osoba.Id);
    }

    private string BuildInlinePersonLabel(string? titul, string jmeno, string prijmeni, string? email, int id)
    {
        var displayName = BuildDisplayName(titul, jmeno, prijmeni, id);
        var normalizedEmail = _textNormalizer.NormalizeEmail(email);
        return string.IsNullOrWhiteSpace(normalizedEmail)
            ? displayName
            : $"{displayName} <{normalizedEmail}>";
    }

    private string BuildInlinePersonLabelFromOsoba(OsobaEntity? osoba)
    {
        if (osoba is null)
        {
            return "-";
        }

        return BuildInlinePersonLabel(osoba.Titul, osoba.Jmeno, osoba.Prijmeni, osoba.Email, osoba.Id);
    }

    private static string? ExtractServiceDeskTicketId(string? externalNumber)
    {
        if (string.IsNullOrWhiteSpace(externalNumber))
        {
            return null;
        }

        var digits = new string(externalNumber.Where(char.IsDigit).ToArray());
        return string.IsNullOrWhiteSpace(digits) ? null : digits;
    }

    private static string? BuildServiceDeskUrl(string? ticketId)
    {
        if (string.IsNullOrWhiteSpace(ticketId))
        {
            return null;
        }

        return $"https://servicedesk.fis.acr/Hotline/Ticket/Details/{ticketId}";
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

    private static HashSet<string> BuildNormalizedLoginCandidates(string? rawLogin)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(rawLogin))
        {
            return candidates;
        }

        void add(string? value)
        {
            var normalized = NormalizeAdLogin(value);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                candidates.Add(normalized);
            }
        }

        var trimmed = rawLogin.Trim();
        add(trimmed);

        if (trimmed.Contains('\\'))
        {
            var slashParts = trimmed.Split('\\', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            add(slashParts.LastOrDefault());
        }

        if (trimmed.Contains('@'))
        {
            add(trimmed.Split('@', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault());
        }

        return candidates;
    }

    private static bool LoginEquals(string? storedLogin, IReadOnlyCollection<string> candidates)
    {
        if (candidates.Count == 0 || string.IsNullOrWhiteSpace(storedLogin))
        {
            return false;
        }

        return candidates.Contains(storedLogin.Trim().ToLowerInvariant());
    }

    private static bool LoginContains(string? storedLogin, IReadOnlyCollection<string> candidates)
    {
        if (candidates.Count == 0 || string.IsNullOrWhiteSpace(storedLogin))
        {
            return false;
        }

        var normalized = storedLogin.Trim().ToLowerInvariant();
        return candidates.Any(candidate => normalized.Contains(candidate, StringComparison.Ordinal));
    }

}
