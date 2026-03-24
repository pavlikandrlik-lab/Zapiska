using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Data;

public interface IHarmonogramService : IDictionariesQueriesComposition, IDictionariesCommandsComposition
{
    Task<HarmonogramSchemaDefinition> GetActiveHarmonogramSchemaAsync(CancellationToken ct = default);
    Task<HarmonogramSchemaDefinition> GetSchemaForRecordAsync(ProjektovyZaznamEntity record, CancellationToken ct = default);
    Task<HarmonogramSchemaDefinition> GetSchemaForRecordAsync(int schemaVersion, CancellationToken ct = default);
    IReadOnlyList<RecordScheduleTypeDefinition> BuildRecordScheduleTypeDefinitions(HarmonogramSchemaDefinition schema);
    IReadOnlyList<HarmonogramVypocetKroku> BuildHarmonogramVypocetPublic(DateTime datumZalozeni, IReadOnlyList<HarmonogramTypPar> typy, IReadOnlyDictionary<int, int>? hodnoty);
    HarmonogramSouhrnViewModel BuildHarmonogramSouhrn(IReadOnlyList<HarmonogramVypocetKroku> kroky, DateTime terminUkolu);
    Task<int> EnsurePersistedActiveHarmonogramSchemaVersionAsync(CancellationToken ct = default);
    new Task<CiselnikDetailViewModel> BuildHarmonogramKrokyCiselnikDetailAsync(string key, bool canChangeLockState, CancellationToken ct = default);
    new Task<int> CountHarmonogramCatalogRowsAsync(CancellationToken ct = default);
    new Task<CiselnikDetailViewModel> BuildCiselnikDetailAsync(string id, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    new Task SaveHarmonogramStepRowAsync(SaveCiselnikRowCommand command, CancellationToken ct = default);
    new Task DeleteHarmonogramStepRowAsync(DeleteCiselnikRowCommand command, CancellationToken ct = default);
}
