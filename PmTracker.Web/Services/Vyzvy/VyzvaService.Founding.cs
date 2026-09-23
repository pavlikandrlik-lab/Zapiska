using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy.Contracts;

namespace PmTracker.Web.Services.Vyzvy;

public sealed partial class VyzvaService
{
    /// <summary>Povolený rozsah pořadového čísla výzvy (spec 2026-09-07 §5.1).</summary>
    private const int MinPoradoveVRoce = 1;
    private const int MaxPoradoveVRoce = 999;

    public async Task<VyzvaResult<VyzvaDetail>> ZalozitVyzvuAsync(
        int projektId, int poradoveVRoce, int zalozilOsobaId, DateTime now, CancellationToken ct)
    {
        var projekt = await _db.Projekty.FirstOrDefaultAsync(p => p.Id == projektId, ct);
        if (projekt == null)
            return Fail<VyzvaDetail>(VyzvaErrorCode.ProjectNotFound, "Projekt nenalezen");
        if (string.IsNullOrWhiteSpace(projekt.MistoPlneni))
            return Fail<VyzvaDetail>(VyzvaErrorCode.ProjectMissingMistoPlneni, "Projekt nemá místo plnění");
        if (string.IsNullOrWhiteSpace(projekt.CisloRamcoveSmlouvy))
            return Fail<VyzvaDetail>(VyzvaErrorCode.ProjectMissingCisloRamcoveSmlouvy, "Projekt nemá číslo rámcové smlouvy");

        if (poradoveVRoce < MinPoradoveVRoce || poradoveVRoce > MaxPoradoveVRoce)
            return Fail<VyzvaDetail>(VyzvaErrorCode.InvalidVyzvaNumber,
                $"Číslo výzvy musí být v rozsahu {MinPoradoveVRoce}–{MaxPoradoveVRoce}.");

        var rok = now.Year;
        var smlouva = projekt.CisloRamcoveSmlouvy!;
        if (await JeCisloObsazeneAsync(smlouva, rok, poradoveVRoce, ct))
            return Fail<VyzvaDetail>(VyzvaErrorCode.DuplicateVyzvaNumber,
                $"Výzva {poradoveVRoce}/{rok} už pro tuto rámcovou smlouvu existuje.");

        var vyzva = new VyzvaEntity
        {
            ProjektId = projektId,
            Kod = VyzvaCodeGenerator.Generuj(poradoveVRoce, rok),
            PoradoveVRoce = poradoveVRoce,
            Rok = rok,
            Stav = VyzvaStav.Priprava,
            DatumZalozeni = now,
            ZalozilOsobaId = zalozilOsobaId,
            MistoPlneniSnapshot = projekt.MistoPlneni!,
            CisloRamcoveSmlouvySnapshot = smlouva,
        };
        _db.Vyzvy.Add(vyzva);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Souběh dvou zakládajících: unique index ux_vyzvy_smlouva_rok_poradove porazí
            // druhého. Překládáme na srozumitelnou hlášku místo serverové chyby.
            // (Filtr `when (await ...)` nejde — await v catch filtru C# nedovoluje.)
            _db.Entry(vyzva).State = EntityState.Detached;
            if (!await JeCisloObsazeneAsync(smlouva, rok, poradoveVRoce, ct)) throw;

            return Fail<VyzvaDetail>(VyzvaErrorCode.DuplicateVyzvaNumber,
                $"Výzva {poradoveVRoce}/{rok} už pro tuto rámcovou smlouvu existuje.");
        }

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

    private Task<bool> JeCisloObsazeneAsync(string cisloSmlouvy, int rok, int poradove, CancellationToken ct)
        => _db.Vyzvy.AsNoTracking().AnyAsync(
            v => v.CisloRamcoveSmlouvySnapshot == cisloSmlouvy && v.Rok == rok && v.PoradoveVRoce == poradove, ct);
}
