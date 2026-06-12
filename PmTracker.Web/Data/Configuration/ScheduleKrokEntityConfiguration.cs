using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Data.Configuration;

public sealed class ScheduleKrokEntityConfiguration : IEntityTypeConfiguration<ZaznamHarmonogramKrokEntity>
{
    public void Configure(EntityTypeBuilder<ZaznamHarmonogramKrokEntity> builder)
    {
        builder.ToTable("zaznam_harmonogram_krok");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ZaznamId).HasColumnName("zaznam_id");
        builder.Property(x => x.Poradi).HasColumnName("poradi");
        builder.Property(x => x.PlanDatum).HasColumnName("plan_datum").HasColumnType("date");
        builder.Property(x => x.SkutecnostDatum).HasColumnName("skutecnost_datum").HasColumnType("date");
        builder.Property(x => x.SkutecnostZdroj).HasColumnName("skutecnost_zdroj");
        builder.Property(x => x.SkutecnostRezim).HasColumnName("skutecnost_rezim");
        builder.Property(x => x.PreferredExterniOdkazId).HasColumnName("preferred_externi_odkaz_id");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => new { x.ZaznamId, x.Poradi })
            .IsUnique()
            .HasDatabaseName("UQ_zaznam_harmonogram_krok_zaznam_poradi");

        builder.HasOne<ProjektovyZaznamEntity>()
            .WithMany()
            .HasForeignKey(x => x.ZaznamId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
