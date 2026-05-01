---
title: Smazat projekt
description: Soft-delete projektu — co znamená, jak ho obnovit.
---

# Smazat projekt

## Předpoklady

- Permission `projects.delete` (typicky SuperAdmin / App Admin)

## Postup

1. Hlavní stránka *Projekty* → u řádku tlačítko **Smazat**
2. Otevře se confirm dialog:

   ```
   Opravdu chcete smazat projekt?

   FIS-EIS Modernizace (FIS-EIS)
   Stav: BEZI

   Toto je soft-delete — data zůstávají v DB, projekt se jen skryje
   z přehledů. Obnovu provádí admin přes DB.
   ```

3. Potvrdíš → projekt se označí jako smazaný

## Co soft-delete dělá

- Nastaví `dbo.projekty.is_deleted = 1`
- Nastaví `deleted_at` a `deleted_by`
- Audit log: `PROJECT_DELETED`

## Co soft-delete **NEdělá**

- **Nemaže záznamy / jednání / harmonogram** — zůstávají v DB s vazbou na
  smazaný projekt
- **Nemaže audit log** — historické záznamy zůstávají kompletní
- **Nemaže externí vazby** — zůstávají v `zaznam_externi_odkazy`
- **Nezavře tickety v ServiceDesku** — PM Tracker do SD nezapisuje

## Filtr v přehledu

V přehledu *Projekty* je toggle **Skrýt smazané** (default zapnutý). Pro
zobrazení smazaných projektů ho vypni — řádky s smazanými budou viditelné
(typicky šedé / přeškrtnuté).

V detailu smazaného projektu:

- Většina akcí je read-only (nelze editovat, mazat záznamy)
- Tlačítko **Obnovit** může existovat (pokud máš permission `projects.restore`),
  jinak chybí
- Můžeš číst všechno

## Obnovení smazaného projektu

V aktuální verzi UI **většinou není dostupné**. Obnovu řeší admin přímo v DB:

```sql
UPDATE dbo.projekty
SET is_deleted = 0, deleted_at = NULL, deleted_by = NULL
WHERE id = ?;
```

Po obnovení projekt zase vidíš v default přehledu, můžeš s ním pracovat
normálně.

## Hard-delete

Hard-delete (skutečné smazání z DB) **z UI nikdy není**. Důvody:

- Audit log a historické záznamy by ztratily reference
- Externí vazby a vyjádření by orphanovaly
- Záloha by byla jediná cesta k obnově

Pokud opravdu chceš smazat trvale (např. testovací data), admin to udělá
přímo v DB — typicky přes migrační skript.

## Co když projekt má aktivní záznamy / jednání

Soft-delete **se provede i tak**. Aplikace nemá business pravidlo "nelze
smazat pokud má záznamy" — admin zodpovídá za rozhodnutí.

Po smazání projektu:

- Záznamy v `Moje priority` na uživatelském dashboardu **zmizí** (filtruje
  smazané projekty)
- Globální vyhledávání záznamy ze smazaného projektu **nevrátí**
- Audit zůstává

## Související

- [Upravit projekt](upravit-projekt.md)
- [Detail projektu](prehled-projektu.md)
- [Pomoc → FAQ](../pomoc/faq.md)
