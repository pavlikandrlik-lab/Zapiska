using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Audit;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Security.ViewAs;

namespace PmTracker.Tests.Unit.Security.ViewAs;

public sealed class ViewAsServiceTests
{
    private const int Admin = 10;
    private const int OtherAdmin = 11;
    private const int Plain = 20;
    private const int Target = 30;

    private sealed class RecordingAudit : IAuditWriteService
    {
        public List<(int? Actor, AuditWriteEntry Entry)> Written { get; } = [];
        public bool Throw { get; init; }
        public void Add(int? actorOsobaId, AuditWriteEntry entry) => Written.Add((actorOsobaId, entry));
        public Task WriteAsync(int? actorOsobaId, AuditWriteEntry entry, CancellationToken ct = default)
        {
            if (Throw) throw new InvalidOperationException("audit down");
            Written.Add((actorOsobaId, entry));
            return Task.CompletedTask;
        }
    }

    private static async Task<PmTrackerDbContext> SeedAsync()
    {
        var options = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new PmTrackerDbContext(options);
        db.Osoby.AddRange(
            new OsobaEntity { Id = Admin, Titul = "Ing.", Jmeno = "Ada", Prijmeni = "Adminova" },
            new OsobaEntity { Id = OtherAdmin, Jmeno = "Olga", Prijmeni = "Ostatni" },
            new OsobaEntity { Id = Plain, Jmeno = "Petr", Prijmeni = "Prosty" },
            new OsobaEntity { Id = Target, Jmeno = "Tomas", Prijmeni = "Cil" });
        db.AuthzSuperadmins.AddRange(
            new AuthzSuperadminEntity { OsobaId = Admin },
            new AuthzSuperadminEntity { OsobaId = OtherAdmin });
        await db.SaveChangesAsync();
        return db;
    }

    private static ViewAsService Create(PmTrackerDbContext db, IAuditWriteService? audit = null)
        => new(db, audit ?? new RecordingAudit(), new PersonIdentityMatcher(new TextNormalizer()), NullLogger<ViewAsService>.Instance);

    private static DefaultHttpContext WithCookie(string? value)
    {
        var context = new DefaultHttpContext();
        if (value is not null)
        {
            context.Request.Headers.Cookie = $"{ViewAsCookie.Name}={value}";
        }
        return context;
    }

    private static bool CookieDeleted(HttpContext context)
        => context.Response.Headers.SetCookie.ToString().StartsWith($"{ViewAsCookie.Name}=;", StringComparison.Ordinal);

    [Fact]
    public async Task Evaluate_NoCookie_ReturnsNull_AndTouchesNothing()
    {
        await using var db = await SeedAsync();
        var context = WithCookie(null);

        var decision = await Create(db).EvaluateAsync(context, Admin, CancellationToken.None);

        decision.Should().BeNull();
        context.Response.Headers.SetCookie.Should().BeEmpty();
    }

    [Fact]
    public async Task Evaluate_ValidSuperadminCookie_ReturnsTargetAndRealName()
    {
        await using var db = await SeedAsync();
        var context = WithCookie($"{Admin}-{Target}");

        var decision = await Create(db).EvaluateAsync(context, Admin, CancellationToken.None);

        decision.Should().NotBeNull();
        decision!.TargetOsobaId.Should().Be(Target);
        decision.RealOsobaId.Should().Be(Admin);
        decision.RealDisplayName.Should().Contain("Adminova");
        CookieDeleted(context).Should().BeFalse();
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("11-30")]   // cookie patří jinému skutečnému uživateli
    [InlineData("10-10")]   // cíl = sám sebe
    [InlineData("10-999")]  // cílová osoba neexistuje
    public async Task Evaluate_InvalidCookie_IsIgnoredAndDeleted(string cookie)
    {
        await using var db = await SeedAsync();
        var context = WithCookie(cookie);

        var decision = await Create(db).EvaluateAsync(context, Admin, CancellationToken.None);

        decision.Should().BeNull();
        CookieDeleted(context).Should().BeTrue();
    }

