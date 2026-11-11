using System.Collections.Concurrent;

namespace PmTracker.Web.Services.ServiceDesk.Prodleni;

/// <summary>Rozhoduje, zda je den pracovní (bez víkendů a státních svátků ČR).</summary>
public interface ICeskeSvatky
{
    /// <summary>Pracovní den = ne sobota, ne neděle, ne státní svátek. Čas se ignoruje.</summary>
    bool JePracovniDen(DateTime den);
}

/// <summary>Státní svátky ČR (zákon 245/2000 Sb.) — pevné + Velký pátek (od 2016) + Velikonoční pondělí.
/// Port <c>DataLayer.Hotline.MOSD_External.GetCzechHolidays</c> ze starého ServiceDesku
/// (stejná pravidla i algoritmus Velikonoc), aby penalizace seděla 1:1.</summary>
public sealed class CeskeSvatky : ICeskeSvatky
{
    private static readonly (int M, int D)[] Pevne =
        [(1, 1), (5, 1), (5, 8), (7, 5), (7, 6), (9, 28), (10, 28), (11, 17), (12, 24), (12, 25), (12, 26)];

    private readonly ConcurrentDictionary<int, HashSet<DateTime>> _cache = new();

    public bool JePracovniDen(DateTime den)
    {
        var d = den.Date;
        if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return false;
        return !_cache.GetOrAdd(d.Year, Svatky).Contains(d);
    }

    private static HashSet<DateTime> Svatky(int rok)
    {
        var set = Pevne.Select(p => new DateTime(rok, p.M, p.D)).ToHashSet();
        var nedele = VelikonocniNedele(rok);
        set.Add(nedele.AddDays(1));                    // Velikonoční pondělí
        if (rok >= 2016) set.Add(nedele.AddDays(-2));  // Velký pátek (svátkem od 2016)
        return set;
    }

    // Anonymní gregoriánský algoritmus (Meeus/Jones/Butcher) — totožný s GetEasterSunday originálu.
    private static DateTime VelikonocniNedele(int y)
    {
        int a = y % 19, b = y / 100, c = y % 100, d = b / 4, e = b % 4, f = (b + 8) / 25, g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30, i = c / 4, k = c % 4, l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451, month = (h + l - 7 * m + 114) / 31, day = (h + l - 7 * m + 114) % 31 + 1;
        return new DateTime(y, month, day);
    }
}
