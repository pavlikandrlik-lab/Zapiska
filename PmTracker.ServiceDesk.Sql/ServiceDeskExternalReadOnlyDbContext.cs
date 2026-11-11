using Microsoft.EntityFrameworkCore;
using PmTracker.ServiceDesk.Sql.Entities;

namespace PmTracker.ServiceDesk.Sql;

/// <summary>
/// Read-only kontext databáze starého ServiceDesku (EF6 model <c>ServiceDeskEntities</c>), ze které ServiceDesk
/// počítá penále: předávky a SLA hodiny (spec 2026-10-15 §6). Connection string
/// <c>ServiceDeskExternalReadOnly</c> vyplňuje správce; PM Tracker se k serveru ani názvu databáze neváže.
/// </summary>
public sealed class ServiceDeskExternalReadOnlyDbContext : DbContext
{
    public ServiceDeskExternalReadOnlyDbContext(DbContextOptions<ServiceDeskExternalReadOnlyDbContext> opts) : base(opts)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        ChangeTracker.AutoDetectChangesEnabled = false;
    }

    internal DbSet<TicketExternalEntity> TicketExternal => Set<TicketExternalEntity>();
    internal DbSet<TicketExternalWorkflowEntity> TicketExternalWorkflow => Set<TicketExternalWorkflowEntity>();
    internal DbSet<TicketExternalSlaEntity> TicketExternalSla => Set<TicketExternalSlaEntity>();

    public override int SaveChanges()
        => throw new InvalidOperationException("ServiceDeskExternalReadOnlyDbContext is strictly read-only.");

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
        => throw new InvalidOperationException("ServiceDeskExternalReadOnlyDbContext is strictly read-only.");

    /// <summary>Seed helper jen pro testy (<c>InternalsVisibleTo</c>) — obchází read-only guard nad InMemory providerem.</summary>
    internal int SaveChangesForTests() => base.SaveChanges();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<TicketExternalEntity>(e =>
        {
            e.ToTable("ticket_external", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            // varchar(50): bez IsUnicode(false) by EF posílal nvarchar parametry a SQL by převáděl sloupec řádek po řádku.
            e.Property(x => x.ExternalId).HasColumnName("external_id").IsUnicode(false).HasMaxLength(50).IsRequired();
            e.Property(x => x.ExternalSyncId).HasColumnName("external_sync_id");
            e.Property(x => x.SupplierTag).HasColumnName("supplier_tag").HasMaxLength(50);
            e.Property(x => x.SlaName).HasColumnName("sla_name").HasMaxLength(50);
            e.HasMany(x => x.Workflow)
                .WithOne()
                .HasForeignKey(w => w.TicketExternalId)
                .HasConstraintName("FK_ticket_external_workflow_ticket_external");
        });

        mb.Entity<TicketExternalWorkflowEntity>(e =>
        {
            e.ToTable("ticket_external_workflow", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TicketExternalId).HasColumnName("id_ticket_external");
            e.Property(x => x.Date).HasColumnName("date").HasColumnType("datetime");
            e.Property(x => x.TransferType).HasColumnName("transfertype");
            e.Property(x => x.Supplier).HasColumnName("supplier").HasMaxLength(50);
            e.Property(x => x.DtParam).HasColumnName("dt_param").HasColumnType("datetime");
        });

        mb.Entity<TicketExternalSlaEntity>(e =>
        {
            e.ToTable("ticket_external_sla", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.SlaName).HasColumnName("sla_name").HasMaxLength(8).IsRequired();
            e.Property(x => x.SlaHours).HasColumnName("sla_hours").HasColumnType("float");
        });
    }
}
