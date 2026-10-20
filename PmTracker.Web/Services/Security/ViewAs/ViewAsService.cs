using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Services.Audit;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Services.Security.ViewAs;

public sealed record ViewAsDecision(int RealOsobaId, string RealDisplayName, int TargetOsobaId);

public enum ViewAsStartResult
{
    Started,
    SelfTarget,
    NotSuperAdmin,
    TargetNotFound
}

public interface IViewAsService
{
    Task<ViewAsDecision?> EvaluateAsync(HttpContext httpContext, int realOsobaId, CancellationToken ct);
    Task<ViewAsStartResult> StartAsync(HttpContext httpContext, int realOsobaId, int targetOsobaId, CancellationToken ct);
    Task StopAsync(HttpContext httpContext, int realOsobaId, int? targetOsobaId, CancellationToken ct);
}

public sealed class ViewAsService(
    PmTrackerDbContext dbContext,
    IAuditWriteService auditWriteService,
    IPersonIdentityMatcher personIdentityMatcher,
    ILogger<ViewAsService> logger) : IViewAsService
{
    public async Task<ViewAsDecision?> EvaluateAsync(HttpContext httpContext, int realOsobaId, CancellationToken ct)
    {
        var raw = httpContext.Request.Cookies[ViewAsCookie.Name];
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        if (!ViewAsCookie.TryParse(raw, out var cookieRealOsobaId, out var targetOsobaId))
        {
            return Reject(httpContext, "neplatný formát cookie", realOsobaId, null);
        }

        if (cookieRealOsobaId != realOsobaId)
        {
            return Reject(httpContext, "cookie patří jinému přihlášenému uživateli", realOsobaId, targetOsobaId);
        }

        if (targetOsobaId == realOsobaId)
        {
            return Reject(httpContext, "cílem je sám přihlášený uživatel", realOsobaId, targetOsobaId);
        }

        if (!await IsSuperAdminAsync(realOsobaId, ct))
        {
            return Reject(httpContext, "přihlášený uživatel není superadmin", realOsobaId, targetOsobaId);
        }

        if (!await PersonExistsAsync(targetOsobaId, ct))
        {
            return Reject(httpContext, "cílová osoba neexistuje", realOsobaId, targetOsobaId);
        }

        var realDisplayName = await LoadDisplayNameAsync(realOsobaId, ct);
        return new ViewAsDecision(realOsobaId, realDisplayName, targetOsobaId);
    }

    public async Task<ViewAsStartResult> StartAsync(HttpContext httpContext, int realOsobaId, int targetOsobaId, CancellationToken ct)
    {
        if (targetOsobaId == realOsobaId)
        {
            return ViewAsStartResult.SelfTarget;
        }

        if (!await IsSuperAdminAsync(realOsobaId, ct))
        {
            return ViewAsStartResult.NotSuperAdmin;
        }

        if (targetOsobaId <= 0 || !await PersonExistsAsync(targetOsobaId, ct))
        {
            return ViewAsStartResult.TargetNotFound;
        }

        // Audit PŘED cookie: když se audit nepovede, pohled nezačne.
        await auditWriteService.WriteAsync(realOsobaId, BuildAuditEntry(AuditActionType.ViewAsStart, realOsobaId, targetOsobaId), ct);
        httpContext.Response.Cookies.Append(
            ViewAsCookie.Name,
            ViewAsCookie.Format(realOsobaId, targetOsobaId),
            ViewAsCookie.BuildOptions(httpContext.Request));

        logger.LogInformation(
            "Pohled jako zahájen. RealOsobaId={RealOsobaId} TargetOsobaId={TargetOsobaId}",
            realOsobaId,
            targetOsobaId);
        return ViewAsStartResult.Started;
    }

    public async Task StopAsync(HttpContext httpContext, int realOsobaId, int? targetOsobaId, CancellationToken ct)
    {
        ViewAsCookie.Delete(httpContext);
        if (targetOsobaId is null)
        {
            return;
        }

        // Ukončení musí projít vždy — selhání auditu jen zalogujeme.
        try
        {
            await auditWriteService.WriteAsync(realOsobaId, BuildAuditEntry(AuditActionType.ViewAsStop, realOsobaId, targetOsobaId.Value), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "Audit ukončení pohledu jako selhal. RealOsobaId={RealOsobaId} TargetOsobaId={TargetOsobaId}",
                realOsobaId,
                targetOsobaId);
        }

        logger.LogInformation(
            "Pohled jako ukončen. RealOsobaId={RealOsobaId} TargetOsobaId={TargetOsobaId}",
            realOsobaId,
            targetOsobaId);
    }

    private ViewAsDecision? Reject(HttpContext httpContext, string reason, int realOsobaId, int? targetOsobaId)
    {
        logger.LogWarning(
            "Cookie pohledu jako zamítnuto ({Reason}) a smazáno. RealOsobaId={RealOsobaId} TargetOsobaId={TargetOsobaId}",
            reason,
            realOsobaId,
            targetOsobaId);
        ViewAsCookie.Delete(httpContext);
        return null;
    }

    private Task<bool> IsSuperAdminAsync(int osobaId, CancellationToken ct)
        => dbContext.AuthzSuperadmins.AsNoTracking().AnyAsync(x => x.OsobaId == osobaId, ct);

    private Task<bool> PersonExistsAsync(int osobaId, CancellationToken ct)
        => dbContext.Osoby.AsNoTracking().AnyAsync(x => x.Id == osobaId, ct);

    private async Task<string> LoadDisplayNameAsync(int osobaId, CancellationToken ct)
    {
        var person = await dbContext.Osoby.AsNoTracking()
            .Where(x => x.Id == osobaId)
            .Select(x => new { x.Titul, x.Jmeno, x.Prijmeni })
            .FirstOrDefaultAsync(ct);
        var name = person is null ? null : personIdentityMatcher.BuildPersonName(person.Titul, person.Jmeno, person.Prijmeni);
        return string.IsNullOrWhiteSpace(name) ? $"Uživatel #{osobaId}" : name;
    }

    private static AuditWriteEntry BuildAuditEntry(AuditActionType action, int realOsobaId, int targetOsobaId)
        => new(
            action,
            AuditEntityType.ViewAs,
            targetOsobaId.ToString(CultureInfo.InvariantCulture),
            BeforeState: null,
            AfterState: new { realOsobaId, targetOsobaId });
}
