using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Audit;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.ServiceDesk;

namespace PmTracker.Web.Services.Handlers;

/// <summary>
/// Request DTO pro <see cref="SaveProjectHandler"/>. Wrapuje <see cref="SaveProjectCommand"/>
/// kvůli tomu, aby všechny handlery konzumovaly jednotný tvar (request record + handler).
/// </summary>
public sealed record SaveProjectRequest(SaveProjectCommand Command);

/// <summary>
/// Vertical slice handler: jediný use-case = „uložit (create/update) projekt". Vlastní DI
/// graph (<see cref="PmTrackerDbContext"/> + <see cref="IAuditWriteService"/>), vlastní
/// transakci, vlastní audit write. Proof pattern pro postupnou migraci <c>ProjectService</c>
/// do vertikálních handler slices — viz <see cref="IRequestHandler{TRequest, TResponse}"/>.
/// </summary>
public sealed class SaveProjectHandler : IRequestHandler<SaveProjectRequest, int>
{
    private readonly PmTrackerDbContext _db;
    private readonly IAuditWriteService _audit;
    private readonly ITextNormalizer _textNormalizer;

    public SaveProjectHandler(PmTrackerDbContext db, IAuditWriteService audit, ITextNormalizer textNormalizer)
    {
        _db = db;
        _audit = audit;
        _textNormalizer = textNormalizer;
    }

    public async Task<int> HandleAsync(
        SaveProjectRequest request,
        CurrentUserContextViewModel currentUser,
        CancellationToken ct = default)
    {
        var command = request.Command;
        var statusId = await ResolveProjectStatusIdAsync(command.Stav, ct);

        // Plán 5 Sprint B Task 3: validace vazby na IS proti hardkódovanému katalogu.
        // NULL = bez napojení (povoleno); non-NULL musí být v SdInfoSystemy.
        if (command.ServiceDeskInfoSystemId.HasValue
            && !SdInfoSystemy.IsSupported(command.ServiceDeskInfoSystemId))
        {
            throw new InvalidOperationException(
                $"Informační systém s ID {command.ServiceDeskInfoSystemId.Value} není v katalogu SdInfoSystemy.");
        }

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            if (command.Id.HasValue)
            {
                var existing = await _db.Projekty.FirstOrDefaultAsync(x => x.Id == command.Id.Value, ct)
                    ?? throw new InvalidOperationException($"Projekt {command.Id.Value} nebyl nalezen.");

                var before = ProjectAuditSnapshot.FromEntity(existing);
                existing.CelyNazev = command.Nazev.Trim();
                existing.Zkratka = command.Zkratka.Trim();
                existing.StavId = statusId;
                existing.PouzivatIdentJednani = command.PouzivatIdentJednani;
                existing.MistoPlneni = NormalizeOrNull(command.MistoPlneni);
                existing.CisloRamcoveSmlouvy = NormalizeOrNull(command.CisloRamcoveSmlouvy);
                existing.ServiceDeskInfoSystemId = command.ServiceDeskInfoSystemId;
                await _db.SaveChangesAsync(ct);
                _audit.Add(currentUser.OsobaId, new AuditWriteEntry(
                    AuditActionType.Update,
                    AuditEntityType.Project,
                    existing.Id.ToString(CultureInfo.InvariantCulture),
                    before,
                    ProjectAuditSnapshot.FromEntity(existing)));
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return existing.Id;
            }

            var created = new ProjektEntity
            {
                CelyNazev = command.Nazev.Trim(),
                Zkratka = command.Zkratka.Trim(),
                StavId = statusId,
                PouzivatIdentJednani = command.PouzivatIdentJednani,
                MistoPlneni = NormalizeOrNull(command.MistoPlneni),
                CisloRamcoveSmlouvy = NormalizeOrNull(command.CisloRamcoveSmlouvy),
                ServiceDeskInfoSystemId = command.ServiceDeskInfoSystemId
            };
            _db.Projekty.Add(created);
            await _db.SaveChangesAsync(ct);

            _audit.Add(currentUser.OsobaId, new AuditWriteEntry(
                AuditActionType.Create,
                AuditEntityType.Project,
                created.Id.ToString(CultureInfo.InvariantCulture),
                null,
                ProjectAuditSnapshot.FromEntity(created)));
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return created.Id;
        });
    }

    private async Task<int> ResolveProjectStatusIdAsync(string value, CancellationToken ct)
    {
        var input = value?.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new InvalidOperationException("Stav projektu není vyplněn.");
        }

        var direct = await _db.CiselnikStavuProjektu
            .Where(x => x.Kod == input || x.Nazev == input)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct);
        if (direct.HasValue)
        {
            return direct.Value;
        }

        var normalized = _textNormalizer.Normalize(input);
        var rows = await _db.CiselnikStavuProjektu.AsNoTracking()
            .Select(x => new { x.Id, x.Kod, x.Nazev })
            .ToListAsync(ct);
        var match = rows.FirstOrDefault(x =>
            _textNormalizer.Normalize(x.Kod) == normalized
            || _textNormalizer.Normalize(x.Nazev) == normalized);

        if (match is not null)
        {
            return match.Id;
        }

        throw new InvalidOperationException($"Stav projektu '{value}' neexistuje.");
    }

    private static string? NormalizeOrNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
