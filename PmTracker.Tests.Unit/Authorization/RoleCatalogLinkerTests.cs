using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class RoleCatalogLinkerTests
{
    private static PmTrackerDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PmTrackerDbContext(options);
    }

    [Fact]
    public async Task LinkAsync_ShouldFillAuthzRoleId_ForProjectRole_WhenAuthzRoleWithSameKodExists()
    {
        await using var db = CreateInMemoryDb();
        db.AuthzRoles.Add(new AuthzRoleEntity { Id = 10, Kod = "ADM_PROJ", Nazev = "Projektový admin", IsActive = true, Scope = "PROJECT" });
        db.CiselnikRoliProjektu.Add(new CiselnikRoliProjektuEntity { Id = 1, Kod = "ADM_PROJ", Nazev = "Projektový admin" });
        await db.SaveChangesAsync();

        var linker = new RoleCatalogLinker(db);
        await linker.LinkAsync(CancellationToken.None);

        var linked = await db.CiselnikRoliProjektu.FirstAsync(r => r.Id == 1);
        linked.AuthzRoleId.Should().Be(10);
    }

    [Fact]
    public async Task LinkAsync_ShouldFillAuthzRoleId_ForSubsystemRole()
    {
        await using var db = CreateInMemoryDb();
        db.AuthzRoles.Add(new AuthzRoleEntity { Id = 20, Kod = "VEDOUCI_SUBSYSTEMU", Nazev = "Vedoucí", IsActive = true, Scope = "SUBSYSTEM" });
        db.CiselnikRoliSubsystemu.Add(new CiselnikRoleSubsystemuEntity { Id = 2, Kod = "VEDOUCI_SUBSYSTEMU", Nazev = "Vedoucí" });
        await db.SaveChangesAsync();

        var linker = new RoleCatalogLinker(db);
        await linker.LinkAsync(CancellationToken.None);

        var linked = await db.CiselnikRoliSubsystemu.FirstAsync(r => r.Id == 2);
        linked.AuthzRoleId.Should().Be(20);
    }

    [Fact]
    public async Task LinkAsync_ShouldSkipRow_WhenAuthzRoleWithMatchingKodMissing()
    {
        await using var db = CreateInMemoryDb();
        db.CiselnikRoliProjektu.Add(new CiselnikRoliProjektuEntity { Id = 3, Kod = "UNKNOWN_ROLE", Nazev = "Custom" });
        await db.SaveChangesAsync();

        var linker = new RoleCatalogLinker(db);
        await linker.LinkAsync(CancellationToken.None);

        var unlinked = await db.CiselnikRoliProjektu.FirstAsync(r => r.Id == 3);
        unlinked.AuthzRoleId.Should().BeNull();
    }

    [Fact]
    public async Task LinkAsync_ShouldNotOverwriteExistingLink()
    {
        await using var db = CreateInMemoryDb();
        db.AuthzRoles.Add(new AuthzRoleEntity { Id = 30, Kod = "PROJ_MAN", Nazev = "PM", IsActive = true, Scope = "PROJECT" });
        db.CiselnikRoliProjektu.Add(new CiselnikRoliProjektuEntity { Id = 4, Kod = "PROJ_MAN", Nazev = "PM", AuthzRoleId = 99 });
        await db.SaveChangesAsync();

        var linker = new RoleCatalogLinker(db);
        await linker.LinkAsync(CancellationToken.None);

        var kept = await db.CiselnikRoliProjektu.FirstAsync(r => r.Id == 4);
        kept.AuthzRoleId.Should().Be(99);
    }
}
