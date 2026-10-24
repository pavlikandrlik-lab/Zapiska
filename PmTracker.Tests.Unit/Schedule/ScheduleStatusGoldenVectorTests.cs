using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Schedules;
using Xunit;

namespace PmTracker.Tests.Unit.Schedule;

/// <summary>
/// Spec 2026-10-24 R11: golden-vector parita stavu harmonogramu. Server (BuildSouhrn + ScheduleStatusText,
/// tedy skutečná cesta šablon) a JS zrcadlo (schedule/scheduleStatus.js) musí nad TÝMIŽ případy
/// (tests/js/schedule/__fixtures__/status-cases.json) dát stejný vztah, čísla i texty.
/// </summary>
public sealed class ScheduleStatusGoldenVectorTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Nepodařilo se najít kořen repozitáře.");
    }

    private static JsonDocument LoadFixture()
    {
        var path = Path.Combine(RepoRoot(), "tests", "js", "schedule", "__fixtures__", "status-cases.json");
        File.Exists(path).Should().BeTrue($"sdílené případy musí existovat: {path}");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static DateTime? OptionalDate(JsonElement step, string property)
        => step.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? DateTime.Parse(value.GetString()!, CultureInfo.InvariantCulture)
            : null;

    private static DateTime Date(JsonElement element, string property)
        => DateTime.Parse(element.GetProperty(property).GetString()!, CultureInfo.InvariantCulture);

    [Fact]
    public void Fixture_step_names_match_HarmonogramKroky()
    {
        using var doc = LoadFixture();
        var names = doc.RootElement.GetProperty("kroky").EnumerateArray()
            .Select(k => (k.GetProperty("poradi").GetInt32(), k.GetProperty("nazev").GetString()))
            .ToList();

        names.Should().Equal(HarmonogramKroky.Vse.Select(k => (k.Poradi, (string?)k.Nazev)),
            "JS dostává názvy kroků z řádků editoru (krok.Nazev) — případy musí mluvit stejně");
    }

    [Fact]
    public void BuildSouhrn_and_texts_match_golden_vectors()
    {
        using var doc = LoadFixture();
        foreach (var c in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            var name = c.GetProperty("name").GetString();
            var input = c.GetProperty("input");
            var rows = input.GetProperty("steps").EnumerateArray().Select(s => new ZaznamHarmonogramKrokEntity
            {
                Poradi = (byte)s.GetProperty("poradi").GetInt32(),
                PlanDatum = OptionalDate(s, "plan"),
                SkutecnostDatum = OptionalDate(s, "skutecnost")
            }).ToList();

            var souhrn = HarmonogramDateBlokBuilder.BuildSouhrn(
                Date(input, "start"), rows, Date(input, "termin"), Date(input, "today"));

            var e = c.GetProperty("expected");
            souhrn.StavVztah.ToString().Should().Be(e.GetProperty("vztah").GetString(), $"případ {name}: vztah");
            var aktualni = e.GetProperty("aktualniKrokPoradi");
            souhrn.AktualniKrokPoradi.Should().Be(
                aktualni.ValueKind == JsonValueKind.Null ? (int?)null : aktualni.GetInt32(), $"případ {name}: aktuální krok");
            souhrn.PrekroceniDni.Should().Be(e.GetProperty("prekroceniDni").GetInt32(), $"případ {name}: překročení");
            // Nejdřív do proměnné: „x?.ToString().Should()“ by při null přeskočilo i samotnou kontrolu.
            // JsonElement.GetString() vrací pro JSON null hodnotu null — porovnání tak hlídá obě strany.
            var planoveDokonceni = souhrn.BaselineDokonceni?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            planoveDokonceni.Should().Be(e.GetProperty("planoveDokonceni").GetString(), $"případ {name}: plánové dokončení");
            souhrn.CelkoveTrvaniDni.Should().Be(e.GetProperty("celkoveTrvaniDni").GetInt32(), $"případ {name}: trvání");
            souhrn.Stihame.Should().Be(e.GetProperty("stihame").GetBoolean(), $"případ {name}: štítek");
            ScheduleStatusText.State(souhrn.StavVztah, souhrn.AktualniKrokNazev, souhrn.PrekroceniDni)
                .Should().Be(e.GetProperty("stavText").GetString(), $"případ {name}: text stavu");
            ScheduleStatusText.PlanCompletion(souhrn.BaselineDokonceni)
                .Should().Be(e.GetProperty("planDokonceniText").GetString(), $"případ {name}: plán dokončení");
            ScheduleStatusText.TotalDuration(souhrn.BaselineDokonceni, souhrn.CelkoveTrvaniDni)
                .Should().Be(e.GetProperty("trvaniText").GetString(), $"případ {name}: trvání text");
        }
    }
}
