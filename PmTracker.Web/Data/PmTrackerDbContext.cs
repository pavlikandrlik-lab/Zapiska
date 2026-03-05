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
    public DbSet<CiselnikVyzvaEntity> CiselnikVyzvy => Set<CiselnikVyzvaEntity>();
    public DbSet<CiselnikStavuJednaniEntity> CiselnikStavuJednani => Set<CiselnikStavuJednaniEntity>();
    public DbSet<HarmonogramSablonaEntity> HarmonogramSablony => Set<HarmonogramSablonaEntity>();
    public DbSet<HarmonogramTypEntity> CiselnikHarmonogramTypu => Set<HarmonogramTypEntity>();
    public DbSet<SubsystemEntity> Subsystemy => Set<SubsystemEntity>();
    public DbSet<OsobaEntity> Osoby => Set<OsobaEntity>();
    public DbSet<ProjektEntity> Projekty => Set<ProjektEntity>();
    public DbSet<ObsazeniProjektuEntity> ObsazeniProjektu => Set<ObsazeniProjektuEntity>();
    public DbSet<ProjektSubsystemEntity> ProjektSubsystemy => Set<ProjektSubsystemEntity>();
    public DbSet<ObsazeniSubsystemuProjektuEntity> ObsazeniSubsystemuProjektu => Set<ObsazeniSubsystemuProjektuEntity>();
    public DbSet<ProjektovyZaznamEntity> ProjektoveZaznamy => Set<ProjektovyZaznamEntity>();
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
    public DbSet<ZaznamHarmonogramHodnotaEntity> ZaznamHarmonogramHodnoty => Set<ZaznamHarmonogramHodnotaEntity>();

    public DbSet<AuthzSuperadminEntity> AuthzSuperadmins => Set<AuthzSuperadminEntity>();
    public DbSet<AuthzPermissionCategoryEntity> AuthzPermissionCategories => Set<AuthzPermissionCategoryEntity>();
    public DbSet<AuthzPermissionEntity> AuthzPermissions => Set<AuthzPermissionEntity>();
    public DbSet<AuthzRoleEntity> AuthzRoles => Set<AuthzRoleEntity>();
    public DbSet<AuthzRolePermissionEntity> AuthzRolePermissions => Set<AuthzRolePermissionEntity>();
    public DbSet<AuthzRolePermissionProjectEntity> AuthzRolePermissionProjects => Set<AuthzRolePermissionProjectEntity>();
    public DbSet<AuthzUserRoleEntity> AuthzUserRoles => Set<AuthzUserRoleEntity>();
    public DbSet<AuthzAuditLogEntity> AuthzAuditLog => Set<AuthzAuditLogEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CiselnikStavuProjektuEntity>(entity =>
        {
            entity.ToTable("ciselnik_stavu_projektu");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Kod).HasColumnName("kod");
            entity.Property(x => x.Nazev).HasColumnName("nazev");
            entity.Property(x => x.IsLocked).HasColumnName("is_locked");

            // Explicitly map dependent FK to projekty.stav_id to avoid shadow FK convention.
            entity.HasMany(x => x.Projekty)
                .WithOne()
                .HasForeignKey(x => x.StavId)
                .HasPrincipalKey(x => x.Id);
        });

        modelBuilder.Entity<CiselnikStavuUkoluEntity>(entity =>
        {
            entity.ToTable("ciselnik_stavu_ukolu");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Kod).HasColumnName("kod");
            entity.Property(x => x.Nazev).HasColumnName("nazev");
            entity.Property(x => x.IsFinal).HasColumnName("is_final");
            entity.Property(x => x.IsLocked).HasColumnName("is_locked");
        });

        modelBuilder.Entity<CiselnikKategoriiZaznamuEntity>(entity =>
        {
            entity.ToTable("ciselnik_kategorii_zaznamu");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Kod).HasColumnName("kod");
            entity.Property(x => x.Nazev).HasColumnName("nazev");
            entity.Property(x => x.IsLocked).HasColumnName("is_locked");
        });

        modelBuilder.Entity<CiselnikTypuUkoluEntity>(entity =>
        {
            entity.ToTable("ciselnik_typu_ukolu");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Kod).HasColumnName("kod");
            entity.Property(x => x.Nazev).HasColumnName("nazev");
            entity.Property(x => x.IsLocked).HasColumnName("is_locked");
        });

        modelBuilder.Entity<CiselnikTypuExternichOdkazuEntity>(entity =>
        {
            entity.ToTable("ciselnik_typu_externich_odkazu");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Kod).HasColumnName("kod");
            entity.Property(x => x.Nazev).HasColumnName("nazev");
            entity.Property(x => x.IsLocked).HasColumnName("is_locked");
        });

        modelBuilder.Entity<CiselnikRoliProjektuEntity>(entity =>
        {
            entity.ToTable("ciselnik_roli_projektu");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Kod).HasColumnName("kod");
            entity.Property(x => x.Nazev).HasColumnName("nazev");
            entity.Property(x => x.IsLocked).HasColumnName("is_locked");
        });

        modelBuilder.Entity<CiselnikRoleSubsystemuEntity>(entity =>
        {
            entity.ToTable("ciselnik_roli_subsystemu");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Kod).HasColumnName("kod");
            entity.Property(x => x.Nazev).HasColumnName("nazev");
            entity.Property(x => x.IsLocked).HasColumnName("is_locked");
        });

        modelBuilder.Entity<CiselnikStavuUcastiEntity>(entity =>
        {
            entity.ToTable("ciselnik_stavu_ucasti");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Kod).HasColumnName("kod");
            entity.Property(x => x.Nazev).HasColumnName("nazev");
            entity.Property(x => x.IsLocked).HasColumnName("is_locked");
        });

        modelBuilder.Entity<CiselnikOrganizaceEntity>(entity =>
        {
            entity.ToTable("ciselnik_organizace");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Kod).HasColumnName("kod");
            entity.Property(x => x.Nazev).HasColumnName("nazev");
            entity.Property(x => x.IsLocked).HasColumnName("is_locked");
        });

        modelBuilder.Entity<CiselnikOrganizacniCelekEntity>(entity =>
        {
            entity.ToTable("ciselnik_organizacni_celky");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Kod).HasColumnName("kod");
            entity.Property(x => x.Nazev).HasColumnName("nazev");
            entity.Property(x => x.IsLocked).HasColumnName("is_locked");
        });

        modelBuilder.Entity<CiselnikVyzvaEntity>(entity =>
        {
            entity.ToTable("ciselnik_vyzvy");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Kod).HasColumnName("kod");
            entity.Property(x => x.Nazev).HasColumnName("nazev");
            entity.Property(x => x.Rok).HasColumnName("rok");
            entity.Property(x => x.IsLocked).HasColumnName("is_locked");
        });

        modelBuilder.Entity<CiselnikStavuJednaniEntity>(entity =>
        {
            entity.ToTable("ciselnik_stavu_jednani");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Kod).HasColumnName("kod");
            entity.Property(x => x.Nazev).HasColumnName("nazev");
            entity.Property(x => x.IsLocked).HasColumnName("is_locked");
        });

        modelBuilder.Entity<HarmonogramSablonaEntity>(entity =>
        {
            entity.ToTable("harmonogram_sablony");
            entity.HasKey(x => x.Verze);
            entity.HasIndex(x => x.IsAktivni)
                .IsUnique()
                .HasFilter("[is_aktivni] = 1")
                .HasDatabaseName("UQ_harmonogram_sablony_aktivni");
            entity.Property(x => x.Verze).HasColumnName("verze");
            entity.Property(x => x.DelayBarvaHex).HasColumnName("delay_barva_hex");
            entity.Property(x => x.IsAktivni).HasColumnName("is_aktivni");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.CreatedBy).HasColumnName("created_by");
        });

        modelBuilder.Entity<HarmonogramTypEntity>(entity =>
        {
            entity.ToTable("ciselnik_harmonogram_typu");
            entity.HasKey(x => x.Id);
            entity.HasOne<HarmonogramSablonaEntity>()
                .WithMany()
                .HasForeignKey(x => x.SablonaVerze)
                .HasConstraintName("FK_ciselnik_harmonogram_typu_sablona");
            entity.HasIndex(x => new { x.SablonaVerze, x.Kod })
                .IsUnique()
                .HasDatabaseName("UQ_ciselnik_harmonogram_typu_sablona_kod");
            entity.HasIndex(x => new { x.SablonaVerze, x.KrokPoradi, x.JeZpozdeni })
                .IsUnique()
                .HasDatabaseName("UQ_ciselnik_harmonogram_typu_sablona_krok_zpozdeni");
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Kod).HasColumnName("kod");
            entity.Property(x => x.Nazev).HasColumnName("nazev");
            entity.Property(x => x.Hodnota).HasColumnName("hodnota");
            entity.Property(x => x.IsLocked).HasColumnName("is_locked");
            entity.Property(x => x.SablonaVerze).HasColumnName("sablona_verze");
            entity.Property(x => x.KrokKey).HasColumnName("krok_key");
            entity.Property(x => x.KrokPoradi).HasColumnName("krok_poradi");
            entity.Property(x => x.JeZpozdeni).HasColumnName("je_zpozdeni");
            entity.Property(x => x.BarvaHex).HasColumnName("barva_hex");
        });

        modelBuilder.Entity<SubsystemEntity>(entity =>
        {
            entity.ToTable("subsystemy");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Kod).HasColumnName("kód");
            entity.Property(x => x.Nazev).HasColumnName("nazev");
        });

        modelBuilder.Entity<OsobaEntity>(entity =>
        {
            entity.ToTable("osoby");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Jmeno).HasColumnName("jmeno");
            entity.Property(x => x.Prijmeni).HasColumnName("prijmeni");
            entity.Property(x => x.Titul).HasColumnName("titul");
            entity.Property(x => x.Email).HasColumnName("email");
            entity.Property(x => x.AdLogin).HasColumnName("ad_login");
            entity.Property(x => x.OrganizacniCelekId).HasColumnName("organizacni_celek_id");
            entity.Property(x => x.OrganizaceId).HasColumnName("organizace_id");
            entity.Property(x => x.GuidAd).HasColumnName("Guid_AD");
            entity.Property(x => x.LocationLocked).HasColumnName("location_locked");
        });

        modelBuilder.Entity<ProjektEntity>(entity =>
        {
            entity.ToTable("projekty");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Zkratka).HasColumnName("zkratka");
            entity.Property(x => x.CelyNazev).HasColumnName("cely_nazev");
            entity.Property(x => x.StavId).HasColumnName("stav_id");
            entity.Property(x => x.PouzivatIdentJednani).HasColumnName("pouzivat_ident_jednani");
        });

        modelBuilder.Entity<ObsazeniProjektuEntity>(entity =>
        {
            entity.ToTable("obsazeni_projektu");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ProjektId, x.OsobaId, x.RoleId })
                .IsUnique()
                .HasFilter("[datum_odebrani] IS NULL")
                .HasDatabaseName("UX_obsazeni_projektu_projekt_osoba_role_aktivni");
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.ProjektId).HasColumnName("projekt_id");
            entity.Property(x => x.OsobaId).HasColumnName("osoba_id");
            entity.Property(x => x.RoleId).HasColumnName("role_id");
            entity.Property(x => x.DatumPrirazeni).HasColumnName("datum_prirazeni");
            entity.Property(x => x.DatumOdebrani).HasColumnName("datum_odebrani");
        });

        modelBuilder.Entity<ProjektSubsystemEntity>(entity =>
        {
            entity.ToTable("projekt_subsystemy");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ProjektId, x.SubsystemId })
                .IsUnique()
                .HasFilter("[datum_odebrani] IS NULL")
                .HasDatabaseName("UX_projekt_subsystemy_projekt_subsystem_aktivni");
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.ProjektId).HasColumnName("projekt_id");
            entity.Property(x => x.SubsystemId).HasColumnName("subsystem_id");
            entity.Property(x => x.DatumPrirazeni).HasColumnName("datum_prirazeni");
            entity.Property(x => x.DatumOdebrani).HasColumnName("datum_odebrani");
        });

        modelBuilder.Entity<ObsazeniSubsystemuProjektuEntity>(entity =>
        {
            entity.ToTable("obsazeni_subsystemu_projektu");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ProjektSubsystemId, x.OsobaId, x.RoleSubsystemuId })
                .IsUnique()
                .HasFilter("[datum_odebrani] IS NULL")
                .HasDatabaseName("UX_obsazeni_subsystemu_projektu_aktivni");
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.ProjektSubsystemId).HasColumnName("projekt_subsystem_id");
            entity.Property(x => x.OsobaId).HasColumnName("osoba_id");
            entity.Property(x => x.RoleSubsystemuId).HasColumnName("role_subsystemu_id");
            entity.Property(x => x.DatumPrirazeni).HasColumnName("datum_prirazeni");
            entity.Property(x => x.DatumOdebrani).HasColumnName("datum_odebrani");
        });

        modelBuilder.Entity<ProjektovyZaznamEntity>(entity =>
        {
            entity.ToTable("projektove_zaznamy", table =>
            {
                table.HasCheckConstraint("CK_projektove_zaznamy_cislo_viditelne_typ", "cislo_viditelne_typ IN (0, 1)");
                table.HasCheckConstraint("CK_projektove_zaznamy_cislo_viditelne_consistency", "((cislo_viditelne_typ = 0 AND cislo_jednani_zdroj_id IS NULL AND cislo_viditelne_b = 0) OR (cislo_viditelne_typ = 1 AND cislo_jednani_zdroj_id IS NOT NULL AND cislo_viditelne_b > 0))");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.ProjektId).HasColumnName("projekt_id");
            entity.Property(x => x.KategorieId).HasColumnName("kategorie_id");
            entity.Property(x => x.AktualniTypUkoluId).HasColumnName("aktualni_typ_ukolu_id");
            entity.Property(x => x.StavUkoluId).HasColumnName("stav_ukolu_id");
            entity.Property(x => x.CisloZaznamu).HasColumnName("cislo_zaznamu");
            entity.Property(x => x.CisloViditelne).HasColumnName("cislo_viditelne");
            entity.Property(x => x.CisloViditelneTyp).HasColumnName("cislo_viditelne_typ");
            entity.Property(x => x.CisloViditelneA).HasColumnName("cislo_viditelne_a");
            entity.Property(x => x.CisloViditelneB).HasColumnName("cislo_viditelne_b");
            entity.Property(x => x.CisloJednaniZdrojId).HasColumnName("cislo_jednani_zdroj_id");
            entity.Property(x => x.Nazev).HasColumnName("nazev");
            entity.Property(x => x.Cil).HasColumnName("cil").HasMaxLength(100);
            entity.Property(x => x.Popis).HasColumnName("popis");
            entity.Property(x => x.VlastnikId).HasColumnName("vlastnik_id");
            entity.Property(x => x.DatumZalozeni).HasColumnName("datum_zalozeni");
            entity.Property(x => x.DatumUkonceni).HasColumnName("datum_ukonceni");
            entity.Property(x => x.SubsystemId).HasColumnName("subsystem_id");
            entity.Property(x => x.HarmonogramSablonaVerze).HasColumnName("harmonogram_sablona_verze");
            entity.HasIndex(x => new { x.ProjektId, x.CisloViditelne })
                .HasFilter("[cislo_viditelne] IS NOT NULL")
                .HasDatabaseName("UX_projektove_zaznamy_projekt_cislo_viditelne")
                .IsUnique();
            entity.HasIndex(x => new { x.ProjektId, x.CisloViditelneA, x.CisloViditelneB })
                .HasFilter("[cislo_viditelne_typ] = 1")
                .HasDatabaseName("UX_projektove_zaznamy_projekt_meeting_order")
                .IsUnique();
            entity.HasIndex(x => new { x.ProjektId, x.CisloViditelneA, x.CisloViditelneB, x.CisloZaznamu })
                .HasDatabaseName("IX_projektove_zaznamy_projekt_sort");
            entity.HasOne<HarmonogramSablonaEntity>()
                .WithMany()
                .HasForeignKey(x => x.HarmonogramSablonaVerze)
                .HasConstraintName("FK_projektove_zaznamy_harmonogram_sablona");
            entity.HasOne<JednaniEntity>()
                .WithMany()
                .HasForeignKey(x => x.CisloJednaniZdrojId)
                .HasConstraintName("FK_projektove_zaznamy_cislo_jednani_zdroj")
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ZaznamHistorieZmenTypuEntity>(entity =>
        {
            entity.ToTable("zaznam_historie_zmen_typu");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.ZaznamId).HasColumnName("zaznam_id");
            entity.Property(x => x.PuvodniTypId).HasColumnName("puvodni_typ_id");
            entity.Property(x => x.NovyTypId).HasColumnName("novy_typ_id");
            entity.Property(x => x.DatumZmeny).HasColumnName("datum_zmeny");
            entity.Property(x => x.ZmenilOsobaId).HasColumnName("zmenil_osoba_id");
        });

        modelBuilder.Entity<ZaznamHistorieTerminuEntity>(entity =>
        {
            entity.ToTable("zaznam_historie_terminu");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.ZaznamId).HasColumnName("zaznam_id");
            entity.Property(x => x.PuvodniDatum).HasColumnName("puvodni_datum");
            entity.Property(x => x.NoveDatum).HasColumnName("nove_datum");
            entity.Property(x => x.DatumZmeny).HasColumnName("datum_zmeny");
            entity.Property(x => x.Duvod).HasColumnName("duvod");
        });

        modelBuilder.Entity<ZaznamHistorieVlastnikEntity>(entity =>
        {
            entity.ToTable("zaznam_historie_vlastnik");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.ZaznamId).HasColumnName("zaznam_id");
            entity.Property(x => x.PuvodniVlastnik).HasColumnName("puvodni_vlastnik");
            entity.Property(x => x.NovyVlastnik).HasColumnName("novy_vlastnik");
            entity.Property(x => x.DatumZmeny).HasColumnName("datum_zmeny");
        });

        modelBuilder.Entity<ZaznamHistorieSubsystemEntity>(entity =>
        {
            entity.ToTable("zaznam_historie_subsystem");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.ZaznamId).HasColumnName("zaznam_id");
            entity.Property(x => x.PuvodniSubsystem).HasColumnName("puvodni_subsystem");
            entity.Property(x => x.NovySubsystem).HasColumnName("novy_subsystem");
            entity.Property(x => x.DatumZmeny).HasColumnName("datum_zmeny");
        });

        modelBuilder.Entity<ZaznamHistorieStavuZaznamuEntity>(entity =>
        {
            entity.ToTable("zaznam_historie_stavu_zaznamu");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.ZaznamId).HasColumnName("zaznam_id");
            entity.Property(x => x.PuvodniStav).HasColumnName("puvodni_stav");
            entity.Property(x => x.NovyStav).HasColumnName("novy_stav");
            entity.Property(x => x.DatumZmeny).HasColumnName("datum_zmeny");
        });

        modelBuilder.Entity<ZaznamHistorieStavuProjektuEntity>(entity =>
        {
            entity.ToTable("zaznam_historie_stavu_projektu");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.ZaznamId).HasColumnName("zaznam_id");
            entity.Property(x => x.PuvodniStav).HasColumnName("puvodni_stav");
            entity.Property(x => x.NovyStav).HasColumnName("novy_stav");
            entity.Property(x => x.DatumZmeny).HasColumnName("datum_zmeny");
        });

        modelBuilder.Entity<ZaznamExterniOdkazEntity>(entity =>
        {
            entity.ToTable("zaznam_externi_odkazy");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.ZaznamId).HasColumnName("zaznam_id");
            entity.Property(x => x.TypOdkazuId).HasColumnName("typ_odkazu_id");
            entity.Property(x => x.Cislo).HasColumnName("cislo");
            entity.Property(x => x.PredpokladanaCena).HasColumnName("predpokladana_cena").HasColumnType("decimal(18,2)");
            entity.Property(x => x.DatumObjednani).HasColumnName("datum_objednani");
            entity.Property(x => x.PlanDodani).HasColumnName("plan_dodani");
            entity.Property(x => x.DatumDodani).HasColumnName("datum_dodani");
            entity.Property(x => x.DatumPrevzeti).HasColumnName("datum_prevzeti");
            entity.Property(x => x.Vyzva).HasColumnName("vyzva");
        });

        modelBuilder.Entity<ZaznamSpolupraceEntity>(entity =>
        {
            entity.ToTable("zaznam_spoluprace");
            entity.HasKey(x => new { x.ZaznamId, x.OsobaId });
            entity.Property(x => x.ZaznamId).HasColumnName("zaznam_id");
            entity.Property(x => x.OsobaId).HasColumnName("osoba_id");
        });

        modelBuilder.Entity<JednaniEntity>(entity =>
        {
            entity.ToTable("jednani");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.ProjektId).HasColumnName("projekt_id");
            entity.Property(x => x.CisloJednani).HasColumnName("cislo_jednani");
            entity.Property(x => x.DatumPlanovane).HasColumnName("datum_planovane");
            entity.Property(x => x.CasZacatek).HasColumnName("cas_zacatek").HasColumnType("time(0)");
            entity.Property(x => x.Misto).HasColumnName("misto");
            entity.Property(x => x.StavJednaniId).HasColumnName("stav_jednani_id");
            entity.Property(x => x.UzamklOsobaId).HasColumnName("uzamkl_osoba_id");
        });

        modelBuilder.Entity<UcastEntity>(entity =>
        {
            entity.ToTable("ucast");
            entity.HasKey(x => new { x.JednaniId, x.OsobaId });
            entity.Property(x => x.JednaniId).HasColumnName("jednani_id");
            entity.Property(x => x.OsobaId).HasColumnName("osoba_id");
            entity.Property(x => x.StavUcastiId).HasColumnName("stav_ucasti_id");
        });

        modelBuilder.Entity<VyjadreniEntity>(entity =>
        {
            entity.ToTable("vyjadreni");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.ZaznamId).HasColumnName("zaznam_id");
            entity.Property(x => x.JednaniId).HasColumnName("jednani_id");
            entity.Property(x => x.AutorOsobaId).HasColumnName("autor_osoba_id");
            entity.Property(x => x.TextVyjadreni).HasColumnName("text_vyjadreni");
            entity.Property(x => x.DatumVyjadreni).HasColumnName("datum_vyjadreni");
        });

        modelBuilder.Entity<ZaznamHarmonogramHodnotaEntity>(entity =>
        {
            entity.ToTable("zaznam_harmonogram_hodnoty");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ZaznamId, x.TypId })
                .IsUnique()
                .HasDatabaseName("UQ_zaznam_harmonogram_hodnoty_zaznam_typ");
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.ZaznamId).HasColumnName("zaznam_id");
            entity.Property(x => x.TypId).HasColumnName("typ_id");
            entity.Property(x => x.HodnotaInt).HasColumnName("hodnota_int");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<AuthzSuperadminEntity>(entity =>
        {
            entity.ToTable("superadmins", "authz");
            entity.HasKey(x => x.OsobaId);
            entity.Property(x => x.OsobaId).HasColumnName("osoba_id");
            entity.Property(x => x.Poznamka).HasColumnName("poznamka");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.CreatedBy).HasColumnName("created_by");
        });

        modelBuilder.Entity<AuthzPermissionCategoryEntity>(entity =>
        {
            entity.ToTable("permission_categories", "authz");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Kod).HasColumnName("kod");
            entity.Property(x => x.Nazev).HasColumnName("nazev");
            entity.Property(x => x.SortOrder).HasColumnName("sort_order");
            entity.Property(x => x.IsActive).HasColumnName("is_active");
        });

        modelBuilder.Entity<AuthzPermissionEntity>(entity =>
        {
            entity.ToTable("permissions", "authz");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Klic).HasColumnName("klic");
            entity.Property(x => x.Nazev).HasColumnName("nazev");
            entity.Property(x => x.CategoryId).HasColumnName("category_id");
            entity.Property(x => x.ScopeLevel).HasColumnName("scope_level");
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.Property(x => x.IsSystem).HasColumnName("is_system");
        });

        modelBuilder.Entity<AuthzRoleEntity>(entity =>
        {
            entity.ToTable("roles", "authz");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Kod).HasColumnName("kod");
            entity.Property(x => x.Nazev).HasColumnName("nazev");
            entity.Property(x => x.Popis).HasColumnName("popis");
            entity.Property(x => x.IsSystem).HasColumnName("is_system");
            entity.Property(x => x.IsActive).HasColumnName("is_active");
        });

        modelBuilder.Entity<AuthzRolePermissionEntity>(entity =>
        {
            entity.ToTable("role_permissions", "authz");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.RoleId).HasColumnName("role_id");
            entity.Property(x => x.PermissionId).HasColumnName("permission_id");
            entity.Property(x => x.ScopeMode).HasColumnName("scope_mode");
            entity.Property(x => x.IsAllowed).HasColumnName("is_allowed");
        });

        modelBuilder.Entity<AuthzRolePermissionProjectEntity>(entity =>
        {
            entity.ToTable("role_permission_projects", "authz");
            entity.HasKey(x => new { x.RolePermissionId, x.ProjektId });
            entity.Property(x => x.RolePermissionId).HasColumnName("role_permission_id");
            entity.Property(x => x.ProjektId).HasColumnName("projekt_id");
        });

        modelBuilder.Entity<AuthzUserRoleEntity>(entity =>
        {
            entity.ToTable("user_roles", "authz");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.OsobaId).HasColumnName("osoba_id");
            entity.Property(x => x.RoleId).HasColumnName("role_id");
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<AuthzAuditLogEntity>(entity =>
        {
            entity.ToTable("audit_log", "authz");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.ActorOsobaId).HasColumnName("actor_osoba_id");
            entity.Property(x => x.EntityType).HasColumnName("entity_type");
            entity.Property(x => x.EntityId).HasColumnName("entity_id");
            entity.Property(x => x.Action).HasColumnName("action");
            entity.Property(x => x.OldValue).HasColumnName("old_value");
            entity.Property(x => x.NewValue).HasColumnName("new_value");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        });
    }
}
