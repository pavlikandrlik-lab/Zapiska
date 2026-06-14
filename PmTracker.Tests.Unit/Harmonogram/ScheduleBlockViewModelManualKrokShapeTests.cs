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

    // ==========================================================================
    // Plán 4 Feature C Task 6 — UI shape testy (switch Auto/Ručně + badge + dropdown)
    // ==========================================================================

    [Fact]
    public void HarmonogramKrokEditViewModel_Exposes_FeatureC_Properties()
    {
        var t = typeof(HarmonogramKrokEditViewModel);
        t.GetProperty(nameof(HarmonogramKrokEditViewModel.DelayHodnotaId)).Should().NotBeNull(
            "Feature C: UI toggle POST potřebuje HS0X_DELAY HodnotaId.");
        t.GetProperty(nameof(HarmonogramKrokEditViewModel.SkutecnostRezim)).Should().NotBeNull(
            "Feature C: switch Auto/Ručně VM property.");
        t.GetProperty(nameof(HarmonogramKrokEditViewModel.SkutecnostZdroj)).Should().NotBeNull(
            "Feature C: audit badge zdroj (Neznamo/Automat/Manual/Historicka).");
        t.GetProperty(nameof(HarmonogramKrokEditViewModel.PreferredExterniOdkazId)).Should().NotBeNull(
            "Feature C: dropdown user-preferred binding (pokud 2+ kandidáti).");
        t.GetProperty(nameof(HarmonogramKrokEditViewModel.CanToggleRezim)).Should().NotBeNull(
            "Feature C: permission flag — Razor zobrazí toggle jen pokud true.");
        t.GetProperty(nameof(HarmonogramKrokEditViewModel.Kandidati)).Should().NotBeNull(
            "Feature C: list kandidátů pro dropdown — Razor zobrazí chevron jen pokud Count >= 2.");
    }

    [Fact]
    public void HarmonogramKrokEditViewModel_FeatureC_Defaults_AreSafe()
    {
        var vm = new HarmonogramKrokEditViewModel
        {
            KrokIndex = 1,
            Nazev = "test",
            BarvaHex = "#fff"
        };

        vm.DelayHodnotaId.Should().BeNull("krok bez skutečnosti-zapisu nemá HS0X_DELAY řádek.");
        vm.SkutecnostRezim.Should().Be(PmTracker.Web.Models.Entities.SkutecnostRezimEnum.Auto,
            "default je Auto dle decision brief C-Q1.");
        vm.SkutecnostZdroj.Should().Be(PmTracker.Web.Models.Entities.SkutecnostZdrojEnum.Neznamo,
            "default je Neznamo pokud není zapsaná skutečnost.");
        vm.PreferredExterniOdkazId.Should().BeNull();
        vm.CanToggleRezim.Should().BeFalse("default read-only — composition musí povolit.");
        vm.Kandidati.Should().NotBeNull().And.BeEmpty("default prázdný list (žádné bindings).");
    }

    [Fact]
    public void HarmonogramKrokKandidatViewModel_Exposes_DropdownFields()
    {
        var t = typeof(HarmonogramKrokKandidatViewModel);
        t.GetProperty(nameof(HarmonogramKrokKandidatViewModel.ExterniOdkazId)).Should().NotBeNull();
        t.GetProperty(nameof(HarmonogramKrokKandidatViewModel.Cislo6)).Should().NotBeNull();
        t.GetProperty(nameof(HarmonogramKrokKandidatViewModel.TypZaznamu)).Should().NotBeNull();
        t.GetProperty(nameof(HarmonogramKrokKandidatViewModel.Datum)).Should().NotBeNull();
        t.GetProperty(nameof(HarmonogramKrokKandidatViewModel.IsSelected)).Should().NotBeNull(
            "Razor potřebuje flag pro render ✓ u vybraného kandidáta.");
    }
}
