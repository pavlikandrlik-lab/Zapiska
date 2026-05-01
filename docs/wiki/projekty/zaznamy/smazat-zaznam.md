---
title: Smazat záznam
description: Soft-delete záznamu — co znamená, kdy ho lze obnovit.
---

# Smazat záznam

## Předpoklady

- Permission `records.delete` na projektu
- Záznam **není uzamčený** (`is_locked=0`)

## Postup

1. Detail projektu → tab *Záznamy* → u řádku tlačítko **Smazat**
2. Otevře se **confirm dialog**:

   ```
   Opravdu chcete smazat záznam?

   #123 — "Implementace REST API pro export"
   Typ: PMP, Vlastník: Jan Novák
   Stav: Otevřený

   Toto je soft-delete — data zůstávají v DB, záznam se jen skryje
   z přehledů. Externí vazby a komentáře zůstávají.
   ```

3. Potvrdíš → záznam se označí jako smazaný

## Co soft-delete dělá

- Nastaví `dbo.projektove_zaznamy.is_deleted = 1`
- Nastaví `deleted_at` a `deleted_by`
- Audit log: `RECORD_DELETED`

## Co soft-delete **NEdělá**

- **Nemaže komentáře** — zůstávají v `zaznam_komentare`
- **Nemaže externí vazby** — zůstávají v `zaznam_externi_odkazy`
- **Nemaže audit log** — historie zachována
- **Nemaže harmonogram** — `zaznam_harmonogram_hodnoty` zůstává
- **Nezavře ticket v ServiceDesku** — PM Tracker do SD nezapisuje

## Filtr v přehledu

V přehledu *Záznamy* je toggle **Skrýt smazané** (default zapnutý). Pro
zobrazení smazaných záznamů ho vypni — řádky budou viditelné (typicky šedé /
přeškrtnuté).

V detailu smazaného záznamu:

- Většina akcí je read-only (nelze editovat, mazat komentáře)
- Tlačítko **Obnovit** může existovat (pokud máš `records.restore`)
- Můžeš číst všechno

## Obnovení

V aktuální verzi UI **většinou není dostupné**. Obnovu řeší admin přímo
v DB:

```sql
UPDATE dbo.projektove_zaznamy
SET is_deleted = 0, deleted_at = NULL, deleted_by = NULL
WHERE id = ?;
```

Po obnovení záznam zase vidíš v default přehledu.

## Vliv na uživatelský dashboard

Smazaný záznam **se okamžitě skryje** z:

- Panel *Moje priority* na uživatelském dashboardu
- Globální vyhledávání
- Projektový dashboard *Záznamy přehled* panelu

Důvod: panely default filtrují `is_deleted=0`.

## Vliv na harmonogram

Smazaný záznam **se nezobrazí** v záložce *Harmonogram*. Hodnoty z
`zaznam_harmonogram_hodnoty` zůstávají, ale agregace záznam vyfiltruje.

## Hard-delete

Hard-delete (skutečné DELETE) **z UI nikdy není**. Pokud opravdu chceš
trvalé smazání, admin to udělá v DB — typicky migračním skriptem.

## Související

- [Smazat projekt](../smazat-projekt.md) — analogická logika na úrovni projektu
- [Filtry a režimy zobrazení](filtry-a-rezimy-zobrazeni.md)
