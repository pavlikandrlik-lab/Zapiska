using PmTracker.ServiceDesk.Contracts;

namespace PmTracker.Tests.Integration.TestInfrastructure;

/// <summary>
/// Spec 2026-10-14 R2 — test double pro <see cref="IVyjadreniQueryService"/>: každý tiket hlásí
/// zadaný typ záznamu (PMP/PNF). Automat (HarmonogramSkutecnostSyncService) podle typu mapuje
/// navázané vyjádření na krok harmonogramu; ve fixture je ServiceDesk jinak vypnutý.
/// </summary>
internal sealed class PevnyTypTiketuVyjadreniQueryService(string typZaznamu) : IVyjadreniQueryService
{
    public Task<IReadOnlyList<HotVyjadreniDto>> GetVyjadreniForTicketAsync(string cislo6, DateTime? sinceUtc, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<HotVyjadreniDto>>([]);

    public Task<IReadOnlyDictionary<string, HotZaznamFingerprintDto>> GetHotZaznamFingerprintsAsync(
        IReadOnlyCollection<string> cisla6, CancellationToken ct)
        => Task.FromResult<IReadOnlyDictionary<string, HotZaznamFingerprintDto>>(
            cisla6.ToDictionary(c => c, c => new HotZaznamFingerprintDto(c, new DateTime(2026, 9, 1), null, typZaznamu)));

    public Task<IReadOnlyDictionary<string, VyjadreniSecondaryFingerprintDto>> GetVyjadreniSecondaryFingerprintsAsync(
        IReadOnlyCollection<string> cisla6, CancellationToken ct)
        => Task.FromResult<IReadOnlyDictionary<string, VyjadreniSecondaryFingerprintDto>>(
            new Dictionary<string, VyjadreniSecondaryFingerprintDto>());
}
