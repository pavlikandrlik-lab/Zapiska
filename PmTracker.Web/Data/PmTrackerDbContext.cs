using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Data;

public sealed class PmTrackerDbContext : DbContext
{
    public PmTrackerDbContext(DbContextOptions<PmTrackerDbContext> options)
        : base(options)
    {
    }

    public DbSet<CiselnikStavuProjektuEntity> CiselnikStavuProjektu => Set<CiselnikStavuProjektuEntity>();
    public DbSet<CiselnikStavuUkoluEntity> CiselnikStavuUkolu => Set<CiselnikStavuUkoluEntity>();
    public DbSet<CiselnikKategoriiZaznamuEntity> CiselnikKategoriiZaznamu => Set<CiselnikKategoriiZaznamuEntity>();
    public DbSet<CiselnikTypuUkoluEntity> CiselnikTypuUkolu => Set<CiselnikTypuUkoluEntity>();
    public DbSet<CiselnikTypuExternichOdkazuEntity> CiselnikTypuExternichOdkazu => Set<CiselnikTypuExternichOdkazuEntity>();
    public DbSet<CiselnikRoliProjektuEntity> CiselnikRoliProjektu => Set<CiselnikRoliProjektuEntity>();
    public DbSet<CiselnikRoleSubsystemuEntity> CiselnikRoliSubsystemu => Set<CiselnikRoleSubsystemuEntity>();
    public DbSet<CiselnikStavuUcastiEntity> CiselnikStavuUcasti => Set<CiselnikStavuUcastiEntity>();
    public DbSet<CiselnikOrganizaceEntity> CiselnikOrganizace => Set<CiselnikOrganizaceEntity>();
    public DbSet<CiselnikOrganizacniCelekEntity> CiselnikOrganizacniCelky => Set<CiselnikOrganizacniCelekEntity>();
    public DbSet<VyzvaEntity> Vyzvy => Set<VyzvaEntity>();
    public DbSet<VyzvaHistorieStavuEntity> VyzvaHistorieStavu => Set<VyzvaHistorieStavuEntity>();
    public DbSet<CiselnikStavuJednaniEntity> CiselnikStavuJednani => Set<CiselnikStavuJednaniEntity>();
    public DbSet<SubsystemEntity> Subsystemy => Set<SubsystemEntity>();
    public DbSet<OsobaEntity> Osoby => Set<OsobaEntity>();
    public DbSet<ProjektEntity> Projekty => Set<ProjektEntity>();
    public DbSet<ObsazeniProjektuEntity> ObsazeniProjektu => Set<ObsazeniProjektuEntity>();
    public DbSet<ProjektSubsystemEntity> ProjektSubsystemy => Set<ProjektSubsystemEntity>();
    public DbSet<ObsazeniSubsystemuProjektuEntity> ObsazeniSubsystemuProjektu => Set<ObsazeniSubsystemuProjektuEntity>();
    public DbSet<ProjektovyZaznamEntity> ProjektoveZaznamy => Set<ProjektovyZaznamEntity>();
    public DbSet<ZaznamNavrhEntity> ZaznamNavrhy => Set<ZaznamNavrhEntity>();
    public DbSet<ZaznamPriorityUzivateleEntity> ZaznamPriorityUzivatelu => Set<ZaznamPriorityUzivateleEntity>();
    public DbSet<ZaznamPriorityRebuildStateEntity> ZaznamPriorityRebuildState => Set<ZaznamPriorityRebuildStateEntity>();
    public DbSet<ZaznamHistorieZmenTypuEntity> ZaznamHistorieZmenTypu => Set<ZaznamHistorieZmenTypuEntity>();
    public DbSet<ZaznamHistorieTerminuEntity> ZaznamHistorieTerminu => Set<ZaznamHistorieTerminuEntity>();
    public DbSet<ZaznamHistorieVlastnikEntity> ZaznamHistorieVlastnik => Set<ZaznamHistorieVlastnikEntity>();
    public DbSet<ZaznamHistorieSubsystemEntity> ZaznamHistorieSubsystem => Set<ZaznamHistorieSubsystemEntity>();
    public DbSet<ZaznamHistorieStavuZaznamuEntity> ZaznamHistorieStavuZaznamu => Set<ZaznamHistorieStavuZaznamuEntity>();
    public DbSet<ZaznamHistorieStavuProjektuEntity> ZaznamHistorieStavuProjektu => Set<ZaznamHistorieStavuProjektuEntity>();
    public DbSet<ZaznamExterniOdkazEntity> ZaznamExterniOdkazy => Set<ZaznamExterniOdkazEntity>();
    public DbSet<ZaznamSpolupraceEntity> ZaznamSpoluprace => Set<ZaznamSpolupraceEntity>();
    public DbSet<JednaniEntity> Jednani => Set<JednaniEntity>();
    public DbSet<UcastEntity> Ucast => Set<UcastEntity>();
    public DbSet<VyjadreniEntity> Vyjadreni => Set<VyjadreniEntity>();
    public DbSet<ZaznamHarmonogramVyjadreniVazbaEntity> VyjadreniVazby
        => Set<ZaznamHarmonogramVyjadreniVazbaEntity>();
    public DbSet<ZaznamHarmonogramKrokEntity> ZaznamHarmonogramKroky => Set<ZaznamHarmonogramKrokEntity>();

    public DbSet<AuthzSuperadminEntity> AuthzSuperadmins => Set<AuthzSuperadminEntity>();
    public DbSet<AuthzPermissionCategoryEntity> AuthzPermissionCategories => Set<AuthzPermissionCategoryEntity>();
    public DbSet<AuthzPermissionEntity> AuthzPermissions => Set<AuthzPermissionEntity>();
    public DbSet<AuthzRoleEntity> AuthzRoles => Set<AuthzRoleEntity>();
    public DbSet<AuthzRolePermissionEntity> AuthzRolePermissions => Set<AuthzRolePermissionEntity>();
    public DbSet<AuthzRolePermissionProjectEntity> AuthzRolePermissionProjects => Set<AuthzRolePermissionProjectEntity>();
    public DbSet<AuthzUserRoleEntity> AuthzUserRoles => Set<AuthzUserRoleEntity>();
    public DbSet<AuthzAuditLogEntity> AuthzAuditLog => Set<AuthzAuditLogEntity>();
    public DbSet<SearchReindexCheckpointEntity> SearchReindexCheckpoint => Set<SearchReindexCheckpointEntity>();
    public DbSet<AdSyncSettingsEntity> AdSyncSettings => Set<AdSyncSettingsEntity>();
    public DbSet<SdActiveSyncSettingsEntity> SdActiveSyncSettings => Set<SdActiveSyncSettingsEntity>();
    public DbSet<SdArchiveSyncSettingsEntity> SdArchiveSyncSettings => Set<SdArchiveSyncSettingsEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PmTrackerDbContext).Assembly);
    }
}
