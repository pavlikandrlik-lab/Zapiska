using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Sync;

namespace PmTracker.Web.Services.ActiveDirectory;

public sealed class AdSyncService : IAdSyncService
{
    private const int MaxErrorsStored = 10;

    private readonly PmTrackerDbContext _db;
    private readonly IActiveDirectoryService _ad;
    private readonly ILogger<AdSyncService> _logger;
    private readonly TimeProvider _time;

    public AdSyncService(
        PmTrackerDbContext db,
        IActiveDirectoryService ad,
        ILogger<AdSyncService> logger,
        TimeProvider time)
    {
        _db = db;
        _ad = ad;
        _logger = logger;
        _time = time;
    }

    public async Task<AdSyncResult> SyncAllPeopleAsync(SyncTriggerKind trigger, CancellationToken ct = default)
    {
        var startedAt = _time.GetUtcNow().UtcDateTime;

        var osoby = await _db.Osoby.ToListAsync(ct).ConfigureAwait(false);
        var skippedNoGuid = osoby.Count(o => o.GuidAd is null);
        var withGuids = osoby.Where(o => o.GuidAd is not null).ToList();

        var guids = withGuids.Select(o => o.GuidAd!.Value).Distinct().ToArray();
        if (guids.Length == 0)
        {
            var finishedEmpty = _time.GetUtcNow().UtcDateTime;
            return new AdSyncResult(
                startedAt, finishedEmpty, (long)(finishedEmpty - startedAt).TotalMilliseconds,
                0, 0, skippedNoGuid, 0, Array.Empty<AdSyncErrorItem>());
        }

        var errors = new List<AdSyncErrorItem>();
        var okCount = 0;
        var notFound = 0;

        try
        {
            var response = await _ad.ListByGuidsAsync(guids, ct).ConfigureAwait(false);
            if (!response.Available)
            {
                errors.Add(new AdSyncErrorItem(0, null, response.Message ?? "AD unavailable"));
            }
            else
            {
                var byGuid = response.Persons.ToDictionary(p => p.GuidAd);
                foreach (var osoba in withGuids)
                {
                    if (!byGuid.TryGetValue(osoba.GuidAd!.Value, out var ad))
                    {
                        continue;
                    }

                    try
                    {
                        UpdateOsobaFromAd(osoba, ad);
                        okCount++;
                    }
                    catch (Exception ex)
                    {
                        if (errors.Count < MaxErrorsStored)
                        {
                            errors.Add(new AdSyncErrorItem(osoba.Id, osoba.GuidAd, ex.Message));
                        }
                    }
                }

                notFound = response.NotFoundGuids.Count;
            }

            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AD sync SyncAllPeopleAsync fatal");
            if (errors.Count < MaxErrorsStored)
            {
                errors.Add(new AdSyncErrorItem(0, null, ex.Message));
            }
        }

        var finishedAt = _time.GetUtcNow().UtcDateTime;
        return new AdSyncResult(
            startedAt, finishedAt, (long)(finishedAt - startedAt).TotalMilliseconds,
            okCount, errors.Count, skippedNoGuid, notFound, errors);
    }

    public async Task<AdSinglePersonSyncResult> SyncSinglePersonAsync(int osobaId, CancellationToken ct = default)
    {
        var osoba = await _db.Osoby.FirstOrDefaultAsync(x => x.Id == osobaId, ct).ConfigureAwait(false);
        if (osoba is null)
        {
            return new AdSinglePersonSyncResult(false, false, "Osoba neexistuje");
        }

        if (osoba.GuidAd is null)
        {
            return new AdSinglePersonSyncResult(false, false, "Osoba nemá GuidAd");
        }

        var response = await _ad.ListByGuidsAsync(new[] { osoba.GuidAd.Value }, ct).ConfigureAwait(false);
        if (!response.Available)
        {
            return new AdSinglePersonSyncResult(false, false, response.Message ?? "AD unavailable");
        }

        var adPerson = response.Persons.FirstOrDefault();
        if (adPerson is null)
        {
            return new AdSinglePersonSyncResult(false, false, "Osoba nenalezena v AD");
        }

        UpdateOsobaFromAd(osoba, adPerson);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return new AdSinglePersonSyncResult(true, true, null);
    }

    private static void UpdateOsobaFromAd(OsobaEntity osoba, ActiveDirectoryPersonResult ad)
    {
        osoba.Jmeno = ad.Jmeno;
        osoba.Prijmeni = ad.Prijmeni;
        osoba.Titul = ad.Titul;
        if (!string.IsNullOrWhiteSpace(ad.Email))
        {
            osoba.Email = ad.Email;
        }
        // Company / Department nejsou na OsobaEntity — přeskočit, dokud nebude sloupec.
        // AdLogin se neaktualizuje z AD (je to původní identifikátor).
    }
}
