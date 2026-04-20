using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Services.Vyzvy.Contracts;

public sealed record VyzvaDetail(
    int Id,
    int ProjektId,
    string Kod,
    int Rok,
    int PoradoveVRoce,
    VyzvaStav Stav,
    DateTime DatumZalozeni,
    int ZalozilOsobaId,
    DateTime? DatumOdeslani,
    int? OdeslalOsobaId,
    string MistoPlneniSnapshot,
    string CisloRamcoveSmlouvySnapshot,
    IReadOnlyList<VyzvaDetailItem> Polozky);

public sealed record VyzvaDetailItem(
    int ExterniOdkazId,
    int ZaznamId,
    string Cislo,
    string? StrucneNazev,
    decimal? PredpokladanaCena);
