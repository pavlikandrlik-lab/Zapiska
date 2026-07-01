using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Guard proti silent regresím v ESM JS modulech: import-omission v modulu = ReferenceError
/// při runtime (bundler nic nehlásí).
///
/// User report 2026-04-19: modal po save nešel zavřít, protože `getActiveModalContainer()`
/// byl volán v recordEditor.js bez importu a `recordEditorState` používán v ui.js bez importu.
///
/// (Bundle-rename guardy odstraněny 2026-06-16 spolu s legacy site.bundle.js — ESM nezesuffixovává.)
/// </summary>
public sealed class JsBundleImportConsistencyTests
{
    private static string LocateRepositoryRoot()
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

        return directory.FullName;
    }

    private static string ReadModule(string relativePath)
    {
        var path = Path.Combine(LocateRepositoryRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(path).Should().BeTrue($"modul musí existovat: {path}");
        return File.ReadAllText(path);
    }

    // RecordEditorModule_MustImportGetActiveModalContainer test smazán
    // (refaktor 2026-04-25: modal pro úpravu/tvorbu záznamu odstraněn,
    // close-guard host je vždy document.body — getActiveModalContainer se nepoužívá).

    [Fact]
    public void UiModule_MustNotReferenceRecordEditorStateWithoutImport()
    {
        // Fáze 3B Task 5: ui.js je barrel — runtime registry
        // registerFloatingChooser je v submodulu ui/print.js (aux chooser
        // infrastructure pro print popover + potenciálně další dropdowns).
        var source = ReadModule("PmTracker.Web/wwwroot/js/modules/ui/print.js");

        // Strip komentáře, aby identifier v komentářích nezapočítával.
        var stripped = Regex.Replace(source, @"//.*$", string.Empty, RegexOptions.Multiline);
        stripped = Regex.Replace(stripped, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        var directReferenceRegex = new Regex(@"\brecordEditorState\b");
        if (directReferenceRegex.IsMatch(stripped))
        {
            var importRegex = new Regex(@"import\s*\{[^}]*\brecordEditorState\b[^}]*\}\s*from");
            importRegex.IsMatch(stripped).Should().BeTrue(
                "ui/print.js nesmí používat recordEditorState bez importu (vede na ReferenceError za runtime při scroll/resize).");
        }
        else
        {
            // Preferred stav: aux chooser registry decoupled od recordEditor
            source.Should().Contain(
                "registerFloatingChooser",
                "ui/print.js exportuje registerFloatingChooser runtime registry (aux chooser infrastructure)");
        }
    }
}
