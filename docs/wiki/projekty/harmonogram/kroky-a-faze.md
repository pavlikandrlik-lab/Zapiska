---
title: Kroky a fáze harmonogramu
description: Co znamenají HS01, HS02, … kroky harmonogramu.
---

# Kroky a fáze harmonogramu

Harmonogram záznamu je rozdělen na **kroky** označené HS01, HS02, ... HS0N.
Každý krok reprezentuje fázi v životním cyklu záznamu — od zadání po nasazení.

## Struktura kroku

Každý krok má dvě složky:

### Duration (HS0X_DURATION)

**Kolik dní krok trvá** — plánovaná doba mezi začátkem a koncem fáze.

```
HS01_DURATION = 5  → krok HS01 trvá plánovaně 5 dní
HS02_DURATION = 3  → krok HS02 trvá plánovaně 3 dny
```

Z duration aplikace **počítá baseline** plánu kroku:

```
HS01.start = today
HS01.end   = today + HS01_DURATION

HS02.start = HS01.end
HS02.end   = HS02.start + HS02_DURATION

... atd.
```

### Delay (HS0X_DELAY)

**Odchylka skutečnosti od plánu** — počet dní zpoždění (kladný) nebo předstihu
(záporný).

```
HS01_DELAY = +2  → krok HS01 byl o 2 dny pozdě než plán
HS02_DELAY = 0   → krok HS02 dodržel plán
HS03_DELAY = -1  → krok HS03 byl o den dřív
```

Z delay + baseline aplikace počítá **skutečnost**:

```
HS01.skutecnost.end = HS01.plan.end + HS01_DELAY (dny)
```

## Mapování HS0X na business fáze

Aplikace řídí auto-fill skutečnosti přes matici `HarmonogramKrokDatumMapping`
(viz `HarmonogramKrokDatumMapping.cs`). Matice mapuje **(typ tiketu × krok
poradí) → harvest predikát**:

```
                      Krok 1   Krok 3   Krok 4   Krok 6   Krok 7   Krok 10
NES                   K1       —        —        —        —        —
PMP                   K1       K3       K4_K7    —        —        —
PNF                   K1       —        —        K6       K4_K7    K10
```

Legenda:
- **K1** — datum založení (manuální, ne auto-fill)
- **K3** — odeslání zadání PMP (`Záznam byl založen a předán dodavateli...`)
- **K4_K7** — dodání řešení (`Dodavatel přidal řešení`)
- **K6** — odeslání požadavku PNF (`Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována.`)
- **K10** — nasazení / archivace (`Záznam byl převeden do archivu.`)
- **—** — žádný auto-fill (ruční krok, nebo mimo aktivní sadu typu)

