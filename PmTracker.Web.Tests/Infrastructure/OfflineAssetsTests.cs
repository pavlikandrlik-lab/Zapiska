using System.IO;
using System.Linq;

namespace PmTracker.Web.Tests.Infrastructure;

/// <summary>
/// Regression testy ověřující přítomnost offline SVG assets pro gov-icon Web Component.
/// gov-icon fetchuje ikony z /assets/icons/{type}/{name}.svg?v=4.2.9 — soubory musí existovat.
/// </summary>
public sealed class OfflineAssetsTests
{
    // Cesta k wwwroot/assets/icons/components/ — relativně k výstupnímu adresáři
    private static readonly string IconsComponentsDir = Path.GetFullPath(
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "PmTracker.Web", "wwwroot", "assets", "icons", "components"));

    [Fact]
    public void ComponentsIconsDirectory_Exists()
    {
        Assert.True(Directory.Exists(IconsComponentsDir),
            $"Adresář s ikonami neexistuje: {IconsComponentsDir}");
    }

    [Fact]
    public void CheckSvg_ExistsAndIsNonEmpty()
    {
        var checkSvgPath = Path.Combine(IconsComponentsDir, "check.svg");
        Assert.True(File.Exists(checkSvgPath),
            $"Soubor check.svg nenalezen: {checkSvgPath}");
        var content = File.ReadAllText(checkSvgPath);
        Assert.False(string.IsNullOrWhiteSpace(content), "check.svg je prázdný soubor");
        Assert.True(content.Length > 10, "check.svg je příliš krátký — pravděpodobně poškozený");
    }

    [Fact]
    public void ComponentsIcons_AtLeastTenFilesPresent()
    {
        if (!Directory.Exists(IconsComponentsDir))
        {
            Assert.Fail($"Adresář s ikonami neexistuje: {IconsComponentsDir}");
            return;
        }

        var svgFiles = Directory.GetFiles(IconsComponentsDir, "*.svg");
        Assert.True(svgFiles.Length >= 10,
            $"Očekáváno minimálně 10 SVG ikon v {IconsComponentsDir}, nalezeno: {svgFiles.Length}");
    }

    [Fact]
    public void AllSvgFiles_AreValidSvgContent()
    {
        if (!Directory.Exists(IconsComponentsDir))
        {
            Assert.Fail($"Adresář s ikonami neexistuje: {IconsComponentsDir}");
            return;
        }

        var svgFiles = Directory.GetFiles(IconsComponentsDir, "*.svg");
        Assert.True(svgFiles.Length > 0, "Nebyly nalezeny žádné SVG soubory");

        var invalidFiles = svgFiles
            .Where(f =>
            {
                var content = File.ReadAllText(f).TrimStart();
                // Validní SVG musí začínat <svg nebo <?xml
                return !content.StartsWith("<svg", System.StringComparison.OrdinalIgnoreCase)
                    && !content.StartsWith("<?xml", System.StringComparison.OrdinalIgnoreCase);
            })
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(invalidFiles.Count == 0,
            $"Nalezeny neplatné SVG soubory (pravděpodobně HTML chybové stránky): {string.Join(", ", invalidFiles)}");
    }

    [Theory]
    [InlineData("check.svg")]
    [InlineData("x.svg")]
    [InlineData("arrow-right.svg")]
    [InlineData("check-lg.svg")]
    [InlineData("exclamation-lg.svg")]
    [InlineData("eye.svg")]
    [InlineData("search.svg")]
    public void RequiredIcon_ExistsAndIsValidSvg(string iconFileName)
    {
        var filePath = Path.Combine(IconsComponentsDir, iconFileName);
        Assert.True(File.Exists(filePath), $"Povinná ikona chybí: {iconFileName}");

        var content = File.ReadAllText(filePath).TrimStart();
        Assert.True(
            content.StartsWith("<svg", System.StringComparison.OrdinalIgnoreCase)
            || content.StartsWith("<?xml", System.StringComparison.OrdinalIgnoreCase),
            $"Soubor {iconFileName} není validní SVG (nezačíná <svg nebo <?xml)");
    }
}
