using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Data;

public interface IRecordWriteCommandsComposition
{
    int GetNextCisloZaznamuTransactional(int projektId);

    int AllocateMeetingOrderTransactional(int projektId, int cisloJednani);

    IReadOnlyList<SpolupracovnikOptionViewModel> BuildRecordOwnerCandidates(int projectId, int? selectedOwnerId);

    int EnsurePersistedActiveHarmonogramSchemaVersion();

    IReadOnlyList<RecordScheduleTypeDefinition> ResolveScheduleTypeDefinitionsForRecord(ProjektovyZaznamEntity record);

    IReadOnlyList<RecordScheduleTypeDefinition> ResolveScheduleTypeDefinitionsForSchemaVersion(int schemaVersion);

    IReadOnlyList<int> ResolveLeadEquivalentOsobaIds(int projectId, int subsystemId);
}
