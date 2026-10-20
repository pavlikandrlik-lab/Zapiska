using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// 2026-10-20 (tisk jednání: „Přístup odepřen“ pro PM / vedoucího subsystému):
/// PermissionAuthorizationHandler čte projektId jen z route → query → form. Odkaz nebo formulář
/// na akci s projektovou politikou bez projektId = globální kontrola → 403 pro projektové
/// a subsystémové role. Pojistka projde všechna místa v Razoru a controllerech, kde se na takovou
/// akci skládá URL (Url.Action / RedirectToAction / &lt;a asp-action&gt;) nebo míří formulář.
/// Akce, jejichž klíč mají jen globální role (např. projects.edit), se nekontrolují — tam
/// globální kontrola stačí. Heuristika: Url.Action bez controlleru se přiřadí podle složky
/// view / jména souboru controlleru; Views/Shared bez controlleru se přeskočí.
/// </summary>
public sealed class ProjectScopedPolicyRequestCarriesProjektIdTests
{
    private const string PolicyPrefix = "permission:";

    private sealed record PolicyAction(string Controller, string RoutePrefix, string Action, string ActionPath, string Verb, string Key);

    private sealed record SourceFile(string RelativePath, string? Controller, string Text);

    private static readonly Regex UrlGenerationCall = new(
        @"(?:Url\.Action|RedirectToAction)\(\s*(?:""(?<action>\w+)""|nameof\((?:\w+\.)?(?<action>\w+)\))\s*(?:,\s*""(?<controller>\w+)"")?\s*(?:,\s*new\s*\{(?<args>[^}]*)\})?\s*\)",
        RegexOptions.Compiled);

    private static readonly Regex AnchorTagHelper = new(@"<a\b[^>]*\basp-action=""(?<action>\w+)""[^>]*>", RegexOptions.Compiled);
    private static readonly Regex FormOpenTag = new(@"<form\b[^>]*>", RegexOptions.Compiled);
    private static readonly Regex FormActionButton = new(@"formaction=""@Url\.Action\(""(?<action>\w+)"",\s*""(?<controller>\w+)""\)""", RegexOptions.Compiled);
    private static readonly Regex AspController = new(@"asp-controller=""(?<controller>\w+)""", RegexOptions.Compiled);
    private static readonly Regex AspAction = new(@"asp-action=""(?<action>\w+)""", RegexOptions.Compiled);
    private static readonly Regex LiteralFormAction = new(@"\baction=""/(?<prefix>\w+)/(?<path>\w+)""", RegexOptions.Compiled);
    private static readonly Regex ProjektIdRouteValue = new(@"\bprojektId\b", RegexOptions.Compiled);

