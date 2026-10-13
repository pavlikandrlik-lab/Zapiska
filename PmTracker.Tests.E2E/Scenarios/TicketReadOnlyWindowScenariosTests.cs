using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// 2026-10-13: zástupce vedoucího subsystému (bez vyjadreni.modal.open) klikne na stránce
/// záznamu na bublinu vytěžené skutečnosti a otevře se okno tiketu jen pro čtení — dřív
/// „Nepodařilo se načíst vyjádření" (403). SD je v E2E vypnutý, okno proto ukáže prázdný
/// obsah; ověřuje se režim, odpověď 200 a absence ovládání.
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class TicketReadOnlyWindowScenariosTests
{
    private readonly E2ETestFixture _fixture;

    public TicketReadOnlyWindowScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    private async Task<(int RecordId, int DeputyId)> SeedAsync()
    {
        await using var connection = new SqlConnection(_fixture.Database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @kat INT = (SELECT TOP (1) id FROM dbo.ciselnik_kategorii_zaznamu
                WHERE kod = N'U' OR nazev LIKE N'%kol%' ORDER BY CASE WHEN kod = N'U' THEN 0 ELSE 1 END, id);
            DECLARE @stav INT = (SELECT TOP (1) id FROM dbo.ciselnik_stavu_ukolu ORDER BY id);
            DECLARE @sub INT = (SELECT TOP (1) id FROM dbo.subsystemy ORDER BY id);

            IF NOT EXISTS (SELECT 1 FROM dbo.projekt_subsystemy WHERE projekt_id = @p AND subsystem_id = @sub AND datum_odebrani IS NULL)
                INSERT INTO dbo.projekt_subsystemy (projekt_id, subsystem_id, poradi, datum_prirazeni)
                SELECT @p, @sub, ISNULL(MAX(poradi), 0) + 1, SYSUTCDATETIME() FROM dbo.projekt_subsystemy WHERE projekt_id = @p;
            DECLARE @ps INT = (SELECT TOP (1) id FROM dbo.projekt_subsystemy
                WHERE projekt_id = @p AND subsystem_id = @sub AND datum_odebrani IS NULL ORDER BY id);

            DECLARE @deputy INT = (SELECT TOP (1) id FROM dbo.osoby WHERE email = N'e2e.zastupce@test.local');
            IF @deputy IS NULL
            BEGIN
                INSERT INTO dbo.osoby (jmeno, prijmeni, email, organizacni_celek_id, organizace_id, location_locked)
                SELECT N'E2E', N'Zastupce', N'e2e.zastupce@test.local', organizacni_celek_id, organizace_id, 0
                FROM dbo.osoby WHERE id = @admin;
                SET @deputy = CAST(SCOPE_IDENTITY() AS INT);
            END
            DECLARE @role INT = (SELECT id FROM dbo.ciselnik_roli_subsystemu WHERE kod = N'ZASTUPCE_VEDOUCIHO_SUBSYSTEMU');
            IF NOT EXISTS (SELECT 1 FROM dbo.obsazeni_subsystemu_projektu
                WHERE projekt_subsystem_id = @ps AND osoba_id = @deputy AND role_subsystemu_id = @role AND datum_odebrani IS NULL)
                INSERT INTO dbo.obsazeni_subsystemu_projektu (projekt_subsystem_id, osoba_id, role_subsystemu_id, datum_prirazeni)
                VALUES (@ps, @deputy, @role, SYSUTCDATETIME());

            DECLARE @rid INT = (SELECT TOP (1) id FROM dbo.projektove_zaznamy WHERE projekt_id = @p AND nazev = @marker ORDER BY id);
            IF @rid IS NULL
            BEGIN
                DECLARE @num INT = (SELECT ISNULL(MAX(cislo_zaznamu), 0) + 1 FROM dbo.projektove_zaznamy WHERE projekt_id = @p);
                INSERT INTO dbo.projektove_zaznamy
                    (projekt_id, kategorie_id, stav_ukolu_id, cislo_zaznamu, nazev, cil, popis,
                     vlastnik_id, datum_zalozeni, datum_ukonceni, subsystem_id)
                VALUES (@p, @kat, @stav, @num, @marker, @marker, @marker,
                        @admin, DATEADD(day, -30, CONVERT(DATE, GETDATE())), DATEADD(day, 45, CONVERT(DATE, GETDATE())), @sub);
                SET @rid = CAST(SCOPE_IDENTITY() AS INT);
            END

            DELETE FROM dbo.zaznam_harmonogram_vyjadreni_vazba WHERE zaznam_id = @rid;
            DELETE FROM dbo.zaznam_externi_odkazy WHERE zaznam_id = @rid;
            DELETE FROM dbo.zaznam_harmonogram_krok WHERE zaznam_id = @rid;
            DECLARE @start DATE = DATEADD(day, -30, CONVERT(DATE, GETDATE()));
            DECLARE @i INT = 1;
            WHILE @i <= 10
            BEGIN
                -- Krok 3 = skutečnost vytěžená automatem (zdroj 1, režim Auto 0).
                INSERT INTO dbo.zaznam_harmonogram_krok
                    (zaznam_id, poradi, plan_datum, skutecnost_datum, skutecnost_zdroj, skutecnost_rezim, updated_at)
                VALUES (@rid, @i, DATEADD(day, @i * 7, @start),
                        CASE WHEN @i = 3 THEN DATEADD(day, 20, @start) ELSE NULL END,
                        CASE WHEN @i = 3 THEN 1 ELSE 0 END, 0, SYSUTCDATETIME());
                SET @i = @i + 1;
            END

            DECLARE @typ INT = (SELECT id FROM dbo.ciselnik_typu_externich_odkazu WHERE kod = N'PMP');
            INSERT INTO dbo.zaznam_externi_odkazy (zaznam_id, typ_odkazu_id, cislo, zaradid_do_vyzvy) VALUES (@rid, @typ, N'815001', 0);
            DECLARE @eo INT = CAST(SCOPE_IDENTITY() AS INT);
            INSERT INTO dbo.zaznam_harmonogram_vyjadreni_vazba
                (zaznam_id, externi_odkaz_id, hot_vyjadreni_id, datum_vyjadreni, source, stav, created_at, poradi)
            VALUES (@rid, @eo, 7201, DATEADD(day, 20, @start), 1, 1, SYSUTCDATETIME(), 3);

            SELECT @rid AS rid, @deputy AS deputy;
            """;
        command.Parameters.AddWithValue("@p", _fixture.ProjectId);
        command.Parameters.AddWithValue("@admin", _fixture.AdminOsobaId);
        command.Parameters.AddWithValue("@marker", "E2E okno tiketu jen pro cteni");
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue("seed vrací id záznamu a zástupce");
        return (reader.GetInt32(0), reader.GetInt32(1));
    }

    private static async Task DispatchClickAsync(ILocator locator)
    {
        await locator.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });
        await locator.First.DispatchEventAsync("click");
    }

    [Fact]
    public async Task Zastupce_KlikNaBublinu_OtevreOknoJenProCteni()
    {
        var (recordId, deputyId) = await SeedAsync();
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/Zaznamy/Detail/{recordId}?asUser={deputyId}");

        await DispatchClickAsync(page.Locator("[data-schedule-view-toggle='table']"));
        var bubble = page.Locator(".schedule-table-wrap [data-external-chat-open][data-external-chat-mode='read']");
        await Assertions.Expect(bubble).ToHaveCountAsync(1);

        var responseTask = page.WaitForResponseAsync(r => r.Url.Contains("/Zaznamy/VyjadreniNahled"));
        await DispatchClickAsync(bubble);
        var response = await responseTask;
        response.Status.Should().Be(200, "okno jen pro čtení nepotřebuje klíč vyjadreni.modal.open");
        response.Url.Should().Contain($"asUser={deputyId}", "AJAX nese identitu přes appendCurrentAsUser");

        await Assertions.Expect(page.Locator("[data-chat-modal-root][data-chat-modal-mode='read']")).ToHaveCountAsync(1);
        await Assertions.Expect(page.Locator("[data-chat-modal-root] [data-bubble-step-selector]")).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator("[data-chat-modal-root] [data-reharvest-form]")).ToHaveCountAsync(0);

        await page.Context.CloseAsync();
    }
}
