using System.ComponentModel.DataAnnotations;
using System.Reflection;
using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Modules.Settings;
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
    public void DeleteRolePermission_ShouldDelegateToSettingsCommands()
    {
        var queries = DispatchProxy.Create<ISettingsAuthzQueries, RecordingProxy>();
        var commands = DispatchProxy.Create<ISettingsAuthzCommands, RecordingProxy>();
        var recorder = (RecordingProxy)(object)commands;
        var sut = new SettingsService(queries, commands);
        var command = new DeleteRolePermissionCommand { Id = 42 };
        var currentUser = new CurrentUserContextViewModel
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
            PermissionGrants = []
        };

        sut.DeleteRolePermission(command, currentUser);

        recorder.LastMethodName.Should().Be(nameof(ISettingsAuthzCommands.DeleteRolePermission));
        recorder.LastArguments.Should().ContainInOrder(command, currentUser);
    }

    private static List<ValidationResult> Validate(object instance)
    {
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), validationResults, validateAllProperties: true);
        return validationResults;
    }

    private class RecordingProxy : DispatchProxy
    {
        public string? LastMethodName { get; private set; }
        public object?[] LastArguments { get; private set; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            LastMethodName = targetMethod?.Name;
            LastArguments = args ?? [];

            if (targetMethod is null || targetMethod.ReturnType == typeof(void))
            {
                return null;
            }

            return targetMethod.ReturnType.IsValueType
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }
}
