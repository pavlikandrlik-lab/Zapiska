using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Services.Audit;

internal sealed record ProjectAuditSnapshot(
    int Id,
    string Zkratka,
    string CelyNazev,
    int StavId,
    bool PouzivatIdentJednani,
    int? ServiceDeskInfoSystemId)
{
    public static ProjectAuditSnapshot FromEntity(ProjektEntity entity) => new(
        entity.Id,
        entity.Zkratka,
        entity.CelyNazev,
        entity.StavId,
        entity.PouzivatIdentJednani,
        entity.ServiceDeskInfoSystemId);
}

internal sealed record RecordAuditSnapshot(
    int Id,
    int ProjektId,
    int KategorieId,
    int? AktualniTypUkoluId,
    int? StavUkoluId,
    int CisloZaznamu,
    string? CisloViditelne,
    byte CisloViditelneTyp,
    int CisloViditelneA,
    int CisloViditelneB,
    int? CisloJednaniZdrojId,
    string Nazev,
    string? Cil,
    string? Popis,
    int VlastnikId,
    DateTime DatumZalozeni,
    DateTime DatumUkonceni,
    int SubsystemId,
    int HarmonogramSablonaVerze)
{
    public static RecordAuditSnapshot FromEntity(ProjektovyZaznamEntity entity) => new(
        entity.Id,
        entity.ProjektId,
        entity.KategorieId,
        entity.AktualniTypUkoluId,
        entity.StavUkoluId,
        entity.CisloZaznamu,
        entity.CisloViditelne,
        entity.CisloViditelneTyp,
        entity.CisloViditelneA,
        entity.CisloViditelneB,
        entity.CisloJednaniZdrojId,
        entity.Nazev,
        entity.Cil,
        entity.Popis,
        entity.VlastnikId,
        entity.DatumZalozeni,
        entity.DatumUkonceni,
        entity.SubsystemId,
        entity.HarmonogramSablonaVerze);
}

internal sealed record RecordScheduleValueAuditSnapshot(
    int TypId,
    int? HodnotaInt,  // DESIGN-10-A: nullable napříč auditem (NULL = krok nenastal)
    DateTime UpdatedAt);

internal sealed record RecordScheduleAuditSnapshot(
    int RecordId,
    IReadOnlyList<RecordScheduleValueAuditSnapshot> Values)
{
    public static RecordScheduleAuditSnapshot FromEntities(int recordId, IEnumerable<ZaznamHarmonogramHodnotaEntity> values) => new(
        recordId,
        values
            .OrderBy(item => item.TypId)
            .ThenBy(item => item.Id)
            .Select(item => new RecordScheduleValueAuditSnapshot(
                item.TypId,
                item.HodnotaInt,
                item.UpdatedAt))
            .ToList());
}

internal sealed record CommentAuditSnapshot(
    int Id,
    int ZaznamId,
    int JednaniId,
    int AutorOsobaId,
    string TextVyjadreni,
    DateTime DatumVyjadreni)
{
    public static CommentAuditSnapshot FromEntity(VyjadreniEntity entity) => new(
        entity.Id,
        entity.ZaznamId,
        entity.JednaniId,
        entity.AutorOsobaId,
        entity.TextVyjadreni,
        entity.DatumVyjadreni);
}

internal sealed record MeetingAuditSnapshot(
    int Id,
    int ProjektId,
    int CisloJednani,
    DateTime DatumPlanovane,
    TimeOnly CasZacatek,
    string? Misto,
    int StavJednaniId,
    int? UzamklOsobaId)
{
    public static MeetingAuditSnapshot FromEntity(JednaniEntity entity) => new(
        entity.Id,
        entity.ProjektId,
        entity.CisloJednani,
        entity.DatumPlanovane,
        entity.CasZacatek,
        entity.Misto,
        entity.StavJednaniId,
        entity.UzamklOsobaId);
}

internal sealed record AttendanceAuditSnapshot(
    int JednaniId,
    int OsobaId,
    int StavUcastiId)
{
    public static AttendanceAuditSnapshot FromEntity(UcastEntity entity) => new(
        entity.JednaniId,
        entity.OsobaId,
        entity.StavUcastiId);
}

internal sealed record ProjectMembershipAuditSnapshot(
    int Id,
    int ProjektId,
    int OsobaId,
    int RoleId,
    DateTime DatumPrirazeni,
    DateTime? DatumOdebrani)
{
    public static ProjectMembershipAuditSnapshot FromEntity(ObsazeniProjektuEntity entity) => new(
        entity.Id,
        entity.ProjektId,
        entity.OsobaId,
        entity.RoleId,
        entity.DatumPrirazeni,
        entity.DatumOdebrani);
}

internal sealed record OrderedProjectSubsystemAuditSnapshot(
    int Id,
    int SubsystemId,
    int Poradi);