    [Fact]
    public async Task Evaluate_RealUserNotSuperadmin_IsIgnoredAndDeleted()
    {
        await using var db = await SeedAsync();
        var context = WithCookie($"{Plain}-{Target}");

        var decision = await Create(db).EvaluateAsync(context, Plain, CancellationToken.None);

        decision.Should().BeNull();
        CookieDeleted(context).Should().BeTrue();
    }

    [Fact]
    public async Task Start_Superadmin_WritesAuditThenCookie()
    {
        await using var db = await SeedAsync();
        var audit = new RecordingAudit();
        var context = new DefaultHttpContext();

        var result = await Create(db, audit).StartAsync(context, Admin, Target, CancellationToken.None);

        result.Should().Be(ViewAsStartResult.Started);
        audit.Written.Should().ContainSingle();
        audit.Written[0].Actor.Should().Be(Admin);
        audit.Written[0].Entry.ActionType.Should().Be(AuditActionType.ViewAsStart);
        audit.Written[0].Entry.EntityType.Should().Be(AuditEntityType.ViewAs);
        audit.Written[0].Entry.EntityId.Should().Be(Target.ToString());
        context.Response.Headers.SetCookie.ToString().Should().StartWith($"{ViewAsCookie.Name}={Admin}-{Target}");
    }

    [Theory]
    [InlineData(Plain, Target, ViewAsStartResult.NotSuperAdmin)]
    [InlineData(Admin, Admin, ViewAsStartResult.SelfTarget)]
    [InlineData(Admin, 999, ViewAsStartResult.TargetNotFound)]
    [InlineData(Admin, 0, ViewAsStartResult.TargetNotFound)]
    public async Task Start_Rejected_WritesNoCookieAndNoAudit(int real, int target, ViewAsStartResult expected)
    {
        await using var db = await SeedAsync();
        var audit = new RecordingAudit();
        var context = new DefaultHttpContext();

        var result = await Create(db, audit).StartAsync(context, real, target, CancellationToken.None);

        result.Should().Be(expected);
        audit.Written.Should().BeEmpty();
        context.Response.Headers.SetCookie.Should().BeEmpty();
    }

    [Fact]
    public async Task Start_AuditFails_NoCookie()
    {
        await using var db = await SeedAsync();
        var context = new DefaultHttpContext();

        var act = () => Create(db, new RecordingAudit { Throw = true }).StartAsync(context, Admin, Target, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        context.Response.Headers.SetCookie.Should().BeEmpty("bez auditu se pohled nesmí zahájit");
    }

    [Fact]
    public async Task Stop_DeletesCookie_AndAudits()
    {
        await using var db = await SeedAsync();
        var audit = new RecordingAudit();
        var context = new DefaultHttpContext();

        await Create(db, audit).StopAsync(context, Admin, Target, CancellationToken.None);

        CookieDeleted(context).Should().BeTrue();
        audit.Written.Should().ContainSingle(x => x.Entry.ActionType == AuditActionType.ViewAsStop);
    }

    [Fact]
    public async Task Stop_AuditFails_StillDeletesCookie_AndDoesNotThrow()
    {
        await using var db = await SeedAsync();
        var context = new DefaultHttpContext();

        await Create(db, new RecordingAudit { Throw = true }).StopAsync(context, Admin, Target, CancellationToken.None);

        CookieDeleted(context).Should().BeTrue("ukončení pohledu musí projít vždy");
    }

    [Fact]
    public async Task Stop_WithoutActiveTarget_DeletesCookie_WithoutAudit()
    {
        await using var db = await SeedAsync();
        var audit = new RecordingAudit();
        var context = new DefaultHttpContext();

        await Create(db, audit).StopAsync(context, Admin, null, CancellationToken.None);

        CookieDeleted(context).Should().BeTrue();
        audit.Written.Should().BeEmpty();
    }
}
