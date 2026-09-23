using System.Globalization;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace PmTracker.Web.Services.Diagnostics;

/// <summary>Vstup pro sestavení diagnostického výpisu.</summary>
public sealed record DiagnosticLogRequest(
    DateTime TimestampUtc,
    string ErrorCode,
    string TraceId,
    string RequestLine,
    string Message,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null,
    string? Details = null,
    Exception? Exception = null);

/// <summary>
/// Skládá diagnostický výpis, který uživatel vidí u chyby a může ho zkopírovat.
/// Vytaženo 2026-09-08 z BaseController, kde ho měla jen AJAX cesta — obyčejný GET,
/// který spadl, končil na holé stránce bez jediného detailu.
///
/// Hodnoty formuláře se do výpisu záměrně nedávají: QW-7 (2026-04-22) je odstranil
/// kvůli úniku osobních údajů a strážní test to hlídá.
/// </summary>
public static class DiagnosticLogBuilder
{
    public static string Build(DiagnosticLogRequest request)
    {
        var builder = new StringBuilder(2048);
        builder.Append("TimestampUtc: ").AppendLine(request.TimestampUtc.ToString("O"));
        builder.Append("ErrorCode: ").AppendLine(request.ErrorCode);
        builder.Append("TraceId: ").AppendLine(request.TraceId);
        builder.Append("Request: ").AppendLine(request.RequestLine);
        builder.Append("Message: ").AppendLine(request.Message);

        if (!string.IsNullOrWhiteSpace(request.Details))
        {
            builder.AppendLine("Details:");
            builder.AppendLine(request.Details.Trim());
        }

        if (request.FieldErrors is not null)
        {
            AppendFieldErrorSection(builder, request.FieldErrors);
        }

        if (request.Exception is not null)
        {
            builder.AppendLine("Exception:");
            builder.AppendLine(request.Exception.ToString());
            AppendSqlAndEfDetails(builder, request.Exception);
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendFieldErrorSection(StringBuilder builder, IReadOnlyDictionary<string, string[]> fieldErrors)
    {
        if (fieldErrors.Count == 0)
        {
            return;
        }

        builder.AppendLine("FieldErrors:");
        foreach (var pair in fieldErrors.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            var messages = pair.Value
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Select(message => message.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (messages.Length == 0)
            {
                continue;
            }

            builder.Append("  ")
                .Append(pair.Key)
                .Append(": ")
                .AppendLine(string.Join(" | ", messages));
        }
    }

    /// <summary>
    /// FIX 2026-05-04: <see cref="Exception.ToString"/> sice projde InnerException řetězec, ale
    /// <see cref="SqlException"/> má bohatou diagnostiku (Number, State, Class, Server, Procedure,
    /// LineNumber + <see cref="SqlException.Errors"/> kolekci) která se v default ToString nevypisuje.
    /// EF Core <see cref="DbUpdateException"/> navíc drží <see cref="DbUpdateException.Entries"/>
    /// s entitami které selhaly při SaveChanges. Tato pomocná metoda projde celý řetězec a vypíše
    /// vše co user potřebuje pro debugging "UNEXPECTED_SERVER_ERROR" pádů (typicky FK violation,
    /// unique constraint, NOT NULL, deadlock, schema drift).
    /// </summary>
    private static void AppendSqlAndEfDetails(StringBuilder builder, Exception rootException)
    {
        var sectionHeaderEmitted = false;
        var current = rootException;
        var depth = 0;
        while (current is not null && depth < 10)
        {
            if (current is SqlException sqlEx)
            {
                if (!sectionHeaderEmitted)
                {
                    builder.AppendLine("SqlServer/EFCore details:");
                    sectionHeaderEmitted = true;
                }
                builder.Append("  [SqlException @ depth=")
                    .Append(depth)
                    .Append("] Number=")
                    .Append(sqlEx.Number)
                    .Append(" State=")
                    .Append(sqlEx.State)
                    .Append(" Class=")
                    .Append(sqlEx.Class)
                    .Append(" Server=")
                    .Append(sqlEx.Server ?? "(null)")
                    .Append(" Procedure=")
                    .Append(string.IsNullOrEmpty(sqlEx.Procedure) ? "(none)" : sqlEx.Procedure)
                    .Append(" LineNumber=")
                    .AppendLine(sqlEx.LineNumber.ToString(CultureInfo.InvariantCulture));
                builder.Append("    Message: ").AppendLine(sqlEx.Message);
                if (sqlEx.Errors is { Count: > 0 } errs)
                {
                    for (var i = 0; i < errs.Count; i++)
                    {
                        var err = errs[i];
                        builder.Append("    Errors[").Append(i).Append("]: Number=")
                            .Append(err.Number).Append(" State=").Append(err.State)
                            .Append(" Class=").Append(err.Class)
                            .Append(" Line=").Append(err.LineNumber)
                            .Append(" Procedure=")
                            .Append(string.IsNullOrEmpty(err.Procedure) ? "(none)" : err.Procedure)
                            .Append(" | ").AppendLine(err.Message);
                    }
                }
            }

            if (current is DbUpdateException efEx)
            {
                if (!sectionHeaderEmitted)
                {
                    builder.AppendLine("SqlServer/EFCore details:");
                    sectionHeaderEmitted = true;
                }
                builder.Append("  [DbUpdateException @ depth=")
                    .Append(depth)
                    .Append("] EntryCount=")
                    .AppendLine(efEx.Entries.Count.ToString(CultureInfo.InvariantCulture));
                var entryIndex = 0;
                foreach (var entry in efEx.Entries)
                {
                    if (entryIndex >= 5)
                    {
                        builder.AppendLine("    … (truncated, additional entries omitted)");
                        break;
                    }
                    string keyDescription;
                    try
                    {
                        var key = entry.Metadata.FindPrimaryKey();
                        keyDescription = key is null
                            ? "(no PK metadata)"
                            : string.Join(",", key.Properties.Select(p =>
                                $"{p.Name}={entry.Property(p.Name).CurrentValue ?? "(null)"}"));
                    }
                    catch (Exception readEx)
                    {
                        keyDescription = $"(key read failed: {readEx.GetType().Name})";
                    }
                    builder.Append("    Entry[").Append(entryIndex).Append("] ")
                        .Append(entry.Metadata.ClrType.Name)
                        .Append(" State=").Append(entry.State)
                        .Append(" PK=").AppendLine(keyDescription);
                    entryIndex++;
                }
            }

            current = current.InnerException;
            depth++;
        }
    }
}