internal sealed record ProjectSubsystemAuditSnapshot(
    int Id,
    int ProjektId,
    int SubsystemId,
    int Poradi,
    DateTime DatumPrirazeni,
    DateTime? DatumOdebrani,
    IReadOnlyList<OrderedProjectSubsystemAuditSnapshot>? AktivniPoradiProjektu = null)
{
    public static ProjectSubsystemAuditSnapshot FromEntity(
        ProjektSubsystemEntity entity,
        IEnumerable<ProjektSubsystemEntity>? orderedProjectSubsystems = null) => new(
        entity.Id,
        entity.ProjektId,
        entity.SubsystemId,
        entity.Poradi,
        entity.DatumPrirazeni,
        entity.DatumOdebrani,
        orderedProjectSubsystems?
            .OrderBy(item => item.Poradi)
            .ThenBy(item => item.Id)
            .Select(item => new OrderedProjectSubsystemAuditSnapshot(item.Id, item.SubsystemId, item.Poradi))
            .ToList());
}

internal sealed record ProjectSubsystemRoleAuditSnapshot(
    int Id,
    int ProjektSubsystemId,
    int OsobaId,
    int RoleSubsystemuId,
    DateTime DatumPrirazeni,
    DateTime? DatumOdebrani)
{
    public static ProjectSubsystemRoleAuditSnapshot FromEntity(ObsazeniSubsystemuProjektuEntity entity) => new(
        entity.Id,
        entity.ProjektSubsystemId,
        entity.OsobaId,
        entity.RoleSubsystemuId,
        entity.DatumPrirazeni,
        entity.DatumOdebrani);
}

internal sealed record PersonAuditSnapshot(
    int Id,
    string Jmeno,
    string Prijmeni,
    string? Titul,
    string? Email,
    string? AdLogin,
    int? OrganizacniCelekId,
    int OrganizaceId,
    Guid? GuidAd,
    bool LocationLocked)
{
    public static PersonAuditSnapshot FromEntity(OsobaEntity entity) => new(
        entity.Id,
        entity.Jmeno,
        entity.Prijmeni,
        entity.Titul,
        entity.Email,
        entity.AdLogin,
        entity.OrganizacniCelekId,
        entity.OrganizaceId,
        entity.GuidAd,
        entity.LocationLocked);
}

internal sealed record ProposalAuditSnapshot(
    int Id,
    int ProjektId,
    int? ZaznamId,
    int SubsystemId,
    string TypNavrhu,
    string Stav,
    string PayloadJson,
    int CreatedByOsobaId,
    DateTime CreatedAt,
    int? DecidedByOsobaId,
    DateTime? DecidedAt,
    int? ApprovedRecordId,
    string? DecisionMode = null)
{
    public static ProposalAuditSnapshot FromEntity(ZaznamNavrhEntity entity, string? decisionMode = null) => new(
        entity.Id,
        entity.ProjektId,
        entity.ZaznamId,
        entity.SubsystemId,
        entity.TypNavrhu,
        entity.Stav,
        entity.PayloadJson,
        entity.CreatedByOsobaId,
        entity.CreatedAt,
        entity.DecidedByOsobaId,
        entity.DecidedAt,
        entity.ApprovedRecordId,
        decisionMode);
}

internal sealed record DictionaryAuditSnapshot(
    string DictionaryKey,
    int Id,
    string Kod,
    string Nazev,
    bool IsLocked,
    bool? IsActive = null,
    string? ExtraValue = null);

internal sealed record AuthzUserRoleAuditSnapshot(
    int Id,
    int OsobaId,
    int RoleId,
    bool IsActive,
    DateTime CreatedAt)
{
    public static AuthzUserRoleAuditSnapshot FromEntity(AuthzUserRoleEntity entity) => new(
        entity.Id,
        entity.OsobaId,
        entity.RoleId,
        entity.IsActive,
        entity.CreatedAt);
}

internal sealed record AuthzRoleAuditSnapshot(
    int Id,
    string Kod,
    string Nazev,
    string? Popis,
    bool IsSystem,
    bool IsActive)
{
    public static AuthzRoleAuditSnapshot FromEntity(AuthzRoleEntity entity) => new(
        entity.Id,
        entity.Kod,
        entity.Nazev,
        entity.Popis,
        entity.IsSystem,
        entity.IsActive);
}

internal sealed record AuthzPermissionAuditSnapshot(
    int Id,
    string Klic,
    string Nazev,
    int CategoryId,
    string ScopeLevel,
    bool IsActive,
    bool IsSystem)
{
    public static AuthzPermissionAuditSnapshot FromEntity(AuthzPermissionEntity entity) => new(
        entity.Id,
        entity.Klic,
        entity.Nazev,
        entity.CategoryId,
        entity.ScopeLevel.ToString().ToUpperInvariant(),
        entity.IsActive,
        entity.IsSystem);
}

internal sealed record AuthzRolePermissionAuditSnapshot(
    int Id,
    int RoleId,
    int PermissionId,
    string ScopeMode,
    bool IsAllowed,
    IReadOnlyList<int> ProjektIds)
{
    public static AuthzRolePermissionAuditSnapshot Create(
        AuthzRolePermissionEntity entity,
        IEnumerable<int> projectIds) => new(
        entity.Id,
        entity.RoleId,
        entity.PermissionId,
        entity.ScopeMode.ToString().ToUpperInvariant(),
        entity.IsAllowed,
        projectIds.OrderBy(item => item).ToList());
}
