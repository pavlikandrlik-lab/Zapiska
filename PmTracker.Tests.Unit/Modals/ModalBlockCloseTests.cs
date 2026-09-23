using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Modals;

/// <summary>
/// A7 (2026-07-08): gov-design-system nově renderuje X jako disabled=blockClose →
/// block-close="true" umrtvil křížek ve VŠECH modalech. Atribut nesmí existovat;
/// block-backdrop-close (žádný backdrop-close, drag-select ochrana) zůstává povinný.
/// </summary>
public sealed class ModalBlockCloseTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }
    private static string Read(string rel) => File.ReadAllText(Path.Combine(RepoRoot(), rel));

    [Theory]
    [InlineData("PmTracker.Web/Views/Shared/_ModalLayout.cshtml")]
    [InlineData("PmTracker.Web/wwwroot/js/modules/modals.js")]
    public void GovDialog_HasNoBlockClose_ButKeepsBackdropBlock(string rel)
    {
        var src = Read(rel);
        src.Should().NotContain("block-close=\"true\"",
            "block-close disabluje X v nové gov verzi (disabled: this.blockClose)");
        src.Should().Contain("block-backdrop-close=\"true\"",
            "backdrop-close zůstává zakázaný (modal se zavírá jen X/Escape)");
    }
}
