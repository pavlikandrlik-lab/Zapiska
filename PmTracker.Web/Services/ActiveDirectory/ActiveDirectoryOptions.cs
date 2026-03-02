namespace PmTracker.Web.Services.ActiveDirectory;

public sealed class ActiveDirectoryOptions
{
    public const string SectionName = "PmTracker:ActiveDirectory";

    public string Domain { get; set; } = "acr";
    public int MaxResults { get; set; } = 15;
    public int QueryTimeoutSeconds { get; set; } = 8;
}
