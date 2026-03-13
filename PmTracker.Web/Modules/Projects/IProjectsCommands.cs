using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Projects;

public interface IProjectsCommands
{
    int SaveProject(SaveProjectCommand command, CurrentUserContextViewModel currentUser);
    void SoftDeleteProject(SoftDeleteProjectCommand command, CurrentUserContextViewModel currentUser);
}
