using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Modules.Export.Queries;

public sealed class ExportRecordProjectionBuilder(
    PmTrackerDbContext dbContext,
    IRichTextContentService richTextContentService,
    IPersonIdentityMatcher personIdentityMatcher,
    IExportCommentProjectionBuilder exportCommentProjectionBuilder,
    IExportRecordVisibilityEvaluator exportRecordVisibilityEvaluator) : IExportRecordProjectionBuilder
{
    private static readonly StringComparer Ci = StringComparer.OrdinalIgnoreCase;

    public List<PdfExportRecordViewModel> BuildExportRecords(
        int projectId,
        int? anchorMeetingId,
        int? specificRecordId,
        bool limitComments,
        bool applyMeetingSnapshotRules)
    {
        var records = dbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.ProjektId == projectId)
            .Where(x => !specificRecordId.HasValue || x.Id == specificRecordId.Value)
            .ToList();
        records = OrderRecordsByVisibleNumber(records).ToList();
        var recordIds = records.Select(record => record.Id).ToArray();

        var categories = dbContext.CiselnikKategoriiZaznamu.AsNoTracking().ToDictionary(x => x.Id);
        var taskTypes = dbContext.CiselnikTypuUkolu.AsNoTracking().ToDictionary(x => x.Id);
        var taskStates = dbContext.CiselnikStavuUkolu.AsNoTracking().ToDictionary(x => x.Id);
        var subsystems = dbContext.Subsystemy.AsNoTracking().ToDictionary(x => x.Id);
        var people = dbContext.Osoby.AsNoTracking().ToDictionary(x => x.Id);

        var comments = dbContext.Vyjadreni.AsNoTracking()
            .Where(x => recordIds.Contains(x.ZaznamId))
            .ToList();

        var meetings = dbContext.Jednani.AsNoTracking()
            .Where(x => x.ProjektId == projectId)
            .OrderByDescending(x => x.CisloJednani)
            .ToList();

        var meetingStateMap = dbContext.CiselnikStavuJednani.AsNoTracking().ToDictionary(x => x.Id);
        var externalTypeMap = dbContext.CiselnikTypuExternichOdkazu.AsNoTracking().ToDictionary(x => x.Id);

        var externalLinksByRecord = dbContext.ZaznamExterniOdkazy.AsNoTracking()
            .Where(x => recordIds.Contains(x.ZaznamId))
            .ToList()
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var collaboration = dbContext.ZaznamSpoluprace.AsNoTracking()
            .Where(x => recordIds.Contains(x.ZaznamId))
            .ToList()
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var termHistory = dbContext.ZaznamHistorieTerminu.AsNoTracking()
            .Where(x => recordIds.Contains(x.ZaznamId))
            .OrderByDescending(x => x.DatumZmeny)
            .ToList()
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var meetingById = meetings.ToDictionary(x => x.Id);
        var anchorMeeting = anchorMeetingId.HasValue ? meetings.FirstOrDefault(x => x.Id == anchorMeetingId.Value) : null;
        var anchorState = anchorMeeting is null ? null : meetingStateMap.GetValueOrDefault(anchorMeeting.StavJednaniId);
        var previousMeeting = anchorMeeting is null
            ? null
            : meetings.Where(x => x.CisloJednani < anchorMeeting.CisloJednani)
                .OrderByDescending(x => x.CisloJednani)
                .FirstOrDefault();
        var previousMeetingNumber = previousMeeting?.CisloJednani;

        if (applyMeetingSnapshotRules && anchorMeeting is not null)
        {
            var statusHistoryByRecord = dbContext.ZaznamHistorieStavuZaznamu.AsNoTracking()
                .Where(x => recordIds.Contains(x.ZaznamId))
                .OrderByDescending(x => x.DatumZmeny)
                .ThenByDescending(x => x.Id)
                .ToList()
                .GroupBy(x => x.ZaznamId)
                .ToDictionary(group => group.Key, group => group.ToList());

            var anchorMeetingDate = anchorMeeting.DatumPlanovane.Date;
            var previousMeetingDate = previousMeeting?.DatumPlanovane.Date;

            records = records
                .Where(record => exportRecordVisibilityEvaluator.IsVisibleForMeetingPrint(
                    record,
                    taskStates,
                    statusHistoryByRecord.TryGetValue(record.Id, out var statusHistoryValues)
                        ? statusHistoryValues
                        : Array.Empty<ZaznamHistorieStavuZaznamuEntity>(),
                    anchorMeetingDate,
                    previousMeetingDate))
                .ToList();
        }

        var filteredComments = anchorMeeting is null
            ? comments
            : comments
                .Where(comment =>
                    meetingById.TryGetValue(comment.JednaniId, out var commentMeeting)
                    && commentMeeting.CisloJednani <= anchorMeeting.CisloJednani)
                .ToList();

        var commentGroups = filteredComments
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => group.ToList());

        return records.Select(record =>
        {
            IReadOnlyList<VyjadreniEntity> commentsForRecord = commentGroups.TryGetValue(record.Id, out var groupedComments)
                ? groupedComments
                : [];
            var orderedCommentsForRecord = exportCommentProjectionBuilder.OrderForExport(commentsForRecord, meetingById);
            var selectedComments = limitComments
                ? exportCommentProjectionBuilder.ApplyLimit(orderedCommentsForRecord)
                : orderedCommentsForRecord;

            var commentRows = selectedComments.Select(comment =>
            {
                meetingById.TryGetValue(comment.JednaniId, out var commentMeeting);
                var commentState = commentMeeting is null ? null : meetingStateMap.GetValueOrDefault(commentMeeting.StavJednaniId);
                var author = people.GetValueOrDefault(comment.AutorOsobaId) ?? people.GetValueOrDefault(record.VlastnikId);
                var highlightColor = ResolveHighlightColor(anchorMeeting, anchorState, previousMeetingNumber, commentMeeting);

                return new PdfExportCommentViewModel
                {
                    Autor = BuildDisplayNameFromOsoba(author),
                    Datum = comment.DatumVyjadreni,
                    Text = comment.TextVyjadreni,
                    Delka = richTextContentService.ToPlainText(comment.TextVyjadreni).Length,
                    JednaniCislo = commentMeeting?.CisloJednani,
                    JednaniDatum = commentMeeting?.DatumPlanovane,
                    JednaniStav = commentState?.Nazev,
                    IsNew = !string.IsNullOrWhiteSpace(highlightColor),
                    HighlightColor = highlightColor
                };
            }).ToList();

            IReadOnlyList<ZaznamExterniOdkazEntity> externalRows = externalLinksByRecord.TryGetValue(record.Id, out var externalValues)
                ? externalValues
                : [];
            var external = externalRows
                .Select(link =>
                {
                    var typeCode = externalTypeMap.GetValueOrDefault(link.TypOdkazuId)?.Kod ?? "-";
                    return FormatExternalLinkDisplay(typeCode, link.Cislo, link.PredpokladanaCena, link.PlanDodani);
                })
                .ToList();

            IReadOnlyList<ZaznamSpolupraceEntity> collaborationRows = collaboration.TryGetValue(record.Id, out var collaborationValues)
                ? collaborationValues
                : [];
            var peopleCollab = collaborationRows
                .Select(item => BuildDisplayNameFromOsoba(people.GetValueOrDefault(item.OsobaId)))
                .Distinct(Ci)
                .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            IReadOnlyList<ZaznamHistorieTerminuEntity> termHistoryRows = termHistory.TryGetValue(record.Id, out var termHistoryValues)
                ? termHistoryValues
                : [];
            var historyDates = termHistoryRows
                .Select(x => x.PuvodniDatum)
                .Distinct()
                .OrderByDescending(x => x)
                .ToList();

            var category = categories.GetValueOrDefault(record.KategorieId);
            var taskType = record.AktualniTypUkoluId.HasValue
                ? taskTypes.GetValueOrDefault(record.AktualniTypUkoluId.Value)
                : null;
            var subsystem = subsystems.GetValueOrDefault(record.SubsystemId);

            return new PdfExportRecordViewModel
            {
                ZaznamId = record.Id,
                CisloZaznamu = record.CisloZaznamu,
                CisloViditelne = ResolveVisibleRecordNumber(record),
                CisloViditelneA = ResolveVisibleNumberPartA(record),
                CisloViditelneB = ResolveVisibleNumberPartB(record),
                Nazev = record.Nazev,
                Cil = record.Cil,
                Popis = record.Popis,
                KategorieKod = category?.Kod ?? "-",
                Kategorie = category?.Nazev ?? "-",
                TypUkoluKod = taskType?.Kod,
                TypUkolu = taskType?.Nazev,
                Stav = record.StavUkoluId.HasValue ? taskStates.GetValueOrDefault(record.StavUkoluId.Value)?.Nazev ?? "-" : "-",
                Vlastnik = BuildDisplayNameFromOsoba(people.GetValueOrDefault(record.VlastnikId)),
                SubsystemKod = subsystem?.Kod ?? "-",
                Subsystem = subsystem?.Nazev ?? "-",
                DatumZalozeni = record.DatumZalozeni,
                HistorieTerminu = historyDates,
                Termin = record.DatumUkonceni,
                ExterniVazby = external,
                Spoluprace = peopleCollab,
                Vyjadreni = commentRows
            };
        }).ToList();
    }

    private static string? ResolveHighlightColor(
        JednaniEntity? anchorMeeting,
        CiselnikStavuJednaniEntity? anchorState,
        int? previousMeetingNumber,
        JednaniEntity? commentMeeting)
    {
        if (anchorMeeting is null || commentMeeting is null || anchorState is null)
        {
            return null;
        }

        var anchorIsPreparation = anchorState.Kod.Equals("DRAFT", StringComparison.OrdinalIgnoreCase)
            || anchorState.Nazev.Contains("příprav", StringComparison.OrdinalIgnoreCase);

        if (!anchorIsPreparation)
        {
            return commentMeeting.Id == anchorMeeting.Id ? "#0F4D8A" : null;
        }

        if (commentMeeting.Id == anchorMeeting.Id)
        {
            return "#A63A2B";
        }

        if (previousMeetingNumber.HasValue && commentMeeting.CisloJednani == previousMeetingNumber.Value)
        {
            return "#0F4D8A";
        }

        return null;
    }

    private static string ResolveVisibleRecordNumber(ProjektovyZaznamEntity record)
    {
        if (!string.IsNullOrWhiteSpace(record.CisloViditelne))
        {
            return record.CisloViditelne.Trim();
        }

        return record.CisloZaznamu.ToString(CultureInfo.InvariantCulture);
    }

    private static int ResolveVisibleNumberPartA(ProjektovyZaznamEntity record)
    {
        if (record.CisloViditelneA > 0)
        {
            return record.CisloViditelneA;
        }

        return Math.Max(0, record.CisloZaznamu);
    }

    private static int ResolveVisibleNumberPartB(ProjektovyZaznamEntity record)
    {
        return record.CisloViditelneTyp == 1
            ? Math.Max(1, record.CisloViditelneB)
            : 0;
    }

    private static IEnumerable<ProjektovyZaznamEntity> OrderRecordsByVisibleNumber(IEnumerable<ProjektovyZaznamEntity> rows)
    {
        return rows
            .OrderBy(ResolveVisibleNumberPartA)
            .ThenBy(ResolveVisibleNumberPartB)
            .ThenBy(x => x.CisloZaznamu);
    }

    private string BuildDisplayName(string? titul, string jmeno, string prijmeni, int id)
    {
        var displayName = personIdentityMatcher.BuildPersonName(titul, jmeno, prijmeni);
        return string.IsNullOrWhiteSpace(displayName) ? $"Uživatel #{id}" : displayName;
    }

    private string BuildDisplayNameFromOsoba(OsobaEntity? osoba)
    {
        if (osoba is null)
        {
            return "-";
        }

        return BuildDisplayName(osoba.Titul, osoba.Jmeno, osoba.Prijmeni, osoba.Id);
    }

    private static string FormatEstimatedPrice(decimal estimatedPrice)
    {
        return $"{estimatedPrice.ToString("N2", CultureInfo.GetCultureInfo("cs-CZ"))} Kč";
    }

    private static string FormatExternalLinkDisplay(string typeCode, string number, decimal? estimatedPrice, DateTime? plannedDelivery)
    {
        var header = $"{typeCode} {number}".Trim();
        var details = new List<string>();
        if (estimatedPrice.HasValue)
        {
            details.Add(FormatEstimatedPrice(estimatedPrice.Value));
        }

        if (plannedDelivery.HasValue)
        {
            details.Add($"plán dodání: {plannedDelivery.Value:dd.MM.yyyy}");
        }

        return details.Count > 0
            ? $"{header} ({string.Join(", ", details)})"
            : header;
    }
}
