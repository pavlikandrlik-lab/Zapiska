using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Services.Search;

/// <summary>
/// Mapuje entity z PmTrackerDbContext na SearchDocument. Stateless — volá z SearchIndexer
/// po načtení entity (případně i s předpočítaným kontextem, např. projekt_id pro Vyjádření).
/// Konstanty entity_type jsou definované zde a sdílené napříč indexerem, ACL i deserializací.
/// </summary>
public static class EntityDocumentMapper
{
    public const string TypeProjekt = "projekt";
    public const string TypeZaznam = "zaznam";
    public const string TypeJednani = "jednani";
    public const string TypeOsoba = "osoba";
    public const string TypeSubsystem = "subsystem";
    public const string TypeVyjadreni = "vyjadreni";
    public const string TypeZaznamNavrh = "zaznam_navrh";

    public static SearchDocument MapProjekt(ProjektEntity p, DateTime updatedAt) => new()
    {
        EntityType = TypeProjekt,
        EntityId = p.Id.ToString(),
        ProjektId = p.Id,
        Title = string.IsNullOrWhiteSpace(p.CelyNazev) ? p.Zkratka : p.CelyNazev,
        Body = p.CelyNazev,
        Keywords = string.IsNullOrWhiteSpace(p.Zkratka) ? Array.Empty<string>() : new[] { p.Zkratka },
        UpdatedAt = updatedAt,
        Meta = new Dictionary<string, string?>
        {
            ["zkratka"] = p.Zkratka
        }
    };

    public static SearchDocument MapZaznam(ProjektovyZaznamEntity z, DateTime updatedAt) => new()
    {
        EntityType = TypeZaznam,
        EntityId = z.Id.ToString(),
        ProjektId = z.ProjektId,
        Title = z.Nazev,
        Body = string.Join('\n', new[] { z.Cil, z.Popis }.Where(s => !string.IsNullOrWhiteSpace(s))),
        Keywords = string.IsNullOrWhiteSpace(z.CisloViditelne) ? Array.Empty<string>() : new[] { z.CisloViditelne! },
        UpdatedAt = updatedAt,
        Meta = new Dictionary<string, string?>
        {
            ["cislo_viditelne"] = z.CisloViditelne,
            ["cislo_zaznamu"] = z.CisloZaznamu.ToString(),
            ["subsystem_id"] = z.SubsystemId.ToString()
        }
    };

    public static SearchDocument MapJednani(JednaniEntity j, DateTime updatedAt) => new()
    {
        EntityType = TypeJednani,
        EntityId = j.Id.ToString(),
        ProjektId = j.ProjektId,
        Title = $"Jednání #{j.CisloJednani}",
        Body = j.Misto ?? string.Empty,
        Keywords = new[] { j.CisloJednani.ToString() },
        UpdatedAt = updatedAt,
        Meta = new Dictionary<string, string?>
        {
            ["cislo_jednani"] = j.CisloJednani.ToString(),
            ["datum_planovane"] = j.DatumPlanovane.ToString("o")
        }
    };

    public static SearchDocument MapOsoba(OsobaEntity o, DateTime updatedAt)
    {
        var fullName = string.Join(' ', new[] { o.Titul, o.Jmeno, o.Prijmeni }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        var keywords = new List<string>();
        if (!string.IsNullOrWhiteSpace(o.AdLogin)) keywords.Add(o.AdLogin!);
        if (!string.IsNullOrWhiteSpace(o.Email)) keywords.Add(o.Email!);

        return new SearchDocument
        {
            EntityType = TypeOsoba,
            EntityId = o.Id.ToString(),
            ProjektId = null,
            Title = fullName,
            Body = o.Email ?? string.Empty,
            Keywords = keywords,
            UpdatedAt = updatedAt,
            Meta = new Dictionary<string, string?>
            {
                ["email"] = o.Email,
                ["ad_login"] = o.AdLogin
            }
        };
    }

    public static SearchDocument MapSubsystem(SubsystemEntity s, DateTime updatedAt) => new()
    {
        EntityType = TypeSubsystem,
        EntityId = s.Id.ToString(),
        ProjektId = null,
        Title = s.Nazev,
        Body = s.Nazev,
        Keywords = string.IsNullOrWhiteSpace(s.Kod) ? Array.Empty<string>() : new[] { s.Kod },
        UpdatedAt = updatedAt,
        Meta = new Dictionary<string, string?>
        {
            ["kod"] = s.Kod
        }
    };

    public static SearchDocument MapVyjadreni(VyjadreniEntity v, int projektId, DateTime updatedAt) => new()
    {
        EntityType = TypeVyjadreni,
        EntityId = v.Id.ToString(),
        ProjektId = projektId,
        Title = $"Vyjádření k záznamu #{v.ZaznamId}",
        Body = v.TextVyjadreni,
        Keywords = Array.Empty<string>(),
        UpdatedAt = updatedAt,
        Meta = new Dictionary<string, string?>
        {
            ["zaznam_id"] = v.ZaznamId.ToString(),
            ["jednani_id"] = v.JednaniId.ToString()
        }
    };

    public static SearchDocument MapZaznamNavrh(ZaznamNavrhEntity n, DateTime updatedAt) => new()
    {
        EntityType = TypeZaznamNavrh,
        EntityId = n.Id.ToString(),
        ProjektId = n.ProjektId,
        Title = $"Návrh záznamu ({n.TypNavrhu})",
        Body = n.PayloadJson,
        Keywords = new[] { n.TypNavrhu, n.Stav },
        UpdatedAt = updatedAt,
        Meta = new Dictionary<string, string?>
        {
            ["typ_navrhu"] = n.TypNavrhu,
            ["stav"] = n.Stav,
            ["zaznam_id"] = n.ZaznamId?.ToString()
        }
    };
}
