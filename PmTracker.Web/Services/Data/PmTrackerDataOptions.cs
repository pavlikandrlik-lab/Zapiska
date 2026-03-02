namespace PmTracker.Web.Services.Data;

public sealed class PmTrackerDataOptions
{
    public const string SectionName = "PmTracker:Data";

    public string Provider { get; set; } = "SqlServer";
    public SqlServerDataOptions SqlServer { get; set; } = new();
}

public sealed class SqlServerDataOptions
{
    public string ConnectionStringName { get; set; } = "PmTrackerDb";
    public int CommandTimeoutSeconds { get; set; } = 30;
}