Detail textů: [Harvest predikáty](../../integrace/servicedesk/auto-fill-skutecnosti.md#harvest-predik%C3%A1ty-texty-kter%C3%A9-aplikace-hled%C3%A1).

### Per typ — business fáze

#### NES (Nesrovnalost)

NES nemá auto-fill kroky — zpracovává se primárně ručně. Jen `K1` =
datum založení.

#### PMP (Požadavek metodické podpory)

Auto-fill kroky:

| Krok | Predikát | Business fáze | Co znamená |
|---|---|---|---|
| 1 | K1 (manual) | Datum založení záznamu | Leader vytvoří záznam v PM Trackeru |
| 2 | (ruční) | Schválení / příprava | Leader sponzor schválí zadání |
| 3 | **K3** | Odeslání dodavateli | Dodavatel přijme PMP a založí si ho ("pod značkou: XYZ") |
| 4 | **K4_K7** | Dodání řešení | Dodavatel přidá řešení do `HOT_VYJADRENI` |
| 5 | (ruční) | Akceptace řešení | Uživatel testuje a akceptuje |
| 6-9 | (mimo sadu) | — | PMP nepoužívá kroky 6-9 |
| 10 | **K10** | Archivace | Ticket převeden do archivu (terminal) |

#### PNF (Požadavek nové funkcionality)

Auto-fill kroky:

| Krok | Predikát | Business fáze | Co znamená |
|---|---|---|---|
| 1 | K1 (manual) | Datum založení záznamu | Leader vytvoří záznam |
| 2 | (ruční) | Příprava požadavku | Sepsání PNF |
| 3-5 | (mimo sadu) | — | PNF nepoužívá kroky 3-5 |
| 6 | **K6** | Odeslání požadavku | Akceptace kalkulace, předání dodavateli |
| 7 | **K4_K7** | Dodání funkcionality | Dodavatel přidá řešení |
| 8-9 | (ruční) | Akceptace / nasazení | UAT, produkce nasazení |
| 10 | **K10** | Archivace | Ticket převeden do archivu |

#### Úkol (interní)

Bez auto-fill, plně manuální. Typicky jen 1 krok = "splnění úkolu".

## Pravidlo: ruční kroky 2 / 5 / 8 / 9

Kroky 2, 5, 8, 9 jsou v matici označené jako **mimo auto-fill scope** —
musí se zadat ručně. Důvod: v `HOT_VYJADRENI` neexistuje kategorické
vyjádření pro tyto fáze (např. "schválil sponzor X", "akceptoval uživatel
Y"). Tyto fáze jsou interní pro PM Tracker, ne pro SD.


## Resolver — výběr kandidáta

Pokud má krok více kandidátních vyjádření matching predikát, aplikace volá
`HarmonogramSkutecnostResolver`:

1. Filtruje vazby kde `(TypZaznamu × KrokPoradi) → PredikatKey` matchne
2. Seřadí kandidáty **sestupně podle data** (MAX first)
3. Tie-break při shodném datu: `ASC podle ExterniOdkazId` (deterministické)
4. Pokud má krok **preferred ExterniOdkazId**, použije ho (Manual režim)
5. Jinak použije MAX kandidát (Auto režim)
6. Pokud preferred není v kandidátech (např. po smazání bindingu), fallback
   na MAX + signál `PreferredFallbackApplied=true` → caller clear-uje
   preferred flag

Detail: [Auto-fill ze SD](auto-fill-ze-sd.md).

## Kde se kroky definují

### Per-záznam

Při vytvoření záznamu se kroky **typicky předvyplní** podle typu záznamu
(NES / PMP / PNF / úkol). Default mapping:

- NES → 4 kroky (HS01–HS04)
- PMP → 7 kroků (HS01–HS07)
- PNF → 5 kroků (HS01–HS05)
- Úkol → 1 krok (HS01)

Default je v konfiguraci a lze upravit per projekt.

### Manuální úprava

V editoru záznamu → tab *Harmonogram* můžeš:

- Změnit **plánovanou délku** kroku (HS0X_DURATION)
- Skutečnost (HS0X_DELAY) se obvykle nezadává ručně — je v Auto režimu
  z SD vyjádření

### Návrhy změn

Pro **strukturální změny harmonogramu** (změna duration, přesun kroku,
přidání nového kroku) je doporučená cesta přes [návrhy](../navrhy/) — ne
přímá editace.

## Filtrace v zobrazení

V záložce *Harmonogram* projektu lze filtrovat:

- Skrýt kroky bez delay (jen ty co mají skutečnost)
- Skrýt dokončené záznamy
- Zobrazit jen kritické kroky (HS04, HS06)

## Pro koho

- **Member** — vidí všechny kroky, edituje plán svých záznamů
- **Leader** — schvaluje strukturální změny
- **Admin** — full management

## Související

- [Plán versus skutečnost](plan-vs-skutecnost.md)
- [Auto-fill ze SD](auto-fill-ze-sd.md)
- [Manuální skutečnost](manualni-skutecnost.md)
- [Auto-fill skutečnosti — SD pohled](../../integrace/servicedesk/auto-fill-skutecnosti.md)
