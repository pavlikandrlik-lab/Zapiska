using Microsoft.EntityFrameworkCore;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels.Vyjadreni;

namespace PmTracker.Web.Services.ServiceDesk;

/// <summary>
/// Sestavuje <see cref="VyjadreniModalViewModel"/> pro chat modal. Čte:
/// - externí odkaz + projekt z PM Tracker DB,
/// - vyjádření z HOT_VYJADRENI přes <see cref="IVyjadreniQueryService"/>,
/// - aktuální bindingy (Stav=Active) z <c>zaznam_harmonogram_vyjadreni_vazba</c>,
/// - AD jména z <see cref="AdLoginCache"/>.
/// </summary>
public interface IVyjadreniModalViewModelBuilder
{
    Task<VyjadreniModalViewModel?> BuildAsync(int externiOdkazId, int zaznamId, bool canEdit, CancellationToken ct);
}

public sealed class VyjadreniModalViewModelBuilder : IVyjadreniModalViewModelBuilder
{
    private readonly PmTrackerDbContext _db;
    private readonly IVyjadreniQueryService _vyjadreni;
    private readonly ITicketingQueryService _ticketing;
    private readonly AdLoginCache _adLoginCache;

    public VyjadreniModalViewModelBuilder(
        PmTrackerDbContext db,
        IVyjadreniQueryService vyjadreni,
        ITicketingQueryService ticketing,
        AdLoginCache adLoginCache)
    {
        _db = db;
        _vyjadreni = vyjadreni;
        _ticketing = ticketing;
        _adLoginCache = adLoginCache;
    }

    public async Task<VyjadreniModalViewModel?> BuildAsync(int externiOdkazId, int zaznamId, bool canEdit, CancellationToken ct)
    {
        var eo = await _db.ZaznamExterniOdkazy.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == externiOdkazId && x.ZaznamId == zaznamId, ct).ConfigureAwait(false);
        if (eo is null) return null;

        var zaznam = await _db.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.Id == zaznamId)
            .Select(x => new { x.Id, x.ProjektId, x.HarmonogramSablonaVerze })
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (zaznam is null) return null;

        var vm = new VyjadreniModalViewModel
        {
            ExterniOdkazId = externiOdkazId,
            ZaznamId = zaznamId,
            ProjektId = zaznam.ProjektId,
            TiketCislo = eo.Cislo ?? string.Empty,
            LastHarvestedAt = eo.LastHarvestedAt,
            CanEdit = canEdit
        };

        if (string.IsNullOrWhiteSpace(eo.Cislo))
        {
            vm.EmptyMessage = "Externí odkaz nemá vyplněné 6místné číslo tiketu.";
            return vm;
        }

        var tiket = await _ticketing.GetZaznamAsync(eo.Cislo, ct).ConfigureAwait(false);
        vm.TiketStrucne = tiket?.Strucne;
        vm.TiketTyp = tiket?.TypZaznamu;

        var kroky = await _db.CiselnikHarmonogramTypu.AsNoTracking()
            .Where(t => t.SablonaVerze == zaznam.HarmonogramSablonaVerze && !t.JeZpozdeni)
            .OrderBy(t => t.KrokPoradi)
            .Select(t => new { t.KrokKey, t.KrokPoradi, t.Nazev, t.BarvaHex })
            .ToListAsync(ct).ConfigureAwait(false);

        var activeBindings = await _db.VyjadreniVazby.AsNoTracking()
            .Where(v => v.ZaznamId == zaznamId
                     && v.ExterniOdkazId == externiOdkazId
                     && v.Stav == (byte)VazbaStav.Active)
            .ToListAsync(ct).ConfigureAwait(false);
        var bindingByKey = activeBindings.ToDictionary(b => b.KrokKey);

        vm.Kroky = kroky.Select(k =>
        {
            bindingByKey.TryGetValue(k.KrokKey, out var b);
            return new StepperKrokViewModel
            {
                KrokKey = k.KrokKey,
                KrokPoradi = k.KrokPoradi,
                Nazev = k.Nazev,
                BarvaHex = k.BarvaHex,
                AktualniVyjadreniId = b?.HotVyjadreniId,
                AktualniVyjadreniDatum = b?.DatumVyjadreni,
                AktualniSource = b?.Source
            };
        }).ToList();

        var list = await _vyjadreni.GetVyjadreniForTicketAsync(eo.Cislo, sinceUtc: null, ct).ConfigureAwait(false);
        var bindingByHotId = activeBindings.ToDictionary(b => b.HotVyjadreniId);

        var bubliny = new List<BublinaViewModel>(list.Count);
        foreach (var v in list)
        {
            var autor = await _adLoginCache.ResolveDisplayNameAsync(v.Zpracoval, ct).ConfigureAwait(false);
            bindingByHotId.TryGetValue(v.Id, out var binding);
            var predikat = HarvestPredicates.ClassifyPopis(v.Popis);
            bubliny.Add(new BublinaViewModel
            {
                VyjadreniId = v.Id,
                Datum = v.Datum,
                Autor = autor,
                AutorLogin = v.Zpracoval,
                Popis = v.Popis,
                Tym = v.Tym,
                Typ = v.Typ,
                Predikat = predikat == HarvestPredicateKind.None ? null : predikat.ToString(),
                NavazanoNaKrokKey = binding?.KrokKey,
                NavazanoNaKrokPoradi = binding is null
                    ? null
                    : vm.Kroky.FirstOrDefault(k => k.KrokKey == binding.KrokKey)?.KrokPoradi
            });
        }
        vm.Bubliny = bubliny;

        if (bubliny.Count == 0)
        {
            vm.EmptyMessage = "Žádná vyjádření zatím nejsou dostupná (buď ještě nebyla vytěžena, nebo ServiceDesk je offline).";
        }

        return vm;
    }
}
