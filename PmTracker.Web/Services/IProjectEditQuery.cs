using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services;

/// <summary>
/// Dedikovaný single-use-case entry point pro sestavení <see cref="ZaznamEditViewModel"/>
/// při otevření editor modalu / stránky. Orchestrace všech dotazů potřebných pro edit
/// view je na jednom místě (SRP); volající (controllery, RecordProposalService) pracují
/// jen s tímto interfacem, ne s mnoha partials <c>RecordService.EditorQueries</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Roadmap:</b> První iterace (2026-04-23) deleguje na <see cref="IRecordService.BuildZaznamEditAsync(int, CancellationToken)"/>
/// — zavedení třídy jako SRP kotvy. Další kroky (postupně):
/// </para>
/// <list type="number">
/// <item>Nahradit lookup queries přes <see cref="ILookupTableCache"/> (již IMemoryCache-backed) — done jinde.</item>
/// <item>Sjednotit record-scoped data (external links, collaboration ids, harmonogram hodnoty,
/// schedule version) do <b>jedné projected EF Core query</b> s joiny.</item>
/// <item>Nahradit orchestration uvnitř <c>BuildZaznamEditForEntityAsync</c> voláním
/// <c>IProjectEditQuery.GetEditModelAsync</c> a postupně odtud migrovat logiku.</item>
/// <item>Až DB round-trip count klesne na minimum, zvážit, jestli je potřeba paralelizace
/// přes <see cref="Microsoft.EntityFrameworkCore.IDbContextFactory{TContext}"/> — očekáváme,
/// že ne.</item>
/// </list>
/// </remarks>
public interface IProjectEditQuery
{
    /// <summary>
    /// Sestaví kompletní <see cref="ZaznamEditViewModel"/> pro existující záznam s daným id.
    /// </summary>
    Task<ZaznamEditViewModel> GetEditModelAsync(int recordId, CancellationToken ct = default);
}
