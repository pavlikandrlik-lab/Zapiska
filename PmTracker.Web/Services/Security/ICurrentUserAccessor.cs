namespace PmTracker.Web.Services.Security;

/// <summary>
/// Poskytuje přístup k aktuálně přihlášené osobě z HTTP request kontextu.
/// Implementace čte z HttpContext.Items (naplněno v middleware nebo BaseController).
/// </summary>
public interface ICurrentUserAccessor
{
    int? OsobaId { get; }
}
