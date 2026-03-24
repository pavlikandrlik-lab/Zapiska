using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Data;

public interface IRecordWriteCommandsComposition
{
    Task<int> GetNextCisloZaznamuTransactionalAsync(int projektId, CancellationToken ct = default);

    Task<int> AllocateMeetingOrderTransactionalAsync(int projektId, int cisloJednani, CancellationToken ct = default);

    Task<IReadOnlyList<SpolupracovnikOptionViewModel>> BuildRecordOwnerCandidatesAsync(int projectId, int? selectedOwnerId, CancellationToken ct = default);

    Task<int> EnsurePersistedActiveHarmonogramSchemaVersionAsync(CancellationToken ct = default);

    Task<IReadOnlyList<RecordScheduleTypeDefinition>> ResolveScheduleTypeDefinitionsForRecordAsync(ProjektovyZaznamEntity record, CancellationToken ct = default);

    Task<IReadOnlyList<RecordScheduleTypeDefinition>> ResolveScheduleTypeDefinitionsForSchemaVersionAsync(int schemaVersion, CancellationToken ct = default);

    Task<IReadOnlyList<int>> ResolveLeadEquivalentOsobaIdsAsync(int projectId, int subsystemId, CancellationToken ct = default);
}
