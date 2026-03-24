using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Data.Configuration;

internal sealed class PersonEntityConfiguration : IEntityTypeConfiguration<OsobaEntity>
{
    public void Configure(EntityTypeBuilder<OsobaEntity> builder)
    {
        builder.ToTable("osoby");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Jmeno).HasColumnName("jmeno");
        builder.Property(x => x.Prijmeni).HasColumnName("prijmeni");
        builder.Property(x => x.Titul).HasColumnName("titul");
        builder.Property(x => x.Email).HasColumnName("email");
        builder.Property(x => x.AdLogin).HasColumnName("ad_login");
        builder.Property(x => x.OrganizacniCelekId).HasColumnName("organizacni_celek_id");
        builder.Property(x => x.OrganizaceId).HasColumnName("organizace_id");
        builder.Property(x => x.GuidAd).HasColumnName("Guid_AD");
        builder.Property(x => x.LocationLocked).HasColumnName("location_locked");
    }
}
