using PmTracker.Web.Services.Vyzvy.Contracts;

namespace PmTracker.Web.Models.ViewModels.Vyzvy;

public sealed class ReassignModalViewModel
{
    public int ProjektId { get; init; }
    public required IReadOnlyList<VyzvaBufferItem> BufferPolozky { get; init; }
    public required IReadOnlyList<VyzvaDetail> PripravaVyzvy { get; init; }
}
