---
title: Identifikátor jednání
description: Formát čísla jednání a toggle "Používat identifikátor podle jednání" na projektu.
---

# Identifikátor jednání

## Co je identifikátor jednání

Číslo jednání je **stringový identifikátor** v rámci projektu. Příklady:

- `8201`
- `8201-2`
- `2026-04-25`
- `R_DAN-001`

Aplikace **nemá pevný formát** — záleží na konvencích projektu.

## Unikátnost

Číslo musí být **unikátní v rámci jednoho projektu**. Stejné číslo může
existovat napříč projekty.

```
Projekt FIS-EIS:    Jednání 8201 ✓
Projekt FIS-EIS:    Jednání 8201 ✗ (duplicate, validation error)

Projekt FIS-EIS:    Jednání 8201 ✓
Projekt ISSP:       Jednání 8201 ✓ (jiný projekt, OK)
```

Validace probíhá při uložení nového jednání nebo změně čísla existujícího.

## Toggle na projektu — "Používat identifikátor záznamů podle jednání"

V editaci projektu je přepínač **`PouzivatIdentJednani`** (hovorově "8201
toggle"). Když je **zapnutý**:

- Záznamy vytvořené **během konkrétního jednání** mají v UI a exportech
  zobrazený **identifikátor jednání** místo vlastního `id`
- Příklad: záznam #123 vytvořený během jednání 8201 se v exportech zobrazí
  jako "8201/123" nebo jen "8201" (podle nastavení formátu)

Když je **vypnutý**:

- Záznamy mají vlastní pořadové ID podle pořadí v projektu (#1, #2, #3, ...)
- Vazba na jednání zůstává v DB, ale není v default zobrazení vidět

## Kdy zapnout toggle

Toggle je vhodný pro projekty kde **business kontext = jednání**:

- Schvalovací procesy (každé jednání = další iterace)
- Pravidelné porady (8201 = série porad o IS)

Není vhodný pro projekty kde záznamy existují **mimo kontext jednání**:

- Continuous developement (úkoly se zakládají kdykoli)
- Bug tracking

## Kde se identifikátor zobrazuje

Když je toggle zapnutý, identifikátor jednání se objevuje v:

- **Tabulka záznamů** — sloupec ID
- **Detail záznamu** — hlavička
- **Tisk záznamu** — hlavička exportu
- **Vyhledávání** — výsledky
- **Globální vyhledávání** — výsledky

## Proč existuje formát "8201"

Konvence "8201" pochází z **legacy systému** kde se identifikátory porad
formátovaly jako 4-cifer číslo. PM Tracker tu konvenci přebírá pro
backward compatibility a je-li na projektu legitimní business zvyklost.

## Změna formátu

Formát identifikátoru **se nemění z UI**. Pokud chceš jiný formát, musíš
manuálně přepisovat čísla při vytváření jednání.

## Změna toggle po čase

Pokud zapnul toggle po čase (existují už záznamy bez vazby na jednání):

- **Existující záznamy** zůstávají bez vazby — zobrazují se s vlastním ID
- **Nové záznamy vytvořené během jednání** budou mít vazbu a zobrazují se
  s číslem jednání

Pokud vypneš toggle:

- **Všechny záznamy** se vrátí na vlastní ID
- Vazba na jednání **zůstává v DB** (jen se v default UI nepoužívá)

## Permissions

- `projects.edit` — změnit toggle (součást metadat projektu)
- `meetings.edit` — změnit číslo existujícího jednání
- `meetings.create` — vytvořit jednání s konkrétním číslem

## Související

- [Nové jednání](nove-jednani.md)
- [Upravit projekt](../upravit-projekt.md) — toggle se nastavuje tam
- [Pomoc → FAQ](../../pomoc/faq.md)
