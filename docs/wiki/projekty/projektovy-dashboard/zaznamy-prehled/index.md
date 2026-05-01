---
title: Záznamy přehled (panel)
description: Panel projektového dashboardu — agregovaný pohled na záznamy projektu.
---

# Záznamy přehled (panel)

Panel **Záznamy přehled** v projektovém dashboardu ukazuje **agregovaný pohled
na záznamy projektu** — ne plný seznam k editaci, ale přehled klíčových položek.

## Vztah k záložce *Záznamy*

| Co | Záložka *Záznamy* | Panel *Záznamy přehled* |
|---|---|---|
| Účel | Pracovní seznam k editaci | Přehled / report |
| Granularita | Všechny záznamy | Top N podle priority |
| Akce | Vytvořit, upravit, smazat | Read-only |
| Filtrace | Detailní | Vysoká úroveň |

## Co panel ukazuje

### Top priority záznamy

Default **5–10 nejvyšších priorit** napříč projektem:

- Záznamy v aktivních stavech (otevřené, čekající)
- S blížícím se nebo překročeným termínem
- S vyšší urgentností

Stejná logika priority jako [Moje priority](../../../uzivatelsky-dashboard/moje-priority.md),
jen filtrovaná na konkrétní projekt (ne napříč všemi).

### Skupiny podle typu

Counter pro každý typ:

```
NES:  12 otevřených (3 v prodlení)
PMP:   8 otevřených (1 dodaný)
PNF:   5 otevřených
Úkoly: 14 otevřených
```

### Klikací řádky

Klik na položku → otevře se [editor záznamu](../../zaznamy/upravit-zaznam.md).

## Filtrace

Panel **respektuje permission keys** — záznamy které user nemůže vidět
(různé scope role) se vůbec nezobrazí.

Aktivní filtry:

- `is_deleted = 0` (skrýt smazané)
- Default skrytí `HOTOVO` / `ZRUSENO` stavů
- Top N podle priority

User nemá možnost filtry měnit — pro detailní filtraci musí přejít do
záložky *Záznamy*.

## Pro koho

- **Vedoucí projektu** (`VLASTNIK_PROJEKTU` / `ADM_PROJ`) — denní pohled "co je urgentní"
- **Člen projektu** (`PROJ_MAN`) — vidí top priority projektu (komplementární k *Moje
  priority* na user dashboardu)
- **Sponsor / Admin** — quick check stavu projektu

## Permission

`dashboard.records.view`. Bez toho se panel **vůbec nerenderuje** (layout
se přizpůsobí, místo zaplní jiný panel nebo zůstane prázdné).

## Lazy loading

Panel se **načítá samostatně** přes AJAX po načtení hlavní stránky:

- Hlavní stránka renderuje rychle (skeleton placeholder pro panel)
- Po dokončení load se objeví obsah

Pokud lazy load selže (např. server chyba), panel zobrazí:

```
⚠️ Nepodařilo se načíst přehled záznamů.
[Zkusit znovu]
```

Klik *Zkusit znovu* spustí re-load.

## Cache

Data jsou **cachována** pro performance:

- Update typicky po každé změně záznamu (reaktivně)
- Plus periodický rebuild (nightly)

Pro force refresh můžeš:

- Hard reload stránky (`Ctrl+Shift+R`)
- (V některých verzích) tlačítko *Refresh* v hlavičce panelu

## Související

- [Záznamy v projektu](../../zaznamy/) — plný seznam k editaci
- [Statistiky](../statistiky/) — KPI metriky agregace
- [Moje priority — uživatelský dashboard](../../../uzivatelsky-dashboard/moje-priority.md)
