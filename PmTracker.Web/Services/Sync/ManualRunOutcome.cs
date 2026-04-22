namespace PmTracker.Web.Services.Sync;

/// <summary>
/// Výsledek pokusu spustit sync job manuálně (admin „Spustit teď").
/// Accepted=false znamená rate-limit, běžící job, nebo neznámý JobKey.
/// </summary>
public sealed record ManualRunOutcome(bool Accepted, string Message);
