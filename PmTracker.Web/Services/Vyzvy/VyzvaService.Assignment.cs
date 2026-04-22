using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Vyzvy;

public sealed partial class VyzvaService
{
    public async Task<VyzvaResult<Unit>> NastavitZaradidAsync(
        int externiOdkazId, bool zaradit, CancellationToken ct)
    {
        var osobaId = _currentUser.OsobaId;
        if (osobaId == null)
            return Fail<Unit>(VyzvaErrorCode.AccessDenied, "Není přihlášený uživatel");

        var odkaz = await _db.ZaznamExterniOdkazy.FirstOrDefaultAsync(ev => ev.Id == externiOdkazId, ct);
        if (odkaz == null)
            return Fail<Unit>(VyzvaErrorCode.ExternalLinkNotFound, "Externí vazba nenalezena");

        // ACL: resolve owning projektId and verify records.edit before any state checks
        var projektId = await _db.ProjektoveZaznamy
            .Where(z => z.Id == odkaz.ZaznamId)
            .Select(z => z.ProjektId)
            .FirstOrDefaultAsync(ct);

        if (!await _authz.HasPermissionAsync(osobaId.Value, PermissionKeys.RecordsEdit, projektId, ct: ct))
            return Fail<Unit>(VyzvaErrorCode.AccessDenied, "Nemáte oprávnění upravovat záznamy tohoto projektu");

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
            // projektId already resolved above for ACL check — reuse it
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
        var osobaId = _currentUser.OsobaId;
        if (osobaId == null)
            return Fail<Unit>(VyzvaErrorCode.AccessDenied, "Není přihlášený uživatel");

        var odkaz = await _db.ZaznamExterniOdkazy.FirstOrDefaultAsync(ev => ev.Id == externiOdkazId, ct);
        if (odkaz == null)
            return Fail<Unit>(VyzvaErrorCode.ExternalLinkNotFound, "Externí vazba nenalezena");

        // ACL: source project check
        var sourceProjektId = await _db.ProjektoveZaznamy
            .Where(z => z.Id == odkaz.ZaznamId)
            .Select(z => z.ProjektId)
            .FirstOrDefaultAsync(ct);

        if (!await _authz.HasPermissionAsync(osobaId.Value, PermissionKeys.RecordsEdit, sourceProjektId, ct: ct))
            return Fail<Unit>(VyzvaErrorCode.AccessDenied, "Nemáte oprávnění upravovat záznamy zdrojového projektu");

        // ACL: target project check (only when cross-project)
        if (cilovaVyzvaId.HasValue)
        {
            var targetProjektId = await _db.Vyzvy
                .Where(v => v.Id == cilovaVyzvaId.Value)
                .Select(v => (int?)v.ProjektId)
                .FirstOrDefaultAsync(ct);

            if (targetProjektId.HasValue && targetProjektId.Value != sourceProjektId)
            {
                if (!await _authz.HasPermissionAsync(osobaId.Value, PermissionKeys.RecordsEdit, targetProjektId.Value, ct: ct))
                    return Fail<Unit>(VyzvaErrorCode.AccessDenied, "Nemáte oprávnění upravovat záznamy cílového projektu");
            }
        }

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
