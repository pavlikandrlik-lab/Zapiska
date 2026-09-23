using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Data.Configuration;

internal sealed class ZaznamHarmonogramVyjadreniVazbaEntityConfiguration
    : IEntityTypeConfiguration<ZaznamHarmonogramVyjadreniVazbaEntity>
{
    public void Configure(EntityTypeBuilder<ZaznamHarmonogramVyjadreniVazbaEntity> b)
    {
        b.ToTable("zaznam_harmonogram_vyjadreni_vazba");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ZaznamId).HasColumnName("zaznam_id");
        b.Property(x => x.Poradi).HasColumnName("poradi");
        b.Property(x => x.ExterniOdkazId).HasColumnName("externi_odkaz_id");
        b.Property(x => x.HotVyjadreniId).HasColumnName("hot_vyjadreni_id");
        b.Property(x => x.DatumVyjadreni).HasColumnName("datum_vyjadreni");
        b.Property(x => x.Source).HasColumnName("source");
        b.Property(x => x.Stav).HasColumnName("stav");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.CreatedByOsobaId).HasColumnName("created_by_osoba_id");
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        b.Property(x => x.DeletedByOsobaId).HasColumnName("deleted_by_osoba_id");

        b.HasIndex(x => new { x.ZaznamId, x.Poradi, x.Stav })
            .HasDatabaseName("ix_zhvv_zaznam_poradi_stav");
        b.HasIndex(x => x.ExterniOdkazId)
            .HasDatabaseName("ix_zhvv_externi_odkaz");

        // FK_zhvv_externi_odkaz je v databázi ON DELETE NO ACTION (db_upgrade_1_3_6).
        // Vztah musí být v modelu, i když se přes něj nenaviguje: bez něj EF nezná
        // závislost mezi bindingem a externí vazbou a při jejich současném mazání
        // pošle DELETE v libovolném pořadí → SQL 547 (hlášení 2026-09-06).
        b.HasOne<ZaznamExterniOdkazEntity>()
            .WithMany()
            .HasForeignKey(x => x.ExterniOdkazId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
