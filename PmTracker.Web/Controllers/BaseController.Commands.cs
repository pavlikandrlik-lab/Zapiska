using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Controllers;

public abstract partial class BaseController
{
    protected IActionResult ExecuteValidatedCommand(
        Func<bool> hasPermission,
        string invalidAjaxMessage,
        string invalidFallbackMessage,
        Func<IActionResult> onInvalidRedirect,
        Func<IActionResult> onSuccessRedirect,
        Func<IActionResult>? onAjaxSuccess,
        Action operation,
        Func<Exception, IActionResult>? onExceptionRedirect = null)
    {
        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest())
            {
                return AjaxInvalidModelResult(invalidAjaxMessage);
            }

            var fieldErrors = BuildModelStateFieldErrors();
            TempData["ErrorMessage"] = $"{invalidAjaxMessage}: {JoinFieldErrors(fieldErrors, invalidFallbackMessage)}";
            return onInvalidRedirect();
        }

        return ExecuteCommand(
            hasPermission,
            onSuccessRedirect,
            onAjaxSuccess,
            operation,
            onExceptionRedirect);
    }

    protected async Task<IActionResult> ExecuteValidatedCommandAsync(
        Func<bool> hasPermission,
        string invalidAjaxMessage,
        string invalidFallbackMessage,
        Func<IActionResult> onInvalidRedirect,
        Func<Task<IActionResult>> onSuccessRedirect,
        Func<Task<IActionResult>>? onAjaxSuccess,
        Func<Task> operation,
        Func<Exception, Task<IActionResult>>? onExceptionRedirect = null)
    {
        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest())
            {
                return AjaxInvalidModelResult(invalidAjaxMessage);
            }

            var fieldErrors = BuildModelStateFieldErrors();
            TempData["ErrorMessage"] = $"{invalidAjaxMessage}: {JoinFieldErrors(fieldErrors, invalidFallbackMessage)}";
            return onInvalidRedirect();
        }

        return await ExecuteCommandAsync(
            hasPermission,
            onSuccessRedirect,
            onAjaxSuccess,
            operation,
            onExceptionRedirect);
    }

    protected async Task<IActionResult> ExecuteValidatedCommandAsync(
        Func<Task<bool>> hasPermission,
        string invalidAjaxMessage,
        string invalidFallbackMessage,
        Func<IActionResult> onInvalidRedirect,
        Func<Task<IActionResult>> onSuccessRedirect,
        Func<Task<IActionResult>>? onAjaxSuccess,
        Func<Task> operation,
        Func<Exception, Task<IActionResult>>? onExceptionRedirect = null)
    {
        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest())
            {
                return AjaxInvalidModelResult(invalidAjaxMessage);
            }

            var fieldErrors = BuildModelStateFieldErrors();
            TempData["ErrorMessage"] = $"{invalidAjaxMessage}: {JoinFieldErrors(fieldErrors, invalidFallbackMessage)}";
            return onInvalidRedirect();
        }

        return await ExecuteCommandAsync(
            hasPermission,
            onSuccessRedirect,
            onAjaxSuccess,
            operation,
            onExceptionRedirect);
    }

    protected IActionResult ExecuteCommand(
        Func<bool> hasPermission,
        Func<IActionResult> onSuccessRedirect,
        Func<IActionResult>? onAjaxSuccess,
        Action operation,
        Func<Exception, IActionResult>? onExceptionRedirect = null)
    {
        if (!hasPermission())
        {
            if (IsAjaxRequest())
            {
                return AjaxForbiddenResult();
            }

            return Forbid();
        }

        try
        {
            operation();
            if (IsAjaxRequest() && onAjaxSuccess is not null)
            {
                return onAjaxSuccess();
            }

            return onSuccessRedirect();
        }
        catch (Exception ex)
        {
            return HandleCommandException(ex, onSuccessRedirect, onExceptionRedirect);
        }
    }

    protected async Task<IActionResult> ExecuteCommandAsync(
        Func<bool> hasPermission,
        Func<Task<IActionResult>> onSuccessRedirect,
        Func<Task<IActionResult>>? onAjaxSuccess,
        Func<Task> operation,
        Func<Exception, Task<IActionResult>>? onExceptionRedirect = null)
    {
        if (!hasPermission())
        {
            if (IsAjaxRequest())
            {
                return AjaxForbiddenResult();
            }

            return Forbid();
        }

        try
        {
            await operation();
            if (IsAjaxRequest() && onAjaxSuccess is not null)
            {
                return await onAjaxSuccess();
            }

            return await onSuccessRedirect();
        }
        catch (Exception ex)
        {
            return await HandleCommandExceptionAsync(ex, onSuccessRedirect, onExceptionRedirect);
        }
    }

    protected async Task<IActionResult> ExecuteCommandAsync(
        Func<Task<bool>> hasPermission,
        Func<Task<IActionResult>> onSuccessRedirect,
        Func<Task<IActionResult>>? onAjaxSuccess,
        Func<Task> operation,
        Func<Exception, Task<IActionResult>>? onExceptionRedirect = null)
    {
        if (!await hasPermission())
        {
            if (IsAjaxRequest())
            {
                return AjaxForbiddenResult();
            }

            return Forbid();
        }

        try
        {
            await operation();
            if (IsAjaxRequest() && onAjaxSuccess is not null)
            {
                return await onAjaxSuccess();
            }

            return await onSuccessRedirect();
        }
        catch (Exception ex)
        {
            return await HandleCommandExceptionAsync(ex, onSuccessRedirect, onExceptionRedirect);
        }
    }

    private IActionResult HandleCommandException(
        Exception exception,
        Func<IActionResult> onSuccessRedirect,
        Func<Exception, IActionResult>? onExceptionRedirect)
    {
        if (IsAjaxRequest())
        {
            return BuildAjaxExceptionResult(exception);
        }

        TempData["ErrorMessage"] = exception.Message;
        if (onExceptionRedirect is not null)
        {
            return onExceptionRedirect(exception);
        }

        return onSuccessRedirect();
    }

    private async Task<IActionResult> HandleCommandExceptionAsync(
        Exception exception,
        Func<Task<IActionResult>> onSuccessRedirect,
        Func<Exception, Task<IActionResult>>? onExceptionRedirect)
    {
        if (IsAjaxRequest())
        {
            return BuildAjaxExceptionResult(exception);
        }

        TempData["ErrorMessage"] = exception.Message;
        if (onExceptionRedirect is not null)
        {
            return await onExceptionRedirect(exception);
        }

        return await onSuccessRedirect();
    }

    private IActionResult BuildAjaxExceptionResult(Exception exception)
    {
        if (exception is RecordValidationException validationException)
        {
            return AjaxErrorResult(
                validationException.Message,
                validationException.ErrorCode,
                validationException,
                validationException.FieldErrors,
                validationException.DiagnosticLog);
        }

        if (exception is InvalidOperationException invalidOperationException)
        {
            return AjaxErrorResult(
                invalidOperationException.Message,
                AjaxErrorCodes.OperationFailed,
                invalidOperationException);
        }

        return AjaxErrorResult(
            "Operaci se nepodařilo dokončit.",
            AjaxErrorCodes.UnexpectedServerError,
            exception);
    }
}
