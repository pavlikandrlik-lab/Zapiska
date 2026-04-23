using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Handlers;

/// <summary>
/// Vertical slice pattern: každý use-case má vlastní třídu <c>XxxHandler</c> s jedinou
/// veřejnou metodou <see cref="HandleAsync"/>. SRP: jeden handler = jedna odpovědnost.
/// Žádný mediator — controllery injectují konkrétní handler přímo (ušetří indirekci
/// a reflection-based dispatch).
/// </summary>
/// <remarks>
/// <para>
/// <b>DRY přes infrastrukturu, ne přes base classes</b>: Sdílená logika (validace,
/// DbContext transakce, audit logging) se sdílí přes injectovaná DI services (policies,
/// repositories, audit writer), nikoli přes dědičnost.
/// </para>
/// <para>
/// <b>Migrace z god-service</b>: Staré <c>ProjectService.XxxAsync</c> metody zůstávají
/// jako tenká fasáda delegující na handler, dokud se z volajících sites postupně migruje
/// na handler přímo. God-service se ztenčuje s každým PR, nikoli big-bang refactor.
/// </para>
/// </remarks>
public interface IRequestHandler<TRequest, TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
}
