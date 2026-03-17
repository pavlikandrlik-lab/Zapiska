using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Services.Data;

public sealed class RecordCommentCommandsUseCase(
    PmTrackerDbContext dbContext,
    IRichTextContentService richTextContentService,
    ICommentAuthorizationPolicy commentAuthorizationPolicy,
    TimeProvider timeProvider) : IRecordCommentCommandsUseCase
{
    public void AddComment(AddCommentCommand command, CurrentUserContextViewModel currentUser)
    {
        var normalizedText = richTextContentService.NormalizeForStorage(command.Text);
        if (!richTextContentService.HasVisibleText(normalizedText))
        {
            throw new InvalidOperationException("Vyjádření nesmí být prázdné.");
        }

        var record = dbContext.ProjektoveZaznamy
            .AsNoTracking()
            .FirstOrDefault(x => x.Id == command.ZaznamId)
            ?? throw new InvalidOperationException($"Záznam {command.ZaznamId} neexistuje.");

        var meeting = dbContext.Jednani
            .AsNoTracking()
            .FirstOrDefault(x => x.Id == command.JednaniId);
        if (meeting is null)
        {
            throw new InvalidOperationException($"Jednání {command.JednaniId} neexistuje.");
        }

        if (meeting.ProjektId != record.ProjektId)
        {
            throw new InvalidOperationException("Vybrané jednání nepatří k tomuto záznamu.");
        }

        EnsureMeetingAllowsCommentChanges(meeting);

        var subsystemLeadEquivalentOsobaIds = ResolveLeadEquivalentOsobaIds(record.ProjektId, record.SubsystemId);
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
        dbContext.SaveChanges();
        WriteAudit(currentUser.OsobaId, "vyjadreni", note.Id.ToString(CultureInfo.InvariantCulture), "create", null, JsonSerializer.Serialize(note));
    }

    public void UpdateComment(UpdateCommentCommand command, CurrentUserContextViewModel currentUser)
    {
        var normalizedText = richTextContentService.NormalizeForStorage(command.Text);
        if (!richTextContentService.HasVisibleText(normalizedText))
        {
            throw new InvalidOperationException("Vyjádření nesmí být prázdné.");
        }

        var comment = dbContext.Vyjadreni.FirstOrDefault(x => x.Id == command.Id)
            ?? throw new InvalidOperationException($"Vyjádření {command.Id} nebylo nalezeno.");

        var meeting = dbContext.Jednani
            .AsNoTracking()
            .FirstOrDefault(x => x.Id == comment.JednaniId)
            ?? throw new InvalidOperationException($"Jednání {comment.JednaniId} neexistuje.");

        var record = dbContext.ProjektoveZaznamy
            .AsNoTracking()
            .FirstOrDefault(x => x.Id == comment.ZaznamId)
            ?? throw new InvalidOperationException($"Záznam {comment.ZaznamId} neexistuje.");

        if (meeting.ProjektId != record.ProjektId)
        {
            throw new InvalidOperationException("Vyjádření je navázáno na neplatnou kombinaci záznamu a jednání.");
        }

        EnsureMeetingAllowsCommentChanges(meeting);

        if (!CanModifyComment(comment, record, currentUser))
        {
            throw new InvalidOperationException("Nemáte oprávnění upravit toto vyjádření.");
        }

        var old = JsonSerializer.Serialize(comment);
        comment.TextVyjadreni = normalizedText;
        comment.DatumVyjadreni = GetLocalNow();
        dbContext.SaveChanges();
        WriteAudit(currentUser.OsobaId, "vyjadreni", comment.Id.ToString(CultureInfo.InvariantCulture), "update", old, JsonSerializer.Serialize(comment));
    }

    public void DeleteComment(DeleteCommentCommand command, CurrentUserContextViewModel currentUser)
    {
        var comment = dbContext.Vyjadreni.FirstOrDefault(x => x.Id == command.Id)
            ?? throw new InvalidOperationException($"Vyjádření {command.Id} nebylo nalezeno.");

        var meeting = dbContext.Jednani
            .AsNoTracking()
            .FirstOrDefault(x => x.Id == comment.JednaniId)
            ?? throw new InvalidOperationException($"Jednání {comment.JednaniId} neexistuje.");

        var record = dbContext.ProjektoveZaznamy
            .AsNoTracking()
            .FirstOrDefault(x => x.Id == comment.ZaznamId)
            ?? throw new InvalidOperationException($"Záznam {comment.ZaznamId} neexistuje.");

        if (meeting.ProjektId != record.ProjektId)
        {
            throw new InvalidOperationException("Vyjádření je navázáno na neplatnou kombinaci záznamu a jednání.");
        }

        EnsureMeetingAllowsCommentChanges(meeting);

        if (!CanModifyComment(comment, record, currentUser))
        {
            throw new InvalidOperationException("Nemáte oprávnění smazat toto vyjádření.");
        }

        var old = JsonSerializer.Serialize(comment);
        dbContext.Vyjadreni.Remove(comment);
        dbContext.SaveChanges();
        WriteAudit(currentUser.OsobaId, "vyjadreni", comment.Id.ToString(CultureInfo.InvariantCulture), "delete", old, null);
    }

    private DateTime GetLocalNow()
        => timeProvider.GetLocalNow().LocalDateTime;

    private bool CanModifyComment(VyjadreniEntity comment, ProjektovyZaznamEntity record, CurrentUserContextViewModel currentUser)
    {
        var subsystemLeadEquivalentOsobaIds = ResolveLeadEquivalentOsobaIds(record.ProjektId, record.SubsystemId);
        return commentAuthorizationPolicy.CanModifyComment(
            currentUser,
            record.ProjektId,
            subsystemLeadEquivalentOsobaIds,
            comment.AutorOsobaId);
    }

    private void EnsureMeetingAllowsCommentChanges(JednaniEntity meeting)
    {
        var status = dbContext.CiselnikStavuJednani
            .AsNoTracking()
            .FirstOrDefault(x => x.Id == meeting.StavJednaniId);

        if (MeetingStatePolicy.IsReadOnly(meeting, status))
        {
            throw new InvalidOperationException("Vyjádření u uzavřeného jednání nelze upravovat ani mazat. Nejprve jednání otevřete.");
        }
    }

    private List<int> ResolveLeadEquivalentOsobaIds(int projectId, int subsystemId)
    {
        var leadRoleIds = dbContext.CiselnikRoliSubsystemu.AsNoTracking()
            .Where(x => x.Kod == SubsystemRoleCodes.Lead || x.Kod == SubsystemRoleCodes.DeputyLead)
            .Select(x => x.Id)
            .ToHashSet();
        if (leadRoleIds.Count == 0)
        {
            return [];
        }

        var activeProjectSubsystems = dbContext.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == projectId && !x.DatumOdebrani.HasValue)
            .ToList();
        var activeProjectSubsystemIds = activeProjectSubsystems.Select(x => x.Id).ToHashSet();

        return dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
            .Where(x => activeProjectSubsystemIds.Contains(x.ProjektSubsystemId)
                && !x.DatumOdebrani.HasValue
                && leadRoleIds.Contains(x.RoleSubsystemuId))
            .ToList()
            .GroupBy(x => activeProjectSubsystems.First(ps => ps.Id == x.ProjektSubsystemId).SubsystemId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(x => x.OsobaId).Distinct().OrderBy(x => x).ToList())
            .GetValueOrDefault(subsystemId, []);
    }

    private void WriteAudit(int? actorOsobaId, string entityType, string entityId, string action, string? oldValue, string? newValue)
    {
        dbContext.AuthzAuditLog.Add(new AuthzAuditLogEntity
        {
            ActorOsobaId = actorOsobaId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            OldValue = oldValue,
            NewValue = newValue,
            CreatedAt = DateTime.UtcNow
        });
        dbContext.SaveChanges();
    }
}
