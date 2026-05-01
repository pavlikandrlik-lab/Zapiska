---
title: Plán versus skutečnost
description: Jak harmonogram počítá odchylku plán × skutečnost.
---

# Plán vs. skutečnost

Klíčový koncept harmonogramu: **plán** a **skutečnost** jsou dvě separátní
osy. Aplikace porovnává jejich rozdíl a prezentuje **delay** (zpoždění).

## Plán (baseline)

**Plánovaný termín kroku** — co kdy mělo být.

- Pochází z **manuálního zadání leadera** (typicky při vytvoření záznamu)
- Nebo z **schváleného návrhu** (`navrhy/`)
- Reprezentován hodnotou **HS0X_DURATION** (kolik dní krok trvá)

Z duration aplikace dopočítá baseline data:

```
HS01.start  = záznam.created_at (nebo termín odeslání)
HS01.end    = HS01.start + HS01_DURATION
HS02.start  = HS01.end
HS02.end    = HS02.start + HS02_DURATION
... atd.
```

Plán se **nemění auto-fill ze SD** — je to baseline, který se nemění bez
explicitního schválení.

## Skutečnost

**Reálný termín kroku** — co kdy reálně proběhlo.

Pochází z:

- **Auto-fill ze SD vyjádření** (default Auto režim) — typicky pro externí
  záznamy (NES / PMP / PNF)
- **Manuálního zadání uživatele** (Manual režim)
- **Migrace historických dat** (Historicka — před zavedením auto-fill,
  cca 2026-04-25)

Reprezentována hodnotou **HS0X_DELAY** (počet dní odchylky od baseline).

## Odchylka (delay)

```
delay = HS0X_DELAY (signed integer)
```

- **Kladný delay** (např. `+5`) — skutečnost je o 5 dní později než plán
  (zpoždění)
- **Nulový delay** (`0`) — dodrženo
- **Záporný delay** (např. `-2`) — skutečnost je o 2 dny dřív (předstih)

Skutečné datum:

```
HS01.skutecnost.end = HS01.plan.end + HS01_DELAY (dny)
```

Aplikace v UI obvykle **zobrazuje delay** (počet dní), ne absolutní datum.
Důvod: delay je informačně bohatší (vidíš zpoždění na první pohled),
absolutní datum lze rychle dopočítat.

## Vizualizace v UI

V záložce *Harmonogram*:

```
                  HS01    HS02    HS03    HS04    HS05
Záznam #123      [+2]    [+1]    [0]     [-1]    [—]
                  🤖      ✍️      🤖              📜

Záznam #124      [0]     [+5]    [—]     [—]     [—]
                  📜      ✍️
```

Konvence:

- **`[+2]` = +2 dny zpoždění** (kladné číslo = zpoždění)
- **`[—]` = žádná skutečnost** (Neznámo, krok nedosažený)
- **Ikona pod číslem** = badge zdroje (🤖 / ✍️ / 📜 / —)

## Barevné kódování delay

Aplikace typicky barví delay podle závažnosti:

| Delay | Barva | Význam |
|---|---|---|
| `< 0` (předstih) | Modrá / Zelená | Předstih, dobré |
| `0` | Bez barvy | Dodrženo |
| `+1 až +5` | Žlutá / Oranžová | Drobné zpoždění |
| `+6 a víc` | Červená | Vážnější zpoždění |

Konkrétní prahy závisí na konfiguraci (`PriorityOverdueCapDays` aj.).

## Cumulative delay

Kromě per-krok delay aplikace počítá i **cumulative delay** — celkové
zpoždění **k danému kroku**:

```
cumulative_delay(HS03) = HS01_DELAY + HS02_DELAY + HS03_DELAY
                       = +2 + +1 + 0
                       = +3 dny
```

To pomáhá vidět "kde se delay nahromadilo" — někdy první krok je dodržený
ale později se to nasčítá.

## Rebuild baseline po schválení návrhu

Pokud projde [návrh změny plánu](../navrhy/) a aplikuje se:

- Baseline (plán) se přepíše novou hodnotou
- Delay se přepočítá z **nové baseline** (delay se může změnit, i když
  skutečnost zůstává)

Příklad:

```
Před schválením:
  HS01_DURATION = 5
  HS01_DELAY = +2 (skutečnost = baseline + 2)

Návrh: HS01_DURATION → 7

Po aplikaci návrhu:
  HS01_DURATION = 7
  HS01_DELAY = 0 (skutečnost odpovídá nové baseline)
```

Tím změna plánu **smaže virtual zpoždění** — pokud se reálně termín posunul
a sponzor to schválil, není to "zpoždění" ale "změna plánu".

## Pro koho

- **Member** — sleduje delay svých záznamů
- **Leader** — sleduje delay celého projektu
- **Reviewer / Auditor** — analýza projektu

## Související

- [Kroky a fáze](kroky-a-faze.md)
- [Auto-fill ze SD](auto-fill-ze-sd.md)
- [Manuální skutečnost](manualni-skutecnost.md)
- [Návrhy](../navrhy/) — změny plánu
