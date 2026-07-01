using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using PmTracker.Web.Services.Schedules;
using Xunit;

namespace PmTracker.Tests.Unit.Schedule;

/// <summary>
/// Golden-vector parita: C# <see cref="ScheduleBarLayoutCalculator"/> musí dávat shodné výsledky
/// jako JS dvojče (scheduleAxis.js) nad TÝMIŽ fixtures (tests/js/schedule/__fixtures__/axis-cases.json).
/// JS strana ověřuje stejné fixtures přes node:test. Tím je osa zamčená v obou implementacích.
/// </summary>
public sealed class ScheduleAxisGoldenVectorTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Nepodařilo se najít kořen repozitáře.");
    }

    [Fact]
    public void Compute_MatchesGoldenVectors()
    {
        var path = Path.Combine(RepoRoot(), "tests", "js", "schedule", "__fixtures__", "axis-cases.json");
        File.Exists(path).Should().BeTrue($"sdílené fixtures musí existovat: {path}");

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var c in doc.RootElement.EnumerateArray())
        {
            var name = c.GetProperty("name").GetString();
            var inp = c.GetProperty("input");
            var steps = inp.GetProperty("steps").EnumerateArray().Select(s => new ScheduleDateStepResult(
                s.GetProperty("poradi").GetInt32(),
                DateTime.Parse(s.GetProperty("planStart").GetString()!),
                DateTime.Parse(s.GetProperty("planEnd").GetString()!),
                s.GetProperty("maSkutecnost").GetBoolean(),
                DateTime.Parse(s.GetProperty("skutecnostStart").GetString()!),
                DateTime.Parse(s.GetProperty("skutecnostEnd").GetString()!),
                HarmonogramKrokStav.Ceka, false)).ToList();

            var layout = ScheduleBarLayoutCalculator.Compute(
                DateTime.Parse(inp.GetProperty("start").GetString()!),
                DateTime.Parse(inp.GetProperty("deadline").GetString()!),
                DateTime.Parse(inp.GetProperty("today").GetString()!),
                steps);

            var exp = c.GetProperty("expected");
            layout.AxisStart.Should().Be(DateTime.Parse(exp.GetProperty("axisStart").GetString()!), $"case {name}");
            layout.AxisEnd.Should().Be(DateTime.Parse(exp.GetProperty("axisEnd").GetString()!), $"case {name}");
            layout.TotalDays.Should().Be(exp.GetProperty("totalDays").GetInt32(), $"case {name}");

            if (exp.GetProperty("todayPct").ValueKind == JsonValueKind.Null)
                layout.TodayPct.Should().BeNull($"case {name}");
            else
                layout.TodayPct!.Value.Should().BeApproximately(exp.GetProperty("todayPct").GetDouble(), 0.01, $"case {name}");

            layout.DeadlinePct!.Value.Should().BeApproximately(exp.GetProperty("deadlinePct").GetDouble(), 0.01, $"case {name}");

            var expTicks = exp.GetProperty("monthTicks").EnumerateArray().ToList();
            layout.MonthTicks.Should().HaveCount(expTicks.Count, $"case {name} ticks count");
            for (var i = 0; i < expTicks.Count; i++)
            {
                layout.MonthTicks[i].LeftPct.Should().BeApproximately(expTicks[i].GetProperty("left").GetDouble(), 0.01, $"case {name} tick {i}");
                layout.MonthTicks[i].Label.Should().Be(expTicks[i].GetProperty("label").GetString(), $"case {name} tick {i}");
            }

            var expSegs = exp.GetProperty("segments").EnumerateArray().ToList();
            foreach (var es in expSegs)
            {
                var poradi = es.GetProperty("poradi").GetInt32();
                var seg = layout.Segments.Single(s => s.Poradi == poradi);
                seg.PlanLeftPct.Should().BeApproximately(es.GetProperty("planLeft").GetDouble(), 0.01, $"case {name} seg {poradi} planLeft");
                seg.PlanWidthPct.Should().BeApproximately(es.GetProperty("planWidth").GetDouble(), 0.01, $"case {name} seg {poradi} planWidth");
                seg.HasActual.Should().Be(es.GetProperty("hasActual").GetBoolean(), $"case {name} seg {poradi} hasActual");
                seg.ActualWidthPct.Should().BeApproximately(es.GetProperty("actualWidth").GetDouble(), 0.01, $"case {name} seg {poradi} actualWidth");
            }
        }
    }
}
