using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using System.Data;

namespace PmTracker.Web.Services.Data;

public sealed class SqlStartupValidatorHostedService : IHostedService
{
    private static readonly string[] RequiredProjectStatusCodes = { "PLAN", "RUN", "DONE", "DELETED" };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SqlStartupValidatorHostedService> _logger;

    public SqlStartupValidatorHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<SqlStartupValidatorHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PmTrackerDbContext>();

        if (!await dbContext.Database.CanConnectAsync(cancellationToken))
        {
            throw new InvalidOperationException("Nelze se připojit k SQL Server databázi (PmTracker). Aplikace běží bez fallbacku, start se ukončí.");
        }

        var projectStatusCodes = await dbContext.CiselnikStavuProjektu
            .AsNoTracking()
            .Select(x => x.Kod)
            .ToListAsync(cancellationToken);

        var missingCodes = RequiredProjectStatusCodes
            .Where(required => !projectStatusCodes.Any(code => string.Equals(code, required, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (missingCodes.Count > 0)
        {
            throw new InvalidOperationException("V DB chybí povinné kódy v ciselnik_stavu_projektu: " + string.Join(", ", missingCodes));
        }

        var hasEmailColumn = await HasColumnAsync(dbContext, "dbo.osoby", "email", cancellationToken);
        if (!hasEmailColumn)
        {
            throw new InvalidOperationException("V DB chybí sloupec dbo.osoby.email. Obnovte databázi přes PMTracker_insert_sql nebo doplňte sloupec ručně.");
        }

        var hasCommentAuthorColumn = await HasColumnAsync(dbContext, "dbo.vyjadreni", "autor_osoba_id", cancellationToken);
        if (!hasCommentAuthorColumn)
        {
            throw new InvalidOperationException("V DB chybí sloupec dbo.vyjadreni.autor_osoba_id. Obnovte databázi přes PMTracker_insert_sql nebo doplňte sloupec ručně.");
        }

        _logger.LogInformation("SQL startup validace proběhla úspěšně.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task<bool> HasColumnAsync(
        PmTrackerDbContext dbContext,
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var mustClose = connection.State != ConnectionState.Open;
        if (mustClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*)
                FROM sys.columns
                WHERE object_id = OBJECT_ID(@tableName)
                  AND name = @columnName
                """;

            var tableParam = command.CreateParameter();
            tableParam.ParameterName = "@tableName";
            tableParam.Value = tableName;
            command.Parameters.Add(tableParam);

            var columnParam = command.CreateParameter();
            columnParam.ParameterName = "@columnName";
            columnParam.Value = columnName;
            command.Parameters.Add(columnParam);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result) > 0;
        }
        finally
        {
            if (mustClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
