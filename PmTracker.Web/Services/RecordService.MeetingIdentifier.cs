using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Audit;

namespace PmTracker.Web.Services;

/// <summary>
/// Fáze 3C Task 1: RecordService.MeetingIdentifier.cs — AssignMeetingIdentifierAsync
/// + validation + LoadOpenProjectMeetingsAsync helper sdílený se SaveRecord.
/// Další operace (Save, Delete) v samostatných partials.
/// </summary>
public sealed partial class RecordService
{
    public async Task AssignMeetingIdentifierAsync(
        AssignMeetingIdentifierCommand command,
        CurrentUserContextViewModel currentUser,
        IRecordWriteCommandsComposition composition,
        CancellationToken ct = default)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var record = await dbContext.ProjektoveZaznamy
                .FromSqlRaw("SELECT * FROM projektove_zaznamy WITH (UPDLOCK, HOLDLOCK) WHERE id = {0}", command.ZaznamId)
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException($"Záznam {command.ZaznamId} nebyl nalezen.");
            if (record.ProjektId != command.ProjektId)
            {
                throw new InvalidOperationException("Záznam nepatří do vybraného projektu.");
            }

            if (record.CisloViditelneTyp == RecordDisplayNumberTypeMeeting)
            {
                throw new InvalidOperationException("Záznam už má identifikátor podle jednání.");
            }

            var openMeetings = await LoadOpenProjectMeetingsAsync(command.ProjektId, ct);
            if (openMeetings.Count == 0)
            {
                throw new InvalidOperationException("Není dostupné žádné neuzavřené jednání.");
            }

            var meeting = openMeetings
                .FirstOrDefault(x => x.Id == command.JednaniId)
                ?? throw new InvalidOperationException("Vybrané jednání neexistuje.");

            var nextOrder = await composition.AllocateMeetingOrderTransactionalAsync(command.ProjektId, meeting.CisloJednani, ct);
            var old = RecordAuditSnapshot.FromEntity(record);
            record.CisloViditelneTyp = RecordDisplayNumberTypeMeeting;
            record.CisloViditelneA = meeting.CisloJednani;
            record.CisloViditelneB = nextOrder;
            record.CisloJednaniZdrojId = meeting.Id;
            record.CisloViditelne = $"{meeting.CisloJednani}-{nextOrder}";
            await dbContext.SaveChangesAsync(ct);
            auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
                AuditActionType.Assign,
                AuditEntityType.Record,
                record.Id.ToString(CultureInfo.InvariantCulture),
                old,
                RecordAuditSnapshot.FromEntity(record)));
            await dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
    }

    private Task<List<OpenMeetingRow>> LoadOpenProjectMeetingsAsync(int projectId, CancellationToken ct)
        => (
                from meeting in dbContext.Jednani.AsNoTracking()
                join state in dbContext.CiselnikStavuJednani.AsNoTracking() on meeting.StavJednaniId equals state.Id
                where meeting.ProjektId == projectId
                    && !meeting.UzamklOsobaId.HasValue
                    && state.Kod != "CLOSED"
                    && !EF.Functions.Like(state.Nazev, "%uzav%")
                select new OpenMeetingRow(
                    meeting.Id,
                    meeting.CisloJednani))
            .ToListAsync(ct);

    private static bool IsMeetingReadOnly(JednaniEntity? meeting, CiselnikStavuJednaniEntity? status)
        => MeetingStatePolicy.IsReadOnly(meeting, status);
}
