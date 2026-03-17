using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Data;

public interface IRecordEditorQueriesComposition
{
    ZaznamEditViewModel BuildZaznamEditForEntity(
        ProjektovyZaznamEntity record,
        bool isCreate,
        int? forceMeetingIdForNumber = null,
        bool? projectUsesMeetingIdentifier = null,
        IReadOnlyList<JednaniOptionViewModel>? openMeetingOptions = null);

    IReadOnlyList<ProjectSubsystemViewModel> BuildActiveProjectSubsystems(int projectId);

    IReadOnlyList<JednaniListItemViewModel> BuildJednaniList(int projectId);

    IReadOnlyList<JednaniOptionViewModel> BuildOpenMeetingOptions(IReadOnlyList<JednaniListItemViewModel> meetings);

    int? ResolveSelectedMeetingIdForNumber(
        IReadOnlyList<JednaniOptionViewModel> openMeetingOptions,
        int? contextMeetingId);

    IReadOnlyDictionary<int, int> BuildDefaultOwnerOsobaIdsByProjectSubsystem(int projectId);

    int EnsurePersistedActiveHarmonogramSchemaVersion();

    int GetNextCisloZaznamu(int projectId);
}
