using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services;

/// <summary>
/// Výchozí implementace <see cref="IProjectEditQuery"/>. Viz interface pro motivaci a roadmap.
/// </summary>
/// <remarks>
/// Iterace 1: thin delegation na <see cref="IRecordService.BuildZaznamEditAsync(int, CancellationToken)"/>.
/// SRP je už zajištěno (jediná veřejná metoda, jediný use-case); další iterace přenesou logiku
/// sem a ztenčí RecordService.
/// </remarks>
public sealed class ProjectEditQuery : IProjectEditQuery
{
    private readonly IRecordService _recordService;

    public ProjectEditQuery(IRecordService recordService)
    {
        _recordService = recordService;
    }

    public Task<ZaznamEditViewModel> GetEditModelAsync(int recordId, CancellationToken ct = default)
        => _recordService.BuildZaznamEditAsync(recordId, ct);
}
