namespace PmTracker.ServiceDesk.Contracts;

/// <summary>
/// Informační systém ze ServiceDesku. Používá se pro dropdown
/// nastavení IS na projektu a pro lookup hierarchie.
/// </summary>
public sealed record InformacniSystemDto(
    int Id,
    string Nazev,
    string Zkratka,
    bool JeAktivni,
    decimal? Limit,
    decimal? Cerpani);
