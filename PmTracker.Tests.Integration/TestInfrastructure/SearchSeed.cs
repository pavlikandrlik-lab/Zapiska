using Microsoft.Data.SqlClient;
using PmTracker.Web.Services.Common;

namespace PmTracker.Tests.Integration.TestInfrastructure;

/// <summary>
/// Minimální seed pro testy vyhledávání. Píše se přímo SQL, protože přes aplikační
/// službu by to táhlo validace, které s vyhledáváním nesouvisí.
///
/// Sloupce ověřeny proti reálnému schématu (sys.columns) 2026-09-21:
///  * subsystemy má sloupec [kód] (s diakritikou), ne kod;
///  * osoby.organizace_id, projekty.stav_id a jednani.cas_zacatek jsou NOT NULL
///    bez defaultu, takže musí být v INSERTu.
/// </summary>
public sealed class SearchSeed
{
    private readonly string _connectionString;

    public int ProjektId { get; private init; }
    public int SubsystemId { get; private init; }
    public string SubsystemKod { get; private init; } = "R_EIS";
    public int OsobaId { get; private init; }
    public int KategorieId { get; private init; }

    private SearchSeed(string connectionString) => _connectionString = connectionString;

    private async Task<int> ScalarAsync(string sql, params (string Name, object Value)[] ps)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in ps)
        {
            cmd.Parameters.AddWithValue(name, value);
        }
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public static async Task<SearchSeed> CreateAsync(string connectionString)
    {
        var tmp = new SearchSeed(connectionString);

        // osoby.organizace_id je NOT NULL (FK -> ciselnik_organizace); dev seed jednu založí.
        var osobaId = await tmp.ScalarAsync("""
            INSERT INTO dbo.osoby (jmeno, prijmeni, email, organizace_id)
            OUTPUT INSERTED.id
            VALUES (N'Jan', N'Novák', N'jan.novak@example.cz',
                    (SELECT TOP (1) id FROM dbo.ciselnik_organizace ORDER BY id));
            """);

        // Pozor: sloupec se jmenuje [kód], ne kod.
        var subsystemId = await tmp.ScalarAsync("""
            INSERT INTO dbo.subsystemy ([kód], nazev)
            OUTPUT INSERTED.id VALUES (N'R_EIS', N'Registr EIS');
            """);

        var kategorieId = await tmp.ScalarAsync("""
            SELECT TOP (1) id FROM dbo.ciselnik_kategorii_zaznamu ORDER BY id;
            """);

        var seed = new SearchSeed(connectionString)
        {
            OsobaId = osobaId,
            SubsystemId = subsystemId,
            KategorieId = kategorieId
        };

        var projektId = await seed.AddProjectAsync("PRVNI");
        return new SearchSeed(connectionString)
        {
            OsobaId = osobaId,
            SubsystemId = subsystemId,
            KategorieId = kategorieId,
            ProjektId = projektId
        };
    }

    // projekty.stav_id je NOT NULL (FK -> ciselnik_stavu_projektu), bez defaultu.
    public Task<int> AddProjectAsync(string zkratka) => ScalarAsync("""
        INSERT INTO dbo.projekty (zkratka, cely_nazev, stav_id)
        OUTPUT INSERTED.id
        VALUES (@zkratka, @nazev, (SELECT TOP (1) id FROM dbo.ciselnik_stavu_projektu ORDER BY id));
        """, ("@zkratka", zkratka), ("@nazev", $"Projekt {zkratka}"));

    // cislo_zaznamu má unique index (projekt_id, cislo_zaznamu), takže se dopočítává
    // per projekt — jinak druhý záznam ve stejném projektu spadne na duplicitní klíč.
    //
    // Jako aplikace: čistý text plní PmTrackerDbContext; přímý INSERT ho musí doplnit sám.
    // withPlainText: false = data z doby před 1_4_7.
    public Task<int> AddRecordAsync(int projektId, string nazev, string? popis = null,
        string? cisloViditelne = null, bool withPlainText = true) => ScalarAsync("""
        INSERT INTO dbo.projektove_zaznamy
            (projekt_id, kategorie_id, cislo_zaznamu, cislo_viditelne, nazev, popis,
             popis_prosty_text, vlastnik_id, datum_zalozeni, datum_ukonceni, subsystem_id)
        OUTPUT INSERTED.id
        VALUES (@projekt, @kategorie,
                (SELECT ISNULL(MAX(cislo_zaznamu), 0) + 1 FROM dbo.projektove_zaznamy WHERE projekt_id = @projekt),
                @cisloViditelne, @nazev, @popis, @popisProstyText,
                @vlastnik, SYSUTCDATETIME(), SYSUTCDATETIME(), @subsystem);
        """,
        ("@projekt", projektId), ("@kategorie", KategorieId),
        ("@cisloViditelne", (object?)cisloViditelne ?? DBNull.Value),
        ("@nazev", nazev), ("@popis", (object?)popis ?? DBNull.Value),
        ("@popisProstyText", (object?)(withPlainText ? RichTextSearchText.FromHtml(popis) : null) ?? DBNull.Value),
        ("@vlastnik", OsobaId), ("@subsystem", SubsystemId));

    // jednani.cas_zacatek je NOT NULL (time), bez defaultu.
    public Task<int> AddMeetingAsync(int projektId, int cisloJednani) => ScalarAsync("""
        INSERT INTO dbo.jednani (projekt_id, cislo_jednani, datum_planovane, cas_zacatek, stav_jednani_id)
        OUTPUT INSERTED.id
        VALUES (@projekt, @cislo, SYSUTCDATETIME(), '09:00:00',
                (SELECT TOP (1) id FROM dbo.ciselnik_stavu_jednani ORDER BY id));
        """, ("@projekt", projektId), ("@cislo", cisloJednani));

    // Jako aplikace: čistý text plní PmTrackerDbContext; přímý INSERT ho musí doplnit sám.
    // withPlainText: false = data z doby před 1_4_7.
    public Task<int> AddStatementAsync(int zaznamId, int jednaniId, string text, bool withPlainText = true) => ScalarAsync("""
        INSERT INTO dbo.vyjadreni
            (zaznam_id, jednani_id, autor_osoba_id, text_vyjadreni, text_vyjadreni_prosty_text, datum_vyjadreni)
        OUTPUT INSERTED.id VALUES (@zaznam, @jednani, @autor, @text, @textProstyText, SYSUTCDATETIME());
        """,
        ("@zaznam", zaznamId), ("@jednani", jednaniId), ("@autor", OsobaId), ("@text", text),
        ("@textProstyText", (object?)(withPlainText ? RichTextSearchText.FromHtml(text) : null) ?? DBNull.Value));

    public Task<int> AddExternalLinkAsync(int zaznamId, string cislo) => ScalarAsync("""
        INSERT INTO dbo.zaznam_externi_odkazy (zaznam_id, typ_odkazu_id, cislo)
        OUTPUT INSERTED.id
        VALUES (@zaznam, (SELECT TOP (1) id FROM dbo.ciselnik_typu_externich_odkazu ORDER BY id), @cislo);
        """, ("@zaznam", zaznamId), ("@cislo", cislo));
}
