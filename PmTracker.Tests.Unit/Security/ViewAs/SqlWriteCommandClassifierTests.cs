using FluentAssertions;
using PmTracker.Web.Services.Security.ViewAs;

namespace PmTracker.Tests.Unit.Security.ViewAs;

public sealed class SqlWriteCommandClassifierTests
{
    [Theory]
    [InlineData("SET IMPLICIT_TRANSACTIONS OFF;\nSET NOCOUNT ON;\nINSERT INTO [osoby] ([jmeno]) OUTPUT INSERTED.[id] VALUES (@p0);")]
    [InlineData("SET NOCOUNT ON;\nUPDATE [projektove_zaznamy] SET [nazev] = @p0 WHERE [id] = @p1;\nSELECT @@ROWCOUNT;")]
    [InlineData("SET NOCOUNT ON;\nDELETE FROM [vyjadreni] WHERE [id] = @p0;")]
    [InlineData("SET NOCOUNT ON;\nMERGE [osoby] USING (VALUES (@p0, 0)) AS i ([jmeno], _Position) ON 1=0 WHEN NOT MATCHED THEN INSERT ([jmeno]) VALUES (i.[jmeno]);")]
    [InlineData("UPDATE [z]\nSET [z].[heartbeat_at] = @__nowUtc_0\nFROM [zaznam_edit_zamek] AS [z]\nWHERE [z].[zaznam_id] = @__zaznamId_1")]
    [InlineData("DELETE FROM [z]\nFROM [zaznam_edit_zamek] AS [z]\nWHERE [z].[zaznam_id] = @__zaznamId_0")]
    [InlineData("\nMERGE dbo.zaznam_edit_zamek WITH (HOLDLOCK) AS target\nUSING (SELECT @p0 AS zaznam_id) AS source")]
    [InlineData("-- tag\ninsert into x values (1)")]
    [InlineData("TRUNCATE TABLE dbo.x")]
    public void IsWrite_DetectsWriteStatements(string sql)
        => SqlWriteCommandClassifier.IsWrite(sql).Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("SELECT [o].[id], [o].[jmeno] FROM [osoby] AS [o] WHERE [o].[id] = @__id_0")]
    [InlineData("SELECT * FROM projektove_zaznamy WITH (UPDLOCK, HOLDLOCK) WHERE id = @p0")]
    [InlineData("SELECT [z].[datum_update], [z].[last_insert_note] FROM [z]")]
    [InlineData("WITH cte AS (SELECT 1 AS x) SELECT x FROM cte")]
    [InlineData("SELECT CASE WHEN EXISTS (SELECT 1 FROM [authz].[superadmins] AS [s] WHERE [s].[osoba_id] = @__osobaId_0) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END")]
    public void IsWrite_IgnoresReads(string? sql)
        => SqlWriteCommandClassifier.IsWrite(sql).Should().BeFalse();
}
