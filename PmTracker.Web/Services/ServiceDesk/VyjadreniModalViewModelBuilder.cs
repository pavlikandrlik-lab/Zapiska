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
    Task<VyjadreniModalViewModel?> BuildAsync(int externiOdkazId, int zaznamId, bool canEdit, CancellationToken ct)
        => BuildAsync(externiOdkazId, zaznamId, canEdit, canAddAddon: false, ct);

    Task<VyjadreniModalViewModel?> BuildAsync(int externiOdkazId, int zaznamId, bool canEdit, bool canAddAddon, CancellationToken ct);
}

public sealed class VyjadreniModalViewModelBuilder : IVyjadreniModalViewModelBuilder
{
    /// <summary>
    /// Spec 2026-04-28-modal-vyjadreni-redesign §3 — kroky harmonogramu zobrazené ve stepperu
    /// per typ záznamu. NES = žádné (modal stepper úplně skrytý). PMP a PNF zahrnují
    /// auto-fill kroky + dropdown ruční kroky.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<int>> RelevantStepsByType =
        new Dictionary<string, IReadOnlySet<int>>(StringComparer.OrdinalIgnoreCase)
        {
            ["NES"] = new HashSet<int>(),
            ["PMP"] = new HashSet<int> { 1, 2, 3, 4, 5 },
            ["PNF"] = new HashSet<int> { 1, 6, 7, 8, 9, 10 },
        };

    private static IReadOnlySet<int> RelevantStepsForType(string? typZaznamu)
    {
        if (string.IsNullOrWhiteSpace(typZaznamu)) return new HashSet<int>();
        return RelevantStepsByType.TryGetValue(typZaznamu.Trim(), out var set)
            ? set
            : new HashSet<int>();
    }

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

    public Task<VyjadreniModalViewModel?> BuildAsync(int externiOdkazId, int zaznamId, bool canEdit, CancellationToken ct)
        => BuildAsync(externiOdkazId, zaznamId, canEdit, canAddAddon: false, ct);

    public async Task<VyjadreniModalViewModel?> BuildAsync(int externiOdkazId, int zaznamId, bool canEdit, bool canAddAddon, CancellationToken ct)
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
            CanEdit = canEdit,
            // Plán 1 Feature B — 5-slot buffer (3 fixní + 2 add-on).
            // Add-on slot aktivní pouze když má user RecordsScheduleEdit na daném projektu.
            CanAddAddon = canAddAddon
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

        // Spec 2026-04-28-modal-vyjadreni-redesign §3 — filtrovat kroky per typ záznamu.
        // PMP zobrazí {1,2,3,4,5}, PNF {1,6,7,8,9,10}, NES žádné (modal je tehdy bez stepperu).
        var relevantSteps = RelevantStepsForType(vm.TiketTyp);
        vm.Kroky = kroky
            .Where(k => relevantSteps.Contains(k.KrokPoradi))
            .Select(k =>
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
                    AktualniSource = b?.Source,
                    // A-5: předáme reálné vazba-id do UI pro Delete endpoint.
                    VazbaId = b?.Id
                };
            })
            .ToList();

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
                PopisPlainText = VyjadreniHtmlText.ToPlainText(v.Popis),
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
