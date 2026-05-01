---
title: Subsystémy
description: Rozčlenění projektu na subsystémy a přiřazení k nim.
---

# Subsystémy

Subsystém = **logická část projektu** (modul, komponenta, oblast). Velké
projekty mají typicky více subsystémů, malé projekty mohou mít jen jeden
nebo žádný.

## Příklady subsystémů

V FIS-EIS projektu mohou být subsystémy:

| Zkratka | Název | Co obsahuje |
|---|---|---|
| `R_EIS` | EIS jádro | Hlavní funkčnost EIS |
| `R_HFU` | HFU modul | HFU specifické záznamy |
| `R_DAN` | Daňový modul | Daně, výpočty |
| `R_REPORTING` | Reportingový modul | Sestavy, exporty |

Každý subsystém má vlastní:

- **Zkratku** (např. `R_DAN`)
- **Plný název** ("Daňový modul")
- **Aktivitu** (zapnutý / vypnutý)
- **Dodavatele** (firma která ho dodává)
- **Případně priznak GDPR**

## Vztah k záznamu

Záznam projektu může být **přiřazen k subsystému**. To umožňuje:

- **Filtrace** v záložce *Záznamy* podle subsystému
- **Statistiky per subsystém** v projektovém dashboardu
- **Cílení rolí** na konkrétní subsystém (viz [Role v subsystému](role-v-subsystemu.md))

Přiřazení k subsystému je **volitelné** — záznam může být bez subsystému (pak
patří k projektu jako celku).

## Přiřazení subsystému k projektu

V detailu projektu → tab **Tým** → sekce *Subsystémy* → tlačítko
**Přiřadit subsystém**:

1. Otevře se modal s dropdownem **dostupných subsystémů**
2. Dostupné subsystémy = z číselníku (typicky napříč firmou)
3. Vyber subsystém
4. Ulož

Tím se subsystém **propojí s projektem**. Pak ho můžeš:

- Přiřadit záznamům projektu
- Přiřadit role v subsystému specifickým osobám

## Číselník subsystémů

Subsystémy jsou v číselníku, typicky **sdíleném napříč projekty**. Důvod:
subsystém R_DAN znamená to samé v různých projektech (jeden modul,
provázanost na SD `HOT_SUBSYSTEM`).

Editace číselníku je [admin operace](../../ciselniky/editace.md). Většinou
přidává subsystémy admin podle potřeby projektů.

## Subsystém a SD integrace

Pokud má projekt **propojení na IS** (FIS / ISSP), subsystémy jsou vázané
na **`HOT_SUBSYSTEM`** v ServiceDesku:

- Subsystém v PM Trackeru má **zkratku** (např. `R_DAN`)
- Stejnou zkratku má `HOT_SUBSYSTEM.zkratka` v intranetNEW
- Aplikace tu vazbu využívá pro filtraci NES tiketů, statistiky, atd.

Pokud zkratka v PM Trackeru neexistuje v `HOT_SUBSYSTEM`, integrace pro
tento subsystém **nefunguje** (NES filter ho nezobrazí).

## Odebrat subsystém

V tabulce subsystémů → u řádku tlačítko **Odebrat**:

1. Confirm dialog ("Opravdu odebrat? Záznamy přiřazené k tomuto subsystému
   ztratí přiřazení.")
2. Soft-delete přiřazení projekt × subsystém
3. Záznamy s tímto subsystémem **zůstávají v DB**, ale jejich subsystém se
   stane "neaktivní"
4. Audit log: `SUBSYSTEM_REMOVED_FROM_PROJECT`

Odebrání subsystému neznamená smazání samotného číselníku — subsystém pořád
existuje pro jiné projekty.

## Permissions

- `team.subsystem.create` — přiřadit subsystém k projektu
- `team.subsystem.remove` — odebrat
- `team.view` — vidět (implicitní)

## Vztah k role v subsystému

Po přiřazení subsystému k projektu lze přidat **role v subsystému** —
specialisté pro konkrétní subsystém. Detail: [Role v subsystému](role-v-subsystemu.md).

## Související

- [Role v subsystému](role-v-subsystemu.md)
- [Propojení projektu na IS](../../integrace/servicedesk/propojeni-projekt-is.md)
- [Číselníky](../../ciselniky/) — kde se editují subsystémy
