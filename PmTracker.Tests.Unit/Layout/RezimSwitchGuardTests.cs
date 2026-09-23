using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// C3 (2026-07-10): rezim master switch na schvalování návrhu je disabled —
/// JS handler musí disabled host ignorovat (defensivní vrstva nad server-side
/// disabled atributem; gov event by jinak mohl přepnout hidden input).
/// </summary>
public sealed class RezimSwitchGuardTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    [Fact]
    public void RezimMasterSwitch_IgnoresDisabledHost()
    {
        var js = File.ReadAllText(Path.Combine(
            RepoRoot(), "PmTracker.Web/wwwroot/js/modules/harmonogram/rezim-master-switch.js"));
        js.Should().Contain("hasAttribute('disabled')",
            "C3: disabled switch (schvalování návrhu) se nesmí zpracovat v handleChange");
    }
}
