using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Data.Configuration;

internal sealed class MeetingEntityConfiguration : IEntityTypeConfiguration<JednaniEntity>
{
    public void Configure(EntityTypeBuilder<JednaniEntity> builder)
    {
        builder.ToTable("jednani");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProjektId).HasColumnName("projekt_id");
        builder.Property(x => x.CisloJednani).HasColumnName("cislo_jednani");
        builder.Property(x => x.DatumPlanovane).HasColumnName("datum_planovane");
        builder.Property(x => x.CasZacatek).HasColumnName("cas_zacatek").HasColumnType("time(0)");
        builder.Property(x => x.Misto).HasColumnName("misto");
        builder.Property(x => x.StavJednaniId).HasColumnName("stav_jednani_id");
        builder.Property(x => x.UzamklOsobaId).HasColumnName("uzamkl_osoba_id");
    }
}

internal sealed class MeetingAttendanceEntityConfiguration : IEntityTypeConfiguration<UcastEntity>
{
    public void Configure(EntityTypeBuilder<UcastEntity> builder)
    {
        builder.ToTable("ucast");
        builder.HasKey(x => new { x.JednaniId, x.OsobaId });
        builder.Property(x => x.JednaniId).HasColumnName("jednani_id");
        builder.Property(x => x.OsobaId).HasColumnName("osoba_id");
        builder.Property(x => x.StavUcastiId).HasColumnName("stav_ucasti_id");
    }
}

internal sealed class MeetingCommentEntityConfiguration : IEntityTypeConfiguration<VyjadreniEntity>
{
    public void Configure(EntityTypeBuilder<VyjadreniEntity> builder)
    {
        builder.ToTable("vyjadreni");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ZaznamId).HasColumnName("zaznam_id");
        builder.Property(x => x.JednaniId).HasColumnName("jednani_id");
        builder.Property(x => x.AutorOsobaId).HasColumnName("autor_osoba_id");
        builder.Property(x => x.TextVyjadreni).HasColumnName("text_vyjadreni");
        builder.Property(x => x.DatumVyjadreni).HasColumnName("datum_vyjadreni");
    }
}
