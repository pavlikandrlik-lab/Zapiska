using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Spec 2026-09-17 §4.2 — klientské zapojení zámku. Bez importu v bootstrap.js by se
/// modul tiše nenačetl a zámky by se nikdy neuvolňovaly; bez sendBeacon by se uvolnění
/// při zavření tabu nedoručilo.
/// </summary>
public sealed class RecordEditLockClientTests
{
    private static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    private static string Read(string relPath)
        => File.ReadAllText(Path.Combine(LocateRepoRoot(), relPath));

    [Fact]
    public void EditLockModul_Existuje()
    {
        File.Exists(Path.Combine(LocateRepoRoot(), "PmTracker.Web/wwwroot/js/modules/recordEditor/editLock.js"))
            .Should().BeTrue();
    }

    [Fact]
    public void Bootstrap_ImportujeAInicializujeEditLock()
    {
        var src = Read("PmTracker.Web/wwwroot/js/modules/bootstrap.js");

        src.Should().Contain("recordEditor/editLock",
            "bez importu se modul nenačte — Layout načítá site.js, ne bundle");
        src.Should().Contain("initRecordEditLock()",
            "samotný import nestačí, init se musí zavolat v sekvenci");
    }

    [Fact]
    public void EditLock_UvolnujeZamekBeaconem()
    {
        Read("PmTracker.Web/wwwroot/js/modules/recordEditor/editLock.js")
            .Should().Contain("sendBeacon",
                "fetch se při pagehide nedoručí — prohlížeč stránku zahodí dřív");
    }

    [Fact]
    public void KeepAlive_PosilaIdZamku()
    {
        Read("PmTracker.Web/wwwroot/js/modules/session.js")
            .Should().Contain("zaznamId=",
                "heartbeat zámku jede na existujícím keep-alive, editor nemá vlastní časovač");
    }

    [Fact]
    public void ReleaseEndpoint_MazeJenVlastniZamek()
    {
        Read("PmTracker.Web/Services/Records/RecordEditLockService.cs")
            .Should().Contain("x.ZaznamId == zaznamId && x.OsobaId == osobaId",
                "release i heartbeat musí filtrovat na osoba_id — jinak by šlo sáhnout na cizí zámek");
    }
}
