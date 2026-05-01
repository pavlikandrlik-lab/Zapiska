---
title: Nový záznam
description: Jak vytvořit záznam v projektu.
---

# Nový záznam

## Předpoklady

- Permission `records.create` na aktuálním projektu (per-projekt scope)
- Jsi v detailu projektu (záznamy existují vždy v kontextu konkrétního projektu)

## Postup

1. Detail projektu → tab **Záznamy** → tlačítko **Nový záznam**
2. Otevře se editor — buď **modal** nebo **full-page** (záleží na tvojí
   preferenci v profilu)
3. Vyplň pole na záložkách
4. Ulož

## Editor — taby

Editor má **čtyři záložky**:

### Tab 1: Základní

| Pole | Popis |
|---|---|
| **Typ záznamu** | NES / PMP / PNF / úkol — viz [Typy záznamů](typy-zaznamu.md) |
| **Název** | Krátký výstižný název (povinné) |
| **Popis** | Rich-text editor (volitelné, dlouhý popis) |
| **Vlastník** | Osoba zodpovědná. Single-person picker (AD lookup) |
| **Termín plánovaný** | Datum kdy by měl být dokončený |
| **Termín dodaný** | Vyplní se až po dodání |
| **Splněno** | Datum splnění |
| **Stav** | Z číselníku stavů |
| **Urgentnost** | Z číselníku |
| **Kategorie** | Z číselníku (může být per-projekt) |
| **Subsystém** | Pokud projekt má subsystémy |

### Tab 2: Harmonogram

Kroky harmonogramu (HS01 / HS02 / ...) pro tento záznam:

- **Plán** — kolik dní trvá krok
- **Skutečnost** — bude doplněno později (po dodání řešení nebo auto-fill)

Více: [Harmonogram](../harmonogram/).

### Tab 3: Externí vazby

Pokud je typ záznamu **NES / PMP / PNF**:

- **6-cifer ticket id** — povinné, validace na existenci v `HOT_ZAZNAMY`
- Po vyplnění aplikace načte typ z SD a vyplní automaticky
- Můžeš přidat **další referenční vazby** (pokud potřebuješ křížový odkaz)

Pokud typ je **úkol**, tato záložka může být skrytá nebo prázdná.

Detail: [Externí vazba na SD](externi-vazba-sd.md).

### Tab 4: Spolupracující osoby (Collaborators)

AD picker pro výběr osob, které pomáhají na záznamu (vedle vlastníka):

- Mají k záznamu read-write přístup
- Záznam se jim objeví v dashboardu *Moje priority*
- Mohou komentovat, měnit stav

## Modal versus full-page

V profilu si můžeš nastavit **výchozí volbu**:

- **Modal** — dialogové okno překryje stránku. Rychlejší pro krátké úpravy.
- **Full-page** — celá stránka. Lepší pro dlouhé záznamy, zachová URL.

Pokud nemáš preferenci, aplikace tě **při prvním otevření zeptá** ("jak chceš
otevírat editor?") a tu volbu si zapamatuje.

## Validace

- Klient-side při focus-loss pole (povinná pole, formát)
- Server-side při submit (autoritativní)
- Chyby se zobrazí inline u polí

## Po uložení

- Záznam se vytvoří v `dbo.projektove_zaznamy`
- Audit log: `RECORD_CREATED` s tvým loginem
- Pokud má externí vazbu, **reaktivní harvest** se spustí na pozadí
- Aplikace tě:
  - **Modal** → zavře a vrátí na detail projektu (refresh tabulky)
  - **Full-page** → přesměruje na detail záznamu

## Dirty-check

Editor sleduje neuložené změny. Pokud zavřeš editor (nebo se pokusíš
navigovat pryč) s neuloženými změnami, aplikace tě **upozorní**:

```
Máte neuložené změny.
[Zavřít beze změn] [Pokračovat v editaci]
```

To brání nechtěné ztrátě práce.

## Související

- [Upravit záznam](upravit-zaznam.md)
- [Typy záznamů](typy-zaznamu.md)
- [Externí vazba na SD](externi-vazba-sd.md)
- [Komentáře](komentare.md)
