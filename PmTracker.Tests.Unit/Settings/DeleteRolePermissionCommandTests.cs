using System.ComponentModel.DataAnnotations;
using System.Reflection;
using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;
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
    public void DeleteRolePermission_ShouldDelegateToDataStore()
    {
        var dataStore = DispatchProxy.Create<IPmTrackerDataStore, RecordingDataStoreProxy>();
        var recorder = (RecordingDataStoreProxy)(object)dataStore;
        var sut = new SettingsService(dataStore);
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
            PermissionGrants = []
        };

        sut.DeleteRolePermission(command, currentUser);

        recorder.LastMethodName.Should().Be(nameof(IPmTrackerDataStore.DeleteRolePermission));
        recorder.LastArguments.Should().ContainInOrder(command, currentUser);
    }

    private static List<ValidationResult> Validate(object instance)
    {
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), validationResults, validateAllProperties: true);
        return validationResults;
    }

    private class RecordingDataStoreProxy : DispatchProxy
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
