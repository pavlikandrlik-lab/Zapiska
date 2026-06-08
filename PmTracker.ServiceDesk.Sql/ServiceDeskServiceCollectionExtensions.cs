using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PmTracker.ServiceDesk.Contracts;

namespace PmTracker.ServiceDesk.Sql;

public static class ServiceDeskServiceCollectionExtensions
{
    public static IServiceCollection AddServiceDeskIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<TicketingOptions>()
            .Bind(configuration.GetSection(TicketingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var options = configuration.GetSection(TicketingOptions.SectionName).Get<TicketingOptions>()
            ?? new TicketingOptions();

        if (!options.Enabled)
        {
            services.AddScoped<ITicketingQueryService, DisabledTicketingQueryService>();
            services.AddScoped<IVyjadreniQueryService, DisabledVyjadreniQueryService>();
            services.AddScoped<IInformacniSystemQueryService, DisabledInformacniSystemQueryService>();
            return services;
        }

        var connectionString = configuration.GetConnectionString(options.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"ServiceDesk integrace je zapnutá (Ticketing:Enabled=true), ale ConnectionString '{options.ConnectionStringName}' je prázdný. " +
                $"Buď nastav connection string, nebo vypni Ticketing:Enabled.");
        }

        services.AddDbContext<TicketingReadOnlyDbContext>(opts =>
            opts.UseSqlServer(connectionString, sql =>
            {
                sql.CommandTimeout(options.CommandTimeoutSeconds);
                // FIX 2026-05-04: real intranetNEW SQL Server je starší (pre-2016 nebo compatibility
                // level < 130) — nepodporuje OPENJSON. EF Core 8 by jinak `Contains()` na list
                // přeložil na "WHERE x IN (SELECT value FROM OPENJSON(@p) WITH (value int '$'))",
                // což hází 'Incorrect syntax near $'. Compatibility 120 přepne EF na klasické
                // parametr-per-prvek IN clause (= SQL 2014 kompatibilní), které funguje na všech
                // SQL Serverech od 2008+.
                sql.UseCompatibilityLevel(120);
            }));

        services.AddScoped<SqlTicketingQueryService>();
        services.AddScoped<ITicketingQueryService>(sp =>
            new CachingTicketingQueryService(sp.GetRequiredService<SqlTicketingQueryService>()));
        services.AddScoped<IVyjadreniQueryService, SqlVyjadreniQueryService>();
        services.AddScoped<IInformacniSystemQueryService, SqlInformacniSystemQueryService>();

        return services;
    }
}
