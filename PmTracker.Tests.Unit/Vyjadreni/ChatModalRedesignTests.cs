using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Vyjadreni;

/// <summary>
/// Architecture guard pro 2026-04-28 redesign modalu Vyjádření a termíny.
/// Spec: docs/superpowers/specs/2026-04-28-modal-vyjadreni-redesign-design.md
///
/// Zachycuje invarianty:
///   - Native gov-stepper (ne custom pm-chat-stepper)
///   - CSS breakpointy 1600 / 1100 + max-width 1800
///   - Bublinky draggable=false (drag drive z kroku)
///   - Buffer pro nepřiřazené kroky
///   - Smazaný folder pm-chat-stepper
///   - Existence nových JS modulů (sticky, dragSnap, buffer)
/// </summary>
public sealed class ChatModalRedesignTests
{
    private static string LoadRepoText(string relativePath)
    {
        var directory = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PmTracker.sln")))
        {
            directory = directory.Parent;
        }
        if (directory is null)
        {
            throw new System.InvalidOperationException("Nepodařilo se najít kořen repozitáře.");
        }
        var full = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(full).Should().BeTrue($"soubor musí existovat: {full}");
        return File.ReadAllText(full);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PmTracker.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new System.InvalidOperationException("Nepodařilo se najít kořen.");
    }

    [Fact]
    public void ChatModalCshtml_NeprevadiSdDatumVyjadreniNaLocalTime()
    {
        // FIX 2026-06-22: datum SD vyjádření je už lokální čas (SQL datetime, Kind=Unspecified).
        // ToLocalTime by Unspecified bralo jako UTC a uměle přičetlo offset serveru (+1 h:
        // 23:09 → 00:09). Bublina i stepper proto datum zobrazují BEZ ToLocalTime.
        var html = LoadRepoText("PmTracker.Web/Views/Vyjadreni/_ChatModal.cshtml");

        html.Should().NotContain("bubble.Datum.ToLocalTime()",
            "datum bubliny (SD lokální čas) se nesmí převádět přes ToLocalTime");
        html.Should().NotContain("AktualniVyjadreniDatum?.ToLocalTime()",
            "datum bound vyjádření ve stepperu (SD lokální čas) se nesmí převádět přes ToLocalTime");
        // Naopak skutečný UTC timestamp (LastHarvestedAt) ToLocalTime používat MÁ — guard proti chybné „opravě".
        html.Should().Contain("LastHarvestedAt.Value.ToLocalTime()",
            "LastHarvestedAt je UTC (GetUtcNow) → ToLocalTime je správně a má zůstat");
    }

    [Fact]
    public void ChatModalCshtml_PouzivaNativeGovStepper()
    {
        var html = LoadRepoText("PmTracker.Web/Views/Vyjadreni/_ChatModal.cshtml");
        html.Should().Contain("<gov-stepper",
            "Modal musí používat native gov-stepper element (ne custom pm-chat-stepper).");
        html.Should().Contain("<gov-stepper-item",
            "Modal musí obsahovat gov-stepper-item elementy.");
        html.Should().NotContain("<pm-chat-stepper",
            "Custom element pm-chat-stepper byl odstraněn — nahrazen nativním <gov-stepper>.");
    }

    [Fact]
    public void ChatModalCshtml_BublinkyNejsouDraggable()
    {
        var html = LoadRepoText("PmTracker.Web/Views/Vyjadreni/_ChatModal.cshtml");
        // Bubble li element musí mít draggable="false" — žádný drag&drop interaction
        // (2026-04-29 dropdown redesign nahradil drag binding přes dropdown selector).
        html.Should().Contain("draggable=\"false\"",
            "Bublinky nejsou draggable — binding přes dropdown selector v každé bublině.");
    }

    [Fact]
    public void ChatModalCshtml_MaAlignedStepper()
    {
        var html = LoadRepoText("PmTracker.Web/Views/Vyjadreni/_ChatModal.cshtml");
        html.Should().Contain("data-aligned-stepper",
            "Razor musí mít aligned-stepper pro přiřazené kroky (informational dashboard).");
    }

    [Fact]
    public void ChatModalCshtml_VyuzivaColorStateProGovStepperItem()
    {
        var html = LoadRepoText("PmTracker.Web/Views/Vyjadreni/_ChatModal.cshtml");
        // Per 2026-04-29 dropdown redesign: stepper jen aligned (přiřazené kroky)
        // používá success/warning. error color (nepřiřazené) zmizela s bufferem.
        html.Should().Contain("\"success\"",
            "Auto kroky color=success (zelená).");
        html.Should().Contain("\"warning\"",
            "Manuální kroky color=warning (oranžová).");
    }

    [Fact]
    public void ChatModalCss_MaSpravneBreakpointy()
    {
        // Rozhodnutí 2026-06-16: 2 úrovně (70:30 až po ≥ 1100px, pak stack). Prostřední
        // úroveň 60:40 (≤ 1599px) byla odstraněna v b823dd7 a NEobnovuje se — 70:30 je správně.
        var css = LoadRepoText("PmTracker.Web/wwwroot/css/components/chat-modal.css");
        css.Should().Contain("grid-template-columns: 70fr 30fr",
            "Default grid je 70:30 (od ≥ 1100px po velké monitory).");
        css.Should().Contain("max-width: 1099px",
            "Stack breakpoint je při ≤ 1099px.");
        css.Should().Contain("grid-template-columns: 1fr",
            "Pod ≤ 1099px se sloupce skládají pod sebe (jeden sloupec).");
    }

