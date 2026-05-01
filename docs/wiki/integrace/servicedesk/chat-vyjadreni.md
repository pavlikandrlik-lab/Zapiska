---
title: Chat vyjádření z SD ticketu
description: Modal v PM Trackeru, který ukazuje vyjádření ze ServiceDesk ticketu jako chronologický chat.
---

# Chat vyjádření

**Chat modal** je dialogové okno v PM Trackeru, které načte všechna
vyjádření z propojeného SD ticketu a zobrazí je jako chronologický chat.
Slouží k:

- **Přečtení historie komunikace** ticketu bez opouštění PM Trackeru
- **Propojení vyjádření s krokem harmonogramu** (drag & drop)
- **Spuštění re-harvestu** (refresh dat ze SD)

## Kde najít

Editor záznamu (s externí vazbou na SD ticket) → tlačítko **Otevřít chat**.

Tlačítko je k dispozici jen pokud:

- Záznam má vyplněnou externí vazbu (existující 6-ciferné ticket id)
- Aplikace má `Ticketing.Enabled = true`
- User má `vyjadreni.view` permission

## Co modal zobrazuje

### Hlavička

- Číslo ticketu + odkaz do SD UI
- Typ záznamu (NES / PMP / PNF)
- Stav ticketu

### Stepper (`<pm-chat-stepper>`)

Custom HTML element, který renderuje **5 slotů harmonogramu** odpovídajících
hlavním krokům (typicky HS01–HS05). Slouží jako **drop target** pro vyjádření.

Stav slotu:

- **Prázdný** — žádné vyjádření přiřazené, lze drop
- **Naplněný** — vyjádření připojené, vidíš ho v slotu
- **Disabled** — slot není pro tento typ záznamu relevantní

### Bubliny vyjádření

Pro každé vyjádření z `HOT_VYJADRENI` jedna bublina:

| Atribut | Co zobrazí |
|---|---|
| Autor | DisplayName z AD (resolved přes `AdLoginCache`) |
| Datum | Datum a čas vyjádření |
| Tým | Tým autora (z `HOT_VYJADRENI.tym`) |
| Text | Plain text (HTML zbavený značek z `popis`) |
| Klasifikace | Badge K3 / K4_K7 / K6 / K10 / PlanDodani / None |

Bubliny seřazené **chronologicky** (nejstarší → nejnovější). U shody data
secondary sort podle `id`.

### Tlačítka

- **Re-harvest** — ručně spustí refresh dat ze SD (užitečné pokud user ví,
  že v SD právě přidal vyjádření)
- **Odpojit vazbu** (na konkrétním propojeném vyjádření)
- **Zavřít** — zavře modal

## Drag & drop — jak funguje

### Bublinu na slot

User uchopí bublinu vyjádření, přetáhne na konkrétní slot stepperu, pustí.
Aplikace:

1. Zachytí drop event
2. Pošle POST `/Vyjadreni/HarmonogramVazba/Create`
3. Zápis do `vyjadreni_vazby` (record propojení vyjádření × krok harmonogramu)
4. Slot stepperu se vizuálně naplní
5. Sync služba (na pozadí) přepočítá skutečnost harmonogramu, pokud je krok
   v režimu Auto

### Odpojení

Klik na tlačítko *Odpojit* na propojeném slotu. Aplikace:

1. POST `/Vyjadreni/HarmonogramVazba/Delete`
2. Označí vazbu v `vyjadreni_vazby` jako neaktivní
3. Slot se uvolní
4. Sync přepočítá skutečnost (může se vrátit na default Auto kandidáta)

## Klasifikace harvest predikátem

Každé vyjádření má **automatickou klasifikaci** podle textu (`HOT_VYJADRENI.popis`).
Detekce je case-insensitive `Contains` match:

| Predikát | Hledaná fráze | Typ → krok |
|---|---|---|
| **K3 — Odeslání zadání PMP** | `Záznam byl založen a předán dodavateli k řešení pod značkou:` | PMP → 3 |
| **K4 / K7 — Dodání řešení** | `Dodavatel přidal řešení` | PMP → 4, PNF → 7 |
| **K6 — Odeslání požadavku** | `Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována.` | PNF → 6 |
| **K10 — Nasazení / archivace** | `Záznam byl převeden do archivu.` | PMP/PNF → 10 |
| **Plán dodání** | `předal záznam dodavateli :` AND `s termínem plnění dodavatele` | informativní (badge) |
| **None** | Text neodpovídá žádnému predikátu | nepoužívá se |

Pořadí kontroly je dáno (specifičtější predikáty první) — `PlanDodani`
obsahuje fragment "předal záznam dodavateli", takže se kontroluje **před**
K4_K7. Detail v `HarvestPredicates.cs`.

Klasifikace je zobrazená **na bublině** jako badge. Pomáhá:

- User pozná, že vyjádření odpovídá konkrétnímu kroku harmonogramu
- Drag & drop si tip "tohle K4_K7 patří do slotu *Dodání*"
- Auto-fill využívá klasifikaci pro automatickou volbu kandidáta

## Co tady **uděláš**

- Přečteš si historii ticketu chronologicky
- Klikneš odkaz do SD pro plný kontext
- Drag-drop přiřadíš vyjádření k harmonogramu kroku
- Odpojíš existující vazbu
- Spustíš re-harvest

## Co tady **NEuděláš**

- **Nepošleš nové vyjádření do SD** — PM Tracker je read-only. Nové vyjádření
  zadáš přímo ve ServiceDesku.
- **Nemáš vlastní komentáře** — komentáře PM Trackeru jsou v jiném panelu
  na záznamu (viz [Komentáře](../../projekty/zaznamy/komentare.md))

## Re-harvest

Tlačítko **Re-harvest** spustí ad-hoc sync pro tento jediný ticket:

- Prochází `HOT_VYJADRENI` znova
- Aktualizuje cache klasifikací
- Refresh modalu po dokončení

Užitečné když user ví že v SD právě přidal nové vyjádření a chce ho hned
vidět v PM Trackeru (bez čekání na další periodický harvest).

Re-harvest vyžaduje permission `vyjadreni.reharvest`.

## Pm-chat-stepper — custom element

Pro vývojáře: `<pm-chat-stepper>` je custom HTML element registrovaný v
`/wwwroot/js/components/pm-chat-stepper/pm-chat-stepper.js`. Wrapper kolem
gov-stepper s vlastní logikou pro:

- 5-slot buffer
- Chronologii drag&drop validace (nelze přiřadit dřívější vyjádření na pozdější
  krok)
- Visual feedback pro disabled / filled / empty stavy

Detaily implementace mimo scope této wiki.

## Související

- [Tickety PMP/PNF/NES](tickety-pmp-pnf-nes.md)
- [Auto-fill skutečnosti](auto-fill-skutecnosti.md)
- [Záznamy → Komentáře](../../projekty/zaznamy/komentare.md)
- [Záznamy → Externí vazba](../../projekty/zaznamy/externi-vazba-sd.md)
