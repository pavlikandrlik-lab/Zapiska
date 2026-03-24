using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.People;

public interface IPeopleService
{
    Task<OsobyIndexViewModel> BuildOsobyAsync(CancellationToken ct = default);
    Task<int> SaveManualPersonAsync(SaveManualPersonCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task<int> SaveAdPersonAsync(SaveAdPersonCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task DeletePersonAsync(DeletePersonCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
}
