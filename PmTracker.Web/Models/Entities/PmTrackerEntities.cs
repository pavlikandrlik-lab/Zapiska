using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Models.Entities;

public sealed class CiselnikStavuProjektuEntity
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Nazev { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
    public ICollection<ProjektEntity> Projekty { get; set; } = new List<ProjektEntity>();
}

public sealed class CiselnikStavuUkoluEntity
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Nazev { get; set; } = string.Empty;
    public bool IsFinal { get; set; }
    public bool IsLocked { get; set; }
}

public sealed class CiselnikKategoriiZaznamuEntity
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Nazev { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
}

public sealed class CiselnikTypuUkoluEntity
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Nazev { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
}

public sealed class CiselnikTypuExternichOdkazuEntity
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Nazev { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
}

public sealed class CiselnikRoliProjektuEntity
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Nazev { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
    public int? AuthzRoleId { get; set; }
}

public sealed class CiselnikRoleSubsystemuEntity
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Nazev { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
    public int? AuthzRoleId { get; set; }
}

public sealed class CiselnikStavuUcastiEntity
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Nazev { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
}

public sealed class CiselnikOrganizaceEntity
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Nazev { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
}

public sealed class CiselnikOrganizacniCelekEntity
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Nazev { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
}

public sealed class CiselnikStavuJednaniEntity
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Nazev { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
}

public sealed class HarmonogramSablonaEntity
{
    public int Verze { get; set; }
    public string DelayBarvaHex { get; set; } = "#DC2626";
    public bool IsAktivni { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? CreatedBy { get; set; }
}

public sealed class HarmonogramTypEntity
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Nazev { get; set; } = string.Empty;
    public int Hodnota { get; set; }
    public bool IsLocked { get; set; }
    public int SablonaVerze { get; set; }
    public Guid KrokKey { get; set; }
    public int KrokPoradi { get; set; }
    public bool JeZpozdeni { get; set; }
    public string? BarvaHex { get; set; }
}

public sealed class SubsystemEntity
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Nazev { get; set; } = string.Empty;
}

public sealed class OsobaEntity
{
    public int Id { get; set; }
    public string Jmeno { get; set; } = string.Empty;
    public string Prijmeni { get; set; } = string.Empty;
    public string? Titul { get; set; }
    public string? Email { get; set; }
    public string? AdLogin { get; set; }
    public int? OrganizacniCelekId { get; set; }
    public int OrganizaceId { get; set; }
    public Guid? GuidAd { get; set; }
    public bool LocationLocked { get; set; }
}

public sealed class ProjektEntity
{
    public int Id { get; set; }
    public string Zkratka { get; set; } = string.Empty;
    public string CelyNazev { get; set; } = string.Empty;
    public int StavId { get; set; }
    public bool PouzivatIdentJednani { get; set; }
    public string? MistoPlneni { get; set; }
    public string? CisloRamcoveSmlouvy { get; set; }
}

public sealed class ObsazeniProjektuEntity
{
    public int Id { get; set; }
    public int ProjektId { get; set; }
    public int OsobaId { get; set; }
    public int RoleId { get; set; }
    public DateTime DatumPrirazeni { get; set; }
    public DateTime? DatumOdebrani { get; set; }
}

public sealed class ProjektSubsystemEntity
{
    public int Id { get; set; }
    public int ProjektId { get; set; }
    public int SubsystemId { get; set; }
    public int Poradi { get; set; }
    public DateTime DatumPrirazeni { get; set; }
    public DateTime? DatumOdebrani { get; set; }
}

public sealed class ObsazeniSubsystemuProjektuEntity
{
    public int Id { get; set; }
    public int ProjektSubsystemId { get; set; }
    public int OsobaId { get; set; }
    public int RoleSubsystemuId { get; set; }
    public DateTime DatumPrirazeni { get; set; }
    public DateTime? DatumOdebrani { get; set; }
}

