using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Records;
using Xunit;

namespace PmTracker.Tests.Unit.Records;

/// <summary>
/// Schválení návrhu založení staví příkaz uložení z payloadu. Text požadavku a zařazení do
/// bufferu se musí přenést — dřív se ztrácely (spec 2026-09-10 §0.3).
/// </summary>
public sealed class RecordProposalPayloadMapperVazbyTests
{
    [Fact]
    public void BuildSaveCommand_PrenasiTextPozadavkuAZarazeniDoBufferu()
    {
        var command = new RecordProposalPayloadMapper().BuildSaveCommand(new CreateRecordProposalPayload
        {
            ExterniVazby =
            [
                new SaveRecordExterniVazbaCommand
                {
                    Typ = "PNF",
                    Cislo = "336865",
                    Pozadavek = "<p>Chceme sestavu.</p>",
                    ZaradidDoVyzvy = true,
                },
            ],
        });

        var vazba = command.ExterniVazby.Should().ContainSingle().Which;
        vazba.Pozadavek.Should().Be("<p>Chceme sestavu.</p>", "text napsaný v návrhu se nesmí ztratit");
        vazba.ZaradidDoVyzvy.Should().BeTrue("zapnutý přepínač v návrhu platí i po schválení");
    }

    /// <summary>
    /// Druhá polovina téže cesty: návrh se ukládá jako payload. Když text a přepínač vypadnou
    /// už tady, oprava BuildSaveCommand nic nezachrání (sourozenec §0.3).
    /// </summary>
    [Fact]
    public void BuildCreatePayload_PrenasiTextPozadavkuAZarazeniDoBufferu()
    {
        var payload = new RecordProposalPayloadMapper().BuildCreatePayload(new SaveRecordCommand
        {
            ExterniVazby =
            [
                new SaveRecordExterniVazbaCommand
                {
                    Typ = "PNF",
                    Cislo = "336865",
                    Pozadavek = "<p>Chceme sestavu.</p>",
                    ZaradidDoVyzvy = true,
                },
            ],
        });

        var vazba = payload.CreateRecord!.ExterniVazby.Should().ContainSingle().Which;
        vazba.Pozadavek.Should().Be("<p>Chceme sestavu.</p>", "text napsaný v návrhu se musí uložit do payloadu");
        vazba.ZaradidDoVyzvy.Should().BeTrue("zapnutý přepínač se musí uložit do payloadu");
    }
}
