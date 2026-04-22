namespace PmTracker.Web.Services.Vyzvy;

public enum VyzvaErrorCode
{
    None = 0,
    ProjectNotFound = 1,
    ProjectMissingMistoPlneni = 2,
    ProjectMissingCisloRamcoveSmlouvy = 3,
    BufferEmpty = 4,
    InvalidStateTransition = 5,
    VyzvaNotFound = 6,
    ExternalLinkNotFound = 7,
    ExternalLinkNotPnf = 8,
    VyzvaIsLocked = 9,
    PnfAlreadyInAnotherVyzva = 10,
    AccessDenied = 11,
}

public sealed record VyzvaError(VyzvaErrorCode Code, string Message);

public readonly record struct Unit;

public abstract record VyzvaResult<TValue>
{
    public sealed record Ok(TValue Value) : VyzvaResult<TValue>;
    public sealed record Fail(VyzvaError Error) : VyzvaResult<TValue>;
}
