using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Services.Vyzvy;

internal static class VyzvaQueries
{
    public const string PnfKod = "PNF";

    /// <summary>
    /// Filtr pro buffer projektu — PNF externí vazby s ZaradidDoVyzvy=true a bez výzvy.
    /// Joinuje přes ProjektovyZaznam pro získání ProjektId.
    /// </summary>
    public static IQueryable<ZaznamExterniOdkazEntity> WhereVBufferuProjektu(
        this IQueryable<ZaznamExterniOdkazEntity> source,
        PmTrackerDbContext db,
        int projektId,
        int pnfTypId)
        => from ev in source
           join z in db.ProjektoveZaznamy on ev.ZaznamId equals z.Id
           where z.ProjektId == projektId
             && ev.TypOdkazuId == pnfTypId
             && ev.ZaradidDoVyzvy
             && ev.VyzvaId == null
           select ev;

    public static Task<int> GetPnfTypIdAsync(PmTrackerDbContext db, CancellationToken ct)
        => db.CiselnikTypuExternichOdkazu
            .Where(t => t.Kod == PnfKod)
            .Select(t => t.Id)
            .FirstAsync(ct);
}
