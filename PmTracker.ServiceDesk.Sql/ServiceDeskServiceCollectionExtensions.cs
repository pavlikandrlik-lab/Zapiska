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
                sql.CommandTimeout(options.CommandTimeoutSeconds)));

        services.AddScoped<SqlTicketingQueryService>();
        services.AddScoped<ITicketingQueryService>(sp =>
            new CachingTicketingQueryService(sp.GetRequiredService<SqlTicketingQueryService>()));
        services.AddScoped<IVyjadreniQueryService, SqlVyjadreniQueryService>();
        services.AddScoped<IInformacniSystemQueryService, SqlInformacniSystemQueryService>();

        return services;
    }
}
