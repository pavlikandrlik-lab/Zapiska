using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Documentation;

public interface IDocumentationService
{
    DocumentationPageViewModel BuildPage(string key);
}
