using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

/// <summary>
/// JS piny pro přesuny PNF (spec 2026-09-07 §8). Aplikace nemá JS test runner, takže
/// se ověřuje zdroj modulů — stejný vzor jako RecordScheduleToggleJsTests. Chytá regrese,
/// které nejde vidět z C#: neregistrovaný side-effect modul nebo vlastní menu mimo
/// sdílenou floating vrstvu.
/// </summary>
public sealed class VyzvyPresunJsTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    private static string Js(string name) => File.ReadAllText(Path.Combine(
        RepoRoot(), "PmTracker.Web/wwwroot/js/modules", name));

    /// <summary>
    /// 2026-09-07: menu ⋯ u PNF a menu akcí na kartě záznamu sdílí jeden modul
    /// (dluh D7). Obě jsou jen konfigurace nad třídou AnchoredMenu.
    /// </summary>
    [Fact]
    public void AnchoredMenu_ObsluhujeObeMenu()
    {
        var src = Js("ui/anchoredMenu.js");

        src.Should().Contain("[data-vyzvy-menu-trigger]", "menu ⋯ u PNF");
        src.Should().Contain("[data-record-menu-trigger]", "i menu akcí na kartě záznamu");
        src.Should().Contain("class AnchoredMenu", "logika je jednou, menu jsou konfigurace");
    }

    [Fact]
    public void AnchoredMenu_ResolvesPanelFromTrigger_NotFromMenuPanel()
    {
        var src = Js("ui/anchoredMenu.js");

        src.Should().Contain("trigger.closest(\"[data-vyzvy-panel]\")",
            "panel menu je namountovaný ve floating rootu, takže z něj k panelu Výzev nedojdeme");
    }

    /// <summary>
    /// Rotaci šipky na kartě řídí třída na .record-card, ne atribut na triggeru —
    /// pm-button strhává atributy z hostu. Při slučování menu se to nesmělo ztratit.
    /// </summary>
    [Fact]
    public void AnchoredMenu_ZachovavaTriduNaKarteZaznamu()
    {
        var src = Js("ui/anchoredMenu.js");

        src.Should().Contain("record-actions-menu-open");
        src.Should().Contain(".record-card");
    }

    [Fact]
    public void DragDrop_IgnoresForeignDragsAndLockedTiles()
    {
        var src = Js("vyzvy/dragDrop.js");

        src.Should().Contain("text/x-pmtracker-pnf", "vlastní MIME typ odliší naše tažení od cizího");
        src.Should().Contain("types",
            "dragover kontroluje typ dat — jinak by se dlaždice zvýrazňovala i při tažení textu");
        src.Should().Contain("vyzvyDropTarget === \"none\"",
            "zamčená výzva drop nepřijme (spec §8.3)");
    }

    /// <summary>
    /// Při tažení jde u kurzoru chip s cílem přesunu (zadání uživatele 2026-09-08).
    /// Nativní průsvitný duch se potlačí, aby u myši nebyly dvě věci naráz.
    /// </summary>
    [Fact]
    public void DragDrop_UkazujeChipUKurzoru()
    {
        var src = Js("vyzvy/dragDrop.js");

        src.Should().Contain("setDragImage",
            "nativní duch dlaždice se potlačí, u kurzoru zůstane jen chip");
        src.Should().Contain("clientX", "chip jde za kurzorem podle souřadnic z dragover");
    }

    /// <summary>
    /// Text chipu se skládá v šabloně (data-vyzvy-drop-label na dlaždici), ne v JS —
    /// česká hláška patří do Razoru, ne do skriptu.
    /// </summary>
    [Fact]
    public void DragDrop_BereTextChipuZDlazdice()
    {
        var src = Js("vyzvy/dragDrop.js");

        src.Should().Contain("vyzvyDropLabel",
            "popisek cíle je atribut dlaždice, JS ho jen přečte");
    }

    /// <summary>
    /// Nad neplatným cílem se chip schová — zamčená výzva tak rovnou vypadá jako „sem ne".
    /// </summary>
    [Fact]
    public void DragDrop_SchovaChipMimoPlatnyCil()
    {
        var src = Js("vyzvy/dragDrop.js");

        var start = src.IndexOf("function chip", StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1, "chip má vlastní obsluhu");

        src.Should().Contain("hideChip", "mimo platný cíl a po skončení tažení chip mizí");
    }

    [Fact]
    public void PresunPnf_KeepsSelectedTile()
    {
        var src = Js("vyzvy/panelController.js");

        src.Should().Contain("presunPnf", "přesun má jeden vstupní bod pro menu i tažení");
        src.Should().Contain("vybrat: zustatNa",
            "po přesunu zůstává vybraná stejná dlaždice — obrazovka nesmí uskočit (spec §8.2)");
    }

    [Fact]
    public void PresunModules_AreWiredInBootstrap()
    {
        var bootstrap = Js("bootstrap.js");

        bootstrap.Should().Contain("ui/anchoredMenu.js",
            "side-effect modul musí být explicitně importován (memory project_bundle_sync)");
        bootstrap.Should().Contain("vyzvy/dragDrop.js");
    }

    /// <summary>
    /// 2026-09-08: bootstrap panelu bydlel ve vlastním index.js, který bootstrap.js
    /// importoval DŘÍV než panelController.js. Při prvním běhu tedy ještě neexistoval
    /// bindPanel — panel se přesto označil za obsloužený a klikání na dlaždice zůstalo
    /// mrtvé, dokud reloadPanel element nevyměnil. Bootstrap proto žije u bindPanel.
    /// </summary>
    [Fact]
    public void Bootstrap_ZijeUBindPanel_NeVeVlastnimModulu()
    {
        File.Exists(Path.Combine(RepoRoot(), "PmTracker.Web/wwwroot/js/modules/vyzvy/index.js"))
            .Should().BeFalse("samostatný modul zaváděl závislost na pořadí importů");

        Js("vyzvy/panelController.js").Should().Contain("vyzvyBootstrapped",
            "bootstrap je ve stejném souboru jako bindPanel, který volá");

        Js("bootstrap.js").Should().NotContain("vyzvy/index.js", "modul zanikl");
    }

    /// <summary>
    /// Značka „obslouženo" smí padnout až po skutečném navázání. Když padne dřív,
    /// neúspěšný pokus panel otráví natrvalo — druhý průchod ho už nepustí.
    /// </summary>
    [Fact]
    public void Bootstrap_ZnackuNastaviAzPoNavazani()
    {
        var src = Js("vyzvy/panelController.js");

        var start = src.IndexOf("function bootstrap(", StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1, "bootstrap panelu musí v modulu být");

        var body = src[start..];
        var bind = body.IndexOf("bindPanel(panelElement)", StringComparison.Ordinal);
        var znacka = body.IndexOf("vyzvyBootstrapped = 'true'", StringComparison.Ordinal);

        bind.Should().BeGreaterThan(-1, "bootstrap panel opravdu naváže");
        znacka.Should().BeGreaterThan(bind,
            "nejdřív navázat, teprve pak označit — jinak je panel otrávený napořád");
    }

    /// <summary>
    /// Modal „Upravit přiřazení" zrušen 2026-09-07 (spec §8.5) — tažení a menu pokrývají totéž.
    /// </summary>
    [Fact]
    public void ReassignModal_IsGone()
    {
        var vyzvyModules = Path.Combine(RepoRoot(), "PmTracker.Web/wwwroot/js/modules/vyzvy");
        File.Exists(Path.Combine(vyzvyModules, "reassignModal.js")).Should().BeFalse();

        File.Exists(Path.Combine(RepoRoot(), "PmTracker.Web/Views/Vyzvy/ReassignModal.cshtml"))
            .Should().BeFalse();
    }
}
