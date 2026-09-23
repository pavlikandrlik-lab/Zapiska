using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Controllers;

public abstract partial class BaseController
{
    protected void SetBreadcrumbs(params Breadcrumb[] items)
        => SetBreadcrumbs(null, items);

    /// <summary>C1 (2026-07-10): varianta s explicitním cílem šipky ← (origin/kanonická záložka).</summary>
    protected void SetBreadcrumbs(string? backUrl, params Breadcrumb[] items)
        => ViewData["Breadcrumbs"] = new BreadcrumbTrail(items, backUrl);

    /// <summary>Top-level seznam sekce (např. „Projekty") — jediný kořenový drobeček, bez ✕/←.</summary>
    protected void SetSectionRootBreadcrumb(string text)
        => SetBreadcrumbs(new Breadcrumb(text, null, null, false));

    /// <summary>
    /// Kanonická projektová větev: Projekty ▸ projekt ▸ [jednání ▸] [aktuální].
    /// Poslední přítomný prvek je „aktuální" (Url=null). Zachovává ?asUser (dev impersonace).
    /// C1 (2026-07-10): šipka ← = origin (validovaný backUrl) > kanonická záložka Jednání
    /// (když je jednání aktuální drobeček) > URL předposledního drobečku. Klik na
    /// projekt-drobeček zůstává homepage projektu (bez tab = Záznamy).
    /// </summary>
    protected void SetProjectBreadcrumbs(
        int projektId,
        string projektNazev,
        string projektZkratka,
        (int Id, string Label)? meeting = null,
        string? currentText = null,
        string? backUrl = null)
    {
        var items = new List<Breadcrumb>
        {
            new("Projekty", ProjektyUrl(), null, false),
        };

        var projectIsCurrent = meeting is null && currentText is null;
        items.Add(new(projektNazev, projectIsCurrent ? null : ProjektDetailUrl(projektId), projektZkratka, true));

        if (meeting is { } m)
        {
            var meetingIsCurrent = currentText is null;
            items.Add(new(m.Label, meetingIsCurrent ? null : JednaniDetailUrl(m.Id), null, true));
        }

        if (currentText is not null)
        {
            items.Add(new(currentText, null, null, true));
        }

        var resolvedBack = NormalizeLocalReturnUrl(backUrl)
            ?? (meeting is not null && currentText is null
                ? ProjektDetailTabUrl(projektId, "jednani")
                : null);

        SetBreadcrumbs(resolvedBack, items.ToArray());
    }

    private string ProjektyUrl() => Url.Action("Index", "Projekty", WithAsUser(null)) ?? "/Projekty";
    private string ProjektDetailUrl(int id) => Url.Action("Detail", "Projekty", WithAsUser(new { id })) ?? $"/Projekty/Detail/{id}";
    private string JednaniDetailUrl(int id) => Url.Action("Detail", "Jednani", WithAsUser(new { id })) ?? $"/Jednani/Detail/{id}";

    /// <summary>Detail projektu s aktivní záložkou (kanonický cíl šipky ← pro entity záložek).</summary>
    protected string ProjektDetailTabUrl(int id, string tab)
        => Url.Action("Detail", "Projekty", WithAsUser(new { id, tab })) ?? $"/Projekty/Detail/{id}?tab={tab}";

    /// <summary>Origin kandidát pro navigaci zpět: jen lokální URL (open-redirect ochrana).
    /// C1 (2026-07-10): hoisted z Zaznamy/Navrhy controllerů (DRY).</summary>
    protected string? NormalizeLocalReturnUrl(string? returnUrl)
        => !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : null;

    /// <summary>Route values s ?asUser (dev impersonace) pro ruční Url.Action mimo SetProjectBreadcrumbs.</summary>
    protected RouteValueDictionary WithAsUserRoute() => WithAsUser(null);

    /// <summary>Podstránka sekce: Kořen(odkaz) ▸ aktuální(bez odkazu). ✕ na aktuálním i ← míří na kořen.</summary>
    protected void SetSectionChildBreadcrumb(string rootText, string rootUrl, string currentText)
        => SetBreadcrumbs(
            new Breadcrumb(rootText, rootUrl, null, false),
            new Breadcrumb(currentText, null, null, true));

    private RouteValueDictionary WithAsUser(object? routeValues)
    {
        var rvd = routeValues is null ? new RouteValueDictionary() : new RouteValueDictionary(routeValues);
        var asUser = Request.Query["asUser"].ToString();
        if (!string.IsNullOrEmpty(asUser))
        {
            rvd["asUser"] = asUser;
        }
        return rvd;
    }
}
