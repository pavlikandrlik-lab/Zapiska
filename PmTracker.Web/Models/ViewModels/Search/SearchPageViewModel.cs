using PmTracker.Web.Services.Search;

namespace PmTracker.Web.Models.ViewModels.Search;

/// <summary>
/// Dědí <see cref="BaseViewModel"/> — bez toho neprojde generická podmínka
/// <c>AttachCurrentUser&lt;T&gt; where T : BaseViewModel</c> v BaseController.
/// </summary>
public sealed class SearchPageViewModel : BaseViewModel
{
    public required string Query { get; init; }
    public required SearchResult Result { get; init; }
}
