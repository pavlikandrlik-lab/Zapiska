using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Search;

public sealed class GlobalSearchPageViewModel : BaseViewModel
{
    public string Query { get; init; } = string.Empty;
    public GlobalSearchResult Result { get; init; } = new();
    public bool SearchEnabled { get; init; }
}
