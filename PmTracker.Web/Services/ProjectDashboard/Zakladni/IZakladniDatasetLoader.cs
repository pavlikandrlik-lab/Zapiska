namespace PmTracker.Web.Services.ProjectDashboard.Zakladni;

public interface IZakladniDatasetLoader
{
    Task<ZakladniDataset> LoadAsync(int projektId, Obdobi obdobi, CancellationToken ct);
}
