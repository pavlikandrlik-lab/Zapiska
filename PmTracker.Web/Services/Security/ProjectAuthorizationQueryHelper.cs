using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Services.Security;

internal static class ProjectAuthorizationQueryHelper
{
    public static async Task<List<int>> BuildDeletedProjectIdsAsync(
        PmTrackerDbContext dbContext,
        ITextNormalizer textNormalizer,
        CancellationToken ct = default)
    {
        var deletedStatusIds = await dbContext.CiselnikStavuProjektu.AsNoTracking()
                .Select(status => new
                {
                    status.Id,
                    status.Kod,
                    status.Nazev
                })
            .ToListAsync(ct);
        var matchingStatusIds = deletedStatusIds
            .Where(status =>
                string.Equals(status.Kod, "DELETED", StringComparison.OrdinalIgnoreCase)
                || textNormalizer.Normalize(status.Nazev).Contains("smaz", StringComparison.OrdinalIgnoreCase))
            .Select(status => status.Id)
            .Distinct()
            .ToArray();

        if (matchingStatusIds.Length == 0)
        {
            return [];
        }

        return await dbContext.Projekty.AsNoTracking()
            .Where(project => matchingStatusIds.Contains(project.StavId))
            .Select(project => project.Id)
            .Distinct()
            .OrderBy(id => id)
            .ToListAsync(ct);
    }
}
