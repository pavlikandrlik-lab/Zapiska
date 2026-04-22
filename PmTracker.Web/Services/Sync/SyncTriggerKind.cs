namespace PmTracker.Web.Services.Sync;

public enum SyncTriggerKind
{
    Auto = 0,
    Manual = 1
}

public static class SyncTriggerKindExtensions
{
    public static string ToWireString(this SyncTriggerKind kind) => kind switch
    {
        SyncTriggerKind.Auto => "auto",
        SyncTriggerKind.Manual => "manual",
        _ => "auto"
    };

    public static SyncTriggerKind ParseWireString(string? value) => value switch
    {
        "manual" => SyncTriggerKind.Manual,
        _ => SyncTriggerKind.Auto
    };
}
