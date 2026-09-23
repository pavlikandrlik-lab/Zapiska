using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Export;

/// <summary>
/// Názvy stahovaných tiskových PDF. Spec 2026-09-04-serverove-pdf-tisk-design.md, §6.5.
/// </summary>
public static class PdfExportFileName
{
    // Vlastní seznam místo Path.GetInvalidFileNameChars() — ten vrací na Windows a na
    // Unixu různé sady, takže by se testy chovaly jinak na vývojovém Macu než na serveru.
    private static readonly char[] Invalid = ['/', '\\', ':', '*', '?', '"', '<', '>', '|', ' '];

    public static string Build(PdfExportTemplateViewModel model, DateTime localNow)
    {
        var scope = model.NormalizedVariant switch
        {
            "meeting" => $"jednani-{model.JednaniCislo}",
            "task_single" => $"zaznam-{Sanitize(model.Zaznamy.FirstOrDefault()?.CisloViditelne, "0")}",
            _ => "projekt"
        };

        return $"Zapiska_{Sanitize(model.ProjektZkratka, "Projekt")}_{scope}_{localNow:yyyy-MM-dd}.pdf";
    }

    private static string Sanitize(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return string.Concat(value.Trim().Select(ch => Invalid.Contains(ch) ? '_' : ch));
    }
}
