using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Data;

public interface IHarmonogramService
{
    Task<HarmonogramSchemaDefinition> GetActiveHarmonogramSchemaAsync(CancellationToken ct = default);
    Task<HarmonogramSchemaDefinition> GetSchemaForRecordAsync(ProjektovyZaznamEntity record, CancellationToken ct = default);
    Task<HarmonogramSchemaDefinition> GetSchemaForRecordAsync(int schemaVersion, CancellationToken ct = default);
    IReadOnlyList<RecordScheduleTypeDefinition> BuildRecordScheduleTypeDefinitions(HarmonogramSchemaDefinition schema);
    IReadOnlyList<HarmonogramVypocetKroku> BuildHarmonogramVypocetPublic(DateTime datumZalozeni, IReadOnlyList<HarmonogramTypPar> typy, IReadOnlyDictionary<int, int>? hodnoty);
    HarmonogramSouhrnViewModel BuildHarmonogramSouhrn(IReadOnlyList<HarmonogramVypocetKroku> kroky, DateTime terminUkolu);

    /// <summary>
    /// FIX 2026-05-05: rozšířený souhrn s projekcí uplynulého času pro Stav: Nestíháme.
    /// Pokud poslední krok (krok 10) nemá vyplněnou skutečnost, promítne se aktuální datum
    /// do projekce dokončení (= zohlední dny od posledního vyplněného kroku, kdy se nic nepohnulo).
    /// Užívá se v edit modalu pro pole "Stav".
    /// </summary>
    HarmonogramSouhrnViewModel BuildHarmonogramSouhrn(
        IReadOnlyList<HarmonogramVypocetKroku> kroky,
        DateTime terminUkolu,
        DateTime today,
        bool lastStepHasActual);

    Task<int> EnsurePersistedActiveHarmonogramSchemaVersionAsync(CancellationToken ct = default);
}
