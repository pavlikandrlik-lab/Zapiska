using FluentAssertions;
using PmTracker.Web.Services.ProjectDashboard.Zakladni;

namespace PmTracker.Tests.Unit.Dashboard.Zakladni;

/// <summary>
/// Stavové mapování na 4 kýble (spec rozhodnutí): Hotovo = IsFinal &amp;&amp; Kod != CANCEL;
/// Zrušeno = IsFinal &amp;&amp; Kod == CANCEL; Nezahájeno = Kod == NEW (nebo neznámý stav);
/// Rozpracováno = zbytek non-final. Pojmenované konstanty jen NEW/CANCEL.
/// </summary>
public sealed class ZakladniStatusMapperTests
{
    [Fact]
    public void Null_state_is_not_started()
        => ZakladniStatusMapper.Bucket(null).Should().Be(StatusBucket.Nezahajeno);

    [Fact]
    public void New_code_is_not_started()
        => ZakladniStatusMapper.Bucket(new DatasetState(1, "NEW", "Nový", IsFinal: false))
            .Should().Be(StatusBucket.Nezahajeno);

    [Fact]
    public void Non_final_other_is_in_progress()
        => ZakladniStatusMapper.Bucket(new DatasetState(2, "WIP", "Rozpracováno", IsFinal: false))
            .Should().Be(StatusBucket.Rozpracovano);

    [Fact]
    public void Final_non_cancel_is_done()
        => ZakladniStatusMapper.Bucket(new DatasetState(3, "DONE", "Hotovo", IsFinal: true))
            .Should().Be(StatusBucket.Hotovo);

    [Fact]
    public void Final_cancel_is_cancelled()
        => ZakladniStatusMapper.Bucket(new DatasetState(4, "CANCEL", "Zrušeno", IsFinal: true))
            .Should().Be(StatusBucket.Zruseno);

    [Fact]
    public void Cancel_code_is_case_insensitive()
        => ZakladniStatusMapper.Bucket(new DatasetState(5, "cancel", "Zrušeno", IsFinal: true))
            .Should().Be(StatusBucket.Zruseno);
}
