using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Security;
using PmTracker.Web.Services.Settings;

namespace PmTracker.Tests.Unit.Settings;

public sealed class DeleteRolePermissionCommandTests
{
    [Fact]
    public void DeleteRolePermissionCommand_ShouldRequirePositiveId()
    {
        var command = new DeleteRolePermissionCommand();

        var validationResults = Validate(command);

        validationResults.Should()
            .ContainSingle(result => result.ErrorMessage == "Mapování role/akce nebylo vybráno.");
    }

    [Fact]
    public async Task DeleteRolePermissionAsync_ShouldDelegateToCommands()
    {
        var commands = new FakeSettingsAuthzCommands();
        var sut = new SettingsService(new FakeSettingsAuthzQueries(), commands);
        var command = new DeleteRolePermissionCommand { Id = 42 };
        var currentUser = BuildCurrentUser();

        await sut.DeleteRolePermissionAsync(command, currentUser);

        commands.LastDeleteRolePermissionCommand.Should().BeSameAs(command);
        commands.LastDeleteRolePermissionUser.Should().BeSameAs(currentUser);
    }

    private static CurrentUserContextViewModel BuildCurrentUser()
    {
        return new CurrentUserContextViewModel
        {
            OsobaId = 7,
            Jmeno = "Admin",
            Prijmeni = "User",
            DisplayName = "Admin User",
            Email = "admin.user@pmtracker.local",
            OrganizacniCelek = "IT",
            OrganizacniCelekKod = "IT",
            RoleKody = [],
            VisibleProjectIds = [],
            DeletedProjectIds = [],
            Authorization = new AuthorizationSnapshot(
                IsSuperAdmin: false,
                GlobalPermissions: new HashSet<string>(),
                PerProjectPermissions: new Dictionary<int, IReadOnlySet<string>>(),
                PerSubsystemPermissions: new Dictionary<int, IReadOnlySet<string>>())
        };
    }

    private static List<ValidationResult> Validate(object instance)
    {
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), validationResults, validateAllProperties: true);
        return validationResults;
    }

    private sealed class FakeSettingsAuthzQueries : ISettingsAuthzQueries
    {
        public Task<NastaveniDashboardViewModel> BuildNastaveniDashboardAsync(string? section, CurrentUserContextViewModel currentUser, int? userId, int? projektId, CancellationToken cancellationToken = default)
            => Task.FromResult(new NastaveniDashboardViewModel
            {
                Sekce = [],
                AktivniPanel = new NastaveniPanelViewModel
                {
                    SectionKey = "role",
                    Nazev = "Role",
                    Popis = "Panel",
                    Role = [],
                    PermissionCategories = [],
                    Permissions = [],
                    RolePermissionScopes = [],
                    UserRoles = [],
                    EffectivePermissions = new EffectivePermissionsPreviewViewModel
                    {
                        SelectedUserId = 0,
                        SelectedProjectId = null,
                        OsobaId = 0,
                        Osoba = string.Empty,
                        ProjektId = null,
                        ProjektNazev = string.Empty,
                        AvailableUsers = [],
                        AvailableProjects = [],
                        Rows = []
                    },
                    Projekty = []
                },
                SelectedUserId = null,
                SelectedProjektId = null
            });

        public Task<NastaveniPanelViewModel> BuildNastaveniPanelAsync(string? section, CurrentUserContextViewModel currentUser, int? userId, int? projektId, CancellationToken cancellationToken = default)
            => Task.FromResult(new NastaveniPanelViewModel
            {
                SectionKey = "role",
                Nazev = "Role",
                Popis = "Panel",
                Role = [],
                PermissionCategories = [],
                Permissions = [],
                RolePermissionScopes = [],
                UserRoles = [],
                EffectivePermissions = new EffectivePermissionsPreviewViewModel
                {
                    SelectedUserId = 0,
                    SelectedProjectId = null,
                    OsobaId = 0,
                    Osoba = string.Empty,
                    ProjektId = null,
                    ProjektNazev = string.Empty,
                    AvailableUsers = [],
                    AvailableProjects = [],
                    Rows = []
                },
                Projekty = []
            });
    }

    private sealed class FakeSettingsAuthzCommands : ISettingsAuthzCommands
    {
        public DeleteRolePermissionCommand? LastDeleteRolePermissionCommand { get; private set; }
        public CurrentUserContextViewModel? LastDeleteRolePermissionUser { get; private set; }

        public Task SaveAuthzRoleAsync(SaveAuthzRoleCommand command, CurrentUserContextViewModel currentUser, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ToggleAuthzRoleAsync(ToggleAuthzRoleCommand command, CurrentUserContextViewModel currentUser, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAuthzPermissionAsync(SaveAuthzPermissionCommand command, CurrentUserContextViewModel currentUser, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ToggleAuthzPermissionAsync(ToggleAuthzPermissionCommand command, CurrentUserContextViewModel currentUser, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveUserRoleAssignmentAsync(SaveUserRoleAssignmentCommand command, CurrentUserContextViewModel currentUser, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveUserRolesForUserAsync(SaveUserRolesForUserCommand command, CurrentUserContextViewModel currentUser, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveRolePermissionAsync(SaveRolePermissionCommand command, CurrentUserContextViewModel currentUser, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteRolePermissionAsync(DeleteRolePermissionCommand command, CurrentUserContextViewModel currentUser, CancellationToken cancellationToken = default)
        {
            LastDeleteRolePermissionCommand = command;
            LastDeleteRolePermissionUser = currentUser;
            return Task.CompletedTask;
        }
    }
}
