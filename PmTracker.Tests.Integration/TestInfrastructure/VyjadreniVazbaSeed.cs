using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Tests.Integration.TestInfrastructure;

/// <summary>
/// Spec 2026-10-14 R2 — externí vazba PNF s navázaným vyjádřením, jak ji v provozu založí harvest
/// (VyjadreniHarvestService). Zakládá se přímo do DB: ve fixture je ServiceDesk vypnutý a validace
/// by nový tiket odmítla. Automat z ní krok doplní jen s <see cref="PevnyTypTiketuVyjadreniQueryService"/>.
/// </summary>
internal static class VyjadreniVazbaSeed
{
    public const string Cislo = "123456";

    public static async Task<int> SeedPnfVazbaSVyjadrenimAsync(PmTrackerDbContext db, int recordId, int poradi, DateTime datum)
    {
        var typId = await db.CiselnikTypuExternichOdkazu.AsNoTracking()
            .Where(x => x.Kod == "PNF").Select(x => x.Id).FirstAsync();
        var link = new ZaznamExterniOdkazEntity { ZaznamId = recordId, TypOdkazuId = typId, Cislo = Cislo };
        db.ZaznamExterniOdkazy.Add(link);
        await db.SaveChangesAsync();
        db.VyjadreniVazby.Add(new ZaznamHarmonogramVyjadreniVazbaEntity
        {
            ZaznamId = recordId,
            Poradi = (byte)poradi,
            ExterniOdkazId = link.Id,
            HotVyjadreniId = 987654,
            DatumVyjadreni = datum,
            Source = (byte)VazbaSource.Auto,
            Stav = (byte)VazbaStav.Active,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return link.Id;
    }
}
