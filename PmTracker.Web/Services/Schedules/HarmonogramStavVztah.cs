namespace PmTracker.Web.Services.Schedules;

/// <summary>
/// Spec 2026-10-24 R2: vůči čemu se počítá sjednocený stav harmonogramu (skluz / předstih).
/// Pořadí členů: <see cref="AktualniKrok"/> je výchozí hodnota (view model bez souhrnu).
/// </summary>
public enum HarmonogramStavVztah
{
    /// <summary>Aktuální krok má vlastní plán: překročení = dnes − plán aktuálního kroku.</summary>
    AktualniKrok,

    /// <summary>Aktuální krok plán nemá: překročení = dnes − celkový termín záznamu.</summary>
    CelkovyTermin,

    /// <summary>Všechny kroky mají skutečnost, aktuální krok není.</summary>
    Dokonceno
}
