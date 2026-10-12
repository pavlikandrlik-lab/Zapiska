using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Schedules;

/// <summary>
/// Přepínač „Automatické vyplňování harmonogramu" (uživatel 2026-10-08: musí platit i v návrhu).
/// Zapnutý, když žádný krok napojený na ServiceDesk (1/3/4/6/7/10) není v ručním režimu —
/// kroky 2/5/8/9 jsou ruční vždy a přepínač neovlivňují. Jediné pravidlo pro editor záznamu,
/// editor návrhu i detail návrhu.
/// </summary>
public static class HarmonogramAutoFillRule
{
    public const string Auto = "Auto";
    public const string Manual = "Manual";

    public static bool IsAutoFill(IEnumerable<(int Poradi, byte SkutecnostRezim)> kroky)
        => !kroky.Any(k => !HarmonogramManualSteps.IsManual(k.Poradi)
                           && k.SkutecnostRezim == (byte)SkutecnostRezimEnum.Manual);

    public static string? Normalize(string? rezim)
        => string.Equals(rezim, Auto, StringComparison.OrdinalIgnoreCase) ? Auto
            : string.Equals(rezim, Manual, StringComparison.OrdinalIgnoreCase) ? Manual
            : null;
}
