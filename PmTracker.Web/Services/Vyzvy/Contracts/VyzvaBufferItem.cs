namespace PmTracker.Web.Services.Vyzvy.Contracts;

public sealed record VyzvaBufferItem(
    int ExterniOdkazId,
    int ZaznamId,
    string Cislo,
    string? StrucneNazev,
    decimal? PredpokladanaCena);