    [Fact]
    public void SiteCss_ObsahujeChatModalVariantSWidthMin1800()
    {
        // Bug fix 2026-04-29: gov-dialog wrapper bez varianty zůstával na default
        // ~832px, takže obsah uvnitř .pm-chat-modal s max-width 1800px přetékal
        // a vznikal horizontální scroll uvnitř modalu. Width fix se musí
        // aplikovat na gov-dialog[data-modal-container][data-modal-variant="chat-modal"]
        // přes site.css, ne jen na vnitřní content.
        var css = LoadRepoText("PmTracker.Web/wwwroot/css/site.css");
        css.Should().Contain("data-modal-variant=\"chat-modal\"",
            "site.css musí definovat chat-modal variantu pro gov-dialog wrapper.");
        css.Should().Contain("min(95vw, 1800px)",
            "chat-modal varianta musí nastavit --max-width na min(95vw, 1800px).");
    }

    [Fact]
    public void ChatModalJs_NastavujeDataModalVariantNaGovDialog()
    {
        // Bug fix 2026-04-29: chat modal je vytvářen programaticky v chatModal.js
        // (nepoužívá _ModalLayout.cshtml), takže ViewData["ModalVariant"] by se nikdy
        // neaplikoval. Fix musí být v JS — gov-dialog dostane data-modal-container +
        // data-modal-variant="chat-modal" atributy při vytvoření.
        var js = LoadRepoText("PmTracker.Web/wwwroot/js/modules/vyjadreni/chatModal.js");
        js.Should().Contain("data-modal-variant",
            "chatModal.js musí nastavit data-modal-variant na vytvořený gov-dialog.");
        js.Should().Contain("'chat-modal'",
            "chatModal.js musí použít chat-modal variantu (matches site.css rule).");
        js.Should().Contain("data-modal-container",
            "chatModal.js musí nastavit data-modal-container atribut (matches CSS selector).");
    }

    [Fact]
    public void ChatModalCss_ZadnePmChatStepCss()
    {
        var css = LoadRepoText("PmTracker.Web/wwwroot/css/components/chat-modal.css");
        css.Should().NotContain(".pm-chat-step__poradi",
            "Custom kolečko (oval bug) bylo odstraněno — používá se native gov-stepper-item prefix slot.");
        css.Should().NotContain(".pm-chat-stepper--legacy",
            "Legacy fallback ol.pm-chat-stepper--legacy CSS bylo odstraněno.");
    }

    [Fact]
    public void PmChatStepperFolder_BylSmazan()
    {
        var pmChatStepperFolder = Path.Combine(FindRepoRoot(),
            "PmTracker.Web", "wwwroot", "js", "components", "pm-chat-stepper");
        Directory.Exists(pmChatStepperFolder).Should().BeFalse(
            "Folder pm-chat-stepper byl smazán — nahrazen native gov-stepper.");

        var pmChatStepperCss = Path.Combine(FindRepoRoot(),
            "PmTracker.Web", "wwwroot", "css", "components", "pm-chat-stepper.css");
        File.Exists(pmChatStepperCss).Should().BeFalse(
            "CSS pm-chat-stepper.css bylo smazáno.");
    }

    [Fact]
    public void Bootstrap_NeImportujePmChatStepper()
    {
        var js = LoadRepoText("PmTracker.Web/wwwroot/js/modules/bootstrap.js");
        js.Should().NotContain("pm-chat-stepper/pm-chat-stepper",
            "Bootstrap nesmí importovat smazaný pm-chat-stepper modul.");
    }

    [Fact]
    public void StepperStickyModule_Existuje()
    {
        // Sticky modul zachován po 2026-04-29 redesignu — informational dashboard.
        var js = LoadRepoText("PmTracker.Web/wwwroot/js/modules/vyjadreni/stepperSticky.js");
        js.Should().Contain("export function initStepperSticky",
            "Exportovaná init funkce musí existovat.");
        js.Should().Contain("ResizeObserver",
            "Sticky logika musí reagovat na resize.");
        js.Should().Contain("data-aligned-stepper",
            "Sticky logika musí targetovat aligned-stepper.");
    }

    [Fact]
    public void ViewModelBuilder_FiltrujeKrokyPerTyp()
    {
        var cs = LoadRepoText("PmTracker.Web/Services/ServiceDesk/VyjadreniModalViewModelBuilder.cs");
        cs.Should().Contain("[\"PMP\"] = new HashSet<int> { 1, 2, 3, 4, 5 }",
            "Builder musí mít hardcoded set {1,2,3,4,5} pro PMP.");
        cs.Should().Contain("[\"PNF\"] = new HashSet<int> { 1, 6, 7, 8, 9, 10 }",
            "Builder musí mít hardcoded set {1,6,7,8,9,10} pro PNF.");
        cs.Should().Contain("[\"NES\"] = new HashSet<int>()",
            "Builder musí mít prázdný set pro NES.");
        cs.Should().Contain("relevantSteps.Contains(k.KrokPoradi)",
            "Builder musí filtrovat Kroky podle relevantSteps.");
    }
}
