using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Audit;

namespace PmTracker.Tests.Unit.Audit;

public sealed class AuditWriteServiceTests
{
    [Theory]
    [InlineData(AuditActionType.Create, "create")]
    [InlineData(AuditActionType.Update, "update")]
    [InlineData(AuditActionType.Delete, "delete")]
    [InlineData(AuditActionType.SoftDelete, "soft_delete")]
    [InlineData(AuditActionType.Deactivate, "deactivate")]
    [InlineData(AuditActionType.Activate, "activate")]
    [InlineData(AuditActionType.Approve, "approve")]
    [InlineData(AuditActionType.Reject, "reject")]
    [InlineData(AuditActionType.Assign, "assign")]
    [InlineData(AuditActionType.Reorder, "reorder")]
    public void ToDatabaseValue_ShouldMapActionTypes(AuditActionType actionType, string expected)
    {
        actionType.ToDatabaseValue().Should().Be(expected);
    }

    [Theory]
    [InlineData(AuditEntityType.Project, "projekt")]
    [InlineData(AuditEntityType.Record, "zaznam")]
    [InlineData(AuditEntityType.RecordSchedule, "zaznam_harmonogram")]
    [InlineData(AuditEntityType.Comment, "vyjadreni")]
    [InlineData(AuditEntityType.Meeting, "jednani")]
    [InlineData(AuditEntityType.Attendance, "ucast")]
    [InlineData(AuditEntityType.ProjectMembership, "obsazeni_projektu")]
    [InlineData(AuditEntityType.ProjectSubsystem, "projekt_subsystem")]
    [InlineData(AuditEntityType.ProjectSubsystemRole, "projekt_subsystem_role")]
    [InlineData(AuditEntityType.Person, "osoba")]
    [InlineData(AuditEntityType.Dictionary, "ciselnik")]
    [InlineData(AuditEntityType.RecordProposal, "record_proposal")]
    [InlineData(AuditEntityType.AuthzUserRole, "authz_user_role")]
    [InlineData(AuditEntityType.AuthzRole, "authz_role")]
    [InlineData(AuditEntityType.AuthzPermission, "authz_permission")]
    [InlineData(AuditEntityType.AuthzRolePermission, "authz_role_permission")]
    public void ToDatabaseValue_ShouldMapEntityTypes(AuditEntityType entityType, string expected)
    {
        entityType.ToDatabaseValue().Should().Be(expected);
    }

    [Fact]
    public void Add_ShouldPreserveNullActor_AndSerializeBeforeAfterStates()
    {
        using var dbContext = CreateDbContext();
        var sut = new AuditWriteService(dbContext, TimeProvider.System);

        sut.Add(null, new AuditWriteEntry(
            AuditActionType.Update,
            AuditEntityType.Record,
            "42",
            new { Before = "old" },
            new { After = "new" }));

        var auditRow = dbContext.ChangeTracker
            .Entries<AuthzAuditLogEntity>()
            .Select(x => x.Entity)
            .Single();

        auditRow.ActorOsobaId.Should().BeNull();
        auditRow.EntityType.Should().Be("zaznam");
        auditRow.EntityId.Should().Be("42");
        auditRow.Action.Should().Be("update");
        auditRow.CreatedAt.Should().BeCloseTo(TimeProvider.System.GetUtcNow().UtcDateTime, TimeSpan.FromSeconds(5));

        using var oldDocument = JsonDocument.Parse(auditRow.OldValue!);
        using var newDocument = JsonDocument.Parse(auditRow.NewValue!);
        oldDocument.RootElement.GetProperty("before").GetString().Should().Be("old");
        newDocument.RootElement.GetProperty("after").GetString().Should().Be("new");
    }

    private static PmTrackerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=PmTrackerAuditUnit;Trusted_Connection=True;")
            .Options;

        return new PmTrackerDbContext(options);
    }
}
