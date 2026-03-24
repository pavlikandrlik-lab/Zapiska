using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Data.Configuration;

internal sealed class ProjectStateLookupEntityConfiguration : IEntityTypeConfiguration<CiselnikStavuProjektuEntity>
{
    public void Configure(EntityTypeBuilder<CiselnikStavuProjektuEntity> builder)
    {
        builder.ToTable("ciselnik_stavu_projektu");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Kod).HasColumnName("kod");
        builder.Property(x => x.Nazev).HasColumnName("nazev");
        builder.Property(x => x.IsLocked).HasColumnName("is_locked");

        // Explicitly map dependent FK to projekty.stav_id to avoid shadow FK convention.
        builder.HasMany(x => x.Projekty)
            .WithOne()
            .HasForeignKey(x => x.StavId)
            .HasPrincipalKey(x => x.Id);
    }
}

internal sealed class TaskStateLookupEntityConfiguration : IEntityTypeConfiguration<CiselnikStavuUkoluEntity>
{
    public void Configure(EntityTypeBuilder<CiselnikStavuUkoluEntity> builder)
    {
        builder.ToTable("ciselnik_stavu_ukolu");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Kod).HasColumnName("kod");
        builder.Property(x => x.Nazev).HasColumnName("nazev");
        builder.Property(x => x.IsFinal).HasColumnName("is_final");
        builder.Property(x => x.IsLocked).HasColumnName("is_locked");
    }
}

internal sealed class RecordCategoryLookupEntityConfiguration : IEntityTypeConfiguration<CiselnikKategoriiZaznamuEntity>
{
    public void Configure(EntityTypeBuilder<CiselnikKategoriiZaznamuEntity> builder)
    {
        builder.ToTable("ciselnik_kategorii_zaznamu");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Kod).HasColumnName("kod");
        builder.Property(x => x.Nazev).HasColumnName("nazev");
        builder.Property(x => x.IsLocked).HasColumnName("is_locked");
    }
}

internal sealed class TaskTypeLookupEntityConfiguration : IEntityTypeConfiguration<CiselnikTypuUkoluEntity>
{
    public void Configure(EntityTypeBuilder<CiselnikTypuUkoluEntity> builder)
    {
        builder.ToTable("ciselnik_typu_ukolu");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Kod).HasColumnName("kod");
        builder.Property(x => x.Nazev).HasColumnName("nazev");
        builder.Property(x => x.IsLocked).HasColumnName("is_locked");
    }
}

internal sealed class ExternalLinkTypeLookupEntityConfiguration : IEntityTypeConfiguration<CiselnikTypuExternichOdkazuEntity>
{
    public void Configure(EntityTypeBuilder<CiselnikTypuExternichOdkazuEntity> builder)
    {
        builder.ToTable("ciselnik_typu_externich_odkazu");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Kod).HasColumnName("kod");
        builder.Property(x => x.Nazev).HasColumnName("nazev");
        builder.Property(x => x.IsLocked).HasColumnName("is_locked");
    }
}

internal sealed class ProjectRoleLookupEntityConfiguration : IEntityTypeConfiguration<CiselnikRoliProjektuEntity>
{
    public void Configure(EntityTypeBuilder<CiselnikRoliProjektuEntity> builder)
    {
        builder.ToTable("ciselnik_roli_projektu");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Kod).HasColumnName("kod");
        builder.Property(x => x.Nazev).HasColumnName("nazev");
        builder.Property(x => x.IsLocked).HasColumnName("is_locked");
    }
}

internal sealed class SubsystemRoleLookupEntityConfiguration : IEntityTypeConfiguration<CiselnikRoleSubsystemuEntity>
{
    public void Configure(EntityTypeBuilder<CiselnikRoleSubsystemuEntity> builder)
    {
        builder.ToTable("ciselnik_roli_subsystemu");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Kod).HasColumnName("kod");
        builder.Property(x => x.Nazev).HasColumnName("nazev");
        builder.Property(x => x.IsLocked).HasColumnName("is_locked");
    }
}

internal sealed class AttendanceStateLookupEntityConfiguration : IEntityTypeConfiguration<CiselnikStavuUcastiEntity>
{
    public void Configure(EntityTypeBuilder<CiselnikStavuUcastiEntity> builder)
    {
        builder.ToTable("ciselnik_stavu_ucasti");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Kod).HasColumnName("kod");
        builder.Property(x => x.Nazev).HasColumnName("nazev");
        builder.Property(x => x.IsLocked).HasColumnName("is_locked");
    }
}

