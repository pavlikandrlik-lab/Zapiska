namespace PmTracker.Web.Models.ViewModels.Vyzvy;

/// <summary>
/// Výzva pro tisk (spec 2026-09-07 §9). Word i PDF staví z tohoto modelu, aby se obsah
/// obou formátů nemohl rozejít. Ceny jsou bez DPH tak, jak přicházejí z HOT_KALKULACE;
/// DPH dopočítává projekce sazbou DphSazba.
/// </summary>
public sealed class VyzvaExportViewModel
{
    public const decimal DphSazba = 0.21m;

    public int VyzvaId { get; init; }
    public int ProjektId { get; init; }
    public required string KodVyzvy { get; init; }
    public int PoradoveVRoce { get; init; }
    public int Rok { get; init; }
    public required string CisloRamcoveSmlouvy { get; init; }
    public required string MistoPlneni { get; init; }

    /// <summary>Zkratka IS z místa plnění — „FIS (EIS): VZ 8201" dá „FIS".</summary>
    public required string InformacniSystem { get; init; }

    public IReadOnlyList<VyzvaExportPozadavekViewModel> Pozadavky { get; init; }
        = Array.Empty<VyzvaExportPozadavekViewModel>();

    public decimal CelkemBezDph { get; init; }
    public decimal CelkemDph { get; init; }
    public decimal CelkemSDph { get; init; }
    public decimal LicenceBezDph { get; init; }
    public decimal LicenceDph { get; init; }
    public decimal LicenceSDph { get; init; }
}

public sealed class VyzvaExportPozadavekViewModel
{
    /// <summary>Poř. č. římsky s tečkou: „I.", „II.", … (komentář autora vzoru 2026-09-10).</summary>
    public required string PoradoveOznaceni { get; init; }
    public int ZaznamId { get; init; }
    /// <summary>Č. úkolu VP s prefixem „RU", např. „RU867-5". Null bez čísla.</summary>
    public string? CisloUkoluVp { get; init; }
    public string? Nazev { get; init; }
    public required string CisloHtl { get; init; }
    /// <summary>
    /// Text požadavku ze vstupu pracovníka — sanitizované HTML (spec 2026-09-08 §5.7).
    /// Do 2026-09-08 se sem dával popis tiketu z HOT_ZAZNAMY; ten se už nepoužívá,
    /// ani jako náhrada za prázdnou hodnotu.
    /// </summary>
    public string? PozadavekHtml { get; init; }

    /// <summary>Nikdy null — bez dat se tiskne prázdná tabulka se čtyřmi řádky.</summary>
    public required VyzvaExportKalkulaceViewModel Kalkulace { get; init; }
}

public sealed class VyzvaExportKalkulaceViewModel
{
    public IReadOnlyList<VyzvaExportKalkulaceRadekViewModel> Radky { get; init; }
        = Array.Empty<VyzvaExportKalkulaceRadekViewModel>();
    public decimal CelkemBezDph { get; init; }
    public decimal CelkemDph { get; init; }
    public decimal CelkemSDph { get; init; }

    /// <summary>Součet cena_a až cena_i je kladný — tiskne se tabulka činností (spec B5).</summary>
    public bool MaCinnosti { get; init; }

    /// <summary>Kalkulace má licenci (cena_l &gt; 0) — tiskne se tabulka licencí (spec B5).</summary>
    public bool MaLicenci { get; init; }

    /// <summary>Řádky tabulky licencí z rozpad_licence; bez licence prázdné.</summary>
    public IReadOnlyList<VyzvaExportLicenceRadekViewModel> LicenceRadky { get; init; }
        = Array.Empty<VyzvaExportLicenceRadekViewModel>();

    public decimal? PocetLicenci { get; init; }
    public decimal? SazbaLicence { get; init; }
    /// <summary>Součet řádků licence bez DPH; null bez licence.</summary>
    public decimal? CenaLicence { get; init; }
}

public sealed class VyzvaExportKalkulaceRadekViewModel
{
    public required string Kod { get; init; }      // A / B / C / D
    public required string Nazev { get; init; }    // Analýza / Programové úpravy / Testování / Implementace
    public decimal? Rozsah { get; init; }          // hodiny
    public decimal? Sazba { get; init; }           // Kč/hod
    public decimal? CenaBezDph { get; init; }
    public decimal? CenaDph { get; init; }
    public decimal? CenaSDph { get; init; }
}

/// <summary>Řádek tabulky licencí. POL a PPOL model nenese — tisknou se vždy prázdné (spec B5).</summary>
public sealed class VyzvaExportLicenceRadekViewModel
{
    public int Kod { get; init; }                  // 1, 2, …
    public required string Nazev { get; init; }
    public decimal CenaBezDph { get; init; }
    public decimal CenaDph { get; init; }
    public decimal CenaSDph { get; init; }
}
