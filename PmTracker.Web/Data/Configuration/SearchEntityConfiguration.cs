using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Data.Configuration;

internal sealed class SearchReindexCheckpointEntityConfiguration : IEntityTypeConfiguration<SearchReindexCheckpointEntity>
{
    public void Configure(EntityTypeBuilder<SearchReindexCheckpointEntity> builder)
    {
        builder.ToTable("search_reindex_checkpoint", "dbo");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.LastProcessedAuditId).HasColumnName("last_processed_audit_id");
        builder.Property(x => x.LastProcessedAt).HasColumnName("last_processed_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    }
}
