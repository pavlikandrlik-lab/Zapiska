using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Data.Configuration;

internal sealed class ProjectEntityConfiguration : IEntityTypeConfiguration<ProjektEntity>
{
    public void Configure(EntityTypeBuilder<ProjektEntity> builder)
    {
        builder.ToTable("projekty");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Zkratka).HasColumnName("zkratka");
        builder.Property(x => x.CelyNazev).HasColumnName("cely_nazev");
        builder.Property(x => x.StavId).HasColumnName("stav_id");
        builder.Property(x => x.PouzivatIdentJednani).HasColumnName("pouzivat_ident_jednani");
        builder.Property(x => x.MistoPlneni).HasColumnName("misto_plneni").HasMaxLength(500);
        builder.Property(x => x.CisloRamcoveSmlouvy).HasColumnName("cislo_ramcove_smlouvy").HasMaxLength(100);
        // Plán 5 Sprint B Task 1: logická reference na HOT_IS.ID v intranetNEW.
        // Bez FK — cizí DB; validace přes SdInfoSystemy katalog (aplikační).
        builder.Property(x => x.ServiceDeskInfoSystemId).HasColumnName("servicedesk_info_system_id");
    }
}

internal sealed class ProjectStaffingEntityConfiguration : IEntityTypeConfiguration<ObsazeniProjektuEntity>
{
    public void Configure(EntityTypeBuilder<ObsazeniProjektuEntity> builder)
    {
        builder.ToTable("obsazeni_projektu");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.ProjektId, x.OsobaId, x.RoleId })
            .IsUnique()
            .HasFilter("[datum_odebrani] IS NULL")
            .HasDatabaseName("UX_obsazeni_projektu_projekt_osoba_role_aktivni");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProjektId).HasColumnName("projekt_id");
        builder.Property(x => x.OsobaId).HasColumnName("osoba_id");
        builder.Property(x => x.RoleId).HasColumnName("role_id");
        builder.Property(x => x.DatumPrirazeni).HasColumnName("datum_prirazeni");
        builder.Property(x => x.DatumOdebrani).HasColumnName("datum_odebrani");
    }
}

internal sealed class ProjectSubsystemEntityConfiguration : IEntityTypeConfiguration<ProjektSubsystemEntity>
{
    public void Configure(EntityTypeBuilder<ProjektSubsystemEntity> builder)
    {
        builder.ToTable("projekt_subsystemy");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.ProjektId, x.SubsystemId })
            .IsUnique()
            .HasFilter("[datum_odebrani] IS NULL")
            .HasDatabaseName("UX_projekt_subsystemy_projekt_subsystem_aktivni");
        builder.HasIndex(x => new { x.ProjektId, x.Poradi })
            .IsUnique()
            .HasFilter("[datum_odebrani] IS NULL")
            .HasDatabaseName("UX_projekt_subsystemy_projekt_poradi_aktivni");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProjektId).HasColumnName("projekt_id");
        builder.Property(x => x.SubsystemId).HasColumnName("subsystem_id");
        builder.Property(x => x.Poradi).HasColumnName("poradi");
        builder.Property(x => x.DatumPrirazeni).HasColumnName("datum_prirazeni");
        builder.Property(x => x.DatumOdebrani).HasColumnName("datum_odebrani");
    }
}

internal sealed class ProjectSubsystemStaffingEntityConfiguration : IEntityTypeConfiguration<ObsazeniSubsystemuProjektuEntity>
{
    public void Configure(EntityTypeBuilder<ObsazeniSubsystemuProjektuEntity> builder)
    {
        builder.ToTable("obsazeni_subsystemu_projektu");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.ProjektSubsystemId, x.OsobaId, x.RoleSubsystemuId })
            .IsUnique()
            .HasFilter("[datum_odebrani] IS NULL")
            .HasDatabaseName("UX_obsazeni_subsystemu_projektu_aktivni");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProjektSubsystemId).HasColumnName("projekt_subsystem_id");
        builder.Property(x => x.OsobaId).HasColumnName("osoba_id");
        builder.Property(x => x.RoleSubsystemuId).HasColumnName("role_subsystemu_id");
        builder.Property(x => x.DatumPrirazeni).HasColumnName("datum_prirazeni");
        builder.Property(x => x.DatumOdebrani).HasColumnName("datum_odebrani");
    }
}
