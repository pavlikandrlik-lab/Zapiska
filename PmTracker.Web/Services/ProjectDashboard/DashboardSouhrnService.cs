using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.ServiceDesk.Prodleni;

namespace PmTracker.Web.Services.ProjectDashboard;

/// <summary>
/// Souhrnný pás dashboardu projektu (spec 2026-10-15 §8): aktuální provozní stav nezávislý na období.
/// Dlaždice se skládají podle oprávnění k záložkám; ticketové jen s napojením na IS.
/// V režimu „Zobrazit jako" dostává kontext cílové osoby, takže filtr práv sedí automaticky.
/// </summary>
public sealed class DashboardSouhrnService
{
    private readonly IProjectDashboardService _dashboard;
    private readonly IProdleniService _prodleni;

    public DashboardSouhrnService(IProjectDashboardService dashboard, IProdleniService prodleni)
    {
        _dashboard = dashboard;
        _prodleni = prodleni;
    }

    public async Task<DashboardSouhrnViewModel> BuildAsync(int projektId, CurrentUserContextViewModel user, DateTime now, CancellationToken ct)
    {
        var dlazdice = new List<SouhrnDlazdice>();

        if (user.HasPermission(PermissionKeys.DashboardRecordsView, projektId))
        {
            var zaznamy = await _dashboard.BuildRecordsPanelAsync(projektId, now, ct);
            var poTerminu = zaznamy.Records.Count(r => r.Category == DashboardRecordCategory.Delayed);
            var ceka = zaznamy.Records.Count(r => r.Category == DashboardRecordCategory.AwaitingActual);
            dlazdice.Add(new SouhrnDlazdice("ukoly-po-terminu", poTerminu, "Úkoly po termínu", null,
                poTerminu > 0 ? SouhrnStav.Kriticke : SouhrnStav.Ok, "zaznamy", "delayed"));
            dlazdice.Add(new SouhrnDlazdice("ceka-na-skutecnost", ceka, "Čeká na skutečnost", null,
                ceka > 0 ? SouhrnStav.Varovani : SouhrnStav.Ok, "zaznamy", "awaiting"));
        }

        if (user.HasPermission(PermissionKeys.DashboardNesView, projektId))
        {
            var prehled = await _prodleni.GetPrehledAsync(projektId, ct);
            switch (prehled.Stav)
            {
                case ProdleniStavDat.Ok:
                {
                    var vProdleni = prehled.VProdleni.Select(p => p.TicketId).Distinct().Count();
                    var hrozi = prehled.Hrozi.Select(p => p.TicketId).Distinct().Count();
                    dlazdice.Add(new SouhrnDlazdice("tickety-v-prodleni", vProdleni, "Tickety v prodlení",
                        vProdleni > 0 ? PenaltyReasonFormatter.Dny(prehled.SoucetDni) : null,
                        vProdleni > 0 ? SouhrnStav.Kriticke : SouhrnStav.Ok, "nes", "v-prodleni"));
                    dlazdice.Add(new SouhrnDlazdice("hrozi-prodleni", hrozi, "Hrozí prodlení", null,
                        hrozi > 0 ? SouhrnStav.Varovani : SouhrnStav.Ok, "nes", "hrozi"));
                    break;
                }
                case ProdleniStavDat.Nedostupne:
                    dlazdice.Add(new SouhrnDlazdice("tickety-v-prodleni", null, "Tickety v prodlení", "ServiceDesk neodpovídá",
                        SouhrnStav.Nedostupne, "nes", "v-prodleni"));
                    dlazdice.Add(new SouhrnDlazdice("hrozi-prodleni", null, "Hrozí prodlení", "ServiceDesk neodpovídá",
                        SouhrnStav.Nedostupne, "nes", "hrozi"));
                    break;
                // BezNapojeni: projekt bez IS nebo bez databáze ServiceDesku → ticketové dlaždice se nevykreslí.
            }
        }

        return new DashboardSouhrnViewModel { Dlazdice = dlazdice };
    }
}
