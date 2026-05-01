using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels.SDConnector;
using PmTracker.Web.Services.Audit;
using PmTracker.Web.Services.Security;
using PmTracker.Web.Services.ServiceDesk;

namespace PmTracker.Web.Controllers;

/// <summary>
/// Plán C Task 19 — diagnostická stránka /SDConnector.
/// Admin view stavu harvestu: počet externích odkazů, kolik je právě vytěženo,
/// per-řádek odkaz na re-harvest (přes existující endpoint <c>POST /Vyjadreni/ReHarvest</c>).
/// </summary>
[Authorize(Policy = "permission:settings.sd.view")]
[Route("SDConnector")]
public sealed class SDConnectorController : Controller
{
    // Regex kompilovaný jen jednou — používá se v Inspect i Load akcích pro validaci 6-ciferného čísla tiketu.
    private static readonly Regex Cislo6Regex = new(@"^\d{6}$", RegexOptions.Compiled);

    private readonly PmTrackerDbContext _db;
    private readonly IVyjadreniHarvestService _harvest;
    private readonly TimeProvider _time;
    private readonly IOptions<TicketingOptions> _ticketingOptions;
    private readonly ILogger<SDConnectorController> _logger;
    private readonly IAuditWriteService _auditWriteService;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly ITicketingQueryService _ticketing;
    private readonly IVyjadreniQueryService _vyjadreni;
    private readonly AdLoginCache _adCache;

    public SDConnectorController(
        PmTrackerDbContext db,
        IVyjadreniHarvestService harvest,
        TimeProvider time,
        IOptions<TicketingOptions> ticketingOptions,
        ILogger<SDConnectorController> logger,
        IAuditWriteService auditWriteService,
        ICurrentUserAccessor currentUser,
        ITicketingQueryService ticketing,
        IVyjadreniQueryService vyjadreni,
        AdLoginCache adCache)
    {
        _db = db;
        _harvest = harvest;
        _time = time;
        _ticketingOptions = ticketingOptions;
        _logger = logger;
        _auditWriteService = auditWriteService;
        _currentUser = currentUser;
        _ticketing = ticketing;
        _vyjadreni = vyjadreni;
        _adCache = adCache;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var options = _ticketingOptions.Value;
        var nowUtc = _time.GetUtcNow().UtcDateTime;
        var lastDay = nowUtc.AddDays(-1);

        var vm = new SDConnectorViewModel
        {
            TicketingEnabled = options.Enabled,
            TicketingConnectionName = options.ConnectionStringName
        };

        var eoRaw = await _db.ZaznamExterniOdkazy.AsNoTracking()
            .Where(x => x.Cislo != null && x.Cislo != "")
            .Select(x => new { x.Id, x.ZaznamId, x.Cislo, x.LastHarvestedAt })
            .ToListAsync(ct).ConfigureAwait(false);
        var zaznamIds = eoRaw.Select(x => x.ZaznamId).Distinct().ToArray();
        var projektByZaznam = await _db.ProjektoveZaznamy.AsNoTracking()
            .Where(x => zaznamIds.Contains(x.Id))
            .Select(x => new { x.Id, x.ProjektId })
            .ToDictionaryAsync(x => x.Id, x => x.ProjektId, ct).ConfigureAwait(false);

        var bindings = await _db.VyjadreniVazby.AsNoTracking()
            .Where(x => x.Stav == (byte)VazbaStav.Active)
            .GroupBy(x => x.ExterniOdkazId)
            .Select(g => new
            {
                ExterniOdkazId = g.Key,
                Total = g.Count(),
                Auto = g.Count(v => v.Source == (byte)VazbaSource.Auto),
                Manual = g.Count(v => v.Source == (byte)VazbaSource.Manual),
            })
            .ToDictionaryAsync(x => x.ExterniOdkazId, ct).ConfigureAwait(false);

        var rows = eoRaw.Select(eo =>
        {
            bindings.TryGetValue(eo.Id, out var b);
            projektByZaznam.TryGetValue(eo.ZaznamId, out var projektId);
            return new SDConnectorExterniOdkazRow
            {
                ExterniOdkazId = eo.Id,
                ZaznamId = eo.ZaznamId,
                ProjektId = projektId,
                Cislo = eo.Cislo!,
                LastHarvestedAt = eo.LastHarvestedAt,
                ActiveBindingsCount = b?.Total ?? 0,
                AutoBindingsCount = b?.Auto ?? 0,
                ManualBindingsCount = b?.Manual ?? 0
            };
        })
        .OrderByDescending(x => x.LastHarvestedAt ?? DateTime.MinValue)
        .ToList();

        vm.ExterniOdkazCount = rows.Count;
        vm.HarvestedInLastDayCount = rows.Count(r => r.LastHarvestedAt.HasValue && r.LastHarvestedAt.Value >= lastDay);
        vm.NeverHarvestedCount = rows.Count(r => !r.LastHarvestedAt.HasValue);
        vm.Rows = rows;

        return View(vm);
    }

