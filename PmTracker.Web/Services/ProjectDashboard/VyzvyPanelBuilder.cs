using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Vyzvy;
using PmTracker.Web.Services.Vyzvy.Contracts;

namespace PmTracker.Web.Services.ProjectDashboard;

public sealed class VyzvyPanelBuilder
{
    private readonly IVyzvaService _vyzvaService;
    private readonly PmTrackerDbContext _db;

    public VyzvyPanelBuilder(IVyzvaService vyzvaService, PmTrackerDbContext db)
    {
        _vyzvaService = vyzvaService;
        _db = db;
    }

    public async Task<ProjectDashboardVyzvyPanelViewModel> BuildAsync(
        int projektId, bool muzeEditovat, CancellationToken ct)
    {
        var projekt = await _db.Projekty.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projektId, ct);

        var bufferItems = await _vyzvaService.GetBufferAsync(projektId, ct);
        var vyzvy = await _vyzvaService.GetVyzvyAsync(projektId, ct);

        var jmenaOsob = await LoadOsobaNamesAsync(vyzvy, ct);
        var cisloViditelneById = await LoadCisloViditelneMapAsync(bufferItems, vyzvy, ct);

        var (muzeZalozit, duvod) = BufferZalozitPodminky(bufferItems, projekt);

        return new ProjectDashboardVyzvyPanelViewModel
        {
            ProjektId = projektId,
            MuzeEditovat = muzeEditovat,
            Buffer = new VyzvyPanelBufferViewModel
            {
                Polozky = bufferItems.Select(b => ToPolozka(b, cisloViditelneById)).ToArray(),
                MuzeZaloztVyzvu = muzeEditovat && muzeZalozit,
                DuvodBlokace = muzeZalozit ? null : duvod,
            },
            Vyzvy = vyzvy.Select(v => ToVyzva(v, jmenaOsob, cisloViditelneById)).ToArray(),
        };
    }

    private static (bool MuzeZalozit, string? Duvod) BufferZalozitPodminky(
        IReadOnlyList<VyzvaBufferItem> buffer, ProjektEntity? projekt)
    {
        if (projekt == null) return (false, "Projekt nenalezen.");
        if (string.IsNullOrWhiteSpace(projekt.MistoPlneni))
            return (false, "Projekt nemá vyplněné Místo plnění. Doplňte v editaci projektu.");
        if (string.IsNullOrWhiteSpace(projekt.CisloRamcoveSmlouvy))
            return (false, "Projekt nemá vyplněné Číslo rámcové smlouvy. Doplňte v editaci projektu.");
        if (buffer.Count == 0)
            return (false, "Buffer je prázdný.");
        return (true, null);
    }

    private async Task<IReadOnlyDictionary<int, string>> LoadOsobaNamesAsync(
        IReadOnlyList<VyzvaDetail> vyzvy, CancellationToken ct)
    {
        var ids = vyzvy.SelectMany(v => new[] { (int?)v.ZalozilOsobaId, v.OdeslalOsobaId })
            .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<int, string>();

        return await _db.Osoby.AsNoTracking()
            .Where(o => ids.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => ((o.Titul ?? "") + " " + o.Jmeno + " " + o.Prijmeni).Trim(), ct);
    }

    private async Task<IReadOnlyDictionary<int, string?>> LoadCisloViditelneMapAsync(
        IReadOnlyList<VyzvaBufferItem> buffer, IReadOnlyList<VyzvaDetail> vyzvy, CancellationToken ct)
    {
        var zaznamIds = buffer.Select(b => b.ZaznamId)
            .Concat(vyzvy.SelectMany(v => v.Polozky.Select(p => p.ZaznamId)))
            .Distinct().ToArray();
        if (zaznamIds.Length == 0) return new Dictionary<int, string?>();

        return await _db.ProjektoveZaznamy.AsNoTracking()
            .Where(z => zaznamIds.Contains(z.Id))
            .Select(z => new { z.Id, z.CisloViditelne })
            .ToDictionaryAsync(x => x.Id, x => x.CisloViditelne, ct);
    }

    private static VyzvyPanelPolozkaViewModel ToPolozka(
        VyzvaBufferItem item, IReadOnlyDictionary<int, string?> cisloViditelneById)
        => new()
        {
            ExterniOdkazId = item.ExterniOdkazId,
            ZaznamId = item.ZaznamId,
            Cislo = item.Cislo,
            StrucneNazev = item.StrucneNazev,
            PredpokladanaCena = item.PredpokladanaCena,
            CisloViditelneZaznamu = cisloViditelneById.GetValueOrDefault(item.ZaznamId),
        };

    private static VyzvyPanelPolozkaViewModel ToPolozka(
        VyzvaDetailItem item, IReadOnlyDictionary<int, string?> cisloViditelneById)
        => new()
        {
            ExterniOdkazId = item.ExterniOdkazId,
            ZaznamId = item.ZaznamId,
            Cislo = item.Cislo,
            StrucneNazev = item.StrucneNazev,
            PredpokladanaCena = item.PredpokladanaCena,
            CisloViditelneZaznamu = cisloViditelneById.GetValueOrDefault(item.ZaznamId),
        };

    private static VyzvyPanelVyzvaViewModel ToVyzva(
        VyzvaDetail detail,
        IReadOnlyDictionary<int, string> jmenaOsob,
        IReadOnlyDictionary<int, string?> cisloViditelneById)
    {
        var povoleneStavy = Enum.GetValues<VyzvaStav>()
            .Where(s => s != detail.Stav && VyzvaStateMachine.JePovolenyPrechod(detail.Stav, s))
            .Select(s => s.ToString())
            .ToArray();

        decimal? celkova = null;
        foreach (var p in detail.Polozky)
        {
            if (p.PredpokladanaCena.HasValue)
                celkova = (celkova ?? 0) + p.PredpokladanaCena.Value;
        }

        return new VyzvyPanelVyzvaViewModel
        {
            Id = detail.Id,
            Kod = detail.Kod,
            PoradoveVRoce = detail.PoradoveVRoce,
            Rok = detail.Rok,
            Stav = detail.Stav.ToString(),
            DatumZalozeni = detail.DatumZalozeni,
            ZalozilJmeno = jmenaOsob.GetValueOrDefault(detail.ZalozilOsobaId),
            DatumOdeslani = detail.DatumOdeslani,
            OdeslalJmeno = detail.OdeslalOsobaId.HasValue
                ? jmenaOsob.GetValueOrDefault(detail.OdeslalOsobaId.Value)
                : null,
            Polozky = detail.Polozky.Select(p => ToPolozka(p, cisloViditelneById)).ToArray(),
            CelkovaCena = celkova,
            PovoleneStavy = povoleneStavy,
            Kolapsovano = detail.Stav != VyzvaStav.Priprava,
        };
    }
}
