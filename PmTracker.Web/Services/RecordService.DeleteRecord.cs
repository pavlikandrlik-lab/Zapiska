using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Audit;

namespace PmTracker.Web.Services;

/// <summary>
/// Fáze 3C Task 1: RecordService.DeleteRecord.cs — DeleteRecordAsync.
/// 2026-04-27: Refactor (varianta a) — manuální cleanup chain všech 13 child
/// tabulek byl nahrazen SQL FK CASCADE (db_upgrade_1_3_12_record_delete_cascade.sql).
/// Aplikace nyní jen načte audit snapshots, smaže parent record a SQL Server
/// vykaskáduje cleanup. Pokud bude v budoucnu přidána nová child tabulka,
/// stačí v migraci dát ON DELETE CASCADE — DeleteRecordAsync se nemusí měnit.
/// </summary>
public sealed partial class RecordService
{
    public async Task DeleteRecordAsync(DeleteRecordCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var record = await dbContext.ProjektoveZaznamy
            .FirstOrDefaultAsync(x => x.Id == command.ZaznamId, ct)
            ?? throw new InvalidOperationException($"Záznam {command.ZaznamId} nebyl nalezen.");

        if (record.ProjektId != command.ProjektId)
        {
            throw new InvalidOperationException("Záznam nepatří do vybraného projektu.");
        }

        // FIX 2026-05-03: app-level cleanup approved_record_id (žádný FK = robust).
        await PreflightCleanupChildRowsAsync(command.ZaznamId, ct);

        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            // Audit snapshots — fetch s AsNoTracking, protože entity jsou jen pro audit
            // payload, ne pro modifikaci. Po SQL CASCADE budou tyto rows smazané;
            // tracked stav by zůstal stale a způsoboval by potenciální concurrency
            // exceptions při následných SaveChanges v tom samém DbContext.
            var scheduleRowsForAudit = await dbContext.ZaznamHarmonogramKroky
                .AsNoTracking()
                .Where(x => x.ZaznamId == command.ZaznamId)
                .ToListAsync(ct);
            var commentRowsForAudit = await dbContext.Vyjadreni
                .AsNoTracking()
                .Where(x => x.ZaznamId == command.ZaznamId)
                .ToListAsync(ct);

            var oldRecord = RecordAuditSnapshot.FromEntity(record);
            var oldScheduleSnapshot = RecordScheduleAuditSnapshot.FromEntities(command.ZaznamId, scheduleRowsForAudit);
            var oldCommentSnapshots = commentRowsForAudit.Select(CommentAuditSnapshot.FromEntity).ToList();

            // SQL FK CASCADE (1_3_12 + 1_3_16) vyřeší všechny child tabulky.
            // approved_record_id cleanup proběhl v PreflightCleanupChildRowsAsync.
            dbContext.ProjektoveZaznamy.Remove(record);
            await dbContext.SaveChangesAsync(ct);

            auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
                AuditActionType.Delete,
                AuditEntityType.Record,
                command.ZaznamId.ToString(CultureInfo.InvariantCulture),
                oldRecord,
                null));

            if (scheduleRowsForAudit.Count > 0)
            {
                auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
                    AuditActionType.Delete,
                    AuditEntityType.RecordSchedule,
                    command.ZaznamId.ToString(CultureInfo.InvariantCulture),
                    oldScheduleSnapshot,
                    null));
            }

            foreach (var oldCommentSnapshot in oldCommentSnapshots)
            {
                auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
                    AuditActionType.Delete,
                    AuditEntityType.Comment,
                    oldCommentSnapshot.Id.ToString(CultureInfo.InvariantCulture),
                    oldCommentSnapshot,
                    null));
            }

            await dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
    }

    /// <summary>
    /// app-level cleanup approved_record_id (NEMÁ FK; ostatní child rows = SQL CASCADE).
    /// </summary>
    private async Task PreflightCleanupChildRowsAsync(int zaznamId, CancellationToken ct)
    {
        var navrhyToCleanup = await dbContext.ZaznamNavrhy
            .Where(n => n.ApprovedRecordId == zaznamId)
            .ToListAsync(ct);
        foreach (var n in navrhyToCleanup) n.ApprovedRecordId = null;
        if (navrhyToCleanup.Count > 0) await dbContext.SaveChangesAsync(ct);
    }
}
