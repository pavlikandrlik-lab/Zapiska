using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Data;

/// <summary>
/// Drží čistý text formátovaných polí v souladu s HTML (uživatel 2026-10-08). Volá ho
/// PmTrackerDbContext před každým uložením, takže pokryje všechna místa zápisu: založení
/// a úpravu záznamu (i ze schváleného návrhu), přidání a úpravu vyjádření, externí vazby.
/// Přepočítává jen při založení nebo změně HTML.
/// </summary>
internal static class RichTextSearchTextSync
{
    public static void Apply(ChangeTracker tracker)
    {
        // Jediný průchod: Entries() samo spustí DetectChanges (při zapnuté automatické
        // detekci), vlastní volání ani Entries<T>() pro každý typ by k uložení přidaly další
        // průchody všech sledovaných entit. S vypnutou detekcí platí, co volající sám označil
        // — stejně jako pro SaveChanges.
        foreach (var entry in tracker.Entries())
        {
            switch (entry.Entity)
            {
                case ProjektovyZaznamEntity:
                    Sync(entry, nameof(ProjektovyZaznamEntity.Popis), nameof(ProjektovyZaznamEntity.PopisProstyText));
                    break;
                case VyjadreniEntity:
                    Sync(entry, nameof(VyjadreniEntity.TextVyjadreni), nameof(VyjadreniEntity.TextVyjadreniProstyText));
                    break;
                case ZaznamExterniOdkazEntity:
                    Sync(entry, nameof(ZaznamExterniOdkazEntity.Pozadavek), nameof(ZaznamExterniOdkazEntity.PozadavekProstyText));
                    break;
            }
        }
    }

    // Zápis přes záznam změn (CurrentValue), ne do objektu — EF hodnotu rovnou označí jako
    // změněnou, takže do UPDATE se dostane i bez další detekce změn.
    private static void Sync(EntityEntry entry, string htmlProperty, string plainProperty)
    {
        var html = entry.Property(htmlProperty);
        var htmlChanged = entry.State == EntityState.Added
                          || (entry.State == EntityState.Modified && html.IsModified);
        if (htmlChanged)
        {
            entry.Property(plainProperty).CurrentValue = RichTextSearchText.FromHtml((string?)html.CurrentValue);
        }
    }
}
