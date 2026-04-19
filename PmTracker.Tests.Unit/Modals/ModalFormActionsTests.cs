using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Modals;

/// <summary>
/// Ověřuje, že _ModalFormActions renderuje submit jako pm-button
/// s variantem z ModalFormActionsViewModel.SubmitVariant (Fáze 2E).
/// </summary>
public sealed class ModalFormActionsTests
{
    private static string LoadText(string relativePath)
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
    public void Partial_ShouldUsePmButtonForSubmit()
    {
        var partial = LoadText("PmTracker.Web/Views/Shared/_ModalFormActions.cshtml");

        partial.Should().Contain(
            "<pm-button variant=\"@Model.SubmitVariant\" native-type=\"submit\"",
            "submit button musí být pm-button s variantem z modelu (ne raw CSS string)");
        partial.Should().NotContain(
            "SubmitCssClass",
            "legacy SubmitCssClass byl odstraněn ve prospěch PmButtonVariant");
    }

    [Fact]
    public void DeleteRecordModal_ShouldUseDestructiveVariant()
    {
        var view = LoadText("PmTracker.Web/Views/Projekty/DeleteRecordModal.cshtml");

        view.Should().Contain(
            "SubmitVariant = PmTracker.Web.TagHelpers.PmButtonVariant.Destructive",
            "delete modal musí používat Destructive variant (červené tlačítko)");
        view.Should().NotContain(
            "btn ghost danger",
            "legacy CSS string odstraněn");
    }

    [Fact]
    public void ModalFormActionsViewModel_ShouldDefaultToPrimary()
    {
        var model = LoadText("PmTracker.Web/Models/ViewModels/ModalViewModels.cs");

        model.Should().Contain(
            "SubmitVariant { get; init; }",
            "SubmitVariant property musí existovat");
        model.Should().Contain(
            "PmTracker.Web.TagHelpers.PmButtonVariant.Primary",
            "default hodnota SubmitVariant je Primary");
        model.Should().NotContain(
            "SubmitCssClass",
            "legacy property odstraněna");
    }
}
