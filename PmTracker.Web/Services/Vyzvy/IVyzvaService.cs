using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy.Contracts;

namespace PmTracker.Web.Services.Vyzvy;

public interface IVyzvaService
{
    Task<IReadOnlyList<VyzvaBufferItem>> GetBufferAsync(int projektId, CancellationToken ct);
    /// <summary>Výzvy projektu v daném roce, od nejnovější k nejstarší.</summary>
    Task<IReadOnlyList<VyzvaDetail>> GetVyzvyAsync(int projektId, int rok, CancellationToken ct);

    /// <summary>Roky, ve kterých projekt má aspoň jednu výzvu, sestupně. Prázdné, pokud žádné nemá.</summary>
    Task<IReadOnlyList<int>> GetRokyAsync(int projektId, CancellationToken ct);
    Task<VyzvaDetail?> GetVyzvaAsync(int vyzvaId, CancellationToken ct);

    /// <summary>
    /// Založí prázdnou výzvu s ručně zadaným pořadovým číslem. Číslo se domlouvá externě
    /// (SVA), aplikace ho negeneruje — ověřuje jen rozsah 1–999 a duplicitu v rámci
    /// (číslo rámcové smlouvy, rok). PNF se do výzvy přesouvají samostatnou akcí.
    /// </summary>
    Task<VyzvaResult<VyzvaDetail>> ZalozitVyzvuAsync(
        int projektId, int poradoveVRoce, int zalozilOsobaId, DateTime now, CancellationToken ct);

    /// <summary>Pořadová čísla už obsazená v daném roce a rámcové smlouvě projektu — nápověda do formuláře.</summary>
    Task<IReadOnlyList<int>> GetObsazenaCislaAsync(int projektId, int rok, CancellationToken ct);

    /// <summary>Číslo rámcové smlouvy projektu — kontext pro uživatele ve formuláři Nová výzva.</summary>
    Task<string?> GetCisloRamcoveSmlouvyAsync(int projektId, CancellationToken ct);

    Task<VyzvaResult<VyzvaDetail>> ZmenitStavAsync(
        int vyzvaId, VyzvaStav novyStav, int zmenilOsobaId, DateTime now, CancellationToken ct);

    Task<VyzvaResult<Unit>> NastavitZaradidAsync(
        int externiOdkazId, bool zaradit, CancellationToken ct);

    Task<VyzvaResult<Unit>> PrerditPnfAsync(
        int externiOdkazId, int? cilovaVyzvaId, CancellationToken ct);

    /// <summary>
    /// Zjistí ProjektId externího odkazu. Používá se v controllerech k per-project
    /// autorizačnímu checku (akce přijímá ExterniOdkazId, ne ProjektId přímo).
    /// </summary>
    Task<int?> ResolveExterniOdkazProjektIdAsync(int externiOdkazId, CancellationToken ct);
}
