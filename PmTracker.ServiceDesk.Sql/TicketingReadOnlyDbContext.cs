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
    internal DbSet<HotVyjadreniEntity> HotVyjadreni => Set<HotVyjadreniEntity>();
    internal DbSet<HotSubsystemEntity> HotSubsystemy => Set<HotSubsystemEntity>();
    internal DbSet<HotModulyEntity> HotModuly => Set<HotModulyEntity>();
    internal DbSet<HotIsEntity> HotIs => Set<HotIsEntity>();

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

            // V reálné HOT_ZAZNAMY je `radek` typu INT, ale entita ho má jako long
            // (defensivně proti přetečení). SqlBuffer.get_Int64 nedělá widening Int32→Int64,
            // takže EF musí dostat HasConversion<int> aby četl GetInt32 a převedl na long.
            // Bez něj: InvalidCastException při materializaci HotZaznamEntity.
            e.Property(x => x.Radek).HasColumnName("radek").HasConversion<int>();
            // V reálné intranetNEW.dbo.HOT_ZAZNAMY je sloupec `id` typu INT NULL,
            // ale celá appka pracuje s 6-ciferným číslem tiketu jako stringem
            // (regex v SDConnector, DTO HotZaznamDto.Id, fingerprint key).
            // Custom converter: NULL/empty string ↔ NULL v DB, jinak int.Parse.
            // Bez něj EF Core hodí InvalidCastException při materializaci HotZaznamEntity
            // (DB hodnota INT, property string). Memory: "Ticket bez id = mimo scope" —
            // testy seedují empty string Id pro tento edge-case, converter to musí honorovat.
            e.Property(x => x.Id)
                .HasColumnName("id")
                .HasConversion(
                    s => string.IsNullOrEmpty(s) ? (int?)null : int.Parse(s, System.Globalization.CultureInfo.InvariantCulture),
                    i => i.HasValue ? i.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty);
            e.Property(x => x.TypZaznamu).HasColumnName("typ_zaznamu").HasMaxLength(5);
            e.Property(x => x.Strucne).HasColumnName("strucne").HasMaxLength(250);
            e.Property(x => x.Popis).HasColumnName("popis");
            e.Property(x => x.Pid).HasColumnName("pid").HasMaxLength(50);
            e.Property(x => x.Stav).HasColumnName("stav").HasMaxLength(50);
            e.Property(x => x.Splneno).HasColumnName("splneno").HasColumnType("smalldatetime");
            e.Property(x => x.SlaDeadline).HasColumnName("sla_deadline").HasColumnType("datetime");
            e.Property(x => x.Datum).HasColumnName("datum").HasColumnType("smalldatetime");

            // nová pole
            e.Property(x => x.Modul).HasColumnName("modul").HasMaxLength(50);
            e.Property(x => x.Subsystem).HasColumnName("subsystem").HasMaxLength(5);
            e.Property(x => x.TermPl).HasColumnName("term_pl").HasColumnType("smalldatetime");
            e.Property(x => x.DatResT).HasColumnName("dat_res_t").HasColumnType("smalldatetime");
            e.Property(x => x.DatDod).HasColumnName("dat_dod").HasColumnType("smalldatetime");
            e.Property(x => x.Dulezitost).HasColumnName("dulezitost").HasMaxLength(10);
            e.Property(x => x.Zavaznost).HasColumnName("zavaznost").HasMaxLength(50);
            e.Property(x => x.Dodavatel).HasColumnName("dodavatel").HasMaxLength(50);
            e.Property(x => x.ResTym).HasColumnName("res_tym").HasMaxLength(50);
            e.Property(x => x.Zpracoval).HasColumnName("zpracoval").HasMaxLength(50);
            e.Property(x => x.Uzivatel).HasColumnName("uzivatel").HasMaxLength(50);
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(50);
            e.Property(x => x.ZalHfu).HasColumnName("zal_HFU").HasMaxLength(50);
            e.Property(x => x.PriznakZamceni).HasColumnName("priznak_zamceni");
            e.Property(x => x.PriznakGdpr).HasColumnName("priznak_gdpr");
            e.Property(x => x.Schvaleno).HasColumnName("schvaleno");
        });

        mb.Entity<HotKalkulaceEntity>(e =>
        {
            e.ToTable("HOT_KALKULACE", "dbo");
            e.HasKey(x => x.Id);
            // HOT_KALKULACE.id je INT v reálné DB; entita má long pro defensivní headroom.
            // HasConversion<int>() nutné aby SqlDataReader nečetl GetInt64 (= InvalidCastException).
            e.Property(x => x.Id).HasColumnName("id").HasConversion<int>();
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

        mb.Entity<HotVyjadreniEntity>(e =>
        {
            e.ToTable("HOT_VYJADRENI", "dbo");
            e.HasKey(x => x.Id);
            // HOT_VYJADRENI.id je INT v reálné DB; entita má long.
            // HasConversion<int>() — viz Radek/HotKalkulace komentář.
            e.Property(x => x.Id).HasColumnName("id").HasConversion<int>();
            e.Property(x => x.Typ).HasColumnName("typ").HasMaxLength(10);
            e.Property(x => x.Pid).HasColumnName("pid").HasMaxLength(50);
            e.Property(x => x.Datum).HasColumnName("datum");
            e.Property(x => x.Zpracoval).HasColumnName("zpracoval").HasMaxLength(200);
            e.Property(x => x.Popis).HasColumnName("popis");
            e.Property(x => x.Tym).HasColumnName("tym").HasMaxLength(50);
            // viditelne_dodavateli je TINYINT v reálné DB; entita má int? pro DTO kontrakt.
            // Bez HasConversion<byte?>() volá EF GetInt32 → InvalidCastException Byte → Int32.
            e.Property(x => x.ViditelneDodavateli).HasColumnName("viditelne_dodavateli").HasConversion<byte?>();

            e.HasIndex(x => x.Pid).HasDatabaseName("ix_hot_vyjadreni_pid");
        });

        mb.Entity<HotSubsystemEntity>(e =>
        {
            e.ToTable("HOT_SUBSYSTEM", "dbo");
            // reálné PK je zkratka
            e.HasKey(x => x.Zkratka);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Nazev).HasColumnName("nazev").HasMaxLength(50);
            e.Property(x => x.Zkratka).HasColumnName("zkratka").HasMaxLength(5).IsRequired();
            e.Property(x => x.Aktivita).HasColumnName("aktivita").HasMaxLength(10);
            e.Property(x => x.Dodavatel).HasColumnName("dodavatel").HasMaxLength(10);
            e.Property(x => x.PriznakGdprSub).HasColumnName("priznak_gdpr_sub");
        });

        mb.Entity<HotModulyEntity>(e =>
        {
            e.ToTable("HOT_MODULY", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Modul).HasColumnName("modul").HasMaxLength(50);
            e.Property(x => x.Zkratka).HasColumnName("zkratka").HasMaxLength(10).IsRequired();
            e.Property(x => x.Subsystem).HasColumnName("subsystem").HasMaxLength(5);
            e.Property(x => x.IdIS).HasColumnName("id_IS");
            e.Property(x => x.Faze).HasColumnName("faze").HasMaxLength(8);
            e.Property(x => x.Aktivita).HasColumnName("aktivita").HasMaxLength(10);
            e.Property(x => x.Dodavatel).HasColumnName("dodavatel").HasMaxLength(50);
        });

        mb.Entity<HotIsEntity>(e =>
        {
            e.ToTable("HOT_IS", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID");   // Pozor: v DB je 'ID' (velká písmena) — viz discovery
            e.Property(x => x.Nazev).HasColumnName("nazev").HasMaxLength(50);
            e.Property(x => x.Zkratka).HasColumnName("zkratka").HasMaxLength(10);
            e.Property(x => x.Aktivita).HasColumnName("aktivita").HasMaxLength(10).IsFixedLength();
            e.Property(x => x.Limit).HasColumnName("limit").HasColumnType("numeric(18,2)");
            e.Property(x => x.Cerpani).HasColumnName("cerpani").HasColumnType("numeric(18,2)");
            e.Property(x => x.Semafor).HasColumnName("semafor").HasMaxLength(5);
        });
    }
}
