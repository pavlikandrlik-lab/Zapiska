using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

[Collection(E2ECollection.CollectionName)]
public sealed class SettingsRoleActionScenariosTests
{
    private readonly E2ETestFixture _fixture;

    public SettingsRoleActionScenariosTests(E2ETestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RoleActionMapping_ShouldOfferDelete_ForExistingAllowedMapping()
    {
        var page = await _fixture.NewPageAsync();
        SeededRolePermission? mapping = null;

        try
        {
            mapping = await InsertRolePermissionAsync(scopeMode: "ALL", includeProject: false);

            await page.GotoAsync(SettingsUrl());

            var row = GetRolePermissionRow(page, mapping);

            await Expect(row).ToContainTextAsync("Ano");
            await Expect(row.GetByRole(AriaRole.Button, new() { Name = "Upravit" })).ToBeVisibleAsync();
            await Expect(row.GetByRole(AriaRole.Button, new() { Name = "Smazat" })).ToBeVisibleAsync();
        }
        finally
        {
            if (mapping is not null)
            {
                await DeleteRolePermissionDirectAsync(mapping.Id);
            }

            await page.Context.CloseAsync();
        }
    }

    [Fact]
    public async Task RoleActionMapping_Delete_ShouldRemoveRow_FromPanel()
    {
        var page = await _fixture.NewPageAsync();
        var mapping = await InsertRolePermissionAsync(scopeMode: "INCLUDE", includeProject: true);

        try
        {
            await page.GotoAsync(SettingsUrl());

            page.Dialog += async (_, dialog) => await dialog.AcceptAsync();

            var row = GetRolePermissionRow(page, mapping);
            await row.GetByRole(AriaRole.Button, new() { Name = "Smazat" }).ClickAsync();

            (await RolePermissionExistsAsync(mapping.Id)).Should().BeFalse();
            (await CountRolePermissionProjectsAsync(mapping.Id)).Should().Be(0);
        }
        finally
        {
            await DeleteRolePermissionDirectAsync(mapping.Id);
            await page.Context.CloseAsync();
        }
    }

    [Fact]
    public async Task RoleActionModal_ShouldNotOfferDenyOption()
    {
        var page = await _fixture.NewPageAsync();
        var pair = await GetAvailableRolePermissionPairAsync();
        int? createdRolePermissionId = null;

        try
        {
            await page.GotoAsync(SettingsUrl());

            await page.GetByRole(AriaRole.Button, new() { Name = "Přidat mapování" }).ClickAsync();

            var modal = page.Locator(".modal-overlay");
            await Expect(modal).ToBeVisibleAsync();
            await Expect(modal.Locator("select[name='IsAllowed']")).ToHaveCountAsync(0);
            await Expect(modal.Locator("label").Filter(new LocatorFilterOptions { HasTextString = "Povolení" })).ToHaveCountAsync(0);
            await Expect(modal.Locator("input[name='IsAllowed'][value='true']")).ToHaveCountAsync(1);

            await modal.Locator("select[name='RoleId']").SelectOptionAsync(pair.RoleId.ToString());
            await modal.Locator("select[name='PermissionId']").SelectOptionAsync(pair.PermissionId.ToString());
            await modal.Locator("select[name='ScopeMode']").SelectOptionAsync("ALL");
            await modal.GetByRole(AriaRole.Button, new() { Name = "Uložit mapování" }).ClickAsync();

            var createdRow = GetRolePermissionRow(page, pair.RoleKod, pair.PermissionKlic);
            await Expect(createdRow).ToContainTextAsync("Ano");

            createdRolePermissionId = await FindRolePermissionIdAsync(pair.RoleId, pair.PermissionId);
            createdRolePermissionId.Should().NotBeNull();
        }
        finally
        {
            if (createdRolePermissionId.HasValue)
            {
                await DeleteRolePermissionDirectAsync(createdRolePermissionId.Value);
            }

            await page.Context.CloseAsync();
        }
    }

    private string SettingsUrl()
    {
        return $"{_fixture.BaseUrl}/Nastaveni?section=role-akce&asUser={_fixture.AdminOsobaId}";
    }

    private async Task<SeededRolePermission> InsertRolePermissionAsync(string scopeMode, bool includeProject)
    {
        var pair = await GetAvailableRolePermissionPairAsync();

        await using var connection = new SqlConnection(_fixture.Database.ConnectionString);
        await connection.OpenAsync();

        await using var insertRolePermission = connection.CreateCommand();
        insertRolePermission.CommandText = """
            INSERT INTO authz.role_permissions (role_id, permission_id, scope_mode, is_allowed)
            VALUES (@roleId, @permissionId, @scopeMode, 1);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """;
        insertRolePermission.Parameters.AddWithValue("@roleId", pair.RoleId);
        insertRolePermission.Parameters.AddWithValue("@permissionId", pair.PermissionId);
        insertRolePermission.Parameters.AddWithValue("@scopeMode", scopeMode);

        var rolePermissionId = Convert.ToInt32(await insertRolePermission.ExecuteScalarAsync());

        if (includeProject)
        {
            await using var insertProject = connection.CreateCommand();
            insertProject.CommandText = """
                INSERT INTO authz.role_permission_projects (role_permission_id, projekt_id)
                VALUES (@rolePermissionId, @projektId);
                """;
            insertProject.Parameters.AddWithValue("@rolePermissionId", rolePermissionId);
            insertProject.Parameters.AddWithValue("@projektId", _fixture.ProjectId);
            await insertProject.ExecuteNonQueryAsync();
        }

        return new SeededRolePermission(
            rolePermissionId,
            pair.RoleId,
            pair.PermissionId,
            pair.RoleKod,
            pair.PermissionKlic);
    }

    private async Task<AvailableRolePermissionPair> GetAvailableRolePermissionPairAsync()
    {
        await using var connection = new SqlConnection(_fixture.Database.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TOP (1)
                r.id,
                r.kod,
                p.id,
                p.klic
            FROM authz.roles r
            CROSS JOIN authz.permissions p
            LEFT JOIN authz.role_permissions rp
                ON rp.role_id = r.id
               AND rp.permission_id = p.id
            WHERE r.is_active = 1
              AND p.is_active = 1
              AND rp.id IS NULL
            ORDER BY r.id, p.id;
            """;

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Pro E2E test se nepodařilo najít volnou kombinaci role a akce.");
        }

        return new AvailableRolePermissionPair(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetInt32(2),
            reader.GetString(3));
    }

    private async Task<int?> FindRolePermissionIdAsync(int roleId, int permissionId)
    {
        await using var connection = new SqlConnection(_fixture.Database.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TOP (1) id
            FROM authz.role_permissions
            WHERE role_id = @roleId
              AND permission_id = @permissionId;
            """;
        command.Parameters.AddWithValue("@roleId", roleId);
        command.Parameters.AddWithValue("@permissionId", permissionId);

        var result = await command.ExecuteScalarAsync();
        return result is null ? null : Convert.ToInt32(result);
    }

    private async Task<bool> RolePermissionExistsAsync(int rolePermissionId)
    {
        await using var connection = new SqlConnection(_fixture.Database.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM authz.role_permissions
            WHERE id = @id;
            """;
        command.Parameters.AddWithValue("@id", rolePermissionId);

        return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
    }

    private async Task<int> CountRolePermissionProjectsAsync(int rolePermissionId)
    {
        await using var connection = new SqlConnection(_fixture.Database.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM authz.role_permission_projects
            WHERE role_permission_id = @id;
            """;
        command.Parameters.AddWithValue("@id", rolePermissionId);

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private async Task DeleteRolePermissionDirectAsync(int rolePermissionId)
    {
        await using var connection = new SqlConnection(_fixture.Database.ConnectionString);
        await connection.OpenAsync();

        await using var deleteProjects = connection.CreateCommand();
        deleteProjects.CommandText = """
            DELETE FROM authz.role_permission_projects
            WHERE role_permission_id = @id;
            """;
        deleteProjects.Parameters.AddWithValue("@id", rolePermissionId);
        await deleteProjects.ExecuteNonQueryAsync();

        await using var deleteRolePermission = connection.CreateCommand();
        deleteRolePermission.CommandText = """
            DELETE FROM authz.role_permissions
            WHERE id = @id;
            """;
        deleteRolePermission.Parameters.AddWithValue("@id", rolePermissionId);
        await deleteRolePermission.ExecuteNonQueryAsync();
    }

    private static ILocator GetRolePermissionRow(IPage page, SeededRolePermission mapping)
    {
        return GetRolePermissionRow(page, mapping.RoleKod, mapping.PermissionKlic);
    }

    private static ILocator GetRolePermissionRow(IPage page, string roleKod, string permissionKlic)
    {
        return page.Locator("table tbody tr")
            .Filter(new LocatorFilterOptions { HasTextString = roleKod })
            .Filter(new LocatorFilterOptions { HasTextString = permissionKlic });
    }

    private static ILocatorAssertions Expect(ILocator locator)
    {
        return Assertions.Expect(locator);
    }

    private sealed record AvailableRolePermissionPair(int RoleId, string RoleKod, int PermissionId, string PermissionKlic);

    private sealed record SeededRolePermission(int Id, int RoleId, int PermissionId, string RoleKod, string PermissionKlic);
}
