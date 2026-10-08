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
        tracker.DetectChanges();

        foreach (var entry in tracker.Entries<ProjektovyZaznamEntity>())
        {
            if (HtmlChanged(entry, nameof(ProjektovyZaznamEntity.Popis)))
            {
                entry.Entity.PopisProstyText = RichTextSearchText.FromHtml(entry.Entity.Popis);
            }
        }

        foreach (var entry in tracker.Entries<VyjadreniEntity>())
        {
            if (HtmlChanged(entry, nameof(VyjadreniEntity.TextVyjadreni)))
            {
                entry.Entity.TextVyjadreniProstyText = RichTextSearchText.FromHtml(entry.Entity.TextVyjadreni);
            }
        }

        foreach (var entry in tracker.Entries<ZaznamExterniOdkazEntity>())
        {
            if (HtmlChanged(entry, nameof(ZaznamExterniOdkazEntity.Pozadavek)))
            {
                entry.Entity.PozadavekProstyText = RichTextSearchText.FromHtml(entry.Entity.Pozadavek);
            }
        }
    }

    private static bool HtmlChanged<T>(EntityEntry<T> entry, string htmlProperty) where T : class
        => entry.State == EntityState.Added
           || (entry.State == EntityState.Modified && entry.Property(htmlProperty).IsModified);
}
