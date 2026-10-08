using FluentAssertions;

namespace PmTracker.Tests.Unit.Data;

/// <summary>
/// Detektor zápisů HTML mimo háček čistého textu (drobnost z review 2026-10-08). Starý
/// regulární výraz chytal jen <c>UPDATE dbo.tabulka SET …</c> — tvar bez <c>dbo.</c>,
/// s hranatými závorkami, s aliasem nebo <c>MERGE</c> prošel bez povšimnutí.
/// </summary>
public sealed class RichTextHtmlWriteGuardTests
{
    [Theory]
    [InlineData("UPDATE dbo.projektove_zaznamy SET popis = @p WHERE id = 1")]
    [InlineData("UPDATE projektove_zaznamy SET popis = @p")]
    [InlineData("UPDATE [dbo].[vyjadreni] SET [text_vyjadreni] = @t WHERE id = @id")]
    [InlineData("update dbo.zaznam_externi_odkazy set pozadavek = N'x'")]
    [InlineData("UPDATE z SET z.popis = @p FROM dbo.projektove_zaznamy z WHERE z.id = 1")]
    [InlineData("MERGE INTO dbo.zaznam_externi_odkazy AS t USING src AS s ON t.id = s.id WHEN MATCHED THEN UPDATE SET t.pozadavek = s.pozadavek")]
    [InlineData("await db.ProjektoveZaznamy.Where(x => x.Id == 1).ExecuteUpdateAsync(s => s.SetProperty(x => x.Popis, \"a\"), ct)")]
    [InlineData("db.Vyjadreni.ExecuteUpdate(s => s.SetProperty(v => v.TextVyjadreni, v => v.TextVyjadreni + \"!\"))")]
    public void ZapisHtmlMimoEf_Najde(string zdroj)
    {
        RichTextHtmlWriteGuard.FindRawHtmlWrites(zdroj).Should().ContainSingle();
    }

    [Theory]
    [InlineData("UPDATE dbo.projektove_zaznamy SET nazev = @n WHERE popis = @p")]
    [InlineData("UPDATE dbo.projektove_zaznamy SET popis_prosty_text = NULL WHERE popis_prosty_text IS NOT NULL")]
    [InlineData("UPDATE dbo.ciselnik_stavu_ukolu SET popis = @p WHERE id = 1")]
    [InlineData("SELECT popis FROM dbo.projektove_zaznamy WHERE id = 1")]
    [InlineData("await db.ZaznamEditZamky.Where(x => x.Id == 1).ExecuteUpdateAsync(s => s.SetProperty(x => x.HeartbeatAt, now), ct)")]
    [InlineData("db.ProjektoveZaznamy.ExecuteUpdate(s => s.SetProperty(x => x.PopisProstyText, (string?)null))")]
    public void JinyPrikaz_Nenajde(string zdroj)
    {
        RichTextHtmlWriteGuard.FindRawHtmlWrites(zdroj).Should().BeEmpty();
    }

    [Fact]
    public void ViceprikazovyZdroj_NajdeJenPrikazSeZapisemHtml()
    {
        const string zdroj = """
            UPDATE dbo.projektove_zaznamy SET nazev = N'a';
            UPDATE dbo.vyjadreni SET text_vyjadreni = N'b' WHERE id = 1;
            """;

        RichTextHtmlWriteGuard.FindRawHtmlWrites(zdroj).Should().ContainSingle()
            .Which.Should().Contain("text_vyjadreni");
    }

    [Theory]
    [InlineData("UPDATE dbo.projektove_zaznamy SET popis = REPLACE(popis, N'a', N'b')")]
    [InlineData("UPDATE dbo.vyjadreni SET text_vyjadreni = N'x', popis_prosty_text = NULL")]
    [InlineData("UPDATE dbo.zaznam_externi_odkazy SET pozadavek = N'x', pozadavek_prosty_text = N'x'")]
    public void Migrace_ZmenaHtmlBezVynulovaniCistehoTextu_Najde(string sql)
    {
        RichTextHtmlWriteGuard.FindHtmlWritesWithoutPlainReset(sql).Should().ContainSingle();
    }

    [Theory]
    [InlineData("UPDATE dbo.projektove_zaznamy SET popis = REPLACE(popis, N'a', N'b'), popis_prosty_text = NULL")]
    [InlineData("UPDATE [dbo].[vyjadreni] SET [text_vyjadreni] = N'x', [text_vyjadreni_prosty_text] = NULL")]
    [InlineData("UPDATE dbo.projektove_zaznamy SET popis_prosty_text = NULL")]
    public void Migrace_ZmenaHtmlSVynulovanimCistehoTextu_Projde(string sql)
    {
        RichTextHtmlWriteGuard.FindHtmlWritesWithoutPlainReset(sql).Should().BeEmpty();
    }

    [Theory]
    [InlineData("db_upgrade_1_4_7_prosty_text_hledani.sql", false)]
    [InlineData("db_upgrade_1_4_6_richtext_unicode.sql", false)]
    [InlineData("db_upgrade_0_4_membership_subsystems.sql", false)]
    [InlineData("db_upgrade_1_4_8_neco.sql", true)]
    [InlineData("db_upgrade_1_5_0_neco.sql", true)]
    [InlineData("db_upgrade_2_0_neco.sql", true)]
    public void Migrace_HlidaSeJenNovejsiNez147(string soubor, bool hlidat)
    {
        RichTextHtmlWriteGuard.IsMigrationAfterPlainTextColumns(soubor).Should().Be(hlidat);
    }
}
