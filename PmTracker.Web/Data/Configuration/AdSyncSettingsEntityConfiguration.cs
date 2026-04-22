using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Data.Configuration;

internal sealed class AdSyncSettingsEntityConfiguration
    : IEntityTypeConfiguration<AdSyncSettingsEntity>
{
    public void Configure(EntityTypeBuilder<AdSyncSettingsEntity> builder)
    {
        builder.ToTable("ad_sync_settings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.IsEnabled).HasColumnName("is_enabled");
        builder.Property(x => x.PeriodMinutes).HasColumnName("period_minutes");
        builder.Property(x => x.AnchorAt).HasColumnName("anchor_at");
        builder.Property(x => x.LastRunAt).HasColumnName("last_run_at");
        builder.Property(x => x.LastTriggerKind).HasColumnName("last_trigger_kind").HasMaxLength(16);
        builder.Property(x => x.LastResultJson).HasColumnName("last_result_json");
        builder.Property(x => x.IsRunning).HasColumnName("is_running");
        builder.Property(x => x.RunStartedAt).HasColumnName("run_started_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedByOsobaId).HasColumnName("updated_by_osoba_id");
    }
}
