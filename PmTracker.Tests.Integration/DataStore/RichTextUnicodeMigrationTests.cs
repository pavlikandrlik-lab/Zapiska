using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using PmTracker.Tests.Common;
using PmTracker.Tests.Integration.TestInfrastructure;

namespace PmTracker.Tests.Integration.DataStore;

/// <summary>
/// db_upgrade_1_4_6: rich text (popis, vyjádření, požadavek) se ukládá v Unicode a bez
/// entit pro písmena. Sloupce typu text (CP1250) by znaky mimo kódovou stránku změnily
/// na „?", proto je skript převádí na NVARCHAR(MAX); data v entitách převede zpět.
/// </summary>
[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class RichTextUnicodeMigrationTests
{
    private const string MigrationFile = "db_upgrade_1_4_6_richtext_unicode.sql";

    private readonly SqlIntegrationFixture _fixture;

    public RichTextUnicodeMigrationTests(SqlIntegrationFixture fixture) => _fixture = fixture;

    private static async Task<T?> ScalarAsync<T>(string connectionString, string sql, params (string Name, object Value)[] ps)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in ps)
        {
            cmd.Parameters.AddWithValue(name, value);
        }

        var result = await cmd.ExecuteScalarAsync();
        return result is null or DBNull ? default : (T)result;
    }

    private static Task RunMigrationAsync(string connectionString) =>
        SqlScriptRunner.ExecuteScriptsAsync(connectionString, [Path.Combine(RepositoryPaths.Root, MigrationFile)]);

    [Theory]
    [InlineData("vyjadreni", "text_vyjadreni")]
    [InlineData("projektove_zaznamy", "popis")]
    [InlineData("zaznam_historie_terminu", "duvod")]
    [InlineData("zaznam_externi_odkazy", "pozadavek")]
    public async Task Sloupce_JsouUnicodeNVarcharMax(string table, string column)
    {
        var db = await _fixture.CreateDatabaseAsync("richtext_typy");

        var type = await ScalarAsync<string>(db.ConnectionString, """
            SELECT ty.name + '(' + CAST(c.max_length AS varchar(10)) + ')'
            FROM sys.columns c JOIN sys.types ty ON ty.user_type_id = c.user_type_id
            WHERE c.object_id = OBJECT_ID(@t) AND c.name = @c
            """, ("@t", "dbo." + table), ("@c", column));

        type.Should().Be("nvarchar(-1)");
    }

    [Fact]
    public async Task Vyjadreni_UloziZnakyMimoCp1250BezeZtraty()
    {
        var db = await _fixture.CreateDatabaseAsync("richtext_roundtrip");
        var seed = await SearchSeed.CreateAsync(db.ConnectionString);
        var zaznamId = await seed.AddRecordAsync(seed.ProjektId, "Záznam");
        var jednaniId = await seed.AddMeetingAsync(seed.ProjektId, cisloJednani: 9101);
        const string text = "<p>Řešení 😀 Жук → ≥ ✓</p>";

        var id = await seed.AddStatementAsync(zaznamId, jednaniId, text);

        var stored = await ScalarAsync<string>(db.ConnectionString,
            "SELECT text_vyjadreni FROM dbo.vyjadreni WHERE id = @id", ("@id", id));
        stored.Should().Be(text);
    }

    [Fact]
    public async Task Migrace_PrevedeEntityNaZnaky_NebezpecneZnakyNecha_AJeIdempotentni()
    {
        var db = await _fixture.CreateDatabaseAsync("richtext_migrace");
        var seed = await SearchSeed.CreateAsync(db.ConnectionString);

        // Starý tvar = výstup HtmlEncoder.Default, jak ho do 1.4.5 ukládal RichTextContentService.
        static string Legacy(string text) => HtmlEncoder.Default.Encode(text);
        var popisLegacy = $"<p>{Legacy("Ověřit zálohování „serveru“ & <kód>")}</p>";
        var vyjadreniLegacy = $"<ul><li>{Legacy("Rozhodnuto o řešení 😀 Жук")}</li></ul>";
        var pozadavekLegacy = $"<p>{Legacy("Požadavek: dodat 3× „licenci“")}</p>";
        const string bezEntit = "<p>Holý text bez entit</p>";

        popisLegacy.Should().Contain("&#x").And.Contain("&amp;").And.Contain("&lt;", "seed má odpovídat starému tvaru");

        var zaznamId = await seed.AddRecordAsync(seed.ProjektId, "Záznam", popis: popisLegacy);
        var zaznamBezEntitId = await seed.AddRecordAsync(seed.ProjektId, "Záznam 2", popis: bezEntit);
        var jednaniId = await seed.AddMeetingAsync(seed.ProjektId, cisloJednani: 9102);
        var vyjadreniId = await seed.AddStatementAsync(zaznamId, jednaniId, vyjadreniLegacy);
        var odkazId = await seed.AddExternalLinkAsync(zaznamId, "123456");
        await ScalarAsync<object>(db.ConnectionString,
            "UPDATE dbo.zaznam_externi_odkazy SET pozadavek = @p WHERE id = @id", ("@p", pozadavekLegacy), ("@id", odkazId));

        await RunMigrationAsync(db.ConnectionString);

        async Task AssertStateAsync()
        {
            (await ScalarAsync<string>(db.ConnectionString, "SELECT popis FROM dbo.projektove_zaznamy WHERE id = @id", ("@id", zaznamId)))
                .Should().Be("<p>Ověřit zálohování „serveru“ &amp; &lt;kód&gt;</p>");
            (await ScalarAsync<string>(db.ConnectionString, "SELECT text_vyjadreni FROM dbo.vyjadreni WHERE id = @id", ("@id", vyjadreniId)))
                .Should().Be("<ul><li>Rozhodnuto o řešení 😀 Жук</li></ul>");
            (await ScalarAsync<string>(db.ConnectionString, "SELECT pozadavek FROM dbo.zaznam_externi_odkazy WHERE id = @id", ("@id", odkazId)))
                .Should().Be("<p>Požadavek: dodat 3× „licenci“</p>");
            (await ScalarAsync<string>(db.ConnectionString, "SELECT popis FROM dbo.projektove_zaznamy WHERE id = @id", ("@id", zaznamBezEntitId)))
                .Should().Be(bezEntit);
        }

        await AssertStateAsync();

        await RunMigrationAsync(db.ConnectionString);
        await AssertStateAsync();
    }
}
