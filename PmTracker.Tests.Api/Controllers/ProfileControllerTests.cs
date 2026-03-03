using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Tests.Api.Controllers;

[Collection(ApiSqlCollection.CollectionName)]
public sealed class ProfileControllerTests
{
    private readonly ApiSqlFixture _fixture;

    public ProfileControllerTests(ApiSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Index_ShouldRenderRightsGroupedByRole()
    {
        var userId = await _fixture.EnsurePersonAsync("ApiProfileRoleUser");

        await using (var dbContext = _fixture.CreateDbContext())
        {
            var roleWithPermission = await (
                from role in dbContext.AuthzRoles.AsNoTracking()
                join rolePermission in dbContext.AuthzRolePermissions.AsNoTracking() on role.Id equals rolePermission.RoleId
                join permission in dbContext.AuthzPermissions.AsNoTracking() on rolePermission.PermissionId equals permission.Id
                where role.IsActive && permission.IsActive
                orderby role.Kod, permission.Klic
                select new
                {
                    role.Id,
                    role.Kod,
                    PermissionKlic = permission.Klic
                })
                .FirstAsync();

            var assignmentExists = await dbContext.AuthzUserRoles
                .AnyAsync(x => x.OsobaId == userId && x.RoleId == roleWithPermission.Id && x.IsActive);

            if (!assignmentExists)
            {
                dbContext.AuthzUserRoles.Add(new AuthzUserRoleEntity
                {
                    OsobaId = userId,
                    RoleId = roleWithPermission.Id,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
                await dbContext.SaveChangesAsync();
            }

            using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
            var response = await client.GetAsync($"/Profil?asUser={userId}");
            var html = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.OK, html);
            html.Should().Contain("Moje role a práva");
            html.Should().Contain("profile-role-card");
            html.Should().Contain(roleWithPermission.Kod);
            html.Should().Contain(roleWithPermission.PermissionKlic);
            html.Should().NotContain("Diagnostický náhled výsledných oprávnění podle rolí a rozsahů.");
        }
    }
}
