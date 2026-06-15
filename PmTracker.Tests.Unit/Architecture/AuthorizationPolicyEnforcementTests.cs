using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Architekturní testy vynucující, že každá mutující akce v controllerech
/// má alespoň základní autorizační atribut ([Authorize] nebo [AllowAnonymous]),
/// a ideálně specifickou policy (permission:xxx).
/// </summary>
public class AuthorizationPolicyEnforcementTests
{
    // Mutující HTTP verby — POST, PUT, DELETE.
    private static readonly HashSet<string> MutatingHttpMethods =
        new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "DELETE" };

    // --------------------------------------------------------------------------
    // Test 1 (HARD): každá mutující akce musí mít [Authorize] nebo [AllowAnonymous]
    // --------------------------------------------------------------------------

    [Fact]
    public void Every_Mutating_Action_Must_Have_AuthorizationAttribute()
    {
        // Arrange + Act
        var violations = FindMutatingActions()
            .Where(x => !HasAnyAuthorizationDecision(x.Controller, x.Method))
            .Select(x => $"{x.Controller.FullName}.{x.Method.Name}")
            .ToList();

        // Assert
        violations.Should().BeEmpty(
            "každá POST/PUT/DELETE action musí mít [Authorize] nebo [AllowAnonymous]. " +
            "Seznam chybějících: " + string.Join(", ", violations));
    }

    // --------------------------------------------------------------------------
    // Test 2 (SOFT): každá mutující akce by měla mít specifickou policy,
    // nebo být explicitně allowlistována s odůvodněním.
    // --------------------------------------------------------------------------

    [Fact]
    public void Every_Mutating_Action_Should_Have_SpecificPolicy_OrBeExplicitlyAllowlisted()
    {
        var allowlist = new HashSet<string>
        {
            // ---- ZaznamyController ----

            // Save: OR-composite — records.edit OR (records.schedule.edit OR records.schedule.add)
            // + projektId přichází z form body, není v route → PermissionAuthorizationHandler
            // nemůže provést per-project check.
            "PmTracker.Web.Controllers.ZaznamyController.Save",

            // DeleteRecord: projektId z form body (DeleteRecordCommand.ProjektId), ne route.
            "PmTracker.Web.Controllers.ZaznamyController.DeleteRecord",

            // AssignMeetingIdentifier: projektId z form body (AssignMeetingIdentifierCommand.ProjektId).
            "PmTracker.Web.Controllers.ZaznamyController.AssignMeetingIdentifier",

            // ---- ProjektyController ----

            // SaveProject: dynamická create/edit větev — projects.create OR projects.edit
            // Nelze vyjádřit jedinou policy (create se provádí bez projektId v route).
            "PmTracker.Web.Controllers.ProjektyController.SaveProject",

            // JednaniController.Save: dynamická create/edit větev — meetings.create OR meetings.edit
            // projektId je v command (form body), ne v route pro create branch.
            "PmTracker.Web.Controllers.JednaniController.Save",

            // ---- ProjektyController team-management (H-1 IDOR fix) ----
            //
            // Všechny následující team-management akce dostávají projektId z form body
            // (command.ProjektId) nebo z query parametru (projektId) — NIKDY z route.
            // PermissionAuthorizationHandler čte projektId pouze z RouteValues, takže
            // [Authorize(Policy="permission:team.xxx")] by degradoval na global-only
            // check a zablokoval by oprávněné project-scoped uživatele (+ byl by to latent
            // IDOR, kdyby byl team klíč grantován globálně).
            // Per-project check je proveden v body přes ExecuteTeamValidatedActionAsync /
            // ExecuteTeamActionAsync, které volají CurrentUserContext.HasPermission(
            // per-action team klíč, projektId) — per-action redesign 2026-04-23.
            "PmTracker.Web.Controllers.ProjektyController.SaveTeamMember",
            "PmTracker.Web.Controllers.ProjektyController.RemoveTeamMember",
            "PmTracker.Web.Controllers.ProjektyController.AssignProjectRole",
            "PmTracker.Web.Controllers.ProjektyController.DeactivateProjectRole",
            "PmTracker.Web.Controllers.ProjektyController.AssignProjectSubsystem",
            "PmTracker.Web.Controllers.ProjektyController.ReorderProjectSubsystem",
            "PmTracker.Web.Controllers.ProjektyController.DeactivateProjectSubsystem",
            "PmTracker.Web.Controllers.ProjektyController.AssignProjectSubsystemRole",
            "PmTracker.Web.Controllers.ProjektyController.DeactivateProjectSubsystemRole",

            // ---- JednaniController ----

            // SaveStatus: projektId je načten z DB (GetMeetingProjectIdAsync), není v route.
            // Body provádí: HasPermission(MeetingsEdit, projektId).
            "PmTracker.Web.Controllers.JednaniController.SaveStatus",

            // SaveAttendance: projektId přichází z form body, ne z route.
            // Body provádí: HasPermission(MeetingsEdit, projektId).
            "PmTracker.Web.Controllers.JednaniController.SaveAttendance",

            // AddMeetingParticipant: command.ProjektId z form body, ne z route.
            // Body provádí: HasPermission(MeetingsEdit, command.ProjektId).
            "PmTracker.Web.Controllers.JednaniController.AddMeetingParticipant",

            // SaveNotes: projektId z form body, ne z route. Duální gate
            // (meetings.notes.edit OR meetings.notes.subsystemlead) + service filter
            // (CommentAuthorizationPolicy.CanSaveMeetingNote) — OR-composite nejde vyjádřit
            // jednou policy. Per-action redesign 2026-04-23.
            "PmTracker.Web.Controllers.JednaniController.SaveNotes",

            // ---- NavrhyController ----

            // ApproveProposal / RejectProposal: per-action 2026-04-23 mají vlastní policy
            // na atributu (proposals.accept / proposals.reject), ale projektId přichází
            // z form body, ne z route → PermissionAuthorizationHandler nemůže provést
            // per-project check. Body-level gate přes RecordProposalAuthorizationPolicy
            // (IDOR admin bypass pro proposals.edit.any).
            "PmTracker.Web.Controllers.NavrhyController.ApproveProposal",
            "PmTracker.Web.Controllers.NavrhyController.RejectProposal",

            // RejectAndTakeOverCreateProposal: proposals.takeover (per-action redesign)
            // + projektId z body.
            "PmTracker.Web.Controllers.NavrhyController.RejectAndTakeOverCreateProposal",

            // RejectAndEditProposal: proposals.reject + records.edit — OR composite
            // nelze vyjádřit jednou policy (body-level check).
            "PmTracker.Web.Controllers.NavrhyController.RejectAndEditProposal",

            // ---- VyzvyController ----

            // Zalozit: projektId z form body (ZaloztRequest.ProjektId), ne z route.
            // Body provádí: CanAccessProject(request.ProjektId).
            "PmTracker.Web.Controllers.VyzvyController.Zalozit",

            // ZmenitStav: vyzvaId z formu, projektId se načítá z DB.
            // Body provádí: CanAccessProject(vyzva.ProjektId).
            "PmTracker.Web.Controllers.VyzvyController.ZmenitStav",

            // SetZaradid: pracuje s ExterniOdkazId — projektId není dostupný v route ani body.
            // projektId se resolvuje z ExterniOdkazId v service; service volá
            // IAuthorizationService.HasPermissionAsync(VyzvyPnfAssign, projektId) — per-action
            // redesign 2026-04-23; ověřeno VyzvaServiceAssignmentAuthzTests.
            "PmTracker.Web.Controllers.VyzvyController.SetZaradid",

            // Prerdit: pracuje s ExterniOdkazId a CilovaVyzvaId — žádný projektId v route.
            // projektId se resolvuje z ExterniOdkazId/CilovaVyzvaId v service; service volá
            // IAuthorizationService.HasPermissionAsync(VyzvyPnfReassign) na zdrojovém i cílovém
            // projektu — per-action redesign 2026-04-23; ověřeno VyzvaServiceAssignmentAuthzTests.
            "PmTracker.Web.Controllers.VyzvyController.Prerdit",

            // ---- ScheduleController ----

            // Recalc: kalkulační endpoint pro preview harmonogramu (stateless výpočet).
            // Nepotřebuje per-project autorizaci — pracuje jen se vstupy bez DB operace.
            // Třídy kontroleru nemá [Authorize], žádná policy nemůže být aplikována.
            "PmTracker.Web.Controllers.ScheduleController.Recalc",

            // ---- ExterniOdkazController (Plán B) ----

            // Sync: projektId přichází z form body (POST /ExterniOdkaz/Sync) — není v route.
            // PermissionAuthorizationHandler čte projektId pouze z RouteValues, takže
            // [Authorize(Policy="permission:records.edit")] by degradoval na global-only check.
            // Body provádí: _authz.HasPermissionAsync(osobaId, RecordsEdit, projektId).
            "PmTracker.Web.Controllers.ExterniOdkazController.Sync",

            // ---- VyjadreniModalController (Plán C) ----

            // CreateVazba/DeleteVazba: projektId přichází z JSON body, ne z route.
            // Manuální _authz.HasPermissionAsync(osobaId, RecordsEdit, projektId) — stejný
            // vzor jako ExterniOdkazController.Sync.
            "PmTracker.Web.Controllers.VyjadreniModalController.CreateVazba",
            "PmTracker.Web.Controllers.VyjadreniModalController.DeleteVazba",

            // Refresh (T6 direct sync, sd-sync-revise Task 14): projektId přichází z form body,
            // ne z route. Manuální _authz.HasPermissionAsync(osobaId, RecordsEdit, projektId).
            "PmTracker.Web.Controllers.VyjadreniModalController.Refresh",

            // ---- HarmonogramController (Plán 4 Feature C) ----

            // SelectCandidate / PreviewSync: projektId se resolvuje z DB (ZaznamId →
            // ProjektovyZaznam.ProjektId). Není v route ani body, proto manuální
            // _authz.HasPermissionAsync(osobaId, RecordsScheduleEdit, projektId).
            // PreviewSync (DESIGN-9-C, 2026-05-01) — staging endpoint s ComputePlan, žádný DB write.
            // Pozn.: per-krok ToggleRezim + BulkSetRezim odstraněny (2026-06 datum-model úklid) —
            // master switch persistuje rezim při Save přes HarmonogramRezim, ne přes endpoint.
            "PmTracker.Web.Controllers.HarmonogramController.SelectCandidate",
            "PmTracker.Web.Controllers.HarmonogramController.PreviewSync",
        };

        // Act
        var violations = FindMutatingActions()
            .Where(x => !HasAllowAnonymous(x.Controller, x.Method))
            .Where(x => !HasSpecificPolicy(x.Method))
            .Select(x => $"{x.Controller.FullName}.{x.Method.Name}")
            .Where(fqn => !allowlist.Contains(fqn))
            .ToList();

        // Assert
        violations.Should().BeEmpty(
            "Mutating actions by měly používat [Authorize(Policy=\"permission:...\")]. " +
            "Pokud je potřeba body-level check kvůli OR-composite logice, přidej FQN do allowlistu " +
            "s komentářem vysvětlujícím proč. " +
            "Chybějící: " + string.Join(", ", violations));
    }

    // --------------------------------------------------------------------------
    // Helpers
    // --------------------------------------------------------------------------

    private record ActionDescriptor(Type Controller, MethodInfo Method);

    /// <summary>
    /// Vrátí všechny veřejné akce MVC controllerů, které jsou mapovány na mutující HTTP metody
    /// (POST, PUT, DELETE). Pracuje přes reflexi na assembly PmTracker.Web.
    /// </summary>
    private static IEnumerable<ActionDescriptor> FindMutatingActions()
    {
        var assembly = typeof(Program).Assembly;

        return assembly.GetTypes()
            .Where(IsController)
            .SelectMany(controller =>
                controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(IsMutatingAction)
                    .Select(method => new ActionDescriptor(controller, method)));
    }

    private static bool IsController(Type type) =>
        !type.IsAbstract
        && !type.IsGenericTypeDefinition
        && typeof(ControllerBase).IsAssignableFrom(type)
        && (type.Name.EndsWith("Controller", StringComparison.Ordinal)
            || type.GetCustomAttribute<ControllerAttribute>() is not null
            || type.GetCustomAttribute<ApiControllerAttribute>() is not null);

    private static bool IsMutatingAction(MethodInfo method)
    {
        if (method.IsSpecialName) return false;

        // Explicitní HTTP method atributy — použij GetCustomAttributes (plural) pro
        // robustnost vůči partial classes a atributové dědičnosti.
        if (method.GetCustomAttributes<HttpPostAttribute>(inherit: false).Any()) return true;
        if (method.GetCustomAttributes<HttpPutAttribute>(inherit: false).Any()) return true;
        if (method.GetCustomAttributes<HttpDeleteAttribute>(inherit: false).Any()) return true;
        if (method.GetCustomAttributes<HttpPatchAttribute>(inherit: false).Any()) return true;

        // Pokud není žádný HTTP method atribut, MVC provede routing na základě jména.
        // Akce bez atributu nepoužívají POST konvenci z reflexe — přeskočíme je.
        return false;
    }

    /// <summary>
    /// Vrátí true, pokud metoda nebo její controller má [Authorize] (s nebo bez policy) nebo [AllowAnonymous].
    /// </summary>
    private static bool HasAnyAuthorizationDecision(Type controller, MethodInfo method)
    {
        return HasAuthorize(method)
               || HasAuthorize(controller)
               || HasAllowAnonymous(controller, method);
    }

    private static bool HasAuthorize(MemberInfo member) =>
        member.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any();

    private static bool HasAllowAnonymous(Type controller, MethodInfo method) =>
        method.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null
        || controller.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null;

    /// <summary>
    /// Vrátí true, pokud metoda má [Authorize(Policy = "permission:...")] — specifická policy.
    /// Bare [Authorize] bez policy se nepočítá.
    /// </summary>
    private static bool HasSpecificPolicy(MethodInfo method) =>
        method.GetCustomAttributes<AuthorizeAttribute>(inherit: false)
            .Any(a => !string.IsNullOrWhiteSpace(a.Policy));
}
