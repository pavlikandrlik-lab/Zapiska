using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Security;
using PmTracker.Web.Services.Audit;

namespace PmTracker.Web.Services;

public sealed class CommentService(
    PmTrackerDbContext dbContext,
    IRichTextContentService richTextContentService,
    ICommentAuthorizationPolicy commentAuthorizationPolicy,
    IAuditWriteService auditWriteService,
    IProjectRoleCache projectRoleCache,
    TimeProvider timeProvider) : ICommentService
{
    public async Task AddCommentAsync(AddCommentCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var normalizedText = NormalizeCommentText(command.Text);
        var record = await dbContext.ProjektoveZaznamy
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == command.ZaznamId, ct)
            ?? throw new InvalidOperationException($"Záznam {command.ZaznamId} neexistuje.");

        var meeting = await dbContext.Jednani
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == command.JednaniId, ct)
            ?? throw new InvalidOperationException($"Jednání {command.JednaniId} neexistuje.");

        if (meeting.ProjektId != record.ProjektId)
        {
            throw new InvalidOperationException("Vybrané jednání nepatří k tomuto záznamu.");
        }

        await EnsureMeetingAllowsCommentChangesAsync(meeting, ct);

        var subsystemLeadEquivalentOsobaIds = await ResolveLeadEquivalentOsobaIdsAsync(record.ProjektId, record.SubsystemId, ct);
        var meetingState = await dbContext.CiselnikStavuJednani
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == meeting.StavJednaniId, ct);
        var canAdd = commentAuthorizationPolicy.CanAddComment(
            currentUser,
            record.ProjektId,
            subsystemLeadEquivalentOsobaIds,
            IsDraftMeeting(meetingState));
        if (!canAdd)
        {
            throw new InvalidOperationException("Nemáte oprávnění přidat vyjádření k tomuto záznamu.");
        }

        var note = new VyjadreniEntity
        {
            ZaznamId = command.ZaznamId,
            JednaniId = command.JednaniId,
            AutorOsobaId = currentUser.OsobaId,
            TextVyjadreni = normalizedText,
            DatumVyjadreni = GetLocalNow()
        };

        dbContext.Vyjadreni.Add(note);
        await dbContext.SaveChangesAsync(ct);
        auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
            AuditActionType.Create,
            AuditEntityType.Comment,
            note.Id.ToString(CultureInfo.InvariantCulture),
            null,
            CommentAuditSnapshot.FromEntity(note)));
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateCommentAsync(UpdateCommentCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var normalizedText = NormalizeCommentText(command.Text);
        var comment = await dbContext.Vyjadreni.FirstOrDefaultAsync(x => x.Id == command.Id, ct)
            ?? throw new InvalidOperationException($"Vyjádření {command.Id} nebylo nalezeno.");

        var meeting = await dbContext.Jednani
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == comment.JednaniId, ct)
            ?? throw new InvalidOperationException($"Jednání {comment.JednaniId} neexistuje.");

        var record = await dbContext.ProjektoveZaznamy
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == comment.ZaznamId, ct)
            ?? throw new InvalidOperationException($"Záznam {comment.ZaznamId} neexistuje.");

        if (meeting.ProjektId != record.ProjektId)
        {
            throw new InvalidOperationException("Vyjádření je navázáno na neplatnou kombinaci záznamu a jednání.");
        }

        await EnsureMeetingAllowsCommentChangesAsync(meeting, ct);

        var meetingState = await dbContext.CiselnikStavuJednani
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == meeting.StavJednaniId, ct);
        if (!await CanModifyCommentAsync(comment, record, meetingState, currentUser, ct))
        {
            throw new InvalidOperationException("Nemáte oprávnění upravit toto vyjádření.");
        }

        var old = CommentAuditSnapshot.FromEntity(comment);
        comment.TextVyjadreni = normalizedText;
        comment.DatumVyjadreni = GetLocalNow();
        await dbContext.SaveChangesAsync(ct);
        auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
            AuditActionType.Update,
            AuditEntityType.Comment,
            comment.Id.ToString(CultureInfo.InvariantCulture),
            old,
            CommentAuditSnapshot.FromEntity(comment)));
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteCommentAsync(DeleteCommentCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var comment = await dbContext.Vyjadreni.FirstOrDefaultAsync(x => x.Id == command.Id, ct)
            ?? throw new InvalidOperationException($"Vyjádření {command.Id} nebylo nalezeno.");

        // FIX 2026-05-04: orphan-tolerant cleanup. Pokud meeting/record byly smazané (cascade
        // race nebo manual DB cleanup), comment zůstal orphan v DB. Před fixem throw
        // "Jednání X neexistuje" / "Záznam X neexistuje" → user nemohl smazat orphan vyjádření.
        // Per user feedback "logiku bych očekával robustní": pokud parent chybí, comment je
        // bezpochyby smazatelný (= orphan cleanup), authorization fallback na SuperAdmin only.
        var meeting = await dbContext.Jednani
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == comment.JednaniId, ct);
        var record = await dbContext.ProjektoveZaznamy
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == comment.ZaznamId, ct);

        if (meeting is not null && record is not null)
        {
            // Standardní path — oba parent existují, plná authorization check.
            if (meeting.ProjektId != record.ProjektId)
            {
                throw new InvalidOperationException("Vyjádření je navázáno na neplatnou kombinaci záznamu a jednání.");
            }

            await EnsureMeetingAllowsCommentChangesAsync(meeting, ct);

            var meetingState = await dbContext.CiselnikStavuJednani
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == meeting.StavJednaniId, ct);
            if (!await CanModifyCommentAsync(comment, record, meetingState, currentUser, ct))
            {
                throw new InvalidOperationException("Nemáte oprávnění smazat toto vyjádření.");
            }
        }
        else
        {
            // Orphan path — meeting nebo record neexistuje. Jen SuperAdmin může cleanup.
            if (!currentUser.IsSuperAdmin)
            {
                throw new InvalidOperationException(
                    $"Vyjádření #{comment.Id} je orphan (parent jednání/záznam neexistuje). " +
                    "Cleanup může provést pouze SuperAdmin.");
            }
        }

        var old = CommentAuditSnapshot.FromEntity(comment);
        dbContext.Vyjadreni.Remove(comment);
        await dbContext.SaveChangesAsync(ct);
        auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
            AuditActionType.Delete,
            AuditEntityType.Comment,
            comment.Id.ToString(CultureInfo.InvariantCulture),
            old,
            null));
        await dbContext.SaveChangesAsync(ct);
    }

    public Task SaveMeetingNoteAsync(SaveMeetingNoteCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
        => SaveMeetingNotesBatchAsync(
            command.JednaniId,
            [(command.ZaznamId, command.Text)],
            currentUser,
            ct);

    public async Task SaveMeetingNotesBatchAsync(int meetingId, IEnumerable<(int ZaznamId, string Text)> rows, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var normalizedRows = rows
            .Where(x => x.ZaznamId > 0 && !string.IsNullOrWhiteSpace(x.Text))
            .Select(x => (x.ZaznamId, Text: x.Text))
            .ToList();
        if (normalizedRows.Count == 0)
        {
            return;
        }

        var meeting = await dbContext.Jednani.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == meetingId, ct)
            ?? throw new InvalidOperationException($"Jednání {meetingId} neexistuje.");
        await EnsureMeetingAllowsCommentChangesAsync(meeting, ct);
        var meetingState = await dbContext.CiselnikStavuJednani
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == meeting.StavJednaniId, ct);
        var isDraftMeeting = IsDraftMeeting(meetingState);

        var recordIds = normalizedRows.Select(x => x.ZaznamId).Distinct().ToList();
        var recordsById = await dbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.ProjektId == meeting.ProjektId && recordIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
        var leadEquivalentOsobaIdsBySubsystem = await ResolveLeadEquivalentOsobaIdsBySubsystemAsync(meeting.ProjektId, ct);
        var localNow = GetLocalNow();
        var notes = new List<VyjadreniEntity>(normalizedRows.Count);

        foreach (var row in normalizedRows)
        {
            if (!recordsById.TryGetValue(row.ZaznamId, out var record))
            {
                throw new InvalidOperationException($"Záznam {row.ZaznamId} neexistuje.");
            }

            var normalizedText = NormalizeCommentText(row.Text);
            var subsystemLeadEquivalentOsobaIds = leadEquivalentOsobaIdsBySubsystem.GetValueOrDefault(record.SubsystemId, []);
            // F3.8: zápis z jednání používá meetings.notes.edit / .subsystemlead (record-level filter),
            // ne obecný comments.add — vedoucí subsystému píše jen svůj subsystém v DRAFT.
            if (!commentAuthorizationPolicy.CanSaveMeetingNote(currentUser, record.ProjektId, subsystemLeadEquivalentOsobaIds, isDraftMeeting))
            {
                throw new InvalidOperationException("Nemáte oprávnění zapsat poznámku k tomuto záznamu v rámci jednání.");
            }

            notes.Add(new VyjadreniEntity
            {
                ZaznamId = record.Id,
                JednaniId = meetingId,
                AutorOsobaId = currentUser.OsobaId,
                TextVyjadreni = normalizedText,
                DatumVyjadreni = localNow
            });
        }

        dbContext.Vyjadreni.AddRange(notes);
        await dbContext.SaveChangesAsync(ct);

        foreach (var note in notes)
        {
            auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
                AuditActionType.Create,
                AuditEntityType.Comment,
                note.Id.ToString(CultureInfo.InvariantCulture),
                null,
                CommentAuditSnapshot.FromEntity(note)));
        }

        await dbContext.SaveChangesAsync(ct);
    }

    private string NormalizeCommentText(string text)
    {
        var normalizedText = richTextContentService.NormalizeForStorage(text);
        if (!richTextContentService.HasVisibleText(normalizedText))
        {
            throw new InvalidOperationException("Vyjádření nesmí být prázdné.");
        }

        return normalizedText;
    }

    private DateTime GetLocalNow()
        => timeProvider.GetLocalNow().LocalDateTime;

    private DateTime GetUtcNow()
        => timeProvider.GetUtcNow().UtcDateTime;

    private async Task<bool> CanModifyCommentAsync(
        VyjadreniEntity comment,
        ProjektovyZaznamEntity record,
        CiselnikStavuJednaniEntity? meetingState,
        CurrentUserContextViewModel currentUser,
        CancellationToken ct)
    {
        var subsystemLeadEquivalentOsobaIds = await ResolveLeadEquivalentOsobaIdsAsync(record.ProjektId, record.SubsystemId, ct);
        return commentAuthorizationPolicy.CanModifyComment(
            currentUser,
            record.ProjektId,
            subsystemLeadEquivalentOsobaIds,
            comment.AutorOsobaId,
            IsDraftMeeting(meetingState));
    }

    private async Task EnsureMeetingAllowsCommentChangesAsync(JednaniEntity meeting, CancellationToken ct)
    {
        var status = await dbContext.CiselnikStavuJednani
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == meeting.StavJednaniId, ct);

        if (MeetingStatePolicy.IsReadOnly(meeting, status))
        {
            throw new InvalidOperationException("Vyjádření u uzavřeného jednání nelze upravovat ani mazat. Nejprve jednání otevřete.");
        }
    }

    private static bool IsDraftMeeting(CiselnikStavuJednaniEntity? status)
        => string.Equals(status?.Kod, "DRAFT", StringComparison.OrdinalIgnoreCase);

    private Task<IReadOnlyList<int>> ResolveLeadEquivalentOsobaIdsAsync(int projectId, int subsystemId, CancellationToken ct)
        => projectRoleCache.GetLeadEquivalentOsobaIdsAsync(projectId, subsystemId, ct);

    private Task<IReadOnlyDictionary<int, IReadOnlyList<int>>> ResolveLeadEquivalentOsobaIdsBySubsystemAsync(int projectId, CancellationToken ct)
        => projectRoleCache.GetLeadEquivalentOsobaIdsBySubsystemAsync(projectId, ct);

}
