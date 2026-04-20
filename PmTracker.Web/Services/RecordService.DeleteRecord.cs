using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Audit;

namespace PmTracker.Web.Services;

/// <summary>
/// Fáze 3C Task 1: RecordService.DeleteRecord.cs — DeleteRecordAsync + soft-delete
/// policy + cascade audit + priority invalidation.
/// Další operace (Save, MeetingIdentifier) v samostatných partials.
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

        await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var historyTypeRows = await dbContext.ZaznamHistorieZmenTypu.Where(x => x.ZaznamId == command.ZaznamId).ToListAsync(ct);
        var historyDeadlineRows = await dbContext.ZaznamHistorieTerminu.Where(x => x.ZaznamId == command.ZaznamId).ToListAsync(ct);
        var historyOwnerRows = await dbContext.ZaznamHistorieVlastnik.Where(x => x.ZaznamId == command.ZaznamId).ToListAsync(ct);
        var historySubsystemRows = await dbContext.ZaznamHistorieSubsystem.Where(x => x.ZaznamId == command.ZaznamId).ToListAsync(ct);
        var historyStateRows = await dbContext.ZaznamHistorieStavuZaznamu.Where(x => x.ZaznamId == command.ZaznamId).ToListAsync(ct);
        var historyProjectStateRows = await dbContext.ZaznamHistorieStavuProjektu.Where(x => x.ZaznamId == command.ZaznamId).ToListAsync(ct);
        var externalLinkRows = await dbContext.ZaznamExterniOdkazy.Where(x => x.ZaznamId == command.ZaznamId).ToListAsync(ct);
        var collaborationRows = await dbContext.ZaznamSpoluprace.Where(x => x.ZaznamId == command.ZaznamId).ToListAsync(ct);
        var scheduleRows = await dbContext.ZaznamHarmonogramHodnoty.Where(x => x.ZaznamId == command.ZaznamId).ToListAsync(ct);
        var commentRows = await dbContext.Vyjadreni.Where(x => x.ZaznamId == command.ZaznamId).ToListAsync(ct);
        var oldRecord = RecordAuditSnapshot.FromEntity(record);
        var oldScheduleSnapshot = RecordScheduleAuditSnapshot.FromEntities(command.ZaznamId, scheduleRows);
        var oldCommentSnapshots = commentRows.Select(CommentAuditSnapshot.FromEntity).ToList();

        if (historyTypeRows.Count > 0)
        {
            dbContext.ZaznamHistorieZmenTypu.RemoveRange(historyTypeRows);
        }

        if (historyDeadlineRows.Count > 0)
        {
            dbContext.ZaznamHistorieTerminu.RemoveRange(historyDeadlineRows);
        }

        if (historyOwnerRows.Count > 0)
        {
            dbContext.ZaznamHistorieVlastnik.RemoveRange(historyOwnerRows);
        }

        if (historySubsystemRows.Count > 0)
        {
            dbContext.ZaznamHistorieSubsystem.RemoveRange(historySubsystemRows);
        }

        if (historyStateRows.Count > 0)
        {
            dbContext.ZaznamHistorieStavuZaznamu.RemoveRange(historyStateRows);
        }

        if (historyProjectStateRows.Count > 0)
        {
            dbContext.ZaznamHistorieStavuProjektu.RemoveRange(historyProjectStateRows);
        }

        if (externalLinkRows.Count > 0)
        {
            dbContext.ZaznamExterniOdkazy.RemoveRange(externalLinkRows);
        }

        if (collaborationRows.Count > 0)
        {
            dbContext.ZaznamSpoluprace.RemoveRange(collaborationRows);
        }

        if (scheduleRows.Count > 0)
        {
            dbContext.ZaznamHarmonogramHodnoty.RemoveRange(scheduleRows);
        }

        if (commentRows.Count > 0)
        {
            dbContext.Vyjadreni.RemoveRange(commentRows);
        }

        var priorityRows = await dbContext.ZaznamPriorityUzivatelu
            .Where(x => x.ZaznamId == command.ZaznamId)
            .ToListAsync(ct);
        if (priorityRows.Count > 0)
        {
            dbContext.ZaznamPriorityUzivatelu.RemoveRange(priorityRows);
        }

        if (historyTypeRows.Count > 0
            || historyDeadlineRows.Count > 0
            || historyOwnerRows.Count > 0
            || historySubsystemRows.Count > 0
            || historyStateRows.Count > 0
            || historyProjectStateRows.Count > 0
            || externalLinkRows.Count > 0
            || collaborationRows.Count > 0
            || scheduleRows.Count > 0
            || commentRows.Count > 0
            || priorityRows.Count > 0)
        {
            await dbContext.SaveChangesAsync(ct);
        }

        dbContext.ProjektoveZaznamy.Remove(record);
        await dbContext.SaveChangesAsync(ct);
        auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
            AuditActionType.Delete,
            AuditEntityType.Record,
            command.ZaznamId.ToString(CultureInfo.InvariantCulture),
            oldRecord,
            null));
        if (scheduleRows.Count > 0)
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
    }
}
