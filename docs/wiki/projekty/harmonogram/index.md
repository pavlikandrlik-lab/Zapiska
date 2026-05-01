---
title: Harmonogram
description: Plán a skutečnost projektových kroků. Auto-fill ze ServiceDesk vyjádření.
---

# Harmonogram

Harmonogram je **plánovaná a skutečná osa projektových kroků**. Aplikace eviduje:

- **Plán** — co kdy mělo být (ze záznamu, ze schváleného návrhu)
- **Skutečnost** — co kdy reálně proběhlo (ručně nebo auto-fill ze SD)
- **Odchylku (delay)** — počet dní zpoždění proti plánu

Harmonogram je **per-záznam** — každý záznam má svůj harmonogram s kroky. Plus
se agreguje na úrovni projektu (záložka *Harmonogram* ukazuje souhrn).

## Struktura

```
Záznam (NES / PMP / PNF / úkol)
  └─ Harmonogram
       ├─ Krok HS01 (např. Zadání)
       │    ├─ Plán: 5 dní
       │    └─ Skutečnost: 7 dní (delay: +2)
       ├─ Krok HS02 (např. Schválení)
       │    ├─ Plán: 3 dny
       │    └─ Skutečnost: 4 dny (delay: +1)
       ├─ Krok HS03 (Zpracování)
       └─ ... HS04, HS05, ... HS0N
```

## Klíčové pojmy

- **HS01–HS0N** — kódové označení kroků (viz [Kroky a fáze](kroky-a-faze.md))
- **Plán** — baseline, kolik dní krok má trvat
- **Skutečnost** — reálná hodnota delay v dnech (rozdíl baseline a aktuálního)
- **Režim Auto / Ručně** — řídí, zda sync služba automaticky aktualizuje
  skutečnost ze SD vyjádření, nebo zda user manuálně fixoval hodnotu
- **Badge zdroje** — vizuální indikátor odkud hodnota přišla (🤖 Automat /
  ✍️ Manual / 📜 Historicka / — Neznámo)

## Layout záložky Harmonogram

V detailu projektu → tab *Harmonogram*:

### Souhrnný pohled

Tabulka záznamů s sloupci pro každý krok:

```
Záznam | HS01 | HS02 | HS03 | HS04 | HS05 |
123    | +2   | +1   | 0    | -1   | —    |
124    | 0    | 0    | +5🤖 | —    | —    |
125    | +1   | +3✍️ | —    | —    | —    |
```

(Čísla = delay v dnech, ikony = badge zdroje.)

### Filtry

- **Skrýt dokončené záznamy** (default zapnutý)
- **Skrýt smazané**
- **Subsystém**

### Detail kroku

Klik na buňku → otevře se detail / popover:

- Plán hodnota
- Skutečnost hodnota
- Badge zdroje + tooltip
- Tlačítko **Auto / Ručně** toggle (pokud máš permission)
- **Dropdown alternativních kandidátů** (pokud existují) — pro režim Ručně

## Co je v této sekci wiki

- [Kroky a fáze](kroky-a-faze.md) — co znamená HS01, HS02, …
- [Plán versus skutečnost](plan-vs-skutecnost.md) — jak se počítá delay
- [Auto-fill ze SD](auto-fill-ze-sd.md) — automatické doplňování
- [Manuální skutečnost](manualni-skutecnost.md) — ruční zápis + režim Auto/Ručně

## Vztah k externí vazbě

Pokud má záznam **externí vazbu na SD ticket**, harmonogram se může
**automaticky vyplňovat** ze SD vyjádření. Workflow:

1. Uživatel propojí záznam s ticketem (vyplní 6-cif. id)
2. Sync služba načte vyjádření z SD
3. Klasifikuje vyjádření harvest predikáty (K3 / K4_K7 / K6 / K10 / Plán dodání)
4. Klasifikované vyjádření se přiřadí ke kroku harmonogramu (přes typ delay)
5. Sync přepočítá skutečnost — delay v dnech od baseline

Pokud user chce zafixovat hodnotu, přepne krok na **Ručně** — sync ji už
nepřepíše.

## Vztah k návrhům

[Návrhy](../navrhy/) na změnu plánu jsou separátní workflow. Vytvoření
návrhu **uzamkne krok** harmonogramu (`pendingScheduleProposalLock`), takže
user nemůže ručně měnit skutečnost dokud není návrh schválen / zamítnut.

## Pro koho

| Persona | Co může |
|---|---|
| **Member** | Vidí harmonogram; edituje skutečnost u svých záznamů |
| **Leader** | + Schvaluje návrhy, uzamyká kroky |
| **Admin** | Vše, včetně překračování zámků |

## Permissions

- `harmonogram.view` — vidět tab
- `harmonogram.edit` — editovat skutečnost manuálně
- `harmonogram.toggle-rezim` — přepínat Auto / Ručně
- `harmonogram.select-candidate` — fixovat preferred kandidáta z dropdownu

## Související

- [Externí vazba na SD](../zaznamy/externi-vazba-sd.md)
- [Auto-fill skutečnosti — integrace SD](../../integrace/servicedesk/auto-fill-skutecnosti.md)
- [Návrhy](../navrhy/)
