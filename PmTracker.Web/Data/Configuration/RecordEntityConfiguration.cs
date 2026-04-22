using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Data.Configuration;

internal sealed class RecordEntityConfiguration : IEntityTypeConfiguration<ProjektovyZaznamEntity>
{
    public void Configure(EntityTypeBuilder<ProjektovyZaznamEntity> builder)
    {
        builder.ToTable("projektove_zaznamy", table =>
        {
            table.HasCheckConstraint("CK_projektove_zaznamy_cislo_viditelne_typ", "cislo_viditelne_typ IN (0, 1)");
            table.HasCheckConstraint("CK_projektove_zaznamy_cislo_viditelne_consistency", "((cislo_viditelne_typ = 0 AND cislo_jednani_zdroj_id IS NULL AND cislo_viditelne_b = 0) OR (cislo_viditelne_typ = 1 AND cislo_jednani_zdroj_id IS NOT NULL AND cislo_viditelne_b > 0))");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProjektId).HasColumnName("projekt_id");
        builder.Property(x => x.KategorieId).HasColumnName("kategorie_id");
        builder.Property(x => x.AktualniTypUkoluId).HasColumnName("aktualni_typ_ukolu_id");
        builder.Property(x => x.StavUkoluId).HasColumnName("stav_ukolu_id");
        builder.Property(x => x.CisloZaznamu).HasColumnName("cislo_zaznamu");
        builder.Property(x => x.CisloViditelne).HasColumnName("cislo_viditelne");
        builder.Property(x => x.CisloViditelneTyp).HasColumnName("cislo_viditelne_typ");
        builder.Property(x => x.CisloViditelneA).HasColumnName("cislo_viditelne_a");
        builder.Property(x => x.CisloViditelneB).HasColumnName("cislo_viditelne_b");
        builder.Property(x => x.CisloJednaniZdrojId).HasColumnName("cislo_jednani_zdroj_id");
        builder.Property(x => x.Nazev).HasColumnName("nazev");
        builder.Property(x => x.Cil).HasColumnName("cil").HasMaxLength(500);
        builder.Property(x => x.Popis).HasColumnName("popis");
        builder.Property(x => x.VlastnikId).HasColumnName("vlastnik_id");
        builder.Property(x => x.DatumZalozeni).HasColumnName("datum_zalozeni");
        builder.Property(x => x.DatumUkonceni).HasColumnName("datum_ukonceni");
        builder.Property(x => x.SubsystemId).HasColumnName("subsystem_id");
        builder.Property(x => x.HarmonogramSablonaVerze).HasColumnName("harmonogram_sablona_verze");
        builder.HasIndex(x => new { x.ProjektId, x.CisloViditelne })
            .HasFilter("[cislo_viditelne] IS NOT NULL")
            .HasDatabaseName("UX_projektove_zaznamy_projekt_cislo_viditelne")
            .IsUnique();
        builder.HasIndex(x => new { x.ProjektId, x.CisloViditelneA, x.CisloViditelneB })
            .HasFilter("[cislo_viditelne_typ] = 1")
            .HasDatabaseName("UX_projektove_zaznamy_projekt_meeting_order")
            .IsUnique();
        builder.HasIndex(x => new { x.ProjektId, x.CisloViditelneA, x.CisloViditelneB, x.CisloZaznamu })
            .HasDatabaseName("IX_projektove_zaznamy_projekt_sort");
        builder.HasOne<HarmonogramSablonaEntity>()
            .WithMany()
            .HasForeignKey(x => x.HarmonogramSablonaVerze)
            .HasConstraintName("FK_projektove_zaznamy_harmonogram_sablona");
        builder.HasOne<JednaniEntity>()
            .WithMany()
            .HasForeignKey(x => x.CisloJednaniZdrojId)
            .HasConstraintName("FK_projektove_zaznamy_cislo_jednani_zdroj")
            .OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class RecordTypeChangeHistoryEntityConfiguration : IEntityTypeConfiguration<ZaznamHistorieZmenTypuEntity>
{
    public void Configure(EntityTypeBuilder<ZaznamHistorieZmenTypuEntity> builder)
    {
        builder.ToTable("zaznam_historie_zmen_typu");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ZaznamId).HasColumnName("zaznam_id");
        builder.Property(x => x.PuvodniTypId).HasColumnName("puvodni_typ_id");
        builder.Property(x => x.NovyTypId).HasColumnName("novy_typ_id");
        builder.Property(x => x.DatumZmeny).HasColumnName("datum_zmeny");
        builder.Property(x => x.ZmenilOsobaId).HasColumnName("zmenil_osoba_id");
        builder.HasIndex(x => x.ZaznamId)
            .HasDatabaseName("IX_zaznam_historie_zmen_typu_zaznam_id");
    }
}

internal sealed class RecordProposalEntityConfiguration : IEntityTypeConfiguration<ZaznamNavrhEntity>
{
    public void Configure(EntityTypeBuilder<ZaznamNavrhEntity> builder)
    {
        builder.ToTable("zaznam_navrhy", table =>
        {
            table.HasCheckConstraint("CK_zaznam_navrhy_typ", "typ_navrhu IN ('CREATE_RECORD', 'SCHEDULE_PLAN_CHANGE')");
            table.HasCheckConstraint("CK_zaznam_navrhy_stav", "stav IN ('PENDING', 'APPROVED', 'REJECTED')");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProjektId).HasColumnName("projekt_id");
        builder.Property(x => x.ZaznamId).HasColumnName("zaznam_id");
        builder.Property(x => x.SubsystemId).HasColumnName("subsystem_id");
        builder.Property(x => x.TypNavrhu).HasColumnName("typ_navrhu").HasMaxLength(64);
        builder.Property(x => x.Stav).HasColumnName("stav").HasMaxLength(32);
        builder.Property(x => x.PayloadJson).HasColumnName("payload_json");
        builder.Property(x => x.CreatedByOsobaId).HasColumnName("created_by_osoba_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.DecidedByOsobaId).HasColumnName("decided_by_osoba_id");
        builder.Property(x => x.DecidedAt).HasColumnName("decided_at");
        builder.Property(x => x.ApprovedRecordId).HasColumnName("approved_record_id");
        builder.Property(x => x.RowVersion).HasColumnName("row_version").IsRowVersion();

        builder.HasIndex(x => new { x.ProjektId, x.CreatedAt })
            .HasDatabaseName("IX_zaznam_navrhy_projekt_created_at");
        builder.HasIndex(x => new { x.ZaznamId, x.TypNavrhu, x.Stav })
            .HasDatabaseName("IX_zaznam_navrhy_zaznam_typ_stav");
        builder.HasIndex(x => new { x.ZaznamId, x.TypNavrhu })
            .HasFilter("[stav] = 'PENDING' AND [typ_navrhu] = 'SCHEDULE_PLAN_CHANGE'")
            .HasDatabaseName("UX_zaznam_navrhy_pending_schedule_per_record")
            .IsUnique();
    }
}

internal sealed class RecordPriorityMatrixEntityConfiguration : IEntityTypeConfiguration<ZaznamPriorityUzivateleEntity>
{
    public void Configure(EntityTypeBuilder<ZaznamPriorityUzivateleEntity> builder)
    {
        builder.ToTable("zaznam_priority_uzivatelu");
        builder.HasKey(x => new { x.ZaznamId, x.OsobaId });
        builder.Property(x => x.ZaznamId).HasColumnName("zaznam_id");
        builder.Property(x => x.OsobaId).HasColumnName("osoba_id");
        builder.Property(x => x.Score).HasColumnName("score");
        builder.Property(x => x.ComputedAt).HasColumnName("computed_at");
        builder.Property(x => x.RoleWeight).HasColumnName("role_weight");
        builder.Property(x => x.DeadlineSignal).HasColumnName("deadline_signal");
        builder.Property(x => x.MilestoneSignal).HasColumnName("milestone_signal");
        builder.HasIndex(x => new { x.OsobaId, x.Score, x.ZaznamId })
            .HasDatabaseName("IX_zaznam_priority_uzivatelu_osoba_score_zaznam")
            .IsDescending(false, true, false);
    }
}

internal sealed class RecordPriorityRebuildStateEntityConfiguration : IEntityTypeConfiguration<ZaznamPriorityRebuildStateEntity>
{
    public void Configure(EntityTypeBuilder<ZaznamPriorityRebuildStateEntity> builder)
    {
        builder.ToTable("zaznam_priority_rebuild_state", table =>
        {
            table.HasCheckConstraint("CK_zaznam_priority_rebuild_state_singleton", "id = 1");
            table.HasCheckConstraint(
                "CK_zaznam_priority_rebuild_state_status",
                "last_full_rebuild_status IN ('NEVER', 'SUCCESS', 'FAILED', 'RUNNING')");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.LastFullRebuildAt).HasColumnName("last_full_rebuild_at");
        builder.Property(x => x.LastFullRebuildStatus).HasColumnName("last_full_rebuild_status").HasMaxLength(16);
        builder.Property(x => x.LastFullRebuildDurationMs).HasColumnName("last_full_rebuild_duration_ms");
        builder.Property(x => x.LastFullRebuildTaskCount).HasColumnName("last_full_rebuild_task_count");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    }
}

internal sealed class RecordDeadlineHistoryEntityConfiguration : IEntityTypeConfiguration<ZaznamHistorieTerminuEntity>
{
    public void Configure(EntityTypeBuilder<ZaznamHistorieTerminuEntity> builder)
    {
        builder.ToTable("zaznam_historie_terminu");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ZaznamId).HasColumnName("zaznam_id");
        builder.Property(x => x.PuvodniDatum).HasColumnName("puvodni_datum");
        builder.Property(x => x.NoveDatum).HasColumnName("nove_datum");
        builder.Property(x => x.DatumZmeny).HasColumnName("datum_zmeny");
        builder.Property(x => x.Duvod).HasColumnName("duvod");
        builder.HasIndex(x => x.ZaznamId)
            .HasDatabaseName("IX_zaznam_historie_terminu_zaznam_id");
    }
}

internal sealed class RecordOwnerHistoryEntityConfiguration : IEntityTypeConfiguration<ZaznamHistorieVlastnikEntity>
{
    public void Configure(EntityTypeBuilder<ZaznamHistorieVlastnikEntity> builder)
    {
        builder.ToTable("zaznam_historie_vlastnik");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ZaznamId).HasColumnName("zaznam_id");
        builder.Property(x => x.PuvodniVlastnik).HasColumnName("puvodni_vlastnik");
        builder.Property(x => x.NovyVlastnik).HasColumnName("novy_vlastnik");
        builder.Property(x => x.DatumZmeny).HasColumnName("datum_zmeny");
        builder.HasIndex(x => x.ZaznamId)
            .HasDatabaseName("IX_zaznam_historie_vlastnik_zaznam_id");
    }
}

internal sealed class RecordSubsystemHistoryEntityConfiguration : IEntityTypeConfiguration<ZaznamHistorieSubsystemEntity>
{
    public void Configure(EntityTypeBuilder<ZaznamHistorieSubsystemEntity> builder)
    {
        builder.ToTable("zaznam_historie_subsystem");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ZaznamId).HasColumnName("zaznam_id");
        builder.Property(x => x.PuvodniSubsystem).HasColumnName("puvodni_subsystem");
        builder.Property(x => x.NovySubsystem).HasColumnName("novy_subsystem");
        builder.Property(x => x.DatumZmeny).HasColumnName("datum_zmeny");
        builder.HasIndex(x => x.ZaznamId)
            .HasDatabaseName("IX_zaznam_historie_subsystem_zaznam_id");
    }
}

internal sealed class RecordStateHistoryEntityConfiguration : IEntityTypeConfiguration<ZaznamHistorieStavuZaznamuEntity>
{
    public void Configure(EntityTypeBuilder<ZaznamHistorieStavuZaznamuEntity> builder)
    {
        builder.ToTable("zaznam_historie_stavu_zaznamu");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ZaznamId).HasColumnName("zaznam_id");
        builder.Property(x => x.PuvodniStav).HasColumnName("puvodni_stav");
        builder.Property(x => x.NovyStav).HasColumnName("novy_stav");
        builder.Property(x => x.DatumZmeny).HasColumnName("datum_zmeny");
        builder.HasIndex(x => x.ZaznamId)
            .HasDatabaseName("IX_zaznam_historie_stavu_zaznamu_zaznam_id");
    }
}

internal sealed class RecordProjectStateHistoryEntityConfiguration : IEntityTypeConfiguration<ZaznamHistorieStavuProjektuEntity>
{
    public void Configure(EntityTypeBuilder<ZaznamHistorieStavuProjektuEntity> builder)
    {
        builder.ToTable("zaznam_historie_stavu_projektu");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ZaznamId).HasColumnName("zaznam_id");
        builder.Property(x => x.PuvodniStav).HasColumnName("puvodni_stav");
        builder.Property(x => x.NovyStav).HasColumnName("novy_stav");
        builder.Property(x => x.DatumZmeny).HasColumnName("datum_zmeny");
        builder.HasIndex(x => x.ZaznamId)
            .HasDatabaseName("IX_zaznam_historie_stavu_projektu_zaznam_id");
    }
}

