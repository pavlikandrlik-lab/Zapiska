using PmTracker.Web.Services.Export;

namespace PmTracker.Web.Modules.Export;

public static class ExportModuleServiceCollectionExtensions
{
    public static IServiceCollection AddExportModule(this IServiceCollection services)
    {
        services.AddScoped<IExportTemplateQueries, ExportTemplateQueries>();
        services.AddScoped<IExportTemplateUseCase, ExportTemplateUseCase>();
        services.AddScoped<IWordExportService, OpenXmlWordExportService>();
        return services;
    }
}
