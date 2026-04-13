using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Services;

public sealed partial class ProjectService
{
    private async Task<Dictionary<int, int>> BuildActiveProjectSubsystemOrderBySubsystemIdAsync(int projectId, CancellationToken ct)
    {
        return await BuildActiveProjectSubsystemOrderBySubsystemIdAsync(projectId, subsystemIds: null, ct);
    }

    private Task<Dictionary<int, int>> BuildActiveProjectSubsystemOrderBySubsystemIdAsync(
        int projectId,
        IReadOnlyCollection<int>? subsystemIds,
        CancellationToken ct)
    {
        return ProjectSubsystemOrderingQuery.LoadActiveOrderBySubsystemIdAsync(dbContext, projectId, subsystemIds, ct);
    }

    private async Task<int> ResolveNextProjectSubsystemOrderAsync(int projectId, CancellationToken ct)
    {
        return await ProjectSubsystemOrderingQuery.ResolveNextActiveOrderAsync(dbContext, projectId, ct);
    }
}
