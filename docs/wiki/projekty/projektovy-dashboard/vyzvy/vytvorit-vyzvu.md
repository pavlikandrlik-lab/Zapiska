---
title: Vytvořit výzvu
description: Postup vytvoření nové výzvy projektu.
---

# Vytvořit výzvu

## Předpoklady

- Permission `vyzvy.create` na projektu
- Projekt má vyplněné **Místo plnění** a **Číslo rámcové smlouvy**
  (slouží jako hlavička generované výzvy)

Bez těchto metadat je tlačítko *Nová výzva* disabled — aplikace by neuměla
vygenerovat strukturovaný dokument výzvy.

## Postup

1. Otevři projektový dashboard → panel **Výzvy**
2. Klikni na **+ Nová výzva** (nebo na buffer card)
3. Otevře se modal s formulářem
4. Vyplň pole
5. Ulož

## Pole formuláře

| Pole | Povinné | Popis |
|---|---|---|
| **Interní ID výzvy** | (auto) | Vygeneruje se automaticky (VZ-001, VZ-002, …) |
| **Název** | ✓ | Krátký výstižný název (např. "Daňový modul: Q2 dodávka") |
| **Popis** | — | Detailní popis scope, požadavků, akceptačních kritérií |
| **Dodavatel** | ✓ | Z číselníku dodavatelů (může být per-projekt nebo globální) |
| **Termín dodání** | ✓ | Datum kdy má být dodáno |
| **Vázaný záznam** | — | Volitelně: na který [záznam](../../zaznamy/) projektu se výzva váže |
| **Cena** | — | Odhadovaná částka (pro reporting) |
| **Stav** | (auto) | Default `Vytvořena` |

## Po uložení

### 1. Záznam výzvy se vytvoří

V tabulce výzev (např. `dbo.vyzvy`) se vytvoří záznam s atributy z formuláře.

### 2. Audit log

```
TYPE: VYZVA_CREATED
PROJECT: 42
VYZVA: VZ-005
ACTOR: jan.novak
TIMESTAMP: ...
```

### 3. Karta se objeví v panelu

Po refresh dashboardu (nebo automaticky lazy reload) je výzva vidět jako
karta v panelu *Výzvy*.

### 4. Případné další akce

- **Schválení** (pokud workflow vyžaduje) — leader nebo sponsor schválí
- **Vygenerování dokumentu** — Word / PDF s detaily výzvy
- **Odeslání dodavateli** — externí kanál (mail, SD ticket)

## Generovaný dokument

Po vytvoření výzvy lze **vygenerovat textový dokument** (typicky Word / PDF):

- Hlavička: hlavičkový papír, místo plnění, rámcová smlouva
- Tělo: detail výzvy
- Patička: kontakty, podpisy

Tlačítko *Vygenerovat dokument* je v detailu výzvy. Generovaný soubor se
stáhne do `Downloads/`. Aplikace si ho neukládá.

## Vazba na záznam

Pokud při vytvoření výběřeš **Vázaný záznam**:

- Vznikne propojení v DB (`vyzva.zaznam_id`)
- V detailu záznamu se zobrazí "Výzva: VZ-005"
- V detailu výzvy odkaz na záznam

Pokud nevyplníš, výzva existuje samostatně (pro některé use case OK —
např. obecné požadavky bez konkrétního záznamu).

## Schvalování výzvy

V některých organizacích **vyžaduje výzva schválení** (ne každý leader může
zadat dodavateli práci za peníze). Workflow:

```
Vytvořena → Ke schválení → Schválena → Odeslána → V realizaci → Dodáno → Akceptováno
              ↓
           Zamítnuta (terminal)
```

Konkrétní stavy závisí na konfiguraci.

## Místo plnění a rámcová smlouva

Atributy projektu **Místo plnění** a **Číslo rámcové smlouvy** se přebírají
**z metadat projektu** — nastavují se při [Upravit projekt](../../upravit-projekt.md).

Příklad obsahu:

- **Místo plnění**: `FIS (EIS): VZ 8201, Tychonova 1, 160 01 Praha 6`
- **Číslo rámcové smlouvy**: `23106000271`

Pokud projekt nemá vyplněné, **nelze vytvořit výzvu** (validace selže).

## Permissions

| Akce | Klíč |
|---|---|
| Vytvořit výzvu | `vyzvy.create` |
| Schválit výzvu | `vyzvy.approve` |
| Editovat výzvu | `vyzvy.edit` |
| Smazat výzvu | `vyzvy.delete` |
| Vidět panel | `dashboard.vyzvy.view` |

## Související

- [Buffer cards](buffer-cards.md)
- [Upravit projekt](../../upravit-projekt.md) — místo plnění + smlouva
- [Záznamy](../../zaznamy/) — záznamy které lze vázat k výzvě
