using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Guard proti silent regresím v site.bundle.js a JS modulech:
/// - import-omission v modulu = ReferenceError při runtime (bundler nic nehlásí)
/// - bundle rename conflicting definitions na `X2` ale missing references v cizích modulech zůstávají `X`
///
/// User report 2026-04-19: modal po save nešel zavřít, protože `getActiveModalContainer()`
/// byl volán v recordEditor.js bez importu a `recordEditorState` používán v ui.js bez importu.
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

    [Fact]
    public void RecordEditorModule_MustImportGetActiveModalContainer()
    {
        // Fáze 3B Task 1: recordEditor.js je barrel — getActiveModalContainer
        // je v submodulu draft.js (close-guard dialog flow)
        var source = ReadModule("PmTracker.Web/wwwroot/js/modules/recordEditor/draft.js");

        // Používá getActiveModalContainer v close-guard flow
        source.Should().Contain(
            "getActiveModalContainer()",
            "recordEditor/draft.js potřebuje getActiveModalContainer() pro umístění close-guard dialogu uvnitř modálu");

        // Musí ho importovat z modals.js (jinak ReferenceError za runtime)
        var importRegex = new Regex(@"import\s*\{[^}]*\bgetActiveModalContainer\b[^}]*\}\s*from\s*[""']\.\.\/modals\.js[""']");
        importRegex.IsMatch(source).Should().BeTrue(
            "recordEditor/draft.js musí importovat getActiveModalContainer z ../modals.js (jinak silent regression – ReferenceError při zavírání dirty modalu)");
    }

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

    [Fact]
    public void SiteBundle_MustNotReferenceUndefinedRecordEditorState()
    {
        var source = ReadModule("PmTracker.Web/wwwroot/js/site.bundle.js");

        // Pokud je `recordEditorState2` definovaný (bundle rename), všechny reference musí být
        // recordEditorState2 – samotné `recordEditorState.` bez suffixu = silent bug
        if (source.Contains("var recordEditorState2"))
        {
            var bareUsageRegex = new Regex(@"(?<![a-zA-Z0-9_])recordEditorState\.[a-zA-Z]");
            var matches = bareUsageRegex.Matches(source);
            matches.Count.Should().Be(0,
                "bundle má definici recordEditorState2 – všechny reference musí mít suffix 2. " +
                $"Nalezeno {matches.Count} nezesuffixovaných použití (silent regression po bundle rename).");
        }
    }

    [Fact]
    public void SiteBundle_MustNotReferenceUndefinedGetActiveModalContainer()
    {
        var source = ReadModule("PmTracker.Web/wwwroot/js/site.bundle.js");

        if (source.Contains("function getActiveModalContainer2"))
        {
            var bareCallRegex = new Regex(@"(?<![a-zA-Z0-9_])getActiveModalContainer\(");
            var matches = bareCallRegex.Matches(source);
            matches.Count.Should().Be(0,
                "bundle má definici getActiveModalContainer2() – všechny volání musí být suffixováné. " +
                $"Nalezeno {matches.Count} nezesuffixovaných volání (silent regression).");
        }
    }
}
