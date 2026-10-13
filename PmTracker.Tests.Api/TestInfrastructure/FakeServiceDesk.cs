using PmTracker.ServiceDesk.Contracts;

namespace PmTracker.Tests.Api.TestInfrastructure;

/// <summary>
/// ServiceDesk pro Api testy, které potřebují obsah tiketu (okno vyjádření, kandidáti
/// harmonogramu). Sdílený pro kolekci — každý test si registruje vlastní čísla tiketů.
/// Číslo v <see cref="OfflineCisla"/> simuluje výpadek SD (výjimka jako u SqlClient).
/// </summary>
public sealed class FakeServiceDesk : ITicketingQueryService, IVyjadreniQueryService
{
    public Dictionary<string, HotZaznamDto> Tikety { get; } = new();
    public Dictionary<string, List<HotVyjadreniDto>> Vyjadreni { get; } = new();
    public HashSet<string> OfflineCisla { get; } = new();

    private void ThrowIfOffline(IEnumerable<string> cisla)
    {
        if (cisla.Any(OfflineCisla.Contains))
        {
            throw new InvalidOperationException("ServiceDesk je v testu nedostupný.");
        }
    }

    public Task<HotZaznamDto?> GetZaznamAsync(string cislo, CancellationToken ct)
    {
        ThrowIfOffline([cislo]);
        return Task.FromResult(Tikety.GetValueOrDefault(cislo));
    }

    public Task<IReadOnlyDictionary<string, HotZaznamDto>> GetZaznamyAsync(IReadOnlyCollection<string> cisla, CancellationToken ct)
    {
        ThrowIfOffline(cisla);
        IReadOnlyDictionary<string, HotZaznamDto> map = cisla.Where(Tikety.ContainsKey).ToDictionary(c => c, c => Tikety[c]);
        return Task.FromResult(map);
    }

    public Task<HotKalkulaceDto?> GetAkceptovanouKalkulaciAsync(string cislo, CancellationToken ct)
        => Task.FromResult<HotKalkulaceDto?>(null);

    public Task<IReadOnlyDictionary<string, HotKalkulaceDto>> GetAkceptovaneKalkulaceAsync(IReadOnlyCollection<string> cisla, CancellationToken ct)
        => Task.FromResult<IReadOnlyDictionary<string, HotKalkulaceDto>>(new Dictionary<string, HotKalkulaceDto>());

    public Task<IReadOnlyList<HotVyjadreniDto>> GetVyjadreniForTicketAsync(string cislo6, DateTime? sinceUtc, CancellationToken ct)
    {
        ThrowIfOffline([cislo6]);
        return Task.FromResult<IReadOnlyList<HotVyjadreniDto>>(Vyjadreni.GetValueOrDefault(cislo6) ?? []);
    }

    public Task<IReadOnlyDictionary<string, HotZaznamFingerprintDto>> GetHotZaznamFingerprintsAsync(IReadOnlyCollection<string> cisla6, CancellationToken ct)
    {
        ThrowIfOffline(cisla6);
        IReadOnlyDictionary<string, HotZaznamFingerprintDto> map = cisla6
            .Where(Tikety.ContainsKey)
            .ToDictionary(c => c, c => new HotZaznamFingerprintDto(c, DateTime.Today.AddDays(-30), "Řešení", Tikety[c].TypZaznamu));
        return Task.FromResult(map);
    }

    public Task<IReadOnlyDictionary<string, VyjadreniSecondaryFingerprintDto>> GetVyjadreniSecondaryFingerprintsAsync(IReadOnlyCollection<string> cisla6, CancellationToken ct)
    {
        ThrowIfOffline(cisla6);
        IReadOnlyDictionary<string, VyjadreniSecondaryFingerprintDto> map = cisla6
            .Where(c => Vyjadreni.TryGetValue(c, out var list) && list.Count > 0)
            .ToDictionary(c => c, c => new VyjadreniSecondaryFingerprintDto(c, Vyjadreni[c].Max(v => v.Id), Vyjadreni[c].Count));
        return Task.FromResult(map);
    }
}