    [HttpPost("ReHarvest")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "permission:vyjadreni.reharvest")]
    public async Task<IActionResult> ReHarvest([FromForm] int externiOdkazId, CancellationToken ct)
    {
        if (externiOdkazId <= 0)
        {
            TempData["SDConnectorError"] = "Neplatný externiOdkazId.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var result = await _harvest.ReHarvestTicketAsync(externiOdkazId, ct).ConfigureAwait(false);

            // Review finding S-3: audit destruktivní admin akce.
            await _auditWriteService.WriteAsync(_currentUser.OsobaId, new AuditWriteEntry(
                AuditActionType.Update,
                AuditEntityType.SdExterniOdkaz,
                externiOdkazId.ToString(CultureInfo.InvariantCulture),
                BeforeState: null,
                AfterState: new
                {
                    Action = "reharvest",
                    Source = "SDConnectorController",
                    result.Fetched,
                    result.Created,
                    result.Superseded,
                    result.Skipped
                }), ct).ConfigureAwait(false);

            TempData["SDConnectorMessage"] =
                $"Re-harvest id={externiOdkazId}: načteno {result.Fetched}, vytvořeno {result.Created}, superseded {result.Superseded}, preskočeno {result.Skipped}.";
        }
        catch (Exception ex)
        {
            // Review finding S-3: full exception server-side, do UI jen TraceId.
            _logger.LogError(ex, "ReHarvest selhal pro externí odkaz {Id}.", externiOdkazId);
            // Review finding M-R2-3: audit entry i při selhání, aby byla stopa pokusu.
            try
            {
                await _auditWriteService.WriteAsync(_currentUser.OsobaId, new AuditWriteEntry(
                    AuditActionType.Update,
                    AuditEntityType.SdExterniOdkaz,
                    externiOdkazId.ToString(CultureInfo.InvariantCulture),
                    BeforeState: null,
                    AfterState: new
                    {
                        Action = "reharvest.failed",
                        Source = "SDConnectorController",
                        TraceId = HttpContext.TraceIdentifier
                    }), ct).ConfigureAwait(false);
            }
            catch (Exception auditEx)
            {
                _logger.LogWarning(auditEx, "ReHarvest: zápis auditu selhání selhal pro {Id}.", externiOdkazId);
            }
            TempData["SDConnectorError"] = $"Re-harvest selhal. TraceId: {HttpContext.TraceIdentifier}";
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Plán 2 Feature A — admin diagnostická podstránka. Renderuje prázdný inspector;
    /// pokud URL obsahuje validní <c>?cislo=XXXXXX</c>, předvyplníme input (JS pak auto-loadne data).
    /// </summary>
    [HttpGet("Inspect")]
    public IActionResult Inspect(string? cislo = null)
    {
        var initial = !string.IsNullOrEmpty(cislo) && Cislo6Regex.IsMatch(cislo) ? cislo : null;
        var vm = new SDConnectorInspectViewModel
        {
            TicketingEnabled = _ticketingOptions.Value.Enabled,
            InitialCislo = initial
        };
        return View(vm);
    }

    /// <summary>
    /// JSON endpoint pro inspector. Vrací raw HOT_ZAZNAMY hlavičku + seznam HOT_VYJADRENI
    /// s plain textem a klasifikací přes <see cref="HarvestPredicates"/>.
    /// </summary>
    [HttpGet("Load")]
    public async Task<IActionResult> Load(string? cislo, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(cislo) || !Cislo6Regex.IsMatch(cislo))
        {
            return BadRequest(new SDConnectorLoadResponse
            {
                Nalezeno = false,
                Error = "Číslo musí být 6 cifer."
            });
        }

        // DIAGNOSTIC: stage tracker pro debug HTTP 500 — odstranit po stabilizaci.
        var stage = "init";
        try
        {
            // 1) Fingerprint z HOT_ZAZNAMY — existence check + datum/stav/typ záznamu.
            stage = "fingerprint";
            var fingerprints = await _vyjadreni
                .GetHotZaznamFingerprintsAsync(new[] { cislo }, ct)
                .ConfigureAwait(false);

            if (!fingerprints.TryGetValue(cislo, out var fp))
            {
                return Ok(new SDConnectorLoadResponse
                {
                    Nalezeno = false,
                    Error = $"Ticket #{cislo} v HOT_ZAZNAMY neexistuje."
                });
            }

            // 2) Plný HOT_ZAZNAMY řádek (strucne + popis raw HTML).
            stage = "ticket-full";
            var ticket = await _ticketing.GetZaznamAsync(cislo, ct).ConfigureAwait(false);

            stage = "externi-odkaz-lookup";
            var externiOdkazId = await LookupExterniOdkazIdAsync(cislo, ct).ConfigureAwait(false);

            var header = new SDConnectorRawHeader
            {
                Cislo = cislo,
                TypZaznamu = fp.TypZaznamu ?? ticket?.TypZaznamu,
                Stav = fp.Stav,
                Strucne = ticket?.Strucne,
                PopisRaw = ticket?.Popis,
                Datum = fp.Datum,
                ExterniOdkazId = externiOdkazId
            };

            // 3) Všechna vyjádření — plain text + klasifikace.
            stage = "vyjadreni-fetch";
            var vyjadreni = await _vyjadreni
                .GetVyjadreniForTicketAsync(cislo, sinceUtc: null, ct)
                .ConfigureAwait(false);

            stage = "vyjadreni-classify";
            var bubliny = new List<SDConnectorBublinaDto>(vyjadreni.Count);
            foreach (var v in vyjadreni)
            {
                var display = !string.IsNullOrWhiteSpace(v.Zpracoval)
                    ? await _adCache.ResolveDisplayNameAsync(v.Zpracoval, ct).ConfigureAwait(false)
                    : null;
                var plain = VyjadreniHtmlText.ToPlainText(v.Popis);
                var kind = HarvestPredicates.ClassifyPopis(v.Popis);

                bubliny.Add(new SDConnectorBublinaDto
                {
                    HotId = v.Id,
                    Typ = v.Typ ?? string.Empty,
                    Datum = v.Datum,
                    LoginRaw = v.Zpracoval,
                    AutorDisplayName = display,
                    Tym = v.Tym,
                    PopisRaw = v.Popis,
                    PopisPlainText = plain,
                    ClassifiedAs = ClassificationKey(kind)
                });
            }

            return Ok(new SDConnectorLoadResponse
            {
                Nalezeno = true,
                Raw = header,
                Bubliny = bubliny
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Diag] Inspect/Load selhal ve fázi {Stage} pro ticket {Cislo}.", stage, cislo);
            // DIAGNOSTIC: surface plnou exception do JSON response, aby user mohl
            // poslat stack trace bez chození do logů (production logs jsou off).
            // Bezpečnostní compromise: vystavuje internal stack trace, ale endpoint
            // je za [Authorize(Policy = "permission:settings.sd.view")] = jen admin.
            // Po vyřešení root cause TENTO try/catch + logger.LogError nech, ALE
            // odstraň ex.StackTrace z Error stringu (zachovej typ+message).
            var diag = $"[stage={stage}] {ex.GetType().FullName}: {ex.Message}";
            if (ex.InnerException is not null)
            {
                diag += $"\n  inner: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}";
            }
            diag += $"\n{ex.StackTrace}";
            return StatusCode(500, new SDConnectorLoadResponse
            {
                Nalezeno = false,
                Error = diag
            });
        }
    }

    /// <summary>
    /// DIAGNOSTIC plain-text endpoint pro debug HTTP 500 v Load. Otevři přímo v URL liště:
    ///   /SDConnector/Diag?cislo=363139
    /// Vrací text/plain — žádný JS, žádný JSON, žádný middleware exception handler nepřepíše.
    /// Spouští stejnou pipeline jako Load, na exception zachytí plný stack trace a inner
    /// exceptions a vypíše je do response.
    /// Po vyřešení root cause smazat (i s try/catch v Load).
    /// </summary>
    [HttpGet("Diag")]
    public async Task<IActionResult> Diag(string? cislo, CancellationToken ct)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== /SDConnector/Diag — plain-text diagnostic ===");
        sb.AppendLine($"Time:    {DateTime.UtcNow:O} (UTC)");
        sb.AppendLine($"Cislo:   {cislo ?? "(null)"}");
        sb.AppendLine($"User:    {(User.Identity?.IsAuthenticated == true ? User.Identity.Name : "(anonymous)")}");
        sb.AppendLine($"Ticketing.Enabled: {_ticketingOptions.Value.Enabled}");
        sb.AppendLine($"Ticketing.ConnectionStringName: {_ticketingOptions.Value.ConnectionStringName}");
        sb.AppendLine();

        if (string.IsNullOrEmpty(cislo) || !Cislo6Regex.IsMatch(cislo))
        {
            sb.AppendLine("ERROR: Cislo musi byt 6 cifer. Pridej do URL ?cislo=XXXXXX.");
            return Content(sb.ToString(), "text/plain; charset=utf-8");
        }

        var stage = "init";
        try
        {
            stage = "fingerprint";
            sb.AppendLine($"[{stage}] start...");
            var fingerprints = await _vyjadreni.GetHotZaznamFingerprintsAsync(new[] { cislo }, ct).ConfigureAwait(false);
            sb.AppendLine($"[{stage}] OK, fingerprints count={fingerprints.Count}");

            if (!fingerprints.TryGetValue(cislo, out var fp))
            {
                sb.AppendLine($"INFO: Ticket #{cislo} v HOT_ZAZNAMY neexistuje (fingerprint vratil prazdne).");
                return Content(sb.ToString(), "text/plain; charset=utf-8");
            }
            sb.AppendLine($"[{stage}] fp.Datum={fp.Datum:O}, fp.Stav={fp.Stav}, fp.TypZaznamu={fp.TypZaznamu}");

            stage = "ticket-full";
            sb.AppendLine($"[{stage}] start (full HotZaznamEntity materialization)...");
            var ticket = await _ticketing.GetZaznamAsync(cislo, ct).ConfigureAwait(false);
            sb.AppendLine($"[{stage}] OK, ticket={(ticket == null ? "(null)" : $"Id={ticket.Id}, TypZaznamu={ticket.TypZaznamu}, len(strucne)={ticket.Strucne?.Length ?? 0}")}");

            stage = "externi-odkaz";
            sb.AppendLine($"[{stage}] start...");
            var externiOdkazId = await LookupExterniOdkazIdAsync(cislo, ct).ConfigureAwait(false);
            sb.AppendLine($"[{stage}] OK, externiOdkazId={externiOdkazId}");

            stage = "vyjadreni-fetch";
            sb.AppendLine($"[{stage}] start (HotVyjadreniEntity materialization)...");
            var vyjadreni = await _vyjadreni.GetVyjadreniForTicketAsync(cislo, sinceUtc: null, ct).ConfigureAwait(false);
            sb.AppendLine($"[{stage}] OK, count={vyjadreni.Count}");

            stage = "vyjadreni-classify";
            sb.AppendLine($"[{stage}] start...");
            int classified = 0;
            foreach (var v in vyjadreni)
            {
                _ = VyjadreniHtmlText.ToPlainText(v.Popis);
                _ = HarvestPredicates.ClassifyPopis(v.Popis);
                classified++;
            }
            sb.AppendLine($"[{stage}] OK, classified={classified}");

            sb.AppendLine();
            sb.AppendLine("=== ALL STAGES PASSED ===");
            return Content(sb.ToString(), "text/plain; charset=utf-8");
        }
        catch (Exception ex)
        {
            sb.AppendLine();
            sb.AppendLine($"!!! EXCEPTION at stage [{stage}] !!!");
            var current = (Exception?)ex;
            int depth = 0;
            while (current is not null)
            {
                sb.AppendLine();
                sb.AppendLine($"-- Level {depth} --");
                sb.AppendLine($"Type:    {current.GetType().FullName}");
                sb.AppendLine($"Message: {current.Message}");
                sb.AppendLine("Stack:");
                sb.AppendLine(current.StackTrace ?? "(no stack trace)");
                current = current.InnerException;
                depth++;
            }
            _logger.LogError(ex, "[Diag] /SDConnector/Diag selhal ve fazi {Stage} pro ticket {Cislo}.", stage, cislo);
            return Content(sb.ToString(), "text/plain; charset=utf-8");
        }
    }

    /// <summary>
    /// Vrátí Id <c>ZaznamExterniOdkaz</c> pro daný 6-ciferný ticket — pokud existuje vazba,
    /// Inspect UI zobrazí button „Otevřít v chat modalu" (reuse existing modal komponenty).
    /// </summary>
    private async Task<int?> LookupExterniOdkazIdAsync(string cislo, CancellationToken ct)
    {
        return await _db.ZaznamExterniOdkazy.AsNoTracking()
            .Where(x => x.Cislo == cislo)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Mapuje <see cref="HarvestPredicateKind"/> na krátký string key pro JSON/UI badge.
    /// Klienti očekávají K3/K4K7/K6/K10/PlanDodani/None — keep stable pro JS konzumenty.
    /// </summary>
    private static string ClassificationKey(HarvestPredicateKind kind) => kind switch
    {
        HarvestPredicateKind.K3_OdeslaniZadaniPmp => "K3",
        HarvestPredicateKind.K4_K7_DodaniReseni => "K4K7",
        HarvestPredicateKind.K6_OdeslaniPozadavku => "K6",
        HarvestPredicateKind.K10_NasazeniArchivace => "K10",
        HarvestPredicateKind.PlanDodani => "PlanDodani",
        _ => "None"
    };
}
