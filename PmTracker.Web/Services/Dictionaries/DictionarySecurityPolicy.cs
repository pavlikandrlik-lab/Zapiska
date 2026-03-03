namespace PmTracker.Web.Services.Dictionaries;

public static class DictionarySecurityPolicy
{
    private const string HarmonogramKrokyKey = "harmonogram-kroky";
    private const string SubsystemyKey = "subsystemy";

    public static bool IsSuperAdminOnlyDictionary(string? key)
        => string.Equals(NormalizeKey(key), HarmonogramKrokyKey, StringComparison.OrdinalIgnoreCase);

    public static bool SupportsLocking(string? key)
        => !string.Equals(NormalizeKey(key), SubsystemyKey, StringComparison.OrdinalIgnoreCase);

    public static bool CanAccessDictionary(string? key, bool isSuperAdmin)
        => !IsSuperAdminOnlyDictionary(key) || isSuperAdmin;

    public static bool CanModifyRow(string? key, bool rowIsLocked, bool isSuperAdmin)
        => CanAccessDictionary(key, isSuperAdmin) && (isSuperAdmin || !rowIsLocked);

    public static bool CanChangeLockState(string? key, bool isSuperAdmin)
        => SupportsLocking(key) && isSuperAdmin;

    public static bool NormalizeRequestedLockState(string? key, bool requestedIsLocked, bool isSuperAdmin)
        => SupportsLocking(key) && isSuperAdmin && requestedIsLocked;

    private static string NormalizeKey(string? key)
        => (key ?? string.Empty).Trim().ToLowerInvariant();
}
