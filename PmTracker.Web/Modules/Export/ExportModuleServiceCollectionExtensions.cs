using PmTracker.Web.Services.Export;
using PmTracker.Web.Modules.Export.Queries;

namespace PmTracker.Web.Modules.Export;

public static class ExportModuleServiceCollectionExtensions
{
    public static IServiceCollection AddExportModule(this IServiceCollection services)
    {
        services.AddScoped<IExportDataStore, ExportDataStore>();
        services.AddScoped<IExportProjektExistsQueryHandler, ExportProjektExistsQueryHandler>();
        services.AddScoped<IExportMeetingProjectIdQueryHandler, ExportMeetingProjectIdQueryHandler>();
        services.AddScoped<IExportCommentProjectionBuilder, ExportCommentProjectionBuilder>();
        services.AddScoped<IExportRoleProjectionBuilder, ExportRoleProjectionBuilder>();
        services.AddScoped<IExportAttendanceProjectionBuilder, ExportAttendanceProjectionBuilder>();
        services.AddScoped<IExportRecordVisibilityEvaluator, ExportRecordVisibilityEvaluator>();
        services.AddScoped<IExportRecordProjectionBuilder, ExportRecordProjectionBuilder>();
        services.AddScoped<IExportTemplateSummaryBuilder, ExportTemplateSummaryBuilder>();
        services.AddScoped<IExportTemplateQueries, ExportTemplateQueries>();
        services.AddScoped<IExportTemplateUseCase, ExportTemplateUseCase>();
        services.AddScoped<IExportQueries, ExportQueries>();
        services.AddScoped<IWordExportHeaderSectionWriter, OpenXmlWordHeaderSectionWriter>();
        services.AddScoped<IWordExportRichHtmlParagraphWriter, OpenXmlWordRichHtmlParagraphWriter>();
        services.AddScoped<IWordExportRecordHeaderWriter, OpenXmlWordRecordHeaderWriter>();
        services.AddScoped<IWordExportRecordCommentsCellWriter, OpenXmlWordRecordCommentsCellWriter>();
        services.AddScoped<IWordExportRecordPeopleCellWriter, OpenXmlWordRecordPeopleCellWriter>();
        services.AddScoped<IWordExportRecordDeadlinesCellWriter, OpenXmlWordRecordDeadlinesCellWriter>();
        services.AddScoped<IWordExportRecordsSectionWriter, OpenXmlWordRecordsSectionWriter>();
        services.AddScoped<IWordExportService, OpenXmlWordExportService>();
        return services;
    }
}
