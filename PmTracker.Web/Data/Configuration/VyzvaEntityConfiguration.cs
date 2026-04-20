using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Data.Configuration;

internal sealed class VyzvaEntityConfiguration : IEntityTypeConfiguration<VyzvaEntity>
{
    public void Configure(EntityTypeBuilder<VyzvaEntity> builder)
    {
        builder.ToTable("vyzvy");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProjektId).HasColumnName("projekt_id");
        builder.Property(x => x.Kod).HasColumnName("kod").HasMaxLength(20).IsRequired();
        builder.Property(x => x.PoradoveVRoce).HasColumnName("poradove_v_roce");
        builder.Property(x => x.Rok).HasColumnName("rok");
        builder.Property(x => x.Stav).HasColumnName("stav").HasConversion<byte>();
        builder.Property(x => x.DatumZalozeni).HasColumnName("datum_zalozeni");
        builder.Property(x => x.ZalozilOsobaId).HasColumnName("zalozil_osoba_id");
        builder.Property(x => x.DatumOdeslani).HasColumnName("datum_odeslani");
        builder.Property(x => x.OdeslalOsobaId).HasColumnName("odeslal_osoba_id");
        builder.Property(x => x.MistoPlneniSnapshot).HasColumnName("misto_plneni_snapshot").HasMaxLength(500).IsRequired();
        builder.Property(x => x.CisloRamcoveSmlouvySnapshot).HasColumnName("cislo_ramcove_smlouvy_snapshot").HasMaxLength(100).IsRequired();

        builder.HasOne<ProjektEntity>()
            .WithMany()
            .HasForeignKey(x => x.ProjektId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.CisloRamcoveSmlouvySnapshot, x.Rok, x.PoradoveVRoce })
            .IsUnique()
            .HasDatabaseName("ux_vyzvy_smlouva_rok_poradove");

        builder.HasIndex(x => new { x.ProjektId, x.DatumZalozeni })
            .HasDatabaseName("ix_vyzvy_projekt_datum_desc");
    }
}

internal sealed class VyzvaHistorieStavuEntityConfiguration : IEntityTypeConfiguration<VyzvaHistorieStavuEntity>
{
    public void Configure(EntityTypeBuilder<VyzvaHistorieStavuEntity> builder)
    {
        builder.ToTable("vyzva_historie_stavu");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.VyzvaId).HasColumnName("vyzva_id");
        builder.Property(x => x.PuvodniStav).HasColumnName("puvodni_stav").HasConversion<byte?>();
        builder.Property(x => x.NovyStav).HasColumnName("novy_stav").HasConversion<byte>();
        builder.Property(x => x.DatumZmeny).HasColumnName("datum_zmeny");
        builder.Property(x => x.ZmenilOsobaId).HasColumnName("zmenil_osoba_id");

        builder.HasOne<VyzvaEntity>()
            .WithMany()
            .HasForeignKey(x => x.VyzvaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.VyzvaId).HasDatabaseName("ix_vyzva_historie_stavu_vyzva_id");
    }
}
