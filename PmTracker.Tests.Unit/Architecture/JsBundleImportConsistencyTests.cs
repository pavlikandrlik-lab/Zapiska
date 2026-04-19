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
        var source = ReadModule("PmTracker.Web/wwwroot/js/modules/recordEditor.js");

        // Používá getActiveModalContainer v close-guard flow
        source.Should().Contain(
            "getActiveModalContainer()",
            "recordEditor.js potřebuje getActiveModalContainer() pro umístění close-guard dialogu uvnitř modálu");

        // Musí ho importovat z modals.js (jinak ReferenceError za runtime)
        var importRegex = new Regex(@"import\s*\{[^}]*\bgetActiveModalContainer\b[^}]*\}\s*from\s*[""']\./modals\.js[""']");
        importRegex.IsMatch(source).Should().BeTrue(
            "recordEditor.js musí importovat getActiveModalContainer z modals.js (jinak silent regression – ReferenceError při zavírání dirty modalu)");
    }

    [Fact]
    public void UiModule_MustNotReferenceRecordEditorStateWithoutImport()
    {
        var source = ReadModule("PmTracker.Web/wwwroot/js/modules/ui.js");

        // Strip line comments tak, aby identifier v komentáři (vysvětlení proč
        // runtime registry neimportuje recordEditorState) nespouštěl detekci.
        var stripped = Regex.Replace(source, @"//.*$", string.Empty, RegexOptions.Multiline);
        stripped = Regex.Replace(stripped, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        var directReferenceRegex = new Regex(@"\brecordEditorState\b");
        if (directReferenceRegex.IsMatch(stripped))
        {
            // Pokud ui.js reálně odkazuje recordEditorState, musí ho importovat
            var importRegex = new Regex(@"import\s*\{[^}]*\brecordEditorState\b[^}]*\}\s*from");
            importRegex.IsMatch(stripped).Should().BeTrue(
                "ui.js nesmí používat recordEditorState bez importu (vede na ReferenceError za runtime při scroll/resize).");
        }
        else
        {
            // Preferred stav: ui.js používá runtime registry decoupled od recordEditor.js
            source.Should().Contain(
                "registerFloatingChooser",
                "ui.js by měla exportovat registerFloatingChooser runtime registry pro decoupling s recordEditor.js");
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
