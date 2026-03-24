using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Security;

public interface IUserContextResolver
{
    Task<UserContextResolutionResult> ResolveAsync(HttpContext httpContext, CancellationToken ct = default);
}

public sealed class UserContextResolutionResult
{
    public bool IsSuccess { get; }
    public int StatusCode { get; }
    public string? ErrorMessage { get; }
    public CurrentUserContextViewModel? UserContext { get; }

    private UserContextResolutionResult(bool isSuccess, int statusCode, string? errorMessage, CurrentUserContextViewModel? userContext)
    {
        IsSuccess = isSuccess;
        StatusCode = statusCode;
        ErrorMessage = errorMessage;
        UserContext = userContext;
    }

    public static UserContextResolutionResult Success(CurrentUserContextViewModel context) => new(true, StatusCodes.Status200OK, null, context);
    public static UserContextResolutionResult Forbidden(string message) => new(false, StatusCodes.Status403Forbidden, message, null);
    public static UserContextResolutionResult Unauthorized(string message) => new(false, StatusCodes.Status401Unauthorized, message, null);
}
