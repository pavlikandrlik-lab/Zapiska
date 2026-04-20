using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy.Contracts;

namespace PmTracker.Web.Services.Vyzvy;

public sealed partial class VyzvaService
{
    public async Task<VyzvaResult<VyzvaDetail>> ZaloztVyzvuZBufferuAsync(
        int projektId, int zalozilOsobaId, DateTime now, CancellationToken ct)
    {
        var projekt = await _db.Projekty.FirstOrDefaultAsync(p => p.Id == projektId, ct);
        if (projekt == null)
            return Fail<VyzvaDetail>(VyzvaErrorCode.ProjectNotFound, "Projekt nenalezen");
        if (string.IsNullOrWhiteSpace(projekt.MistoPlneni))
            return Fail<VyzvaDetail>(VyzvaErrorCode.ProjectMissingMistoPlneni, "Projekt nemá místo plnění");
        if (string.IsNullOrWhiteSpace(projekt.CisloRamcoveSmlouvy))
            return Fail<VyzvaDetail>(VyzvaErrorCode.ProjectMissingCisloRamcoveSmlouvy, "Projekt nemá číslo rámcové smlouvy");

        var pnfTypId = await GetPnfTypIdAsync(ct);
        var bufferIds = await _db.ZaznamExterniOdkazy.AsNoTracking()
            .WhereVBufferuProjektu(_db, projektId, pnfTypId)
            .Select(ev => ev.Id)
            .ToListAsync(ct);

        if (bufferIds.Count == 0)
            return Fail<VyzvaDetail>(VyzvaErrorCode.BufferEmpty, "Buffer je prázdný");

        var rok = now.Year;
        var existujiciPoradove = await _db.Vyzvy.AsNoTracking()
            .Where(v => v.CisloRamcoveSmlouvySnapshot == projekt.CisloRamcoveSmlouvy && v.Rok == rok)
            .Select(v => v.PoradoveVRoce)
            .ToListAsync(ct);
        var dalsiPoradove = VyzvaCodeGenerator.DalsiPoradoveVRoce(existujiciPoradove);

        var vyzva = new VyzvaEntity
        {
            ProjektId = projektId,
            Kod = VyzvaCodeGenerator.Generuj(dalsiPoradove, rok),
            PoradoveVRoce = dalsiPoradove,
            Rok = rok,
            Stav = VyzvaStav.Priprava,
            DatumZalozeni = now,
            ZalozilOsobaId = zalozilOsobaId,
            MistoPlneniSnapshot = projekt.MistoPlneni!,
            CisloRamcoveSmlouvySnapshot = projekt.CisloRamcoveSmlouvy!,
        };
        _db.Vyzvy.Add(vyzva);
        await _db.SaveChangesAsync(ct);

        var polozky = await _db.ZaznamExterniOdkazy
            .Where(ev => bufferIds.Contains(ev.Id))
            .ToListAsync(ct);
        foreach (var p in polozky) p.VyzvaId = vyzva.Id;

        _db.VyzvaHistorieStavu.Add(new VyzvaHistorieStavuEntity
        {
            VyzvaId = vyzva.Id,
            PuvodniStav = null,
            NovyStav = VyzvaStav.Priprava,
            DatumZmeny = now,
            ZmenilOsobaId = zalozilOsobaId,
        });
        await _db.SaveChangesAsync(ct);

        var detail = await GetVyzvaAsync(vyzva.Id, ct);
        return new VyzvaResult<VyzvaDetail>.Ok(detail!);
    }
}
