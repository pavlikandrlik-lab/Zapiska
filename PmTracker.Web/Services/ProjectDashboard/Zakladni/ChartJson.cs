using System.Text.Json;
using System.Text.Json.Serialization;

namespace PmTracker.Web.Services.ProjectDashboard.Zakladni;

/// <summary>
/// Serializuje <see cref="ChartData"/> do JSON pro klientský ECharts renderer
/// (<c>echartsRender.js</c>). <c>kind</c> jde jako string (čitelné v JS switchi),
/// klíče camelCase.
/// </summary>
public static class ChartJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(ChartData data) => JsonSerializer.Serialize(data, Options);
}
