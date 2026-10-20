using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace PmTracker.Web.Services.Security.ViewAs;

public sealed class ViewAsWriteBlockedException() : InvalidOperationException(ViewAsMessages.ReadOnly)
{
    /// <summary>EF zabalí výjimku z dávky SaveChanges do DbUpdateException — hledáme v celém řetězci.</summary>
    public static bool IsInChain(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is ViewAsWriteBlockedException)
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Pojistka „jen pro čtení“: v requestu s aktivním pohledem odmítne každý zapisující SQL příkaz,
/// i ten, o kterém middleware neví (zápis při GET). Scoped — sdílí <see cref="ViewAsRequestState"/>
/// s requestem; služby na pozadí mají vlastní scope s neaktivním stavem.
/// </summary>
public sealed class ViewAsWriteGuardInterceptor(
    ViewAsRequestState state,
    ILogger<ViewAsWriteGuardInterceptor> logger) : DbCommandInterceptor
{
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Guard(command);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Guard(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Guard(command);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Guard(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        Guard(command);
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        Guard(command);
        return ValueTask.FromResult(result);
    }

    private void Guard(DbCommand command)
    {
        if (!state.BlocksWrites || !SqlWriteCommandClassifier.IsWrite(command.CommandText))
        {
            return;
        }

        var sql = command.CommandText;
        logger.LogError(
            "Pohled jako: zablokován zápis do databáze. RealOsobaId={RealOsobaId} TargetOsobaId={TargetOsobaId} Sql={Sql}",
            state.RealOsobaId,
            state.TargetOsobaId,
            sql.Length > 300 ? sql[..300] : sql);
        throw new ViewAsWriteBlockedException();
    }
}
