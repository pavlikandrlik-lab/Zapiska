using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services;

namespace PmTracker.Tests.Unit.Services;

/// <summary>
/// Vertical slice: <see cref="IProjectEditQuery"/> je single-use-case entry point
/// pro sestavení <see cref="ZaznamEditViewModel"/>. SRP dodržuje tím, že má jedinou
/// veřejnou metodu — orchestraci zná jen ona, ne rozlezlá do RecordService partials.
/// </summary>
public sealed class ProjectEditQueryTests
{
    [Fact]
    public async Task GetEditModelAsync_DelegatesToRecordService_ReturnsBuiltViewModel()
    {
        var recordService = new Mock<IRecordService>();
        // Minimalistický stub — test asseruje pouze identitu vraceného modelu (BeSameAs).
        // `required` members musíme vyplnit (compiler enforcement); konkrétní hodnoty jsou
        // nepodstatné pro delegation test.
        var expected = new ZaznamEditViewModel
        {
            CisloViditelne = string.Empty,
            JednaniProCisloOptions = Array.Empty<JednaniOptionViewModel>(),
            Nazev = string.Empty,
            Cil = string.Empty,
            Kategorie = string.Empty,
            Popis = string.Empty,
            Stav = string.Empty,
            KategorieZaznamu = Array.Empty<string>(),
            StavyUkolu = Array.Empty<string>(),
            TypyUkolu = Array.Empty<string>(),
            Subsystemy = Array.Empty<SubsystemOptionViewModel>(),
            Subsystem = string.Empty,
            Vlastnici = Array.Empty<LookupOptionViewModel>(),
            DostupniVlastnici = Array.Empty<SpolupracovnikOptionViewModel>(),
            DostupniSpolupracovnici = Array.Empty<SpolupracovnikOptionViewModel>(),
            VybraniSpolupracovniciIds = Array.Empty<int>(),
            ExterniVazby = Array.Empty<ExterniOdkazEditViewModel>(),
            TypyExternichOdkazu = Array.Empty<string>(),
        };
        recordService
            .Setup(x => x.BuildZaznamEditAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var sut = new ProjectEditQuery(recordService.Object);

        var actual = await sut.GetEditModelAsync(42, CancellationToken.None);

        actual.Should().BeSameAs(expected);
        recordService.Verify(x => x.BuildZaznamEditAsync(42, It.IsAny<CancellationToken>()), Times.Once);
    }
}
