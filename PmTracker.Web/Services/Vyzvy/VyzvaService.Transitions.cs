using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy.Contracts;

namespace PmTracker.Web.Services.Vyzvy;

public sealed partial class VyzvaService
{
    public async Task<VyzvaResult<VyzvaDetail>> ZmenitStavAsync(
        int vyzvaId, VyzvaStav novyStav, int zmenilOsobaId, DateTime now, CancellationToken ct)
    {
        var vyzva = await _db.Vyzvy.FirstOrDefaultAsync(v => v.Id == vyzvaId, ct);
        if (vyzva == null)
            return Fail<VyzvaDetail>(VyzvaErrorCode.VyzvaNotFound, "Výzva nenalezena");

        var puvodni = vyzva.Stav;
        if (!VyzvaStateMachine.JePovolenyPrechod(puvodni, novyStav))
            return Fail<VyzvaDetail>(VyzvaErrorCode.InvalidStateTransition,
                $"Přechod {puvodni} → {novyStav} není povolen");

        vyzva.Stav = novyStav;

        if (novyStav == VyzvaStav.Odeslano)
        {
            vyzva.DatumOdeslani = now;
            vyzva.OdeslalOsobaId = zmenilOsobaId;
        }

        if (novyStav == VyzvaStav.Zruseno)
        {
            var polozky = await _db.ZaznamExterniOdkazy
                .Where(ev => ev.VyzvaId == vyzvaId)
                .ToListAsync(ct);
            foreach (var p in polozky) p.VyzvaId = null;
        }

        _db.VyzvaHistorieStavu.Add(new VyzvaHistorieStavuEntity
        {
            VyzvaId = vyzvaId,
            PuvodniStav = puvodni,
            NovyStav = novyStav,
            DatumZmeny = now,
            ZmenilOsobaId = zmenilOsobaId,
        });
        await _db.SaveChangesAsync(ct);

        var detail = await GetVyzvaAsync(vyzvaId, ct);
        return new VyzvaResult<VyzvaDetail>.Ok(detail!);
    }
}
