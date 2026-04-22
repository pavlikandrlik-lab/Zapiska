using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Audit;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Dictionaries;

namespace PmTracker.Tests.Unit.Dictionaries;

public sealed class DictionaryServiceTests
{
    [Fact]
    public async Task SaveHarmonogramStepRowAsync_ShouldDelegateToHarmonogramService()
    {
        var harmonogramCatalogService = new FakeHarmonogramCatalogService();
        var sut = new DictionaryService(null!, harmonogramCatalogService, new FakeAuditWriteService(), TimeProvider.System);
        var command = new SaveCiselnikRowCommand
        {
            Key = "harmonogram-kroky",
            Kod = "K1",
            Nazev = "Krok 1"
        };

        await sut.SaveHarmonogramStepRowAsync(command);

        harmonogramCatalogService.LastSaveCommand.Should().BeSameAs(command);
    }

    [Fact]
    public async Task DeleteHarmonogramStepRowAsync_ShouldDelegateToHarmonogramService()
    {
        var harmonogramCatalogService = new FakeHarmonogramCatalogService();
        var sut = new DictionaryService(null!, harmonogramCatalogService, new FakeAuditWriteService(), TimeProvider.System);
        var command = new DeleteCiselnikRowCommand
        {
            Key = "harmonogram-kroky",
            Id = 17
        };

        await sut.DeleteHarmonogramStepRowAsync(command);

        harmonogramCatalogService.LastDeleteCommand.Should().BeSameAs(command);
    }

    private sealed class FakeHarmonogramCatalogService : IHarmonogramCatalogService
    {
        public SaveCiselnikRowCommand? LastSaveCommand { get; private set; }
        public DeleteCiselnikRowCommand? LastDeleteCommand { get; private set; }

        public Task<CiselnikDetailViewModel> BuildHarmonogramKrokyCiselnikDetailAsync(string key, bool canChangeLockState, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> CountHarmonogramCatalogRowsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CiselnikDetailViewModel> BuildCiselnikDetailAsync(string id, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();

        public Task SaveHarmonogramStepRowAsync(SaveCiselnikRowCommand command, CancellationToken ct = default)
        {
            LastSaveCommand = command;
            return Task.CompletedTask;
        }

        public Task DeleteHarmonogramStepRowAsync(DeleteCiselnikRowCommand command, CancellationToken ct = default)
        {
            LastDeleteCommand = command;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAuditWriteService : IAuditWriteService
    {
        public void Add(int? actorOsobaId, AuditWriteEntry entry)
        {
        }

        public Task WriteAsync(int? actorOsobaId, AuditWriteEntry entry, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
