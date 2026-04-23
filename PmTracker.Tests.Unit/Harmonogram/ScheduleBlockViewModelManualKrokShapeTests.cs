using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Records;

namespace PmTracker.Tests.Unit.Harmonogram;

/// <summary>
/// Plán D Task 8 (RED): schema contract pro UI vrstvu.
/// _ScheduleBlock.cshtml musí umět rozlišit tři zdroje skutečnosti
/// (FromVyjadreni / Manual / None) — VM je zdrojem pravdy.
/// </summary>
public sealed class ScheduleBlockViewModelManualKrokShapeTests
{
    [Fact]
    public void HarmonogramKrokEditViewModel_Exposes_KrokKey()
    {
        typeof(HarmonogramKrokEditViewModel)
            .GetProperty(nameof(HarmonogramKrokEditViewModel.KrokKey))
            .Should().NotBeNull(
                "Plán D Task 8: UI musí umět navázat manual krok na GUID KrokKey z CiselnikHarmonogramTypu.");
    }

    [Fact]
    public void HarmonogramKrokEditViewModel_Exposes_ZdrojSkutecnosti()
    {
        typeof(HarmonogramKrokEditViewModel)
            .GetProperty(nameof(HarmonogramKrokEditViewModel.ZdrojSkutecnosti))
            .Should().NotBeNull(
                "Plán D Task 8: UI musí rozlišit tři zdroje skutečnosti (None/FromVyjadreni/Manual).");
    }

    [Fact]
    public void HarmonogramKrokEditViewModel_Exposes_SourceVyjadreniId()
    {
        typeof(HarmonogramKrokEditViewModel)
            .GetProperty(nameof(HarmonogramKrokEditViewModel.SourceVyjadreniId))
            .Should().NotBeNull(
                "Plán D Task 8: u FromVyjadreni musí UI zobrazit odkaz na chat modal přes HOT_VYJADRENI id.");
    }

    [Fact]
    public void HarmonogramKrokEditViewModel_Exposes_SourceVyjadreniDatum()
    {
        typeof(HarmonogramKrokEditViewModel)
            .GetProperty(nameof(HarmonogramKrokEditViewModel.SourceVyjadreniDatum))
            .Should().NotBeNull(
                "Plán D Task 8: tooltip 'Z vyjádření {datum}' potřebuje DatumVyjadreni z vazby.");
    }

    [Fact]
    public void HarmonogramKrokEditViewModel_Exposes_SourceExterniOdkazId()
    {
        typeof(HarmonogramKrokEditViewModel)
            .GetProperty(nameof(HarmonogramKrokEditViewModel.SourceExterniOdkazId))
            .Should().NotBeNull(
                "Plán D Task 8: klik na chat ikonu routuje přes data-external-odkaz-id, které získáme z ExterniOdkazId vazby.");
    }

    [Fact]
    public void HarmonogramKrokEditViewModel_Exposes_IsManualKrok()
    {
        typeof(HarmonogramKrokEditViewModel)
            .GetProperty(nameof(HarmonogramKrokEditViewModel.IsManualKrok))
            .Should().NotBeNull(
                "Plán D Task 8: UI potřebuje přímo vědět, zda krok patří do HarmonogramManualSteps {2,5,8,9}.");
    }

    [Fact]
    public void HarmonogramBlockViewModel_Exposes_LockedManualKrokKeys()
    {
        typeof(HarmonogramBlockViewModel)
            .GetProperty(nameof(HarmonogramBlockViewModel.LockedManualKrokKeys))
            .Should().NotBeNull(
                "Plán D Task 8: UI musí respektovat pending návrh z PendingScheduleProposalLockState.LockedManualKrokKeys.");
    }

    [Fact]
    public void HarmonogramBlockViewModel_Exposes_CanEditManualActual()
    {
        typeof(HarmonogramBlockViewModel)
            .GetProperty(nameof(HarmonogramBlockViewModel.CanEditManualActual))
            .Should().NotBeNull(
                "Plán D Task 8: UI renderuje input jen pro records.edit a když schedule není locked.");
    }

    [Fact]
    public void HarmonogramKrokEditViewModel_KrokKey_Default_Is_EmptyGuid()
    {
        var vm = new HarmonogramKrokEditViewModel
        {
            KrokIndex = 1,
            Nazev = "test",
            BarvaHex = "#fff"
        };

        vm.KrokKey.Should().Be(Guid.Empty, "default hodnota musí být Guid.Empty kvůli back-compat při existujících call-sitech.");
        vm.ZdrojSkutecnosti.Should().Be(ZdrojSkutecnosti.None);
        vm.IsManualKrok.Should().BeFalse();
    }

    [Fact]
    public void HarmonogramBlockViewModel_LockedManualKrokKeys_Default_Is_Empty()
    {
        var vm = new HarmonogramBlockViewModel();

        vm.LockedManualKrokKeys.Should().NotBeNull();
        vm.LockedManualKrokKeys.Should().BeEmpty();
        vm.CanEditManualActual.Should().BeFalse("default je read-only; composition musí explicitně nastavit true.");
    }
}
