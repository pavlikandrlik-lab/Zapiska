using System.Text.Json;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Services.Audit;

public enum AuditActionType
{
    Create,
    Update,
    Delete,
    SoftDelete,
    Deactivate,
    Activate,
    Approve,
    Reject,
    Assign,
    Reorder
}

public enum AuditEntityType
{
    Project,
    Record,
    RecordSchedule,
    Comment,
    Meeting,
    Attendance,
    ProjectMembership,
    ProjectSubsystem,
    ProjectSubsystemRole,
    Person,
    Dictionary,
    RecordProposal,
    AuthzUserRole,
    AuthzRole,
    AuthzPermission,
    AuthzRolePermission,
    /// <summary>
    /// Review finding S-3: admin re-harvest externí vazby (/Vyjadreni/ReHarvest
    /// nebo /SDConnector/ReHarvest). Entity ID = externiOdkazId.
    /// </summary>
    SdExterniOdkaz
}

public static class AuditActorIds
{
    public const int System = -1;
}

public sealed record AuditWriteEntry(
    AuditActionType ActionType,
    AuditEntityType EntityType,
    string EntityId,
    object? BeforeState,
    object? AfterState);

public interface IAuditWriteService
{
    void Add(int? actorOsobaId, AuditWriteEntry entry);
    Task WriteAsync(int? actorOsobaId, AuditWriteEntry entry, CancellationToken ct = default);
}

internal static class AuditVocabulary
{
    public static string ToDatabaseValue(this AuditActionType actionType) => actionType switch
    {
        AuditActionType.Create => "create",
        AuditActionType.Update => "update",
        AuditActionType.Delete => "delete",
        AuditActionType.SoftDelete => "soft_delete",
        AuditActionType.Deactivate => "deactivate",
        AuditActionType.Activate => "activate",
        AuditActionType.Approve => "approve",
        AuditActionType.Reject => "reject",
        AuditActionType.Assign => "assign",
        AuditActionType.Reorder => "reorder",
        _ => throw new ArgumentOutOfRangeException(nameof(actionType), actionType, null)
    };

    public static string ToDatabaseValue(this AuditEntityType entityType) => entityType switch
    {
        AuditEntityType.Project => "projekt",
        AuditEntityType.Record => "zaznam",
        AuditEntityType.RecordSchedule => "zaznam_harmonogram",
        AuditEntityType.Comment => "vyjadreni",
        AuditEntityType.Meeting => "jednani",
        AuditEntityType.Attendance => "ucast",
        AuditEntityType.ProjectMembership => "obsazeni_projektu",
        AuditEntityType.ProjectSubsystem => "projekt_subsystem",
        AuditEntityType.ProjectSubsystemRole => "projekt_subsystem_role",
        AuditEntityType.Person => "osoba",
        AuditEntityType.Dictionary => "ciselnik",
        AuditEntityType.RecordProposal => "record_proposal",
        AuditEntityType.AuthzUserRole => "authz_user_role",
        AuditEntityType.AuthzRole => "authz_role",
        AuditEntityType.AuthzPermission => "authz_permission",
        AuditEntityType.AuthzRolePermission => "authz_role_permission",
        AuditEntityType.SdExterniOdkaz => "sd_externi_odkaz",
        _ => throw new ArgumentOutOfRangeException(nameof(entityType), entityType, null)
    };
}

internal sealed class AuditWriteService(
    PmTrackerDbContext dbContext,
    TimeProvider timeProvider) : IAuditWriteService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public void Add(int? actorOsobaId, AuditWriteEntry entry)
    {
        dbContext.AuthzAuditLog.Add(new AuthzAuditLogEntity
        {
            ActorOsobaId = actorOsobaId,
            EntityType = entry.EntityType.ToDatabaseValue(),
            EntityId = entry.EntityId,
            Action = entry.ActionType.ToDatabaseValue(),
            OldValue = Serialize(entry.BeforeState),
            NewValue = Serialize(entry.AfterState),
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        });
    }

    public async Task WriteAsync(int? actorOsobaId, AuditWriteEntry entry, CancellationToken ct = default)
    {
        Add(actorOsobaId, entry);
        await dbContext.SaveChangesAsync(ct);
    }

    private static string? Serialize(object? state)
        => state is null ? null : JsonSerializer.Serialize(state, SerializerOptions);
}
