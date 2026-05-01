---
title: Filtrace NES panelu
description: Co určuje které NES tickety se v panelu zobrazí.
---

# Filtrace NES panelu

Panel [NES v prodlení](index.md) zobrazuje **podmnožinu ticketů ze
ServiceDesku**. Tato stránka popisuje **kritéria filtrace** — proč některé
tickety vidíš a jiné ne.

## Klíčové filtry

Aplikace aplikuje **AND** filtraci — ticket musí splnit **všechna** kritéria,
aby se zobrazil.

### 1. Typ záznamu

```sql
WHERE typ_zaznamu = 'NES'
```

Pouze NES tickety. PMP a PNF tickety se v tomto panelu **nezobrazují**, ani
když jsou v prodlení.

### 2. Informační systém projektu

```sql
WHERE id_IS = (projekt.ServiceDeskInfoSystemId)
```

Filtruje na FIS (`HOT_IS.ID = 1`) nebo ISSP (`HOT_IS.ID = 2`) podle
[propojení projektu na IS](../../../integrace/servicedesk/propojeni-projekt-is.md).

### 3. Aktivní stavy

```sql
WHERE stav IN ('otevreno', 'dodavatel', 'odsouhlaseno', ...)
   AND stav NOT IN ('uzavreno', 'archiv', 'zruseno')
```

Konkrétní seznam aktivních stavů závisí na číselníku stavů v ServiceDesku.
Default vyloučeny:

- `uzavreno` (uzavřené tickety jsou vyřešené)
- `archiv` (archivované, mimo aktivní set)
- `zruseno` (zamítnuté)

### 4. Překročený termín

```sql
WHERE term_pl < TODAY
   OR (term_pl IS NOT NULL AND splneno IS NULL AND term_pl < TODAY)
```

Termín dodání musí být **dříve než dnes**. Tickety bez termínu nebo s budoucím
termínem se nezobrazují (nejsou v prodlení).

### 5. Subsystém (volitelné)

V některých instalacích je dál filtrace na subsystémy projektu. Pokud
projekt **má přiřazené subsystémy**, panel ukazuje jen tickety těchto
subsystémů.

## Co se NEzobrazí

Pro každý filtr je opačný případ:

| Filtr | Neuvidíš |
|---|---|
| Typ NES | PMP, PNF, ostatní |
| IS projektu | Tickety jiných IS než projektový |
| Aktivní stavy | Uzavřené / archivované |
| Překročený termín | Dosud neexpirované |
| Subsystém | Tickety mimo subsystémy projektu |

## Když panel je prázdný — diagnostika

```
1. Má projekt propojení na IS?
   → Pokud ne, panel ukáže graceful state, ne prázdnou tabulku
   → Vyřeš v [Editaci projektu](../../upravit-projekt.md)

2. Reálně existují NES v prodlení daného IS?
   → Otevři /SDConnector/Inspect, ověř existenci konkrétního NES
   → Pokud reálně nejsou, panel správně prázdný

3. Sync proběhl?
   → /SDConnector → KPI "Vytěženo za 24 h"
   → Pokud 0, harvest má problémy

4. Chyba SD connection?
   → /SDConnector/Diag?cislo=XXXXXX
   → Plný stack trace pokud něco selhává
```

## Co kdyby panel zobrazoval příliš mnoho

V projektu s **velkým počtem NES** může být tabulka přetížená. Aktuálně
**není UI filtrace per panel** (např. "jen tickety dodavatele X"). Pro
detailní filtraci musíš jít do ServiceDesku přímo.

V budoucnu může přibýt:

- Filter per dodavatel
- Filter per priorita / urgentnost
- Filter per stav

(Feature request — pro teď jen indikativní seznam.)

## Tickety bez `id`

Z [memory `feedback_sd_ticket_id_required`](../../../slovnicek.md): tickety bez
6-ciferného `id` jsou **mimo scope PM Trackeru**. Ani v NES panelu se
takové tickety nezobrazí — i kdyby jiné kritéria splnily.

Aplikace nikdy nemapuje na fallback `id=0`.

## Jak to ovlivňuje grafy / KPI

Statistika "Průměr dnů zpoždění" v hlavičce panelu se počítá **jen z
viditelných řádků** — ne ze všech NES v SD. Pokud měníš filtraci (přidáš
subsystém), KPI se přepočítá.

## Související

- [NES panel](index.md)
- [Export do Excelu](export-excel.md)
- [Propojení projektu na IS](../../../integrace/servicedesk/propojeni-projekt-is.md)
- [Tickety PMP/PNF/NES](../../../integrace/servicedesk/tickety-pmp-pnf-nes.md)