internal sealed class OrganizationLookupEntityConfiguration : IEntityTypeConfiguration<CiselnikOrganizaceEntity>
{
    public void Configure(EntityTypeBuilder<CiselnikOrganizaceEntity> builder)
    {
        builder.ToTable("ciselnik_organizace");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Kod).HasColumnName("kod");
        builder.Property(x => x.Nazev).HasColumnName("nazev");
        builder.Property(x => x.IsLocked).HasColumnName("is_locked");
    }
}

internal sealed class OrganizationUnitLookupEntityConfiguration : IEntityTypeConfiguration<CiselnikOrganizacniCelekEntity>
{
    public void Configure(EntityTypeBuilder<CiselnikOrganizacniCelekEntity> builder)
    {
        builder.ToTable("ciselnik_organizacni_celky");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Kod).HasColumnName("kod");
        builder.Property(x => x.Nazev).HasColumnName("nazev");
        builder.Property(x => x.IsLocked).HasColumnName("is_locked");
    }
}

internal sealed class ChallengeLookupEntityConfiguration : IEntityTypeConfiguration<CiselnikVyzvaEntity>
{
    public void Configure(EntityTypeBuilder<CiselnikVyzvaEntity> builder)
    {
        builder.ToTable("ciselnik_vyzvy");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Kod).HasColumnName("kod");
        builder.Property(x => x.Nazev).HasColumnName("nazev");
        builder.Property(x => x.Rok).HasColumnName("rok");
        builder.Property(x => x.IsLocked).HasColumnName("is_locked");
    }
}

internal sealed class MeetingStateLookupEntityConfiguration : IEntityTypeConfiguration<CiselnikStavuJednaniEntity>
{
    public void Configure(EntityTypeBuilder<CiselnikStavuJednaniEntity> builder)
    {
        builder.ToTable("ciselnik_stavu_jednani");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Kod).HasColumnName("kod");
        builder.Property(x => x.Nazev).HasColumnName("nazev");
        builder.Property(x => x.IsLocked).HasColumnName("is_locked");
    }
}

internal sealed class ScheduleTemplateLookupEntityConfiguration : IEntityTypeConfiguration<HarmonogramSablonaEntity>
{
    public void Configure(EntityTypeBuilder<HarmonogramSablonaEntity> builder)
    {
        builder.ToTable("harmonogram_sablony");
        builder.HasKey(x => x.Verze);
        builder.HasIndex(x => x.IsAktivni)
            .IsUnique()
            .HasFilter("[is_aktivni] = 1")
            .HasDatabaseName("UQ_harmonogram_sablony_aktivni");
        builder.Property(x => x.Verze).HasColumnName("verze");
        builder.Property(x => x.DelayBarvaHex).HasColumnName("delay_barva_hex");
        builder.Property(x => x.IsAktivni).HasColumnName("is_aktivni");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
    }
}

internal sealed class ScheduleTypeLookupEntityConfiguration : IEntityTypeConfiguration<HarmonogramTypEntity>
{
    public void Configure(EntityTypeBuilder<HarmonogramTypEntity> builder)
    {
        builder.ToTable("ciselnik_harmonogram_typu");
        builder.HasKey(x => x.Id);
        builder.HasOne<HarmonogramSablonaEntity>()
            .WithMany()
            .HasForeignKey(x => x.SablonaVerze)
            .HasConstraintName("FK_ciselnik_harmonogram_typu_sablona");
        builder.HasIndex(x => new { x.SablonaVerze, x.Kod })
            .IsUnique()
            .HasDatabaseName("UQ_ciselnik_harmonogram_typu_sablona_kod");
        builder.HasIndex(x => new { x.SablonaVerze, x.KrokPoradi, x.JeZpozdeni })
            .IsUnique()
            .HasDatabaseName("UQ_ciselnik_harmonogram_typu_sablona_krok_zpozdeni");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Kod).HasColumnName("kod");
        builder.Property(x => x.Nazev).HasColumnName("nazev");
        builder.Property(x => x.Hodnota).HasColumnName("hodnota");
        builder.Property(x => x.IsLocked).HasColumnName("is_locked");
        builder.Property(x => x.SablonaVerze).HasColumnName("sablona_verze");
        builder.Property(x => x.KrokKey).HasColumnName("krok_key");
        builder.Property(x => x.KrokPoradi).HasColumnName("krok_poradi");
        builder.Property(x => x.JeZpozdeni).HasColumnName("je_zpozdeni");
        builder.Property(x => x.BarvaHex).HasColumnName("barva_hex");
    }
}

internal sealed class SubsystemLookupEntityConfiguration : IEntityTypeConfiguration<SubsystemEntity>
{
    public void Configure(EntityTypeBuilder<SubsystemEntity> builder)
    {
        builder.ToTable("subsystemy");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Kod).HasColumnName("kód");
        builder.Property(x => x.Nazev).HasColumnName("nazev");
    }
}
