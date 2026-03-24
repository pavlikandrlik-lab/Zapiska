using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Data;

public interface IRecordEditorQueriesComposition
{
    Task<ZaznamEditViewModel> BuildZaznamEditForEntityAsync(
        ProjektovyZaznamEntity record,
        bool isCreate,
        int? forceMeetingIdForNumber = null,
        bool? projectUsesMeetingIdentifier = null,
        IReadOnlyList<JednaniOptionViewModel>? openMeetingOptions = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<ProjectSubsystemViewModel>> BuildActiveProjectSubsystemsAsync(int projectId, CancellationToken ct = default);

    Task<IReadOnlyList<JednaniListItemViewModel>> BuildJednaniListAsync(int projectId, CancellationToken ct = default);

    Task<IReadOnlyList<JednaniOptionViewModel>> BuildOpenMeetingOptionsAsync(IReadOnlyList<JednaniListItemViewModel> meetings, CancellationToken ct = default);

    int? ResolveSelectedMeetingIdForNumber(
        IReadOnlyList<JednaniOptionViewModel> openMeetingOptions,
        int? contextMeetingId);

    Task<IReadOnlyDictionary<int, int>> BuildDefaultOwnerOsobaIdsByProjectSubsystemAsync(int projectId, CancellationToken ct = default);

    Task<int> EnsurePersistedActiveHarmonogramSchemaVersionAsync(CancellationToken ct = default);

    Task<int> GetNextCisloZaznamuAsync(int projectId, CancellationToken ct = default);
}
