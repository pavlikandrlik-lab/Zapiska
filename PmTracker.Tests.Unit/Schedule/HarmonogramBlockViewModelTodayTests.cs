using System;
using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using Xunit;

namespace PmTracker.Tests.Unit.Schedule;

/// <summary>
/// Guard: HarmonogramBlockViewModel nese lokální „dnes" (sjednocený zdroj s osou + data-schedule-today).
/// Hlavní pokrytí lokálního data je v Api render testu; tady jen smoke na existenci a round-trip vlastnosti.
/// </summary>
public sealed class HarmonogramBlockViewModelTodayTests
{
    [Fact]
    public void Today_RoundTrips()
    {
        var vm = new HarmonogramBlockViewModel { Today = new DateTime(2026, 6, 23) };
        vm.Today.Should().Be(new DateTime(2026, 6, 23));
    }
}
