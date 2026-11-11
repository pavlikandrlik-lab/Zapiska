using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PmTracker.ServiceDesk.Sql;
using PmTracker.ServiceDesk.Sql.Entities;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

/// <summary>Kontext databáze starého ServiceDesku (spec 2026-10-15 §6.1, schéma z ServiceDesk.edmx).</summary>
public sealed class ServiceDeskExternalReadOnlyDbContextTests
{
    private static ServiceDeskExternalReadOnlyDbContext SqlKontext()
        => new(new DbContextOptionsBuilder<ServiceDeskExternalReadOnlyDbContext>()
            .UseSqlServer("Server=nikam;Database=nic;Integrated Security=True") // model se staví bez připojení
            .Options);

    [Fact]
    public void SaveChanges_vyhodi()
    {
        using var db = new ServiceDeskExternalReadOnlyDbContext(new DbContextOptionsBuilder<ServiceDeskExternalReadOnlyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        db.Invoking(d => d.SaveChanges()).Should().Throw<InvalidOperationException>();
        db.Awaiting(d => d.SaveChangesAsync()).Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData(typeof(TicketExternalEntity), "ticket_external")]
    [InlineData(typeof(TicketExternalWorkflowEntity), "ticket_external_workflow")]
    [InlineData(typeof(TicketExternalSlaEntity), "ticket_external_sla")]
    public void Mapovani_tabulek_a_sloupcu_tabulky(Type entita, string tabulka)
    {
        using var db = SqlKontext();
        var typ = db.Model.FindEntityType(entita)!;

        typ.GetTableName().Should().Be(tabulka);
        typ.GetSchema().Should().Be("dbo");
    }

    [Theory]
    [InlineData(typeof(TicketExternalEntity), nameof(TicketExternalEntity.Id), "id")]
    [InlineData(typeof(TicketExternalEntity), nameof(TicketExternalEntity.ExternalId), "external_id")]
    [InlineData(typeof(TicketExternalEntity), nameof(TicketExternalEntity.ExternalSyncId), "external_sync_id")]
    [InlineData(typeof(TicketExternalEntity), nameof(TicketExternalEntity.SupplierTag), "supplier_tag")]
    [InlineData(typeof(TicketExternalEntity), nameof(TicketExternalEntity.SlaName), "sla_name")]
    [InlineData(typeof(TicketExternalWorkflowEntity), nameof(TicketExternalWorkflowEntity.Id), "id")]
    [InlineData(typeof(TicketExternalWorkflowEntity), nameof(TicketExternalWorkflowEntity.TicketExternalId), "id_ticket_external")]
    [InlineData(typeof(TicketExternalWorkflowEntity), nameof(TicketExternalWorkflowEntity.Date), "date")]
    [InlineData(typeof(TicketExternalWorkflowEntity), nameof(TicketExternalWorkflowEntity.TransferType), "transfertype")]
    [InlineData(typeof(TicketExternalWorkflowEntity), nameof(TicketExternalWorkflowEntity.Supplier), "supplier")]
    [InlineData(typeof(TicketExternalWorkflowEntity), nameof(TicketExternalWorkflowEntity.DtParam), "dt_param")]
    [InlineData(typeof(TicketExternalSlaEntity), nameof(TicketExternalSlaEntity.Id), "id")]
    [InlineData(typeof(TicketExternalSlaEntity), nameof(TicketExternalSlaEntity.SlaName), "sla_name")]
    [InlineData(typeof(TicketExternalSlaEntity), nameof(TicketExternalSlaEntity.SlaHours), "sla_hours")]
    public void Mapovani_tabulek_a_sloupcu(Type entita, string vlastnost, string sloupec)
    {
        using var db = SqlKontext();
        db.Model.FindEntityType(entita)!.FindProperty(vlastnost)!.GetColumnName().Should().Be(sloupec);
    }

    [Fact]
    public void External_id_je_varchar_ne_nvarchar()
    {
        using var db = SqlKontext();
        var p = db.Model.FindEntityType(typeof(TicketExternalEntity))!.FindProperty(nameof(TicketExternalEntity.ExternalId))!;

        p.IsUnicode().Should().BeFalse("external_id je varchar(50); nvarchar parametr by vynutil převod sloupce řádek po řádku");
        p.GetMaxLength().Should().Be(50);
    }

    [Fact]
    public void Predavky_maji_cizi_klic_id_ticket_external()
    {
        using var db = SqlKontext();
        var fk = db.Model.FindEntityType(typeof(TicketExternalWorkflowEntity))!.GetForeignKeys().Single();

        fk.PrincipalEntityType.ClrType.Should().Be(typeof(TicketExternalEntity));
        fk.Properties.Single().GetColumnName().Should().Be("id_ticket_external");
    }

    private static IConfiguration Konfigurace(bool enabled, string? externi)
    {
        var data = new Dictionary<string, string?>
        {
            ["Ticketing:Enabled"] = enabled ? "true" : "false",
            ["ConnectionStrings:TicketingReadOnly"] = "Server=nikam;Database=intranetNEW;Integrated Security=True",
        };
        if (externi is not null) data["ConnectionStrings:ServiceDeskExternalReadOnly"] = externi;
        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }

    [Fact]
    public void Bez_connection_stringu_neregistruje_kontext()
    {
        var services = new ServiceCollection().AddServiceDeskIntegration(Konfigurace(enabled: true, externi: null));
        services.Should().NotContain(d => d.ServiceType == typeof(ServiceDeskExternalReadOnlyDbContext));
    }

    [Fact]
    public void S_vypnutym_ticketingem_neregistruje_kontext()
    {
        var services = new ServiceCollection().AddServiceDeskIntegration(Konfigurace(enabled: false, externi: "Server=nikam;Database=sd"));
        services.Should().NotContain(d => d.ServiceType == typeof(ServiceDeskExternalReadOnlyDbContext));
    }

    [Fact]
    public void S_connection_stringem_registruje_kontext()
    {
        var services = new ServiceCollection().AddServiceDeskIntegration(Konfigurace(enabled: true, externi: "Server=nikam;Database=sd"));
        services.Should().Contain(d => d.ServiceType == typeof(ServiceDeskExternalReadOnlyDbContext));
    }
}
