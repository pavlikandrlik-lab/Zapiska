namespace PmTracker.ServiceDesk.Sql.Entities;

internal sealed class HotPidEntity
{
    public long Id { get; set; }
    public string? Pid { get; set; }
    public int? IdxPou { get; set; }
}
