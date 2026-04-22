using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using PmTracker.ServiceDesk.Sql.Entities;

namespace PmTracker.ServiceDesk.Sql;

public sealed class TicketingReadOnlyDbContext : DbContext
{
    public TicketingReadOnlyDbContext(DbContextOptions<TicketingReadOnlyDbContext> opts) : base(opts)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        ChangeTracker.AutoDetectChangesEnabled = false;
    }

    internal DbSet<HotZaznamEntity> HotZaznamy => Set<HotZaznamEntity>();
    internal DbSet<HotKalkulaceEntity> HotKalkulace => Set<HotKalkulaceEntity>();

    public override int SaveChanges()
        => throw new InvalidOperationException("TicketingReadOnlyDbContext is strictly read-only.");

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
        => throw new InvalidOperationException("TicketingReadOnlyDbContext is strictly read-only.");

    /// <summary>
    /// Seed helper viditelný pouze pro testy přes <c>InternalsVisibleTo</c>.
    /// Obchází read-only guard a volá <see cref="DbContext.SaveChanges()"/> základní třídy,
    /// aby testy mohly naplnit <c>InMemory</c> provider.
    /// </summary>
    internal int SaveChangesForTests() => base.SaveChanges();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<HotZaznamEntity>(e =>
        {
            e.ToTable("HOT_ZAZNAMY", "dbo");
            e.HasKey(x => x.Radek);
            e.Property(x => x.Radek).HasColumnName("radek");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TypZaznamu).HasColumnName("typ_zaznamu");
            e.Property(x => x.Strucne).HasColumnName("strucne");
            e.Property(x => x.Popis).HasColumnName("popis");
            e.Property(x => x.Pid).HasColumnName("pid").HasMaxLength(50);
            e.Property(x => x.Stav).HasColumnName("stav").HasMaxLength(50);
            e.Property(x => x.Splneno).HasColumnName("splneno");
            e.Property(x => x.SlaDeadline).HasColumnName("sla_deadline");
            e.Property(x => x.Datum).HasColumnName("datum");
        });

        mb.Entity<HotKalkulaceEntity>(e =>
        {
            e.ToTable("HOT_KALKULACE", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Pid).HasColumnName("pid");
            e.Property(x => x.IdKalk).HasColumnName("id_kalk");
            e.Property(x => x.Verze).HasColumnName("verze");
            e.Property(x => x.Akceptace).HasColumnName("akceptace");
            e.Property(x => x.Datum).HasColumnName("datum");
            e.Property(x => x.Termin).HasColumnName("termin");
            e.Property(x => x.PracnostA).HasColumnName("pracnost_a");
            e.Property(x => x.PracnostP).HasColumnName("pracnost_p");
            e.Property(x => x.PracnostT).HasColumnName("pracnost_t");
            e.Property(x => x.PracnostI).HasColumnName("pracnost_i");
            e.Property(x => x.SazbaA).HasColumnName("sazba_a");
            e.Property(x => x.SazbaP).HasColumnName("sazba_p");
            e.Property(x => x.SazbaT).HasColumnName("sazba_t");
            e.Property(x => x.SazbaI).HasColumnName("sazba_i");
            e.Property(x => x.CenaA).HasColumnName("cena_a");
            e.Property(x => x.CenaP).HasColumnName("cena_p");
            e.Property(x => x.CenaT).HasColumnName("cena_t");
            e.Property(x => x.CenaI).HasColumnName("cena_i");
            e.Property(x => x.Cena).HasColumnName("cena");
            e.Property(x => x.SazbaL).HasColumnName("sazba_l");
            e.Property(x => x.PocetL).HasColumnName("pocet_l");
            e.Property(x => x.CenaL).HasColumnName("cena_l");
            e.Property(x => x.RozpadLicence).HasColumnName("rozpad_licence");
            e.Property(x => x.Popis).HasColumnName("popis");
            e.Property(x => x.VyjadreniKalk).HasColumnName("vyjadreni_kalk");
            e.Property(x => x.TextTermin).HasColumnName("text_termin");

            e.HasIndex(x => new { x.Pid, x.Akceptace }).HasDatabaseName("ix_hot_kalkulace_pid_akceptace");
        });

    }
}
