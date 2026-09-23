using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Security;

/// <summary>
/// Jediný zdroj pravdy pro „smí uživatel otevřít inline editor záznamu?" (tužka v seznamu záznamů,
/// tlačítko „Upravit" v harmonogramu → obojí odkazuje na <c>ZaznamyController.Edit</c>).
///
/// Mirror serverové gate v <see cref="Controllers.ZaznamyController"/>.Edit: editor je přístupný
/// jen s <c>records.edit</c> NEBO <c>records.schedule.edit</c>. Návrhový klíč
/// <c>proposals.schedule.create</c> editaci NEUMOŽŇUJE (má vlastní návrhový workflow), takže
/// afordance tužky/Upravit ho NESMÍ zahrnovat — jinak UI ukáže akci, kterou server odepře (403).
/// Vedoucí subsystému (má proposals.schedule.create, ne records.edit) proto tužku ani vidět nemá.
/// </summary>
public static class RecordEditorAffordancePolicy
{
    public static bool CanOpenEditor(CurrentUserContextViewModel user, int projektId)
        => user.HasPermission(PermissionKeys.RecordsEdit, projektId)
           || user.HasPermission(PermissionKeys.RecordsScheduleEdit, projektId);
}
