using PmTracker.Web.Services.Sync;

namespace PmTracker.Web.Services.ServiceDesk;

/// <summary>
/// Zdroj reactive triggeru pro SD harvest. Slouží jen k diagnostice / logování —
/// queue dedup ignoruje zdroj, protože harvest je idempotentní nezávisle na triggeru.
/// Spec §8.2.
/// </summary>
public enum SdReactiveSource
{
    /// <summary>T2 — po uložení projektového záznamu s novou/změněnou ext. vazbou.</summary>
    RecordSave = 0,

    /// <summary>T5 — otevření editoru projektového záznamu.</summary>
    EditorOpen = 1,

    /// <summary>T7 — schválení CREATE_RECORD návrhu s externími vazbami.</summary>
    ProposalApprove = 2,

    /// <summary>T8 — otevření Externí vazby / Harmonogram tabu (lazy fallback).</summary>
    TabOpen = 3
}

/// <summary>
/// Scope periodického SD harvestu — aktivní vs. archivní tikety dle HOT_ZAZNAMY.stav.
/// Spec §4.
/// </summary>
public enum HarvestScope
{
    /// <summary>HOT_ZAZNAMY.stav &lt;&gt; 'archiv'. Krátká perioda (default 60 min).</summary>
    Active = 0,

    /// <summary>HOT_ZAZNAMY.stav = 'archiv'. Dlouhá perioda (default 1440 min / 24 h).</summary>
    Archive = 1,

    /// <summary>Oba scope současně — pouze pro manual admin run nebo direct sync volání.</summary>
    All = 2
}

/// <summary>
/// Reactive request pro harvest jedné externí vazby. DedupKey = ExterniOdkazId podle spec §13.4:
/// opakovaný enqueue téhož odkazu ve stejném okamžiku je no-op, dokud consumer nedokončí předchozí run.
/// Zdroj (<see cref="Source"/>) je informativní — pro dedup nerelevantní.
/// </summary>
public sealed record SdReactiveHarvestRequest(int ExterniOdkazId, SdReactiveSource Source)
    : IHasDedupKey
{
    public object DedupKey => ExterniOdkazId;
}

/// <summary>
/// Shrnutí jednoho SD harvest běhu (periodic tick — uloží se do LastResultJson).
/// </summary>
public sealed record SdHarvestResult(
    DateTime StartedAt,
    DateTime FinishedAt,
    int TicketsChecked,
    int TicketsSkippedByFingerprint,
    int TicketsDrilled,
    int BindingsCreated,
    int BindingsUpdated,
    int ErrorCount,
    IReadOnlyList<SdHarvestErrorItem> Errors)
{
    public long DurationMs => (long)(FinishedAt - StartedAt).TotalMilliseconds;

    public static SdHarvestResult Empty(DateTime startedAt)
        => new(startedAt, startedAt, 0, 0, 0, 0, 0, 0, Array.Empty<SdHarvestErrorItem>());
}

public sealed record SdHarvestErrorItem(int ExterniOdkazId, int? TicketId, string Reason);
