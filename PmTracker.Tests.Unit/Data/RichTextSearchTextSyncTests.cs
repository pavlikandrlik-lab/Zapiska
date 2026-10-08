using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Data;

/// <summary>Uživatel 2026-10-08: čistý text se plní při každém uložení, ne jen jednorázově.</summary>
public sealed class RichTextSearchTextSyncTests
{
    private static PmTrackerDbContext Db() => new(new DbContextOptionsBuilder<PmTrackerDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task NovyZaznam_AUpravaPopisu_PlniCistyText()
    {
        await using var db = Db();
        var zaznam = new ProjektovyZaznamEntity { Id = 1, Nazev = "N", Popis = "<p>pes a <b>kočka</b></p>" };
        db.ProjektoveZaznamy.Add(zaznam);
        await db.SaveChangesAsync();
        zaznam.PopisProstyText.Should().Be("pes a kočka");

        zaznam.Popis = "<p>jen <i>pes</i></p>";
        await db.SaveChangesAsync();
        zaznam.PopisProstyText.Should().Be("jen pes");

        zaznam.Popis = null;
        db.SaveChanges();
        zaznam.PopisProstyText.Should().BeNull();
    }

    [Fact]
    public async Task Vyjadreni_APozadavek_PlniCistyText()
    {
        await using var db = Db();
        var vyjadreni = new VyjadreniEntity { Id = 1, ZaznamId = 1, JednaniId = 1, TextVyjadreni = "<p>A&nbsp;<strong>B</strong></p>" };
        var odkaz = new ZaznamExterniOdkazEntity { Id = 1, ZaznamId = 1, TypOdkazuId = 1, Cislo = "1", Pozadavek = "<p>text <u>výzvy</u></p>" };
        db.Vyjadreni.Add(vyjadreni);
        db.ZaznamExterniOdkazy.Add(odkaz);
        await db.SaveChangesAsync();

        vyjadreni.TextVyjadreniProstyText.Should().Be("A B");
        odkaz.PozadavekProstyText.Should().Be("text výzvy");

        vyjadreni.TextVyjadreni = "<p>nové</p>";
        await db.SaveChangesAsync();
        vyjadreni.TextVyjadreniProstyText.Should().Be("nové");
    }

    [Fact]
    public async Task UlozeniBezZmenyHtml_CistyTextNeprepisuje()
    {
        await using var db = Db();
        var zaznam = new ProjektovyZaznamEntity { Id = 1, Nazev = "N", Popis = "<p>pes</p>" };
        db.ProjektoveZaznamy.Add(zaznam);
        await db.SaveChangesAsync();

        zaznam.PopisProstyText = "ručně";   // simulace: jiný zdroj; háček ho bez změny HTML nemá přepsat
        zaznam.Nazev = "Jiný název";
        await db.SaveChangesAsync();

        zaznam.PopisProstyText.Should().Be("ručně");
    }

    [Fact]
    public async Task Ulozeni_ProchaziSledovaneEntityNejvysDvakrat()
    {
        // Dřív háček volal DetectChanges sám a pak ještě v každém ze tří Entries<T>() —
        // s uložením 5 průchodů všech sledovaných entit místo 1 (drobnost z review 2026-10-08).
        await using var db = Db();
        var zaznam = new ProjektovyZaznamEntity { Id = 1, Nazev = "N", Popis = "<p>pes</p>" };
        db.ProjektoveZaznamy.Add(zaznam);
        await db.SaveChangesAsync();

        var pruchody = 0;
        db.ChangeTracker.DetectedAllChanges += (_, _) => pruchody++;
        zaznam.Popis = "<p>kočka</p>";
        await db.SaveChangesAsync();

        pruchody.Should().BeLessThanOrEqualTo(2, "jeden průchod háčku a jeden uložení samotného EF");
        zaznam.PopisProstyText.Should().Be("kočka");
    }

    [Fact]
    public async Task VypnutaAutomatickaDetekce_CistyTextSePresToUlozi()
    {
        // Háček píše přes záznam změn EF, ne do objektu — jinak by se s vypnutou automatickou
        // detekcí změn čistý text spočítal, ale do DB neodešel.
        var nazev = Guid.NewGuid().ToString();
        PmTrackerDbContext Kontext() => new(new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(nazev).Options);

        await using (var db = Kontext())
        {
            db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity { Id = 1, Nazev = "N", Popis = "<p>staré</p>" });
            await db.SaveChangesAsync();
        }

        await using (var db = Kontext())
        {
            db.ChangeTracker.AutoDetectChangesEnabled = false;
            var zaznam = await db.ProjektoveZaznamy.SingleAsync();
            db.Entry(zaznam).Property(x => x.Popis).CurrentValue = "<p>nové <b>slovo</b></p>";
            await db.SaveChangesAsync();
        }

        await using (var db = Kontext())
        {
            (await db.ProjektoveZaznamy.SingleAsync()).PopisProstyText.Should().Be("nové slovo");
        }
    }

    [Fact]
    public void ZadnyZapisNeobchaziHacek()
    {
        // ExecuteUpdate / přímé SQL by čistý text obešlo — nad HTML sloupci se nesmí použít.
        var zdroje = Directory.GetFiles(ResolvePath("PmTracker.Web"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Select(File.ReadAllText);
        zdroje.Should().NotContain(s => Regex.IsMatch(s,
            @"ExecuteUpdate[^;]*(Popis|TextVyjadreni|Pozadavek)\b|UPDATE\s+dbo\.(projektove_zaznamy|vyjadreni|zaznam_externi_odkazy)\s+SET[^;]*(popis|text_vyjadreni|pozadavek)\s*=",
            RegexOptions.IgnoreCase));
    }
}
