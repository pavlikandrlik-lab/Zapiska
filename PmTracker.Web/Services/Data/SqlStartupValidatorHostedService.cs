using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;
using System.Data;

namespace PmTracker.Web.Services.Data;

public sealed class SqlStartupValidatorHostedService : IHostedService
{
    private static readonly string[] RequiredProjectStatusCodes = { "PLAN", "RUN", "DONE", "DELETED" };
    private static readonly string[] RequiredProjectRoleCodes = { ProjectRoleCodes.ProjectOwner, ProjectRoleCodes.Host, ProjectRoleCodes.ProjectAdmin, ProjectRoleCodes.ProjectManager, ProjectRoleCodes.Gestor };
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

    public async Task StartAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PmTrackerDbContext>();

        if (!await dbContext.Database.CanConnectAsync(ct))
        {
            throw new InvalidOperationException("Nelze se připojit k SQL Server databázi (PmTracker). Aplikace běží bez fallbacku, start se ukončí.");
        }

        var projectStatusCodes = await dbContext.CiselnikStavuProjektu
            .AsNoTracking()
            .Select(x => x.Kod)
            .ToListAsync(ct);

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
            .ToListAsync(ct);
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
            .ToListAsync(ct);
        var missingSubsystemRoleCodes = RequiredSubsystemRoleCodes
            .Where(required => !subsystemRoleCodes.Any(code => string.Equals(code, required, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (missingSubsystemRoleCodes.Count > 0)
        {
            throw new InvalidOperationException("V DB chybí povinné kódy v ciselnik_roli_subsystemu: " + string.Join(", ", missingSubsystemRoleCodes));
        }

        var hasEmailColumn = await HasColumnAsync(dbContext, "dbo.osoby", "email", ct);
        if (!hasEmailColumn)
        {
            throw new InvalidOperationException("V DB chybí sloupec dbo.osoby.email. Obnovte databázi přes PMTracker_insert_sql nebo doplňte sloupec ručně.");
        }

        foreach (var requiredTable in new[]
                 {
                     "dbo.projekt_subsystemy",
                     "dbo.ciselnik_roli_subsystemu",
                     "dbo.obsazeni_subsystemu_projektu",
                     "dbo.zaznam_navrhy",
                     "dbo.zaznam_priority_uzivatelu",
                     "dbo.zaznam_priority_rebuild_state"
                 })
        {
            if (!await HasTableAsync(dbContext, requiredTable, ct))
            {
                throw new InvalidOperationException($"V DB chybí tabulka {requiredTable}. Obnovte databázi přes PMTracker_insert_sql nebo spusťte upgrade skript.");
            }
        }

        var hasCommentAuthorColumn = await HasColumnAsync(dbContext, "dbo.vyjadreni", "autor_osoba_id", ct);
        if (!hasCommentAuthorColumn)
        {
            throw new InvalidOperationException("V DB chybí sloupec dbo.vyjadreni.autor_osoba_id. Obnovte databázi přes PMTracker_insert_sql nebo doplňte sloupec ručně.");
        }

        var hasEstimatedExternalLinkPriceColumn = await HasColumnAsync(dbContext, "dbo.zaznam_externi_odkazy", "predpokladana_cena", ct);
        if (!hasEstimatedExternalLinkPriceColumn)
        {
            throw new InvalidOperationException("V DB chybí sloupec dbo.zaznam_externi_odkazy.predpokladana_cena. Obnovte databázi přes PMTracker_insert_sql nebo spusťte db_upgrade_1_1_1_external_link_estimated_price.sql.");
        }

        var hasRecordGoalColumn = await HasColumnAsync(dbContext, "dbo.projektove_zaznamy", "cil", ct);
        if (!hasRecordGoalColumn)
        {
            throw new InvalidOperationException("V DB chybí sloupec dbo.projektove_zaznamy.cil. Obnovte databázi přes PMTracker_insert_sql nebo doplňte sloupec ručně.");
        }
        var recordGoalMaxLength = await GetCharacterMaxLengthAsync(dbContext, "dbo.projektove_zaznamy", "cil", ct);
        if (recordGoalMaxLength.HasValue && recordGoalMaxLength.Value > 0 && recordGoalMaxLength.Value < 500)
        {
            throw new InvalidOperationException(
                $"Sloupec dbo.projektove_zaznamy.cil má délku {recordGoalMaxLength.Value}, ale aplikace vyžaduje alespoň 500 znaků. " +
                "Upravte DB ručně: ALTER TABLE dbo.projektove_zaznamy ALTER COLUMN cil NVARCHAR(500) NULL;");
        }

        var hasLegacyScheduleConstraint = await HasCheckConstraintAsync(
            dbContext,
            "dbo.zaznam_harmonogram_hodnoty",
            "CK_zaznam_harmonogram_hodnoty_hodnota_nonnegative",
            ct);
        if (hasLegacyScheduleConstraint)
        {
            throw new InvalidOperationException("V DB je legacy constraint CK_zaznam_harmonogram_hodnoty_hodnota_nonnegative, který blokuje zápornou skutečnost harmonogramu. Obnovte databázi přes PMTracker_insert_sql nebo spusťte db_upgrade_1_1_0_signed_schedule_actual.sql.");
        }

        foreach (var requiredColumn in new[] { "datum_prirazeni", "datum_odebrani" })
        {
            if (!await HasColumnAsync(dbContext, "dbo.obsazeni_projektu", requiredColumn, ct))
            {
                throw new InvalidOperationException($"V DB chybí sloupec dbo.obsazeni_projektu.{requiredColumn}. Obnovte databázi přes PMTracker_insert_sql nebo spusťte upgrade skript.");
            }
        }

        if (!await HasColumnAsync(dbContext, "dbo.projekt_subsystemy", "poradi", ct))
        {
            throw new InvalidOperationException("V DB chybí sloupec dbo.projekt_subsystemy.poradi. Obnovte databázi přes PMTracker_insert_sql nebo spusťte db_upgrade_1_1_2_project_subsystem_order.sql.");
        }

        foreach (var requiredColumn in new[] { "typ_navrhu", "stav", "payload_json", "created_by_osoba_id", "created_at", "row_version" })
        {
            if (!await HasColumnAsync(dbContext, "dbo.zaznam_navrhy", requiredColumn, ct))
            {
                throw new InvalidOperationException($"V DB chybí sloupec dbo.zaznam_navrhy.{requiredColumn}. Obnovte databázi přes PMTracker_insert_sql nebo spusťte db_upgrade_1_1_3_record_proposals.sql.");
            }
        }

        foreach (var requiredColumn in new[] { "zaznam_id", "osoba_id", "score", "computed_at", "role_weight", "deadline_signal", "milestone_signal" })
        {
            if (!await HasColumnAsync(dbContext, "dbo.zaznam_priority_uzivatelu", requiredColumn, ct))
            {
                throw new InvalidOperationException($"V DB chybí sloupec dbo.zaznam_priority_uzivatelu.{requiredColumn}. Obnovte databázi přes PMTracker_insert_sql nebo spusťte db_upgrade_1_1_4_record_priority_matrix.sql.");
            }
        }

        foreach (var requiredColumn in new[] { "id", "last_full_rebuild_at", "last_full_rebuild_status", "last_full_rebuild_duration_ms", "last_full_rebuild_task_count", "updated_at" })
        {
            if (!await HasColumnAsync(dbContext, "dbo.zaznam_priority_rebuild_state", requiredColumn, ct))
            {
                throw new InvalidOperationException($"V DB chybí sloupec dbo.zaznam_priority_rebuild_state.{requiredColumn}. Obnovte databázi přes PMTracker_insert_sql nebo spusťte db_upgrade_1_1_4_record_priority_matrix.sql.");
            }
        }

        if (!await HasIndexAsync(dbContext, "dbo.zaznam_priority_uzivatelu", "IX_zaznam_priority_uzivatelu_osoba_score_zaznam", ct))
        {
            throw new InvalidOperationException("V DB chybí index IX_zaznam_priority_uzivatelu_osoba_score_zaznam. Obnovte databázi přes PMTracker_insert_sql nebo spusťte db_upgrade_1_1_4_record_priority_matrix.sql.");
        }

        // Authorization unification — Fáze A — migrace db_upgrade_1_2_0
        if (!await HasColumnAsync(dbContext, "authz.roles", "scope", ct))
        {
            throw new InvalidOperationException(
                "V DB chybí sloupec authz.roles.scope. Obnovte databázi přes PMTracker_insert_sql nebo spusťte db_upgrade_1_2_0_authz_role_scope.sql.");
        }

        _logger.LogInformation("SQL startup validace proběhla úspěšně.");
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    private static async Task<bool> HasColumnAsync(
        PmTrackerDbContext dbContext,
        string tableName,
        string columnName,
        CancellationToken ct)
    {
        var connection = dbContext.Database.GetDbConnection();
        var mustClose = connection.State != ConnectionState.Open;
        if (mustClose)
        {
            await connection.OpenAsync(ct);
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

            var result = await command.ExecuteScalarAsync(ct);
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

    private static async Task<int?> GetCharacterMaxLengthAsync(
        PmTrackerDbContext dbContext,
        string tableName,
        string columnName,
        CancellationToken ct)
    {
        var connection = dbContext.Database.GetDbConnection();
        var mustClose = connection.State != ConnectionState.Open;
        if (mustClose)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            var (schemaName, tableOnlyName) = SplitSchemaAndTable(tableName);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT CHARACTER_MAXIMUM_LENGTH
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = @schemaName
                  AND TABLE_NAME = @tableName
                  AND COLUMN_NAME = @columnName
                """;

            var schemaParam = command.CreateParameter();
            schemaParam.ParameterName = "@schemaName";
            schemaParam.Value = schemaName;
            command.Parameters.Add(schemaParam);

            var tableParam = command.CreateParameter();
            tableParam.ParameterName = "@tableName";
            tableParam.Value = tableOnlyName;
            command.Parameters.Add(tableParam);

            var columnParam = command.CreateParameter();
            columnParam.ParameterName = "@columnName";
            columnParam.Value = columnName;
            command.Parameters.Add(columnParam);

            var result = await command.ExecuteScalarAsync(ct);
            return result is null || result == DBNull.Value
                ? null
                : Convert.ToInt32(result);
        }
        finally
        {
            if (mustClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static (string SchemaName, string TableName) SplitSchemaAndTable(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return ("dbo", string.Empty);
        }

        var parts = tableName.Split('.', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2)
        {
            return (parts[0], parts[1]);
        }

        return ("dbo", tableName.Trim());
    }

    private static async Task<bool> HasTableAsync(
        PmTrackerDbContext dbContext,
        string tableName,
        CancellationToken ct)
    {
        var connection = dbContext.Database.GetDbConnection();
        var mustClose = connection.State != ConnectionState.Open;
        if (mustClose)
        {
            await connection.OpenAsync(ct);
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

            var result = await command.ExecuteScalarAsync(ct);
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
        CancellationToken ct)
    {
        var connection = dbContext.Database.GetDbConnection();
        var mustClose = connection.State != ConnectionState.Open;
        if (mustClose)
        {
            await connection.OpenAsync(ct);
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

            var result = await command.ExecuteScalarAsync(ct);
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

    private static async Task<bool> HasIndexAsync(
        PmTrackerDbContext dbContext,
        string tableName,
        string indexName,
        CancellationToken ct)
    {
        var connection = dbContext.Database.GetDbConnection();
        var mustClose = connection.State != ConnectionState.Open;
        if (mustClose)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*)
                FROM sys.indexes
                WHERE object_id = OBJECT_ID(@tableName)
                  AND name = @indexName
                """;

            var tableParam = command.CreateParameter();
            tableParam.ParameterName = "@tableName";
            tableParam.Value = tableName;
            command.Parameters.Add(tableParam);

            var indexParam = command.CreateParameter();
            indexParam.ParameterName = "@indexName";
            indexParam.Value = indexName;
            command.Parameters.Add(indexParam);

            var result = await command.ExecuteScalarAsync(ct);
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
