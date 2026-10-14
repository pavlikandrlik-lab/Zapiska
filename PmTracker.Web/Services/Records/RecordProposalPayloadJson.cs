using System.Text.Json;
using System.Text.Json.Nodes;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Records;

/// <summary>
/// Spec 2026-10-14 R4 — jediné místo, kde se čte JSON návrhu. Návrhy uložené před sloučením
/// skutečnosti do jednoho seznamu nesou ruční data v <c>ManualActualKroky</c>; převedou se na
/// <see cref="SchedulePlanProposalPayload.ActualHarmonogramHodnoty"/> (seznam změn). Prázdná
/// položka znamenala „krok nenastal“, ne smazání, proto se nepřevádí. Díky převodu se
/// rozpracované návrhy nemusí mazat a historie zůstane čitelná.
/// </summary>
public static class RecordProposalPayloadJson
{
    private const string LegacyManualKey = "ManualActualKroky";

    public static RecordProposalPayload Deserialize(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return new RecordProposalPayload();
        }

        var root = JsonNode.Parse(payloadJson);
        if (root is JsonObject obj)
        {
            if (obj["SchedulePlan"] is JsonObject plan)
            {
                PrevedStaraRucniData(plan);
            }
            // Návrh založení skutečnost nenese (HideActual) — starý klíč jen zahodit.
            (obj["CreateRecord"] as JsonObject)?.Remove(LegacyManualKey);
        }

        return root?.Deserialize<RecordProposalPayload>() ?? new RecordProposalPayload();
    }

    private static void PrevedStaraRucniData(JsonObject plan)
    {
        if (plan[LegacyManualKey] is not JsonArray legacy)
        {
            return;
        }
        plan.Remove(LegacyManualKey);

        if (plan["ActualHarmonogramHodnoty"] is not JsonArray actual)
        {
            actual = new JsonArray();
            plan["ActualHarmonogramHodnoty"] = actual;
        }

        var obsazene = actual.OfType<JsonObject>()
            .Select(a => a["Poradi"]?.GetValue<int>())
            .OfType<int>()
            .ToHashSet();
        foreach (var item in legacy.OfType<JsonObject>())
        {
            var poradi = item["Poradi"]?.GetValue<int>();
            var datum = item["AbsolutniDatum"]?.GetValue<string>();
            if (poradi is null || string.IsNullOrWhiteSpace(datum) || !obsazene.Add(poradi.Value))
            {
                continue;
            }
            actual.Add(new JsonObject
            {
                ["Poradi"] = poradi.Value,
                ["SkutecnostDatum"] = datum + "T00:00:00"
            });
        }
    }
}
