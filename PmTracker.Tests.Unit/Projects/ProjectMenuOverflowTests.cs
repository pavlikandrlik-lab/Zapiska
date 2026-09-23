using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Projects;

/// <summary>
/// Projektové menu (Option 2, revize 2026-07-07): 3 primární + sekundární skupina.
/// Aktivní sekundární tab je vždy inline (podtržení), „+" jednosměrně rozbalí neaktivní
/// (bez outside-close), zámeček = trvalé rozbalení. Spec: 2026-07-05 design + 2026-07-07 revize.
/// Source-assertion invarianty (ikony, cookie helper, JS chování, registr, Detail markup).
/// </summary>
public sealed class ProjectMenuOverflowTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }
    private static string Read(string rel) => File.ReadAllText(Path.Combine(RepoRoot(), rel));
    private static bool Exists(string rel) => File.Exists(Path.Combine(RepoRoot(), rel));

    [Fact]
    public void LockIcons_Exist()
    {
        Exists("PmTracker.Web/wwwroot/assets/icons/components/lock.svg").Should().BeTrue();
        Exists("PmTracker.Web/wwwroot/assets/icons/components/unlock.svg").Should().BeTrue();
    }

    [Fact]
    public void CookieHelper_ExportsReadWriteDelete()
    {
        var src = Read("PmTracker.Web/wwwroot/js/modules/preferences/cookie.js");
        src.Should().Contain("export function readCookie");
        src.Should().Contain("export function writeCookie");
        src.Should().Contain("export function deleteCookie");
        src.Should().Contain("SameSite=Lax");
    }

    [Fact]
    public void ProjectMenuJs_HasExpandAndLockBehavior()
    {
        var src = Read("PmTracker.Web/wwwroot/js/modules/projectMenu.js");
        src.Should().Contain("export function initProjectMenuOverflow");
        src.Should().Contain("data-project-menu-group");
        src.Should().Contain("data-project-menu-toggle");
        src.Should().Contain("data-project-menu-lock-toggle");
        src.Should().Contain("is-expanded");
        src.Should().Contain("location.reload");
        // Jednosměrné rozbalení — žádný outside-close ani Escape (na to si user stěžoval).
        src.Should().NotContain("Escape");
    }

    [Fact]
    public void Bootstrap_WiresProjectMenuOverflow()
    {
        var src = Read("PmTracker.Web/wwwroot/js/modules/bootstrap.js");
        src.Should().Contain("initProjectMenuOverflow");
    }

    [Fact]
    public void Registry_DeclaresMenuLockDescriptor()
    {
        var src = Read("PmTracker.Web/wwwroot/js/modules/preferences/registry.js");
        src.Should().Contain("id: \"menuLock\"");
        src.Should().Contain("Trvale rozbalené projektové menu");
    }

    [Fact]
    public void Detail_RendersSecondaryGroup_WithLockAndConditionalExpand()
    {
        var src = Read("PmTracker.Web/Views/Projekty/Detail.cshtml");
        src.Should().Contain("Model.ProjectMenuLocked");
        src.Should().Contain("data-project-menu-group");
        src.Should().Contain("data-project-menu-lock-toggle");
        src.Should().Contain("tab-secondary");
        // Zamčeno = skupina rovnou rozbalená (server), jinak sbaleno + „+".
        src.Should().Contain("is-expanded");
        // Overflow „+" popover (starý design) je pryč.
        src.Should().NotContain("data-project-menu-popover");
    }
}
