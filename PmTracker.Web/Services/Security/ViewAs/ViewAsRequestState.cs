namespace PmTracker.Web.Services.Security.ViewAs;

/// <summary>
/// Scoped stav pohledu pro jeden request. Čte ho middleware i EF pojistka, která nemá
/// přístup k HttpContextu; služby na pozadí mají vlastní scope, a tedy stav neaktivní.
/// </summary>
public sealed class ViewAsRequestState
{
    public bool IsActive { get; private set; }
    public int? RealOsobaId { get; private set; }
    public int? TargetOsobaId { get; private set; }
    public bool WritesPermitted { get; private set; }

    public bool BlocksWrites => IsActive && !WritesPermitted;

    public void Activate(int realOsobaId, int targetOsobaId)
    {
        IsActive = true;
        RealOsobaId = realOsobaId;
        TargetOsobaId = targetOsobaId;
    }

    public void PermitWrites() => WritesPermitted = true;
}
