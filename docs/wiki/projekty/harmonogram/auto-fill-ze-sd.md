---
title: Auto-fill harmonogramu ze SD
description: Automatické vyplnění skutečnosti kroku z dodaných vyjádření ze ServiceDesku.
---

# Auto-fill ze SD

Pokud má záznam **externí vazbu na SD ticket**, aplikace dokáže
**automaticky vyplnit skutečnost** kroků harmonogramu z vyjádření v SD.
Uživatel nemusí ručně přepisovat termíny — sync služba to udělá za něj.

## Pohled uživatele (this stránka)

Tato stránka popisuje **co user vidí a dělá**. Pro detailní popis logiky
sync služby viz [Auto-fill skutečnosti — SD pohled](../../integrace/servicedesk/auto-fill-skutecnosti.md).

## Předpoklady

Pro auto-fill musí být splněno:

1. **Záznam má externí vazbu** na SD ticket — viz
   [Externí vazba](../zaznamy/externi-vazba-sd.md)
2. **Krok harmonogramu má nastavený typ delay** (HS0X_DELAY) — viz
   [Kroky a fáze](kroky-a-faze.md)
3. **Vyjádření v SD je klasifikované** harvest predikátem (ne *None*)
4. **Krok je v režimu Auto** (default) nebo má fixní preferred kandidáta

## Co user uvidí

Po propojení záznamu s ticketem:

1. **Reaktivní harvest** se spustí na pozadí (~sekundy)
2. Vyjádření z SD se klasifikují a přiřadí ke krokům
3. **Skutečnost kroku se automaticky vyplní** delay v dnech
4. V buňce skutečnosti se zobrazí badge **🤖 Automat**

Tooltip badgu vysvětluje "Skutečnost vyplněná automatem ze ServiceDesk
vyjádření #1234".

## Kde se to děje

V záložce *Harmonogram* projektu nebo v editoru záznamu → tab *Harmonogram*.

## Workflow auto-fill (z user pohledu)

```
1. Vytvoříš PMP záznam → propojíš s ticketem #363139
                ↓
2. Aplikace na pozadí: harvest, klasifikace
                ↓
3. Po několika sekundách: skutečnost kroků automaticky vyplněná
                ↓
4. Buňka HS04 ukazuje "+2 dny" 🤖 (z vyjádření "Dodáno o 2 dny později")
                ↓
5. Pokud chceš zafixovat tu hodnotu, přepneš na Ručně
   Pokud chceš jiné vyjádření, použiješ dropdown alternativních kandidátů
```

## Když dorazí novější vyjádření

Pokud sync **později** najde novější vyjádření matching predikát:

- **Auto režim** → přepíše skutečnost. User uvidí změnu při dalším otevření
- **Manual režim** → nepřepíše, hodnota zůstává

## Klasifikace vyjádření

Aplikace klasifikuje vyjádření z SD podle textu na **5 typů harvest predikátů**.
Detail textů: [Auto-fill skutečnosti — SD pohled](../../integrace/servicedesk/auto-fill-skutecnosti.md).

Mapping per typ tiketu (z `HarmonogramKrokDatumMapping.cs`):

| Predikát | Hledaný text | NES krok | PMP krok | PNF krok |
|---|---|---|---|---|
| **K3** | `Záznam byl založen a předán dodavateli k řešení pod značkou:` | — | 3 | — |
| **K4_K7** | `Dodavatel přidal řešení` | — | 4 | 7 |
| **K6** | `Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována.` | — | — | 6 |
| **K10** | `Záznam byl převeden do archivu.` | — | 10 | 10 |
| **Plán dodání** | `předal záznam dodavateli :` AND `s termínem plnění dodavatele` | informativní | informativní | informativní |
| **None** | (žádný match) | ignoruje se | ignoruje se | ignoruje se |

Konkrétní mapping je v `HarmonogramKrokDatumMapping.cs` — viz
[Kroky a fáze](kroky-a-faze.md).

## Důvody proč auto-fill nedoplňuje skutečnost

### 1. Vyjádření je klasifikované jako None

Text v SD neodpovídá žádnému predikátu (např. "termín se posouvá" místo
"Plán dodání: ...").

**Vyřešení**: žádné z UI. Buď přidat predikát (vývojářská změna), nebo zadat
skutečnost ručně.

### 2. Krok nemá nastavený typ delay

V `zaznam_harmonogram_hodnoty` chybí řádek s `ZpozdeniTypId` pro tento krok.

**Vyřešení**: doplnit v editoru záznamu nebo migraci.

### 3. Krok je v režimu Ručně

User cíleně přepnul krok na Ručně — sync nepřepíše.

**Vyřešení**: přepnout zpět na Auto (pokud chceš auto-fill).

### 4. Žádná externí vazba

Záznam je typu *Úkol* nebo nemá vyplněné `id` ticketu.

**Vyřešení**: přidat externí vazbu — viz [Externí vazba](../zaznamy/externi-vazba-sd.md).

### 5. SD je nedostupný

Sync nemůže načíst vyjádření.

**Vyřešení**: počkat než SD bude zase dostupný, případně admin prověří
[/SDConnector](../../integrace/servicedesk/sd-konektor-diagnostika.md).

## Re-harvest

Pokud chceš **rychle aktualizovat** skutečnost (např. víš že dodavatel
právě poslal vyjádření), použij:

- **Re-harvest v chat modalu** — pro tento jeden ticket
- **Re-harvest v `/SDConnector`** — admin operace pro libovolnou vazbu

Re-harvest spustí sync **okamžitě**, bez čekání na další periodický job.

## Permissions

Auto-fill běží **automaticky** bez uživatelské akce. Permission keys
relevantní pro úpravu chování:

- `harmonogram.toggle-rezim` — přepínat Auto / Ručně
- `harmonogram.select-candidate` — fixovat preferred kandidáta
- `vyjadreni.reharvest` — manuálně spustit re-harvest

Bez těchto klíčů sync stále běží, jen ho user nemůže ovlivnit.

## Související

- [Manuální skutečnost a režim Auto/Ručně](manualni-skutecnost.md)
- [Auto-fill skutečnosti — detailní integrace](../../integrace/servicedesk/auto-fill-skutecnosti.md)
- [Chat vyjádření](../../integrace/servicedesk/chat-vyjadreni.md)
- [Plán versus skutečnost](plan-vs-skutecnost.md)
