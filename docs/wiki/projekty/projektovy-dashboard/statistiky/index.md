---
title: Statistiky (panel)
description: KPI panel projektového dashboardu — počty po kvartálech, statistiky subsystémů.
---

# Statistiky (panel)

Panel **Statistiky** ukazuje **KPI a agregované metriky** projektu. Slouží
pro:

- **Reporting** — rychlý pohled na stav projektu
- **Audit / kontrolu** — tendence v čase
- **Plánování** — kde se hromadí zpoždění

## Co panel ukazuje

### KPI sekce

Klíčové ukazatele projektu:

| KPI | Význam |
|---|---|
| **Celkem záznamů** | Počet aktivních záznamů projektu |
| **V prodlení** | Počet záznamů s překročeným termínem |
| **Splněno celkem** | Počet záznamů v koncových stavech |
| **Průměrné zpoždění** | Cumulative delay napříč všemi kroky / počet kroků |
| **% dodrženo** | (záznamy bez delay) / (celkem) |

### Po kvartálech

Tabulka nebo graf: **počty splněných záznamů per kvartal**:

```
Q1/2026:  18 záznamů (z toho 12 v plánu, 6 zpožděných)
Q2/2026:  14 záznamů (z toho 11 v plánu, 3 zpožděné)
Q3/2026:   2 záznamy (rozpracováno)
Q4/2026:   0 (zatím)
```

Graf ukazuje **tendenci** — zlepšuje se čas plnění?

### Statistiky subsystémů

Pokud má projekt subsystémy:

```
R_EIS:  10 záznamů (5 splněno, 5 otevřených), průměrný delay +1.2 dne
R_HFU:   6 záznamů (4 splněno, 2 otevřené),  průměrný delay 0
R_DAN:   8 záznamů (3 splněno, 5 otevřených), průměrný delay +3.5 dne
```

Tabulka pomáhá identifikovat **problematický subsystém**.

## Volba roku

V hlavičce panelu **dropdown výběru roku**:

```
Rok: [ 2026 ▾ ]
       2025
       2024
       Všechny
```

Po změně panel **přepočítá KPI** pro daný rok. Default = aktuální rok.

## Lazy loading

Panel se načítá samostatně přes AJAX. Při změně roku **přeloaduje** jen
sebe, ne celou stránku.

## Permission

`dashboard.statistics.view`. Bez toho se panel nerenderuje.

## Pro koho

- **Vedoucí projektu** (`VLASTNIK_PROJEKTU` / `ADM_PROJ`) — sledování stavu projektu
- **Sponsor / Management** — reporting
- **Auditor** — analýza tendencí

## Cache

KPI jsou **náročné na výpočet** (agregace přes všechny záznamy + harmonogram).
Aplikace je cachuje:

- **Periodicky** — nightly rebuild
- **Reaktivně** — po větších změnách (uložení návrhu, masová úprava)

Při běžné změně (úprava jednoho záznamu) **se KPI nepřepočítávají hned** — až
při dalším nightly rebuild. Důsledek: KPI mohou být *do 24 h zpožděné* za
realitou.

## Refresh

Pro force refresh KPI (admin akce):

- Tlačítko *Refresh KPI* v hlavičce panelu (pokud existuje)
- Server-side trigger v Nastavení → (admin příkaz)

## Co panel **NEukazuje**

- **Konkrétní záznamy** — to dělá panel [Záznamy přehled](../zaznamy-prehled/)
- **NES tickety v prodlení** — to dělá [NES panel](../nes-v-prodleni/)
- **Výzvy** — viz [Výzvy panel](../vyzvy/)

Statistiky jsou **agregovaný pohled** — pro detail klikni do příslušného
panelu nebo záložky.

## Související

- [Záznamy přehled](../zaznamy-prehled/)
- [NES v prodlení](../nes-v-prodleni/)
- [Výzvy](../vyzvy/)
- [Harmonogram](../../harmonogram/) — zdroj dat pro delay statistiky
