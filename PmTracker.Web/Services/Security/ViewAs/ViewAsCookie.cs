using System.Globalization;

namespace PmTracker.Web.Services.Security.ViewAs;

/// <summary>
/// Cookie režimu „Zobrazit jako“. Nepodepisuje se: platnost rozhoduje server při každém
/// requestu (skutečný přihlášený musí být superadmin a shodovat se s první částí hodnoty).
/// </summary>
public static class ViewAsCookie
{
    public const string Name = "pmtracker.viewas";

    public static string Format(int realOsobaId, int targetOsobaId)
        => string.Create(CultureInfo.InvariantCulture, $"{realOsobaId}-{targetOsobaId}");

    public static bool TryParse(string? raw, out int realOsobaId, out int targetOsobaId)
    {
        realOsobaId = 0;
        targetOsobaId = 0;
        if (string.IsNullOrEmpty(raw))
        {
            return false;
        }

        var parts = raw.Split('-');
        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var real)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var target)
            || real <= 0
            || target <= 0)
        {
            return false;
        }

        realOsobaId = real;
        targetOsobaId = target;
        return true;
    }

    public static CookieOptions BuildOptions(HttpRequest request) => new()
    {
        HttpOnly = true,
        Secure = request.IsHttps,
        // Lax, ne Strict: odkaz z Outlooku/Teams by jinak otevřel aplikaci bez pohledu
        // a první další klik by pohled nečekaně „zapnul“.
        SameSite = SameSiteMode.Lax,
        IsEssential = true,
        Path = "/"
    };

    public static void Delete(HttpContext context)
        => context.Response.Cookies.Delete(Name, BuildOptions(context.Request));
}
