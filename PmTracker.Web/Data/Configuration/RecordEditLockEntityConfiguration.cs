using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Data.Configuration;

/// <summary>
/// Spec 2026-09-17 §4.1 — mapování zámku karty. Schéma zakládá
/// db_upgrade_1_4_5_record_edit_lock.sql, EF Migrations se v tomto projektu nepoužívají.
/// </summary>
internal sealed class RecordEditLockEntityConfiguration : IEntityTypeConfiguration<ZaznamEditZamekEntity>
{
    public void Configure(EntityTypeBuilder<ZaznamEditZamekEntity> builder)
    {
        builder.ToTable("zaznam_edit_zamek");
        builder.HasKey(x => x.ZaznamId);
        // Bez ValueGeneratedNever by EF podle konvence považovala celočíselný PK za IDENTITY
        // a hodnotu do INSERTu nedala — sloupec ale IDENTITY není, klíč je vždy id záznamu.
        builder.Property(x => x.ZaznamId).HasColumnName("zaznam_id").ValueGeneratedNever();
        builder.Property(x => x.OsobaId).HasColumnName("osoba_id");
        builder.Property(x => x.ZiskanoAt).HasColumnName("ziskano_at");
        builder.Property(x => x.HeartbeatAt).HasColumnName("heartbeat_at");
    }
}
