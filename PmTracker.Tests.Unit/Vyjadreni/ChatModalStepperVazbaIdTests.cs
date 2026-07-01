using System.IO;
using FluentAssertions;
using PmTracker.Web.Models.ViewModels.Vyjadreni;

namespace PmTracker.Tests.Unit.Vyjadreni;

/// <summary>
/// Review finding A-5: StepperKrokViewModel.VazbaId + _ChatModal.cshtml musí
/// renderovat <c>data-vazba-id</c> na každém kroku, aby JS Delete handler uměl
/// poslat skutečné ID vazby do backend endpointu.
/// </summary>
public sealed class ChatModalStepperVazbaIdTests
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
    public void StepperKrokViewModel_HasVazbaIdProperty()
    {
        typeof(StepperKrokViewModel).GetProperty(nameof(StepperKrokViewModel.VazbaId))
            .Should().NotBeNull("A-5: StepperKrokViewModel musí mít VazbaId pro UI Delete handler");
    }

    [Fact]
    public void ChatModalPartial_RendersDataVazbaIdOnStep()
    {
        var text = LoadRepoText("PmTracker.Web/Views/Vyjadreni/_ChatModal.cshtml");
        text.Should().Contain(
            "data-vazba-id=\"@krok.VazbaId\"",
            "A-5: _ChatModal.cshtml musí renderovat data-vazba-id pro JS Delete handler");
    }

    [Fact]
    public void ChatModalDragDropModule_CallsDeleteBindingWithVazbaId()
    {
        // ESM modul (aplikace běží na site.js; legacy site.bundle.js odstraněn 2026-06-16).
        var text = LoadRepoText("PmTracker.Web/wwwroot/js/modules/vyjadreni/bubbleStepSelector.js");
        text.Should().Contain(
            "/Vyjadreni/HarmonogramVazba/Delete",
            "A-5: modul musí POSTovat na Delete endpoint");
        text.Should().Contain(
            "data-vazba-id",
            "A-5: modul čte skutečnou VazbaId ze step atributu");
    }

    [Fact]
    public void ChatModalModule_ExposesRefreshModalOnPublicApi()
    {
        var text = LoadRepoText("PmTracker.Web/wwwroot/js/modules/vyjadreni/chatModal.js");
        text.Should().Contain(
            "global.pmChatModal = { init, open, close, refreshModal }",
            "A-5: pmChatModal API musí vystavit refreshModal");
    }
}
