using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Services.Common;

/// <summary>
/// Pravidla nad číselníkem stavů úkolů. Číselník se udržuje přímo v databázi
/// (z aplikace se needituje — nabídka číselníků ho neobsahuje), proto je tohle
/// jediné místo, kde se rozhoduje, co je pozastavení a co ukončený stav.
/// Spec 2026-09-05-ukoncene-ukoly-podbarveni-tisk-design.md, §7.1.
/// </summary>
public static class TaskStatusRules
{
    /// <summary>
    /// Pozastavení se pozná podle názvu — číselník nemá vlastní příznak.
    /// Test je doslova takový, jaký byl dosud v exportu, aby se chování nezměnilo.
    /// </summary>
    public static bool IsPausedName(string? nazev)
        => !string.IsNullOrWhiteSpace(nazev)
           && nazev.Contains("pozastav", StringComparison.CurrentCultureIgnoreCase);

    /// <summary>
    /// Ukončený = koncový a zároveň ne pozastavený. Druhá podmínka je hlídání kolize
    /// (rozhodnutí U1): pozastavený stav nesmí platit za ukončený ani tehdy, když ho
    /// někdo v databázi omylem označí jako koncový.
    /// </summary>
    public static bool IsCompleted(CiselnikStavuUkoluEntity? state)
        => state is not null && state.IsFinal && !IsPausedName(state.Nazev);

    /// <summary>
    /// Záznam v koncovém stavu (is_final) nepřijímá nová vyjádření — transparentní historie,
    /// k doplnění se musí znovu otevřít. Na rozdíl od <see cref="IsCompleted"/> platí holé
    /// is_final: pozastavení se tu neřeší (rozhodnutí 2026-10-14, pozastavené se dopisují).
    /// </summary>
    public static bool IsClosedForComments(CiselnikStavuUkoluEntity? state)
        => state is { IsFinal: true };
}
