namespace PmTracker.Web.Services.Export;

/// <summary>
/// Nastavení serverového generování PDF (sekce <c>Export:Pdf</c>).
/// Spec 2026-09-04-serverove-pdf-tisk-design.md, §7.
/// </summary>
public sealed class PdfExportOptions
{
    public const string SectionName = "Export:Pdf";

    /// <summary>Vypínač bez nasazení nové verze — false vrátí tisk na dnešní HTML cestu.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Cesta k msedge.exe. Prázdné = autodetekce ve známých cestách.</summary>
    public string? BrowserExecutablePath { get; set; }

    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>Strop souběžných tisků — jedna instance prohlížeče ≈ 200 MB.</summary>
    public int MaxConcurrent { get; set; } = 2;
}
