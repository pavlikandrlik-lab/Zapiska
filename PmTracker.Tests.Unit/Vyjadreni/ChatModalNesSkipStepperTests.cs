using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Vyjadreni;

/// <summary>
/// Spec 2026-04-28: NES vazby jsou odpojené od harmonogramu — modal „Vyjádření a termíny"
/// pro NES tiket nesmí renderovat stepper sekci ani drag-drop hint.
///
/// Tento regression test čte Razor partial a kontroluje, že stepper sekce + footer hint
/// jsou zabaleny v `if (!isNes)` podmínce.
/// </summary>
public sealed class ChatModalNesSkipStepperTests
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
    public void ChatModalPartial_DefinujeIsNesFlag()
    {
        var html = LoadRepoText("PmTracker.Web/Views/Vyjadreni/_ChatModal.cshtml");
        html.Should().Contain("var isNes = string.Equals(Model.TiketTyp, \"NES\"",
            "Spec 2026-04-28: NES detekce přes Model.TiketTyp pro skip stepper sekce.");
    }

    [Fact]
    public void ChatModalPartial_StepperJeZaSchovanaPodIfNotIsNes()
    {
        var html = LoadRepoText("PmTracker.Web/Views/Vyjadreni/_ChatModal.cshtml");

        // Stepper section musí být v `@if (!isNes) { ... }` bloku.
        html.Should().Contain("@if (!isNes)",
            "Stepper sekce musí být zabalena v `if (!isNes)` pro skrytí u NES tiketu.");
        html.Should().Contain("<section class=\"pm-chat-modal__stepper\"",
            "Stepper section element musí v Razor partialu existovat (jen conditional pro NES).");
    }

    [Fact]
    public void ChatModalPartial_FooterHintRespektujeNes()
    {
        var html = LoadRepoText("PmTracker.Web/Views/Vyjadreni/_ChatModal.cshtml");
        html.Should().Contain("@if (canEdit && !isNes)",
            "Drag-drop hint footer musí být schován pro NES (žádný stepper = nemá kam přetahovat).");
    }

    [Fact]
    public void ChatModalPartial_BublinyNejsouDraggableProNes()
    {
        var html = LoadRepoText("PmTracker.Web/Views/Vyjadreni/_ChatModal.cshtml");
        html.Should().Contain("draggable=\"@(canEdit && !isNes ? \"true\" : \"false\")\"",
            "Bubliny pro NES nejsou draggable (žádný stepper drop target).");
    }
}
