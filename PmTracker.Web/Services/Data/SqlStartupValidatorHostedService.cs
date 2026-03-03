using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;
using System.Data;

namespace PmTracker.Web.Services.Data;

public sealed class SqlStartupValidatorHostedService : IHostedService
{
    private static readonly string[] RequiredProjectStatusCodes = { "PLAN", "RUN", "DONE", "DELETED" };
    private static readonly string[] RequiredProjectRoleCodes = { ProjectRoleCodes.ProjectOwner, ProjectRoleCodes.Host, ProjectRoleCodes.ProjectAdmin };
    private static readonly string[] RequiredSubsystemRoleCodes = { SubsystemRoleCodes.Lead, SubsystemRoleCodes.DeputyLead, SubsystemRoleCodes.Methodik };

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

        var projectRoleCodes = await dbContext.CiselnikRoliProjektu
            .AsNoTracking()
            .Select(x => x.Kod)
            .ToListAsync(cancellationToken);
        var missingProjectRoleCodes = RequiredProjectRoleCodes
            .Where(required => !projectRoleCodes.Any(code => string.Equals(code, required, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (missingProjectRoleCodes.Count > 0)
        {
            throw new InvalidOperationException("V DB chybí povinné kódy v ciselnik_roli_projektu: " + string.Join(", ", missingProjectRoleCodes));
        }

        var subsystemRoleCodes = await dbContext.CiselnikRoliSubsystemu
            .AsNoTracking()
            .Select(x => x.Kod)
            .ToListAsync(cancellationToken);
        var missingSubsystemRoleCodes = RequiredSubsystemRoleCodes
            .Where(required => !subsystemRoleCodes.Any(code => string.Equals(code, required, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (missingSubsystemRoleCodes.Count > 0)
        {
            throw new InvalidOperationException("V DB chybí povinné kódy v ciselnik_roli_subsystemu: " + string.Join(", ", missingSubsystemRoleCodes));
        }

        var hasEmailColumn = await HasColumnAsync(dbContext, "dbo.osoby", "email", cancellationToken);
        if (!hasEmailColumn)
        {
            throw new InvalidOperationException("V DB chybí sloupec dbo.osoby.email. Obnovte databázi přes PMTracker_insert_sql nebo doplňte sloupec ručně.");
        }

        foreach (var requiredTable in new[] { "dbo.projekt_subsystemy", "dbo.ciselnik_roli_subsystemu", "dbo.obsazeni_subsystemu_projektu" })
        {
            if (!await HasTableAsync(dbContext, requiredTable, cancellationToken))
            {
                throw new InvalidOperationException($"V DB chybí tabulka {requiredTable}. Obnovte databázi přes PMTracker_insert_sql nebo spusťte upgrade skript.");
            }
        }

        var hasCommentAuthorColumn = await HasColumnAsync(dbContext, "dbo.vyjadreni", "autor_osoba_id", cancellationToken);
        if (!hasCommentAuthorColumn)
        {
            throw new InvalidOperationException("V DB chybí sloupec dbo.vyjadreni.autor_osoba_id. Obnovte databázi přes PMTracker_insert_sql nebo doplňte sloupec ručně.");
        }

        var hasLegacyScheduleConstraint = await HasCheckConstraintAsync(
            dbContext,
            "dbo.zaznam_harmonogram_hodnoty",
            "CK_zaznam_harmonogram_hodnoty_hodnota_nonnegative",
            cancellationToken);
        if (hasLegacyScheduleConstraint)
        {
            throw new InvalidOperationException("V DB je legacy constraint CK_zaznam_harmonogram_hodnoty_hodnota_nonnegative, který blokuje zápornou skutečnost harmonogramu. Obnovte databázi přes PMTracker_insert_sql nebo spusťte db_upgrade_1_1_0_signed_schedule_actual.sql.");
        }

        foreach (var requiredColumn in new[] { "datum_prirazeni", "datum_odebrani" })
        {
            if (!await HasColumnAsync(dbContext, "dbo.obsazeni_projektu", requiredColumn, cancellationToken))
            {
                throw new InvalidOperationException($"V DB chybí sloupec dbo.obsazeni_projektu.{requiredColumn}. Obnovte databázi přes PMTracker_insert_sql nebo spusťte upgrade skript.");
            }
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

    private static async Task<bool> HasTableAsync(
        PmTrackerDbContext dbContext,
        string tableName,
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
                FROM sys.tables t
                INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
                WHERE CONCAT(s.name, '.', t.name) = @tableName
                """;

            var tableParam = command.CreateParameter();
            tableParam.ParameterName = "@tableName";
            tableParam.Value = tableName;
            command.Parameters.Add(tableParam);

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

    private static async Task<bool> HasCheckConstraintAsync(
        PmTrackerDbContext dbContext,
        string tableName,
        string constraintName,
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
                FROM sys.check_constraints
                WHERE parent_object_id = OBJECT_ID(@tableName)
                  AND name = @constraintName
                """;

            var tableParam = command.CreateParameter();
            tableParam.ParameterName = "@tableName";
            tableParam.Value = tableName;
            command.Parameters.Add(tableParam);

            var constraintParam = command.CreateParameter();
            constraintParam.ParameterName = "@constraintName";
            constraintParam.Value = constraintName;
            command.Parameters.Add(constraintParam);

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
