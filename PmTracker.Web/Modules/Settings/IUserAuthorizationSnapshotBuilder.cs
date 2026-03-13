namespace PmTracker.Web.Modules.Settings;

public interface IUserAuthorizationSnapshotBuilder
{
    UserAuthorizationSnapshot Build(int osobaId);
}
