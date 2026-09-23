using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// Spec 2026-09-17 §4.3 — dva uživatelé nad jedním záznamem. Druhý se do editoru
/// nedostane a uvidí, kdo záznam upravuje.
///
/// Viditelnost se ověřuje přes ToHaveCountAsync, ne ToBeVisibleAsync: gov komponenty
/// mají hostitelský element s nulovou výškou a Playwright je hlásí jako neviditelné.
/// Seed jde raw SQL přes Database.ConnectionString — stejně jako ostatní E2E scénáře.
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class RecordEditLockScenariosTests
{
    private readonly E2ETestFixture _fixture;

    private const string RecordName = "E2E zamek karty";

    public RecordEditLockScenariosTests(E2ETestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DruhyUzivatel_SeDoEditoruNedostane_AVidiKdoZaznamUpravuje()
    {
        var recordId = await ResolveRecordIdAsync();
        var holderId = await EnsureHolderPersonAsync("Novák", "Jan");
        await SetLockAsync(recordId, holderId);

        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/Zaznamy/Edit?id={recordId}&asUser={_fixture.AdminOsobaId}");

        await Assertions.Expect(page.Locator("[data-record-edit-locked='true']")).ToHaveCountAsync(1);
        await Assertions.Expect(page.Locator("form[data-record-editor-form='true']")).ToHaveCountAsync(0);
        (await page.Locator("[data-record-edit-locked='true']").InnerTextAsync())
            .Should().Contain("Novák Jan", "hláška musí pojmenovat držitele zámku");

        // Držitel zámek uvolní (uložil nebo odešel) → editor se otevře.
        await ClearLockAsync(recordId);
        await page.GotoAsync($"{_fixture.BaseUrl}/Zaznamy/Edit?id={recordId}&asUser={_fixture.AdminOsobaId}");

        await Assertions.Expect(page.Locator("form[data-record-editor-form='true']")).ToHaveCountAsync(1);
        await Assertions.Expect(page.Locator("[data-record-edit-locked='true']")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task OtevreniEditoru_VystaviIdZamkuProHeartbeat()
    {
        var recordId = await ResolveRecordIdAsync();
        await ClearLockAsync(recordId);

        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/Zaznamy/Edit?id={recordId}&asUser={_fixture.AdminOsobaId}");
        await Assertions.Expect(page.Locator("form[data-record-editor-form='true']")).ToHaveCountAsync(1);

        var lockId = await page.Locator("html").GetAttributeAsync("data-record-edit-lock-id");
        lockId.Should().Be(recordId.ToString(),
            "bez tohoto atributu by keep-alive neposílal heartbeat a zámek by při delší editaci vypršel");

        await ClearLockAsync(recordId);
    }

    /// <summary>
    /// E2E seed žádné záznamy nezakládá (ostatní scénáře si je tvoří přes UI). Pro test
    /// zámku stačí řádek v DB — editor se otevírá přímo na URL, žádný UI průchod není třeba.
    /// </summary>
    private async Task<int> ResolveRecordIdAsync()
    {
        await using var connection = new SqlConnection(_fixture.Database.ConnectionString);
        await connection.OpenAsync();

        await using var findCommand = connection.CreateCommand();
        findCommand.CommandText = """
            SELECT TOP (1) id
            FROM dbo.projektove_zaznamy
            WHERE projekt_id = @projectId AND nazev = @nazev
            ORDER BY id;
            """;
        findCommand.Parameters.AddWithValue("@projectId", _fixture.ProjectId);
        findCommand.Parameters.AddWithValue("@nazev", RecordName);
        var existing = await findCommand.ExecuteScalarAsync();
        if (existing is not null && existing != DBNull.Value)
        {
            return Convert.ToInt32(existing);
        }

        await using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = """
            DECLARE @kategorieId INT = (SELECT TOP (1) id FROM dbo.ciselnik_kategorii_zaznamu ORDER BY id);
            DECLARE @stavId      INT = (SELECT TOP (1) id FROM dbo.ciselnik_stavu_ukolu ORDER BY id);
            DECLARE @subsystemId INT = (SELECT TOP (1) subsystem_id FROM dbo.projekt_subsystemy
                                        WHERE projekt_id = @projectId ORDER BY subsystem_id);
            DECLARE @cislo INT = ISNULL((SELECT MAX(cislo_zaznamu) FROM dbo.projektove_zaznamy
                                         WHERE projekt_id = @projectId), 0) + 1;

            INSERT INTO dbo.projektove_zaznamy
                (projekt_id, kategorie_id, stav_ukolu_id, cislo_zaznamu, cislo_viditelne,
                 cislo_viditelne_typ, cislo_viditelne_a, cislo_viditelne_b,
                 nazev, vlastnik_id, datum_zalozeni, datum_ukonceni, subsystem_id)
            VALUES
                (@projectId, @kategorieId, @stavId, @cislo, CAST(@cislo AS NVARCHAR(32)),
                 0, @cislo, 0,
                 @nazev, @vlastnikId, '2026-09-01', '2026-12-31', @subsystemId);

            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;
        insertCommand.Parameters.AddWithValue("@projectId", _fixture.ProjectId);
        insertCommand.Parameters.AddWithValue("@nazev", RecordName);
        insertCommand.Parameters.AddWithValue("@vlastnikId", _fixture.AdminOsobaId);
        return Convert.ToInt32(await insertCommand.ExecuteScalarAsync());
    }

    private async Task<int> EnsureHolderPersonAsync(string prijmeni, string jmeno)
    {
        await using var connection = new SqlConnection(_fixture.Database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TOP (1) id
            FROM dbo.osoby
            WHERE id <> @adminId
            ORDER BY id;
            """;
        command.Parameters.AddWithValue("@adminId", _fixture.AdminOsobaId);
        var found = await command.ExecuteScalarAsync();

        // E2E seed zakládá jen admina; druhého uživatele si test musí vytvořit sám.
        if (found is null || found == DBNull.Value)
        {
            await using var createCommand = connection.CreateCommand();
            createCommand.CommandText = """
                DECLARE @orgId INT = (SELECT TOP (1) organizace_id FROM dbo.osoby ORDER BY id);
                DECLARE @orgCelekId INT = (SELECT TOP (1) organizacni_celek_id FROM dbo.osoby ORDER BY id);
                INSERT INTO dbo.osoby(jmeno, prijmeni, titul, email, organizacni_celek_id, organizace_id, Guid_AD)
                VALUES (@jmeno, @prijmeni, NULL, N'e2e.zamek@pmtracker.local', @orgCelekId, @orgId, NULL);
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """;
            createCommand.Parameters.AddWithValue("@jmeno", jmeno);
            createCommand.Parameters.AddWithValue("@prijmeni", prijmeni);
            return Convert.ToInt32(await createCommand.ExecuteScalarAsync());
        }

        var holderId = Convert.ToInt32(found);

        await using var rename = connection.CreateCommand();
        rename.CommandText = """
            UPDATE dbo.osoby
            SET prijmeni = @prijmeni, jmeno = @jmeno
            WHERE id = @id;
            """;
        rename.Parameters.AddWithValue("@prijmeni", prijmeni);
        rename.Parameters.AddWithValue("@jmeno", jmeno);
        rename.Parameters.AddWithValue("@id", holderId);
        await rename.ExecuteNonQueryAsync();
        return holderId;
    }

    private async Task SetLockAsync(int recordId, int osobaId)
    {
        await using var connection = new SqlConnection(_fixture.Database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM dbo.zaznam_edit_zamek WHERE zaznam_id = @recordId;
            INSERT INTO dbo.zaznam_edit_zamek (zaznam_id, osoba_id, ziskano_at, heartbeat_at)
            VALUES (@recordId, @osobaId, SYSUTCDATETIME(), SYSUTCDATETIME());
            """;
        command.Parameters.AddWithValue("@recordId", recordId);
        command.Parameters.AddWithValue("@osobaId", osobaId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task ClearLockAsync(int recordId)
    {
        await using var connection = new SqlConnection(_fixture.Database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM dbo.zaznam_edit_zamek WHERE zaznam_id = @recordId;";
        command.Parameters.AddWithValue("@recordId", recordId);
        await command.ExecuteNonQueryAsync();
    }
}
