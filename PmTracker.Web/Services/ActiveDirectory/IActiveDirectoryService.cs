namespace PmTracker.Web.Services.ActiveDirectory;

public interface IActiveDirectoryService
{
    Task<ActiveDirectorySearchResponse> SearchUsersAsync(string? query, CancellationToken ct = default);
}
