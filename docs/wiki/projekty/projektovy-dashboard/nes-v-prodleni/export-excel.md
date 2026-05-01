---
title: Export NES panelu do Excelu
description: Jak stáhnout NES tabulku jako .xlsx soubor.
---

# Export NES panelu do Excelu

Panel [NES v prodlení](index.md) má **tlačítko Export do Excelu**. Stáhne
tabulku ticketů jako `.xlsx` soubor pro další zpracování (reporting, mail,
archivace).

## Postup

1. Otevři projektový dashboard → panel **NES v prodlení**
2. V hlavičce panelu klikni **Export do Excelu**
3. Browser stáhne `.xlsx` soubor

Default umístění = stažené soubory prohlížeče (`Downloads/`).

## Název souboru

Formát:

```
NES-projekt-{projektId}-{datum}.xlsx
```

Příklad:

```
NES-projekt-42-20260425.xlsx
```

- `projektId` — interní ID projektu
- `datum` — `yyyyMMdd` formát data exportu

## Obsah souboru

### Meta řádek

První řádek obsahuje **metadata exportu**:

```
Export NES v prodlení | Projekt: FIS-EIS Modernizace | Datum: 25.4.2026 14:32
```

### Hlavička sloupců (řádek 2)

```
Ticket ID | Pid | Typ | Stručně | Dodavatel | Termín | Dní v prodlení | Stav | URL na SD
```

### Datové řádky

Pro každý ticket v panelu jeden řádek se sloupci:

- **Ticket ID** — 6-cifer
- **Pid** — z `HOT_ZAZNAMY.pid`
- **Typ** — vždy `NES`
- **Stručně** — krátký popis ticketu
- **Dodavatel**
- **Termín** — datum překročeného termínu
- **Dní v prodlení** — počet dní (kladné číslo)
- **Stav** — aktuální SD stav
- **URL na SD** — `https://servicedesk.fis.acr/Hotline/Ticket/Details/{id}`
  (klikací)

## Formátování v Excelu

- **Sloupec Termín** — date format (DD.MM.YYYY)
- **Sloupec Dní v prodlení** — number format
- **URL** — hyperlink (klik otevře v browseru)
- **Hlavička** — bold, vyšší řádek

## Co se exportuje

Export obsahuje **přesně to co panel ukazuje** — aplikované filtry zahrnuté.
Pokud je v panelu 5 řádků, v souboru je 5 datových řádků.

Filtrace je **stejná jako pro panel** — viz [Filtrace](filtrace.md).

## Použití

### Reporting

- Mailem sponzorovi / managementu
- Příloha k projektovému reportu

### Vlastní analýza

- Pivot tabulky v Excelu
- Sloučení s daty z jiných projektů (manuální consolidace)

### Archivace

- Snapshot stavu k danému datu (typicky konec měsíce / kvartal)

## Tlačítko se nezobrazí když

- Panel je v **graceful state** (projekt nemá IS)
- V panelu **nejsou žádné položky** (`Items.Count == 0`)
- User **nemá permission** pro export

## Permissions

| Akce | Klíč |
|---|---|
| Vidět panel | `dashboard.nes.view` |
| Export do Excelu | (typicky implicitní s `dashboard.nes.view`) |

## Co tady **NEjde**

- **Plánovaný export** (denně mailem) — aplikace nemá scheduler
- **Hromadný export více projektů** — per-projekt
- **Export jiných panelů** do Excelu — jen NES panel má XLSX export. Ostatní
  panely (Záznamy, Statistiky, Výzvy) zatím nemají Excel variant.

## Performance

Generování XLSX trvá **< sekundu** pro typický počet řádků (do stovky). Pro
extrémně velké exporty (tisíce řádků) může chvíli trvat — zobrazí se
spinner.

## Soubor je dočasný

Aplikace si **soubor neukládá** — vygeneruje, pošle browseru, zapomene.
Pokud potřebuješ stejný export později, klikni *Export* znovu (může se
mírně lišit kvůli mezitímnímu sync).

## Související

- [NES panel](index.md)
- [Filtrace](filtrace.md)
- [PDF versus Word versus HTML](../../../export/pdf-vs-word.md)
- [Export](../../../export/) — obecná oblast exportů
