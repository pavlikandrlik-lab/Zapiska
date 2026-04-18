# Specifikace — dashboard projektu: prodlení NES / PMP / PNF

**Stav:** rozpracováno (fáze 5 implementace)
**Závisí na:** [ticketing-integration.md](ticketing-integration.md)

## Kontext

Projektový dashboard ukazuje rychlý přehled stavu projektu. Jeden z chybějících panelů je
**přehled prodlení ticketů** tří typů:

- **NES** — nesrovnalost
- **PMP** — požadavek metodické podpory
- **PNF** — požadavek nové funkcionality

Zdroj dat: ticketovací systém (starý ASP) + zobrazovač (SLA, zpoždění).

## Panel „Prodlení"

Nová dlaždice v `ProjectDashboard/Index`:

```
┌─ Prodlení ─────────────────────────────────────────┐
│   NES    │   PMP    │   PNF                         │
│    3 ⚠   │    1 ⚠   │    5 ⚠                        │
│          │          │                               │
│  ────    │  ────    │  ────                         │
│  T123456 │ T789012 │ T345678 (NES 5d)               │
│  +7d     │ +2d     │ +12d    (PMP 3d)               │
│  T...    │          │ T...                          │
│          │          │                               │
│  [Detail NES]  [Detail PMP]  [Detail PNF]           │
└─────────────────────────────────────────────────────┘
```

- **Tři kolonky** — po jedné per typ (NES, PMP, PNF)
- **Počet prodlených ticketů** (velký boxový číselný + ikona)
- **Top 3 nejhorší** — ticket ID + počet dnů zpoždění, klikací odkaz na detail/modal
- **Tlačítko „Detail"** — rozbalí tabulku všech prodlených ticketů daného typu

## Definice prodlení

**Otevřená otázka** — viz níže D2.

Pracovní hypotéza (k validaci):

| Stav ticketu | Co znamená „prodlení"? |
|---|---|
| Ticket **aktivní** (není v archivu) | SLA termín ze zobrazovače < dnešek AND ticket nemá finální stav |
| Ticket **v archivu** | Neukazuje se (je vyřešen / uzavřen) |

Alternativní hypotéza: **plánovaný termín harmonogramu < dnešek AND krok nemá skutečnost**.

Před implementací potřebujeme rozhodnutí D2.

## Datový zdroj

### Varianta A: Zobrazovač

Zobrazovač drží SLA termín + zpoždění. Pokud má rozumné API / DB pohled:

- **Polling job** `DashboardMetricsSyncJob` (každých 15 min) stáhne metriky do `DashboardProdleniCache`
- UI jen čte cache — rychlé vykreslení

### Varianta B: Výpočet PM Tracker

Pokud zobrazovač nemá rozumné API:

- PM Tracker počítá prodlení z ticketing DB (datum založení + SLA dle typu)
- Pomalejší, ale nezávislé na zobrazovači

Doporučení: **A**, pokud přístupné. Otázka D3 to řeší.

## Datový model

### `DashboardProdleniCache`

Cache pro rychlé vykreslení dashboardu.

| Sloupec | Typ | Popis |
|---|---|---|
| `Id` | bigint | PK |
| `ProjektId` | int | FK |
| `TicketId` | char(6) | |
| `Typ` | varchar(10) | NES / PMP / PNF |
| `SlaTermin` | datetime2 | Kdy měl být hotový |
| `DniProdleni` | int | dnešek - SLA |
| `PosledniAktualizace` | datetime2 | |

Index: `(ProjektId, Typ, DniProdleni DESC)` pro rychlé top-N dotazy.

## UI a ACL

- **Panel se zobrazuje** všem, kdo vidí dashboard projektu
- **Detail tabulka** stejně (klik dolů ukáže seznam)
- **ACL**: respektuje project ACL — uživatel vidí jen tickety z projektů, do kterých má přístup

## Testy (fáze 5)

- **Unit** `DashboardProdleniQueryTests` — SQL dotaz vrací správné tickety
- **Integration** — fixture s 10 tickety různých typů, zpoždění, archivovaných
- **E2E** — dashboard se vykreslí, dlaždice Prodlení zobrazuje správná čísla

## Otevřené otázky

| # | Otázka | Kdo rozhodne | Deadline |
|---|---|---|---|
| D1 | Typů ticketů je jen 3 (NES/PMP/PNF)? | Zodpovězeno: ano, další typy nejsou | — |
| D2 | **Přesná definice prodlení** — SLA ze zobrazovače vs. plán harmonogramu vs. datum založení + default lhůta? | Vedení | Před fází 5 |
| D3 | Zdroj SLA termínu — zobrazovač API/DB, nebo PM Tracker počítá? | Claude doporučení: A (zobrazovač), pokud dostupné | Před fází 5 |
| D4 | Chová se panel při >20 ticketů odlišně? (paginace, stránkování v detailu) | Claude doporučení: detail tabulka s paginace 25/stránka | Při implementaci |
| D5 | Vývoj v čase — historie prodlení (mini-graf dnů)? | Claude doporučení: ano, sparkline za posledních 30 dní | Při implementaci nebo později |
| D6 | Export — lze prodlení vyexportovat do CSV/Excel? | Claude doporučení: ano, standardní export tlačítko | Při implementaci |

## Zodpovědnost

- **Definice prodlení (D2):** vedení / PM methodolog
- **Přístup k zobrazovači:** IT oddělení
- **UI panel:** Claude (dle fáze 2 komponent)
- **SQL query výkonnost:** Claude + DBA review
