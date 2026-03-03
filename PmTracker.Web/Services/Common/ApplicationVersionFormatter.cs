using System.Reflection;

namespace PmTracker.Web.Services.Common;

public static class ApplicationVersionFormatter
{
    public static string FormatDisplayVersion(string? informationalVersion, Version? assemblyVersion)
    {
        var sanitizedInformationalVersion = informationalVersion?.Split('+', 2, StringSplitOptions.TrimEntries)[0];
        if (!string.IsNullOrWhiteSpace(sanitizedInformationalVersion))
        {
            return sanitizedInformationalVersion;
        }

        if (assemblyVersion is null)
        {
            return "n/a";
        }

        return assemblyVersion.Build > 0
            ? $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}"
            : $"{assemblyVersion.Major}.{assemblyVersion.Minor}";
    }

    public static string FormatDisplayVersion(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        return FormatDisplayVersion(informationalVersion, assembly.GetName().Version);
    }
}
