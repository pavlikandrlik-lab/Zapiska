namespace PmTracker.Web.Services.Common;

public sealed class ApplicationVersionProvider : IApplicationVersionProvider
{
    public string DisplayVersion { get; } = ApplicationVersionFormatter.FormatDisplayVersion(typeof(Program).Assembly);
}