    // Klíč formuláře musí být přesně „projektId“ (case-insensitive) — asp-for="Command.ProjektId"
    // by se odeslal jako „Command.ProjektId“ a handler by ho nenašel.
    private static readonly Regex ProjektIdFormField = new(@"name=""projektId""|asp-for=""ProjektId""", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    [Fact]
    public void LinksToProjectScopedGetActions_CarryProjektId()
    {
        var targets = ActionsNeedingProjektIdInRequest().Where(a => a.Verb == "GET").ToList();
        targets.Should().Contain(a => a.Controller == "Export" && a.Action == "JednaniTisk",
            "sanity: tisk jednání je akce s projektovou politikou bez projektId v route");

        var checkedSites = 0;
        var violations = new List<string>();
        foreach (var file in SourceFiles())
        {
            foreach (Match call in UrlGenerationCall.Matches(file.Text))
            {
                var controller = call.Groups["controller"].Success ? call.Groups["controller"].Value : file.Controller;
                var target = Find(targets, controller, call.Groups["action"].Value);
                if (target is null)
                {
                    continue;
                }

                checkedSites++;
                if (!ProjektIdRouteValue.IsMatch(call.Groups["args"].Value))
                {
                    violations.Add($"{file.RelativePath}:{LineOf(file.Text, call.Index)} → {target.Controller}.{target.Action} ({target.Key})");
                }
            }

            foreach (Match tag in AnchorTagHelper.Matches(file.Text))
            {
                var controllerMatch = AspController.Match(tag.Value);
                var target = Find(targets, controllerMatch.Success ? controllerMatch.Groups["controller"].Value : file.Controller, tag.Groups["action"].Value);
                if (target is null)
                {
                    continue;
                }

                checkedSites++;
                if (!tag.Value.Contains("asp-route-projektId", StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add($"{file.RelativePath}:{LineOf(file.Text, tag.Index)} → {target.Controller}.{target.Action} ({target.Key})");
                }
            }
        }

        checkedSites.Should().BeGreaterThanOrEqualTo(20,
            "sanity: kontrola musí najít existující odkazy (detail jednání, tisk úkolu, modaly týmu a jednání)");
        violations.Should().BeEmpty(
            "PermissionAuthorizationHandler čte projektId jen z route → query → form; odkaz bez něj = globální kontrola → " +
            "403 pro projektové a subsystémové role. Doplň projektId = … do route values. Chybí: " + string.Join("; ", violations));
    }

    [Fact]
    public void FormsPostingToProjectScopedActions_CarryProjektIdField()
    {
        var targets = ActionsNeedingProjektIdInRequest().Where(a => a.Verb == "POST").ToList();
        targets.Should().Contain(a => a.Controller == "VyjadreniModal" && a.Action == "ReHarvest",
            "sanity: Znovu vytěžit je POST s projektovou politikou bez projektId v route");

        var checkedForms = 0;
        var violations = new List<string>();
        foreach (var file in SourceFiles().Where(f => f.RelativePath.EndsWith(".cshtml", StringComparison.Ordinal)))
        {
            foreach (Match open in FormOpenTag.Matches(file.Text))
            {
                var bodyStart = open.Index + open.Length;
                var end = file.Text.IndexOf("</form>", bodyStart, StringComparison.Ordinal);
                var body = file.Text[bodyStart..(end < 0 ? file.Text.Length : end)];
                foreach (var target in FormTargets(open.Value, body, file.Controller, targets).Distinct())
                {
                    checkedForms++;
                    if (!ProjektIdFormField.IsMatch(body))
                    {
                        violations.Add($"{file.RelativePath}:{LineOf(file.Text, open.Index)} → {target.Controller}.{target.Action} ({target.Key})");
                    }
                }
            }
        }

        checkedForms.Should().BeGreaterThanOrEqualTo(10,
            "sanity: kontrola musí najít existující formuláře (mazání jednání, komentáře, návrhy, mazání projektu)");
        violations.Should().BeEmpty(
            "formulář na akci s projektovou politikou musí poslat pole projektId (hidden input name=\"projektId\"), " +
            "jinak handler udělá globální kontrolu → 403 pro projektové role. Chybí: " + string.Join("; ", violations));
    }

    /// <summary>
    /// Akce s [Authorize(Policy = "permission:k")], kde k je projektový klíč, který drží aspoň jedna
    /// projektová / subsystémová role, a route akce neobsahuje {projektId}. Controller s politikou,
    /// kterou projektové role nemají (např. SDConnector = settings.sd.view), se přeskočí.
    /// </summary>
    private static IReadOnlyList<PolicyAction> ActionsNeedingProjektIdInRequest()
    {
        var projectLevelKeys = PermissionSeedConfiguration.Actions
            .Where(a => a.ScopeLevel == PermissionScopeLevel.Project)
            .Select(a => a.Klic)
            .ToHashSet(StringComparer.Ordinal);
        var nonGlobalRoles = PermissionSeedConfiguration.Roles
            .Where(r => r.Scope != RoleScope.Global)
            .Select(r => r.Kod)
            .ToHashSet(StringComparer.Ordinal);
        var keysHeldByProjectRoles = PermissionSeedConfiguration.RoleMappings
            .Where(m => m.IsAllowed && nonGlobalRoles.Contains(m.RoleKod))
            .Select(m => m.ActionKlic)
            .ToHashSet(StringComparer.Ordinal);

        var result = new List<PolicyAction>();
        foreach (var controller in typeof(Program).Assembly.GetTypes().Where(IsController))
        {
            if (PolicyKeys(controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true)).Any(k => !keysHeldByProjectRoles.Contains(k)))
            {
                continue;
            }

            var name = controller.Name[..^"Controller".Length];
            var routePrefix = controller.GetCustomAttributes<RouteAttribute>().FirstOrDefault()?.Template ?? name;
            foreach (var method in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var verb = method.GetCustomAttributes<HttpMethodAttribute>().FirstOrDefault();
                if (verb is null)
                {
                    continue;
                }

                var actionPath = verb.Template ?? method.Name;
                if ($"{routePrefix}/{actionPath}".Contains("{projektId", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var key in PolicyKeys(method.GetCustomAttributes<AuthorizeAttribute>()))
                {
                    if (projectLevelKeys.Contains(key) && keysHeldByProjectRoles.Contains(key))
                    {
                        result.Add(new PolicyAction(name, routePrefix, method.Name, actionPath, verb.HttpMethods.First(), key));
                    }
                }
            }
        }

        return result;
    }

    private static IEnumerable<PolicyAction> FormTargets(string openTag, string body, string? fileController, IReadOnlyList<PolicyAction> targets)
    {
        var aspAction = AspAction.Match(openTag);
        if (aspAction.Success)
        {
            var aspController = AspController.Match(openTag);
            var target = Find(targets, aspController.Success ? aspController.Groups["controller"].Value : fileController, aspAction.Groups["action"].Value);
            if (target is not null)
            {
                yield return target;
            }
        }

        var literal = LiteralFormAction.Match(openTag);
        if (literal.Success)
        {
            var target = targets.FirstOrDefault(t =>
                string.Equals(t.RoutePrefix, literal.Groups["prefix"].Value, StringComparison.OrdinalIgnoreCase)
                && string.Equals(t.ActionPath, literal.Groups["path"].Value, StringComparison.OrdinalIgnoreCase));
            if (target is not null)
            {
                yield return target;
            }
        }

        foreach (Match button in FormActionButton.Matches(body))
        {
            var target = Find(targets, button.Groups["controller"].Value, button.Groups["action"].Value);
            if (target is not null)
            {
                yield return target;
            }
        }
    }

    private static PolicyAction? Find(IReadOnlyList<PolicyAction> targets, string? controller, string action) =>
        controller is null
            ? null
            : targets.FirstOrDefault(t =>
                string.Equals(t.Controller, controller, StringComparison.OrdinalIgnoreCase)
                && string.Equals(t.Action, action, StringComparison.Ordinal));

    private static IEnumerable<string> PolicyKeys(IEnumerable<AuthorizeAttribute> attributes) =>
        attributes
            .Select(a => a.Policy)
            .Where(p => p is not null && p.StartsWith(PolicyPrefix, StringComparison.Ordinal))
            .Select(p => p![PolicyPrefix.Length..]);

    private static bool IsController(Type type) =>
        !type.IsAbstract
        && typeof(ControllerBase).IsAssignableFrom(type)
        && type.Name.EndsWith("Controller", StringComparison.Ordinal);

    private static IEnumerable<SourceFile> SourceFiles()
    {
        var web = ArchitectureTestBase.ResolvePath("PmTracker.Web");
        foreach (var path in Directory.EnumerateFiles(Path.Combine(web, "Views"), "*.cshtml", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(web, path).Replace('\\', '/');
            var folder = relative.Split('/')[1];
            yield return new SourceFile(relative, folder == "Shared" ? null : folder, File.ReadAllText(path));
        }

        foreach (var path in Directory.EnumerateFiles(Path.Combine(web, "Controllers"), "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(web, path).Replace('\\', '/');
            var match = Regex.Match(Path.GetFileName(path), @"^(?<name>\w+?)Controller\b");
            yield return new SourceFile(relative, match.Success ? match.Groups["name"].Value : null, File.ReadAllText(path));
        }
    }

    private static int LineOf(string text, int index) => text[..index].Count(c => c == '\n') + 1;
}
