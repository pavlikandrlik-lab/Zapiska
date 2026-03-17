using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Data;

public interface IProjectCommandsUseCase
{
    int SaveProject(SaveProjectCommand command, CurrentUserContextViewModel currentUser);
    void SoftDeleteProject(SoftDeleteProjectCommand command, CurrentUserContextViewModel currentUser);
}