internal sealed class RecordExternalLinkEntityConfiguration : IEntityTypeConfiguration<ZaznamExterniOdkazEntity>
{
    public void Configure(EntityTypeBuilder<ZaznamExterniOdkazEntity> builder)
    {
        builder.ToTable("zaznam_externi_odkazy");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ZaznamId).HasColumnName("zaznam_id");
        builder.Property(x => x.TypOdkazuId).HasColumnName("typ_odkazu_id");
        builder.Property(x => x.Cislo).HasColumnName("cislo");
        builder.Property(x => x.PredpokladanaCena).HasColumnName("predpokladana_cena").HasColumnType("decimal(18,2)");
        builder.Property(x => x.DatumObjednani).HasColumnName("datum_objednani");
        builder.Property(x => x.PlanDodani).HasColumnName("plan_dodani");
        builder.Property(x => x.DatumDodani).HasColumnName("datum_dodani");
        builder.Property(x => x.DatumPrevzeti).HasColumnName("datum_prevzeti");
        builder.Property(x => x.VyzvaId).HasColumnName("vyzva_id");
        builder.Property(x => x.ZaradidDoVyzvy).HasColumnName("zaradid_do_vyzvy").HasDefaultValue(false);

        builder.HasOne<VyzvaEntity>()
            .WithMany()
            .HasForeignKey(x => x.VyzvaId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.Cislo)
            .HasDatabaseName("ux_zaznam_externi_odkazy_cislo_in_vyzve")
            .HasFilter("[vyzva_id] IS NOT NULL")
            .IsUnique();
        builder.HasIndex(x => x.ZaznamId)
            .HasDatabaseName("IX_zaznam_externi_odkazy_zaznam_id");
    }
}

internal sealed class RecordCollaborationEntityConfiguration : IEntityTypeConfiguration<ZaznamSpolupraceEntity>
{
    public void Configure(EntityTypeBuilder<ZaznamSpolupraceEntity> builder)
    {
        builder.ToTable("zaznam_spoluprace");
        builder.HasKey(x => new { x.ZaznamId, x.OsobaId });
        builder.Property(x => x.ZaznamId).HasColumnName("zaznam_id");
        builder.Property(x => x.OsobaId).HasColumnName("osoba_id");
    }
}

internal sealed class RecordScheduleValueEntityConfiguration : IEntityTypeConfiguration<ZaznamHarmonogramHodnotaEntity>
{
    public void Configure(EntityTypeBuilder<ZaznamHarmonogramHodnotaEntity> builder)
    {
        builder.ToTable("zaznam_harmonogram_hodnoty");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.ZaznamId, x.TypId })
            .IsUnique()
            .HasDatabaseName("UQ_zaznam_harmonogram_hodnoty_zaznam_typ");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ZaznamId).HasColumnName("zaznam_id");
        builder.Property(x => x.TypId).HasColumnName("typ_id");
        builder.Property(x => x.HodnotaInt).HasColumnName("hodnota_int");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    }
}
