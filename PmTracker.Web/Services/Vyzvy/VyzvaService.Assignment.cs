using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Services.Vyzvy;

public sealed partial class VyzvaService
{
    public async Task<VyzvaResult<Unit>> NastavitZaradidAsync(
        int externiOdkazId, bool zaradit, CancellationToken ct)
    {
        var odkaz = await _db.ZaznamExterniOdkazy.FirstOrDefaultAsync(ev => ev.Id == externiOdkazId, ct);
        if (odkaz == null)
            return Fail<Unit>(VyzvaErrorCode.ExternalLinkNotFound, "Externí vazba nenalezena");

        var pnfTypId = await GetPnfTypIdAsync(ct);
        if (odkaz.TypOdkazuId != pnfTypId)
            return Fail<Unit>(VyzvaErrorCode.ExternalLinkNotPnf, "Switch lze použít jen pro PNF");

        // Ochrana: nedá se změnit switch u PNF, který je v odeslané výzvě.
        if (odkaz.VyzvaId.HasValue)
        {
            var aktualniVyzva = await _db.Vyzvy.AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == odkaz.VyzvaId!.Value, ct);
            if (aktualniVyzva is { Stav: VyzvaStav.Odeslano })
                return Fail<Unit>(VyzvaErrorCode.VyzvaIsLocked, "Výzva je odeslaná");
        }

        odkaz.ZaradidDoVyzvy = zaradit;

        if (zaradit && odkaz.VyzvaId == null)
        {
            var projektId = await _db.ProjektoveZaznamy
                .Where(z => z.Id == odkaz.ZaznamId)
                .Select(z => z.ProjektId)
                .FirstAsync(ct);

            var cilovaVyzva = await _db.Vyzvy.AsNoTracking()
                .Where(v => v.ProjektId == projektId && v.Stav == VyzvaStav.Priprava)
                .OrderBy(v => v.PoradoveVRoce)
                .Select(v => (int?)v.Id)
                .FirstOrDefaultAsync(ct);

            odkaz.VyzvaId = cilovaVyzva;
        }
        else if (!zaradit && odkaz.VyzvaId.HasValue)
        {
            odkaz.VyzvaId = null;
        }

        await _db.SaveChangesAsync(ct);
        return new VyzvaResult<Unit>.Ok(default);
    }

    public async Task<VyzvaResult<Unit>> PrerditPnfAsync(
        int externiOdkazId, int? cilovaVyzvaId, CancellationToken ct)
    {
        var odkaz = await _db.ZaznamExterniOdkazy.FirstOrDefaultAsync(ev => ev.Id == externiOdkazId, ct);
        if (odkaz == null)
            return Fail<Unit>(VyzvaErrorCode.ExternalLinkNotFound, "Externí vazba nenalezena");

        var pnfTypId = await GetPnfTypIdAsync(ct);
        if (odkaz.TypOdkazuId != pnfTypId)
            return Fail<Unit>(VyzvaErrorCode.ExternalLinkNotPnf, "Jen PNF");

        if (odkaz.VyzvaId.HasValue)
        {
            var src = await _db.Vyzvy.AsNoTracking().FirstOrDefaultAsync(v => v.Id == odkaz.VyzvaId!.Value, ct);
            if (src is { Stav: VyzvaStav.Odeslano })
                return Fail<Unit>(VyzvaErrorCode.VyzvaIsLocked, "Zdrojová výzva je odeslaná");
        }

        if (cilovaVyzvaId.HasValue)
        {
            var cil = await _db.Vyzvy.AsNoTracking().FirstOrDefaultAsync(v => v.Id == cilovaVyzvaId.Value, ct);
            if (cil == null)
                return Fail<Unit>(VyzvaErrorCode.VyzvaNotFound, "Cílová výzva neexistuje");
            if (cil.Stav != VyzvaStav.Priprava)
                return Fail<Unit>(VyzvaErrorCode.VyzvaIsLocked, "Cílová výzva není v Priprava");
        }

        odkaz.VyzvaId = cilovaVyzvaId;
        odkaz.ZaradidDoVyzvy = true; // explicitní přeřazení → switch ON

        await _db.SaveChangesAsync(ct);
        return new VyzvaResult<Unit>.Ok(default);
    }
}
