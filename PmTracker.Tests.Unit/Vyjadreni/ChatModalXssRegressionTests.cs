using System.IO;
using System.Text.Encodings.Web;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Vyjadreni;

/// <summary>
/// Review finding S-1: Stored XSS v chat modalu — <c>@@Html.Raw(bubble.Popis)</c>
/// renderovalo nedůvěryhodný obsah z HOT_VYJADRENI jako HTML. Tento test zajišťuje,
/// že _ChatModal.cshtml používá plain Razor expression (auto-encoding) a že
/// Razor HtmlEncoder chová se očekávaně (encoduje script tag).
/// </summary>
public sealed class ChatModalXssRegressionTests
{
    private static string LoadRepoText(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PmTracker.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Nepodařilo se najít kořen repozitáře (PmTracker.sln).");
        }

        var full = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(full).Should().BeTrue($"soubor musí existovat na cestě {full}");
        return File.ReadAllText(full);
    }

    [Fact]
    public void ChatModal_MustNotUseHtmlRawOnBubblePopis()
    {
        var text = LoadRepoText("PmTracker.Web/Views/Vyjadreni/_ChatModal.cshtml");

        text.Should().NotContain(
            "@Html.Raw(bubble.Popis)",
            "S-1: nedůvěryhodný obsah z HOT_VYJADRENI se nesmí renderovat přes @Html.Raw (stored XSS).");
    }

    [Fact]
    public void ChatModal_BubblePopis_IsRenderedWithPreserveWhitespace()
    {
        var text = LoadRepoText("PmTracker.Web/Views/Vyjadreni/_ChatModal.cshtml");

        // Plain Razor expression — Razor auto-HTML-encoduje hodnotu.
        text.Should().Contain("@bubble.Popis", "popis musí být renderován jako auto-encoded Razor expression");
        // CSS zachování newlines, aby UI formát nebyl regresí proti předchozímu Html.Raw.
        text.Should().Contain("white-space: pre-wrap", "popis musí zachovat formátování přes CSS (bez interpretace HTML)");
    }

    [Fact]
    public void RazorHtmlEncoder_EncodesScriptTagsInBubblePopis()
    {
        // Simuluje to, co dělá Razor pro @@bubble.Popis — HtmlEncoder.Default
        // je to, co ASP.NET Core Razor ve výchozím nastavení používá.
        var maliciousPopis = "<script>alert(1)</script>";
        var encoded = HtmlEncoder.Default.Encode(maliciousPopis);

        encoded.Should().Be("&lt;script&gt;alert(1)&lt;/script&gt;",
            "Razor auto-encoding musí script tagy escapovat na &lt;script&gt;, ne je nechat jako raw <script>.");
        encoded.Should().NotContain("<script>", "raw <script> nesmí projít");
    }
}
