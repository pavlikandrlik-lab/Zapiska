using FluentAssertions;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Services.Records;

namespace PmTracker.Tests.Integration.DataStore;

/// <summary>
/// Spec 2026-10-22 R5–R8, R11: seznamy pro detail jednání a šipky. Záznamy jednání = záznamy
/// zápisu ve stejném pořadí; aktivní záznamy projektu ve stejném pořadí; cizí jednání se ignoruje.
/// </summary>
[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class RecordSequenceServiceTests
{
    private readonly SqlIntegrationFixture _fixture;

    public RecordSequenceServiceTests(SqlIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task MeetingSequence_IsMinutesSetInMinutesOrder()
    {
        var db = await _fixture.CreateDatabaseAsync("record_sequence_meeting");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var seed = await MeetingSequenceSeed.CreateAsync(dbContext);
        var service = IntegrationTestHelper.CreateRecordSequenceService(dbContext);

        (await service.GetMeetingSequenceAsync(seed.Meeting888)).Select(x => x.ZaznamId)
            .Should().Equal(seed.ExpectedMeeting888);
        (await service.GetMeetingSequenceAsync(seed.Meeting887)).Select(x => x.ZaznamId)
            .Should().Equal(seed.ExpectedMeeting887, "první jednání nemá minulé; založený až po něm do něj nepatří");
        (await service.GetMeetingSequenceAsync(int.MaxValue)).Should().BeEmpty("neexistující jednání");
    }

    [Fact]
    public async Task ActiveSequence_IsActiveRecordsInMinutesOrder_WithLabels()
    {
        var db = await _fixture.CreateDatabaseAsync("record_sequence_active");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var seed = await MeetingSequenceSeed.CreateAsync(dbContext);
        var service = IntegrationTestHelper.CreateRecordSequenceService(dbContext);

        var active = await service.GetActiveSequenceAsync(seed.ProjectId);

        active.Select(x => x.ZaznamId).Should().Equal(seed.ExpectedActive);
        active.Single(x => x.ZaznamId == seed.R1).Label.Should().Be("#1 - SeqR1 Record");
    }

    [Fact]
    public async Task Resolve_IgnoresMeetingOfAnotherProject()
    {
        var db = await _fixture.CreateDatabaseAsync("record_sequence_resolve");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var seed = await MeetingSequenceSeed.CreateAsync(dbContext);
        var otherProjectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "SEQOTHER");
        var otherMeetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, otherProjectId, "OPEN", meetingNumber: 901);
        var service = IntegrationTestHelper.CreateRecordSequenceService(dbContext);

        (await service.ResolveAsync(RecordSequenceRequest.ForMeeting(otherMeetingId), seed.ProjectId))
            .Should().BeNull("jednání jiného projektu se ignoruje (spec §6)");
        (await service.ResolveAsync(RecordSequenceRequest.ForMeeting(seed.Meeting888), seed.ProjectId))!
            .Select(x => x.ZaznamId).Should().Equal(seed.ExpectedMeeting888);
        (await service.ResolveAsync(RecordSequenceRequest.Active, seed.ProjectId))!
            .Select(x => x.ZaznamId).Should().Equal(seed.ExpectedActive);
    }
}
