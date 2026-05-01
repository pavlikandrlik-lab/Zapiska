---
title: Nový projekt
description: Jak založit projekt v PM Trackeru.
---

# Nový projekt

## Předpoklady

- Permission `projects.create` (typicky SuperAdmin / App Admin / vybraný leader)

## Postup

1. Hlavní stránka *Projekty* → tlačítko **Nový projekt**
2. Otevře se modal s formulářem
3. Vyplň povinná pole + volitelná
4. Klikni *Uložit*

Po uložení tě aplikace přesměruje na **detail nového projektu**, kde můžeš
hned začít přidávat členy týmu, jednání a záznamy.

## Pole formuláře

| Pole | Povinné | Popis |
|---|---|---|
| **Název** | ✓ | Plný název projektu (např. "FIS-EIS Modernizace") |
| **Zkratka** | ✓ | Krátký identifikátor (např. "FIS-EIS"), používá se v URL |
| **Stav** | ✓ | Default `PLAN`. Možnosti: PLAN, BEZI, DOKONCEN, ARCHIV |
| **Používat identifikátor záznamů podle jednání (8201)** | — | Toggle — pokud zapnutý, záznamy se v exportech označují číslem jednání místo vlastním ID |
| **Místo plnění** | — | Volný text. Použito pro generování výzev (např. "FIS (EIS): VZ 8201, Tychonova 1, 160 01 Praha 6") |
| **Číslo rámcové smlouvy** | — | Pro generování výzev (např. "23106000271") |
| **Informační systém** | — | Dropdown FIS / ISSP / *— bez napojení —*. Volba: [Propojení projektu na IS](../integrace/servicedesk/propojeni-projekt-is.md) |

## Validace

- **Název** musí být neprázdný
- **Zkratka** musí být neprázdná, doporučeno do 50 znaků (kratší je lepší pro
  URL a tabulky)
- **Stav** musí být z platného číselníku
- **Informační systém** musí odpovídat hardcoded katalogu (FIS=1, ISSP=2)
  pokud je vyplněný

## Co se po uložení stane

1. Vytvoří se záznam v `dbo.projekty`
2. Aktuální user dostane **roli VLASTNIK_PROJEKTU** na novém projektu (default;
   konfigurovatelné)
3. Audit log: `PROJECT_CREATED` s tvým loginem
4. Aplikace tě přesměruje na detail projektu

## Co po uložení **nevytvoří**

- Žádné záznamy / jednání / harmonogram — to vše vytváříš ručně po vytvoření
  projektu
- Žádný tým mimo tebe — členy přidáváš v záložce *Tým*

## Související

- [Upravit projekt](upravit-projekt.md)
- [Smazat projekt](smazat-projekt.md)
- [Detail projektu](prehled-projektu.md)
- [Propojení projektu na IS](../integrace/servicedesk/propojeni-projekt-is.md)
