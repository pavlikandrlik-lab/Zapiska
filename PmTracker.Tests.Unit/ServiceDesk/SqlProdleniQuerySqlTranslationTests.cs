using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.ServiceDesk.Sql;

namespace PmTracker.Tests.Unit.ServiceDesk;

/// <summary>
/// Překlad dotazů prodlení pro skutečný SQL Server (bez připojení — jen <c>ToQueryString</c>).
/// InMemory provider převodníky ani SQL sémantiku NULL neověří: 2026-11-11 dotaz s <c>z.Id != ""</c>
/// prošel všemi testy a proti SQL Serveru nevrátil nic (<c>[id] &lt;&gt; NULL</c> není nikdy pravda).
/// </summary>
public sealed class SqlProdleniQuerySqlTranslationTests
{
    private static TicketingReadOnlyDbContext Intranet()
        => new(new DbContextOptionsBuilder<TicketingReadOnlyDbContext>()
            .UseSqlServer("Server=nikde;Database=nic;Encrypt=False", sql => sql.UseCompatibilityLevel(120))
            .Options);

    private static ServiceDeskExternalReadOnlyDbContext Sd()
        => new(new DbContextOptionsBuilder<ServiceDeskExternalReadOnlyDbContext>()
            .UseSqlServer("Server=nikde;Database=nic;Encrypt=False", sql => sql.UseCompatibilityLevel(120))
            .Options);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Tickety_is_se_filtruji_pres_is_not_null_a_nikdy_neporovnavaji_s_null(bool vcetneArchivu)
    {
        using var db = Intranet();

        var sql = SqlProdleniQueryService.ZaznamyIs(db.HotZaznamy, ["R_EIS", "TP_RZA"], vcetneArchivu).ToQueryString();

        sql.Should().Contain("[id] IS NOT NULL");
        sql.Should().NotContain("<> NULL").And.NotContain("= NULL");
        sql.Should().Contain("IN (N'R_EIS', N'TP_RZA')", "úroveň kompatibility 120 nemá OPENJSON");
        if (vcetneArchivu)
        {
            sql.Should().NotContain("archiv");
        }
        else
        {
            sql.Should().Contain("N'archiv'");
        }
    }

    [Fact]
    public void Ticket_external_se_paruje_po_orezu_mezer_na_strane_serveru()
    {
        using var db = Sd();

        var sql = SqlProdleniQueryService.TicketExternalPodleCisel(db.TicketExternal, ["900101", "900108"]).ToQueryString();

        sql.Should().Contain("LTRIM(RTRIM([t].[external_id])) IN ('900101', '900108')",
            "external_id je varchar — parametry nesmí být nvarchar (N'…'), jinak SQL Server nepoužije index");
        sql.Should().NotContain("OPENJSON");
    }
}
