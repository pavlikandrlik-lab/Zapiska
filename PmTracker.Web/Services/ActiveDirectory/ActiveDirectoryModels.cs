namespace PmTracker.Web.Services.ActiveDirectory;

public sealed class ActiveDirectoryPersonResult
{
    public Guid GuidAd { get; init; }
    public string? AdLogin { get; init; }
    public required string DisplayName { get; init; }
    public required string Jmeno { get; init; }
    public required string Prijmeni { get; init; }
    public string? Titul { get; init; }
    public required string Email { get; init; }
    public string? Company { get; init; }
    public string? Department { get; init; }
    public bool CanSelect { get; init; }
    public string? DisabledReason { get; init; }
}

public sealed class ActiveDirectorySearchResponse
{
    public bool Available { get; init; }
    public string? Message { get; init; }
    public required IReadOnlyList<ActiveDirectoryPersonResult> Results { get; init; }
}

public sealed class ActiveDirectoryBatchResponse
{
    public bool Available { get; init; }
    public string? Message { get; init; }
    public required IReadOnlyList<ActiveDirectoryPersonResult> Persons { get; init; }
    public required IReadOnlyList<Guid> NotFoundGuids { get; init; }
}
