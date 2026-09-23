namespace PmTracker.Web.Services.Vyzvy;

public enum VyzvaErrorCode
{
    None = 0,
    ProjectNotFound = 1,
    ProjectMissingMistoPlneni = 2,
    ProjectMissingCisloRamcoveSmlouvy = 3,
    InvalidStateTransition = 5,
    VyzvaNotFound = 6,
    ExternalLinkNotFound = 7,
    ExternalLinkNotPnf = 8,
    VyzvaIsLocked = 9,
    PnfAlreadyInAnotherVyzva = 10,
    AccessDenied = 11,

    // 2026-09-07: číslo výzvy zadává uživatel ručně (spec §5.1). Kód 4 = BufferEmpty
    // zanikl — prázdný buffer už zakládání neblokuje.
    InvalidVyzvaNumber = 12,
    DuplicateVyzvaNumber = 13,
}

public sealed record VyzvaError(VyzvaErrorCode Code, string Message);

public readonly record struct Unit;

public abstract record VyzvaResult<TValue>
{
    public sealed record Ok(TValue Value) : VyzvaResult<TValue>;
    public sealed record Fail(VyzvaError Error) : VyzvaResult<TValue>;
}
