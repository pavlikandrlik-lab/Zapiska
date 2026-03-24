using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Services;

public sealed class CommentService(
    PmTrackerDbContext dbContext,
    IRichTextContentService richTextContentService,
    ICommentAuthorizationPolicy commentAuthorizationPolicy,
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
        var canAdd = commentAuthorizationPolicy.CanAddComment(currentUser, record.ProjektId, subsystemLeadEquivalentOsobaIds);
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
        await WriteAuditAsync(
            currentUser.OsobaId,
            "vyjadreni",
            note.Id.ToString(CultureInfo.InvariantCulture),
            "create",
            null,
            JsonSerializer.Serialize(note),
            ct);
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

        if (!await CanModifyCommentAsync(comment, record, currentUser, ct))
        {
            throw new InvalidOperationException("Nemáte oprávnění upravit toto vyjádření.");
        }

        var old = JsonSerializer.Serialize(comment);
        comment.TextVyjadreni = normalizedText;
        comment.DatumVyjadreni = GetLocalNow();
        await dbContext.SaveChangesAsync(ct);
        await WriteAuditAsync(
            currentUser.OsobaId,
            "vyjadreni",
            comment.Id.ToString(CultureInfo.InvariantCulture),
            "update",
            old,
            JsonSerializer.Serialize(comment),
            ct);
    }

    public async Task DeleteCommentAsync(DeleteCommentCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
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

        if (!await CanModifyCommentAsync(comment, record, currentUser, ct))
        {
            throw new InvalidOperationException("Nemáte oprávnění smazat toto vyjádření.");
        }

        var old = JsonSerializer.Serialize(comment);
        dbContext.Vyjadreni.Remove(comment);
        await dbContext.SaveChangesAsync(ct);
        await WriteAuditAsync(
            currentUser.OsobaId,
            "vyjadreni",
            comment.Id.ToString(CultureInfo.InvariantCulture),
            "delete",
            old,
            null,
            ct);
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
            if (!commentAuthorizationPolicy.CanAddComment(currentUser, record.ProjektId, subsystemLeadEquivalentOsobaIds))
            {
                throw new InvalidOperationException("Nemáte oprávnění přidat vyjádření k tomuto záznamu.");
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
            AddAuditEntry(
                currentUser.OsobaId,
                "vyjadreni",
                note.Id.ToString(CultureInfo.InvariantCulture),
                "create",
                null,
                JsonSerializer.Serialize(note));
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
        CurrentUserContextViewModel currentUser,
        CancellationToken ct)
    {
        var subsystemLeadEquivalentOsobaIds = await ResolveLeadEquivalentOsobaIdsAsync(record.ProjektId, record.SubsystemId, ct);
        return commentAuthorizationPolicy.CanModifyComment(
            currentUser,
            record.ProjektId,
            subsystemLeadEquivalentOsobaIds,
            comment.AutorOsobaId);
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

    private async Task<List<int>> ResolveLeadEquivalentOsobaIdsAsync(int projectId, int subsystemId, CancellationToken ct)
    {
        var leadRoleIds = await dbContext.CiselnikRoliSubsystemu.AsNoTracking()
            .Where(x => x.Kod == SubsystemRoleCodes.Lead || x.Kod == SubsystemRoleCodes.DeputyLead)
            .Select(x => x.Id)
            .ToListAsync(ct);
        if (leadRoleIds.Count == 0)
        {
            return [];
        }

        var leadRoleIdSet = leadRoleIds.ToHashSet();
        var activeProjectSubsystems = await dbContext.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == projectId && !x.DatumOdebrani.HasValue)
            .ToListAsync(ct);
        var activeProjectSubsystemIds = activeProjectSubsystems.Select(x => x.Id).ToHashSet();

        var rows = await dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
            .Where(x => activeProjectSubsystemIds.Contains(x.ProjektSubsystemId)
                && !x.DatumOdebrani.HasValue
                && leadRoleIdSet.Contains(x.RoleSubsystemuId))
            .ToListAsync(ct);

        return rows
            .GroupBy(x => activeProjectSubsystems.First(ps => ps.Id == x.ProjektSubsystemId).SubsystemId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(x => x.OsobaId).Distinct().OrderBy(x => x).ToList())
            .GetValueOrDefault(subsystemId, []);
    }

    private async Task<Dictionary<int, List<int>>> ResolveLeadEquivalentOsobaIdsBySubsystemAsync(int projectId, CancellationToken ct)
    {
        var leadRoleIds = (await dbContext.CiselnikRoliSubsystemu.AsNoTracking()
            .Where(x => x.Kod == SubsystemRoleCodes.Lead || x.Kod == SubsystemRoleCodes.DeputyLead)
            .Select(x => x.Id)
            .ToListAsync(ct))
            .ToHashSet();
        if (leadRoleIds.Count == 0)
        {
            return [];
        }

        var activeProjectSubsystems = await dbContext.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == projectId && !x.DatumOdebrani.HasValue)
            .ToListAsync(ct);
        var subsystemIdByProjectSubsystemId = activeProjectSubsystems.ToDictionary(x => x.Id, x => x.SubsystemId);
        var activeProjectSubsystemIds = subsystemIdByProjectSubsystemId.Keys.ToHashSet();
        var rows = await dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
            .Where(x => activeProjectSubsystemIds.Contains(x.ProjektSubsystemId)
                && !x.DatumOdebrani.HasValue
                && leadRoleIds.Contains(x.RoleSubsystemuId))
            .ToListAsync(ct);

        return rows
            .GroupBy(x => subsystemIdByProjectSubsystemId[x.ProjektSubsystemId])
            .ToDictionary(
                group => group.Key,
                group => group.Select(x => x.OsobaId).Distinct().OrderBy(x => x).ToList());
    }

    private void AddAuditEntry(int? actorOsobaId, string entityType, string entityId, string action, string? oldValue, string? newValue)
    {
        dbContext.AuthzAuditLog.Add(new AuthzAuditLogEntity
        {
            ActorOsobaId = actorOsobaId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            OldValue = oldValue,
            NewValue = newValue,
            CreatedAt = GetUtcNow()
        });
    }

    private async Task WriteAuditAsync(
        int? actorOsobaId,
        string entityType,
        string entityId,
        string action,
        string? oldValue,
        string? newValue,
        CancellationToken ct)
    {
        AddAuditEntry(actorOsobaId, entityType, entityId, action, oldValue, newValue);
        await dbContext.SaveChangesAsync(ct);
    }
}
