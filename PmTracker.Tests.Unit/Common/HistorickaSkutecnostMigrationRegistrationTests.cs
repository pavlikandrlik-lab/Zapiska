using System.IO;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Services.Schedules;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Common;

/// <summary>db_upgrade_1_4_9 — převod historické skutečnosti (uživatel 2026-10-14).</summary>
public sealed class HistorickaSkutecnostMigrationRegistrationTests
{
    private const string MigrationFile = "db_upgrade_1_4_9_historicka_skutecnost.sql";
    private const string AnalyzaFile = "db_upgrade_1_4_9_historicka_skutecnost_analyza.sql";

    [Fact]
    public void Migrace_JeZaregistrovanaVBootstrapSeznamu_AVKontrolnimSkriptu()
    {
        File.ReadAllText(ResolvePath("docs/technical/06-database-bootstrap-migrations.md"))
            .Should().Contain(MigrationFile);
        File.ReadAllText(ResolvePath("db_check_applied_upgrades.sql"))
            .Should().Contain("db_upgrade_1_4_9_historicka_skutecnost");
    }

    [Fact]
    public void Migrace_BeziVTransakci()
    {
        var sql = File.ReadAllText(ResolvePath(MigrationFile));
        sql.Should().Contain("SET XACT_ABORT ON");
        sql.Should().Contain("BEGIN TRANSACTION");
        sql.Should().Contain("COMMIT TRANSACTION");
    }

    [Fact]
    public void Analyza_JenCte()
    {
        var sql = File.ReadAllText(ResolvePath(AnalyzaFile)).ToUpperInvariant();
        foreach (var zakazane in new[] { "UPDATE ", "DELETE ", "INSERT ", "MERGE ", "DROP ", "ALTER ", "TRUNCATE " })
        {
            sql.Should().NotContain(zakazane, "analytický skript nesmí nic měnit");
        }
    }

    private sealed class ZachytavaciLogger : ILogger<HarmonogramAutomatDoplneniHostedService>
    {
        public List<(LogLevel Level, Exception? Exception)> Zaznamy { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Zaznamy.Add((logLevel, exception));
    }

    /// <summary>Selhání (DB, ServiceDesk) nesmí shodit start aplikace — jen se zaloguje.</summary>
    [Fact]
    public async Task HostedService_KdyzSelze_NespadneALogujeError()
    {
        var scopeFactory = new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var logger = new ZachytavaciLogger();
        var sut = new HarmonogramAutomatDoplneniHostedService(scopeFactory, logger);

        await sut.StartAsync(CancellationToken.None);
        await (sut.ExecuteTask ?? Task.CompletedTask);

        logger.Zaznamy.Should().ContainSingle(z => z.Level == LogLevel.Error && z.Exception != null);
    }
}