public sealed class ProjektovyZaznamEntity
{
    public int Id { get; set; }
    public int ProjektId { get; set; }
    public int KategorieId { get; set; }
    public int? AktualniTypUkoluId { get; set; }
    public int? StavUkoluId { get; set; }
    public int CisloZaznamu { get; set; }
    public string? CisloViditelne { get; set; }
    public byte CisloViditelneTyp { get; set; }
    public int CisloViditelneA { get; set; }
    public int CisloViditelneB { get; set; }
    public int? CisloJednaniZdrojId { get; set; }
    public string Nazev { get; set; } = string.Empty;
    public string? Cil { get; set; }
    public string? Popis { get; set; }
    public int VlastnikId { get; set; }
    public DateTime DatumZalozeni { get; set; }
    public DateTime DatumUkonceni { get; set; }
    public int SubsystemId { get; set; }
    public int HarmonogramSablonaVerze { get; set; }
}

public sealed class ZaznamNavrhEntity
{
    public int Id { get; set; }
    public int ProjektId { get; set; }
    public int? ZaznamId { get; set; }
    public int SubsystemId { get; set; }
    public string TypNavrhu { get; set; } = string.Empty;
    public string Stav { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public int CreatedByOsobaId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? DecidedByOsobaId { get; set; }
    public DateTime? DecidedAt { get; set; }
    public int? ApprovedRecordId { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public sealed class ZaznamPriorityUzivateleEntity
{
    public int ZaznamId { get; set; }
    public int OsobaId { get; set; }
    public int Score { get; set; }
    public DateTime ComputedAt { get; set; }
    public int RoleWeight { get; set; }
    public int DeadlineSignal { get; set; }
    public int MilestoneSignal { get; set; }
}

public sealed class ZaznamPriorityRebuildStateEntity
{
    public int Id { get; set; }
    public DateTime? LastFullRebuildAt { get; set; }
    public string LastFullRebuildStatus { get; set; } = string.Empty;
    public long? LastFullRebuildDurationMs { get; set; }
    public int? LastFullRebuildTaskCount { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class ZaznamHistorieZmenTypuEntity
{
    public int Id { get; set; }
    public int ZaznamId { get; set; }
    public int PuvodniTypId { get; set; }
    public int NovyTypId { get; set; }
    public DateTime DatumZmeny { get; set; }
    public int ZmenilOsobaId { get; set; }
}

public sealed class ZaznamHistorieTerminuEntity
{
    public int Id { get; set; }
    public int ZaznamId { get; set; }
    public DateTime PuvodniDatum { get; set; }
    public DateTime NoveDatum { get; set; }
    public DateTime DatumZmeny { get; set; }
    public string Duvod { get; set; } = string.Empty;
}

public sealed class ZaznamHistorieVlastnikEntity
{
    public int Id { get; set; }
    public int ZaznamId { get; set; }
    public int PuvodniVlastnik { get; set; }
    public int NovyVlastnik { get; set; }
    public DateTime DatumZmeny { get; set; }
}

public sealed class ZaznamHistorieSubsystemEntity
{
    public int Id { get; set; }
    public int ZaznamId { get; set; }
    public int PuvodniSubsystem { get; set; }
    public int NovySubsystem { get; set; }
    public DateTime DatumZmeny { get; set; }
}

public sealed class ZaznamHistorieStavuZaznamuEntity
{
    public int Id { get; set; }
    public int ZaznamId { get; set; }
    public int PuvodniStav { get; set; }
    public int NovyStav { get; set; }
    public DateTime DatumZmeny { get; set; }
}

public sealed class ZaznamHistorieStavuProjektuEntity
{
    public int Id { get; set; }
    public int ZaznamId { get; set; }
    public int PuvodniStav { get; set; }
    public int NovyStav { get; set; }
    public DateTime DatumZmeny { get; set; }
}

public sealed class ZaznamExterniOdkazEntity
{
    public int Id { get; set; }
    public int ZaznamId { get; set; }
    public int TypOdkazuId { get; set; }
    public string Cislo { get; set; } = string.Empty;
    public decimal? PredpokladanaCena { get; set; }
    public DateTime? DatumObjednani { get; set; }
    public DateTime? PlanDodani { get; set; }
    public DateTime? DatumDodani { get; set; }
    public DateTime? DatumPrevzeti { get; set; }
    public int? VyzvaId { get; set; }
    public bool ZaradidDoVyzvy { get; set; }
    public DateTime? LastHarvestedAt { get; set; }

    // Fingerprint — spec §5.2. Primary = HOT_ZAZNAMY.datum (last-modified),
    // secondary = MAX(HOT_VYJADRENI.id) + COUNT(*). Slouží pro fingerprint skip
    // v SdHarvestService: když se od posledního harvestu nic nezměnilo, drill preskočit.
    public DateTime? LastKnownHotZaznamDatum { get; set; }
    public long? LastKnownMaxVyjadreniId { get; set; }
    public int? LastKnownVyjadreniCount { get; set; }
}

public sealed class ZaznamSpolupraceEntity
{
    public int ZaznamId { get; set; }
    public int OsobaId { get; set; }
}

public sealed class JednaniEntity
{
    public int Id { get; set; }
    public int ProjektId { get; set; }
    public int CisloJednani { get; set; }
    public DateTime DatumPlanovane { get; set; }
    public TimeOnly CasZacatek { get; set; }
    public string? Misto { get; set; }
    public int StavJednaniId { get; set; }
    public int? UzamklOsobaId { get; set; }
}

public sealed class UcastEntity
{
    public int JednaniId { get; set; }
    public int OsobaId { get; set; }
    public int StavUcastiId { get; set; }
}

public sealed class VyjadreniEntity
{
    public int Id { get; set; }
    public int ZaznamId { get; set; }
    public int JednaniId { get; set; }
    public int AutorOsobaId { get; set; }
    public string TextVyjadreni { get; set; } = string.Empty;
    public DateTime DatumVyjadreni { get; set; }
}

public sealed class ZaznamHarmonogramHodnotaEntity
{
    public int Id { get; set; }
    public int ZaznamId { get; set; }
    public int TypId { get; set; }
    public int HodnotaInt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class AuthzSuperadminEntity
{
    public int OsobaId { get; set; }
    public string? Poznamka { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? CreatedBy { get; set; }
}

public sealed class AuthzPermissionCategoryEntity
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Nazev { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

public sealed class AuthzPermissionEntity
{
    public int Id { get; set; }
    public string Klic { get; set; } = string.Empty;
    public string Nazev { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public PermissionScopeLevel ScopeLevel { get; set; } = PermissionScopeLevel.Project;
    public bool IsActive { get; set; }
    public bool IsSystem { get; set; }
}

public sealed class AuthzRoleEntity
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Nazev { get; set; } = string.Empty;
    public string? Popis { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; }
    public RoleScope Scope { get; set; } = RoleScope.Global;
}

public sealed class AuthzRolePermissionEntity
{
    public int Id { get; set; }
    public int RoleId { get; set; }
    public int PermissionId { get; set; }
    public ScopeMode ScopeMode { get; set; } = ScopeMode.All;
    public bool IsAllowed { get; set; }
}

public sealed class AuthzRolePermissionProjectEntity
{
    public int RolePermissionId { get; set; }
    public int ProjektId { get; set; }
}

public sealed class AuthzUserRoleEntity
{
    public int Id { get; set; }
    public int OsobaId { get; set; }
    public int RoleId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class AuthzAuditLogEntity
{
    public long Id { get; set; }
    public int? ActorOsobaId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class SearchReindexCheckpointEntity
{
    public int Id { get; set; }
    public long LastProcessedAuditId { get; set; }
    public DateTime? LastProcessedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
