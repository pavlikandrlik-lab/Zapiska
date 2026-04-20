using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

public sealed class VyzvaStateMachineTests
{
    [Theory]
    [InlineData(VyzvaStav.Priprava, VyzvaStav.Odeslano)]
    [InlineData(VyzvaStav.Priprava, VyzvaStav.Zruseno)]
    [InlineData(VyzvaStav.Odeslano, VyzvaStav.Priprava)]
    [InlineData(VyzvaStav.Odeslano, VyzvaStav.Zruseno)]
    [InlineData(VyzvaStav.Zruseno, VyzvaStav.Priprava)]
    public void JePovoleny_PovoleneKombinace_True(VyzvaStav z, VyzvaStav na)
        => VyzvaStateMachine.JePovolenyPrechod(z, na).Should().BeTrue();

    [Theory]
    [InlineData(VyzvaStav.Priprava, VyzvaStav.Priprava)]
    [InlineData(VyzvaStav.Odeslano, VyzvaStav.Odeslano)]
    [InlineData(VyzvaStav.Zruseno, VyzvaStav.Odeslano)]
    [InlineData(VyzvaStav.Zruseno, VyzvaStav.Zruseno)]
    public void JePovoleny_NepovoleneKombinace_False(VyzvaStav z, VyzvaStav na)
        => VyzvaStateMachine.JePovolenyPrechod(z, na).Should().BeFalse();
}
