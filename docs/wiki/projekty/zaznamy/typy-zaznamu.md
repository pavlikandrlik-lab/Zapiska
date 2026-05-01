---
title: Typy záznamů
description: NES, PMP, PNF a úkol — co znamenají a kdy se používají.
---

# Typy záznamů

PM Tracker eviduje **čtyři hlavní typy záznamů**. Tři jsou vázané na ServiceDesk
(externí tickety), jeden je čistě interní (úkol).

## Externí typy (vázané na ServiceDesk)

Externí typy odpovídají typům ticketů v `HOT_ZAZNAMY` ve ServiceDesku. Každý
takový záznam v PM Trackeru má **externí vazbu** na konkrétní ticket
(6-ciferné `id`).

### NES — Nesrovnalost

Tickety pro **incidenty / vady** v provozovaném IS:

- Porouchaná funkčnost
- Chyba v datech
- Performance problém
- SLA vada od dodavatele

NES je vázán na **SLA dodavatele** — v `HOT_ZAZNAMY.sla_deadline` má termín
dodání řešení. V PM Trackeru se NES tickety zobrazují v projektovém
dashboardu **NES panelu** jako "v prodlení" když `sla_deadline < dnes`.

**NES typicky nemá kalkulaci** — řeší se v rámci servisní smlouvy.

Subtypy v `subsystem`: `NES I`, `NES II`, `NES III` (úroveň závažnosti),
`Vada A`, `Vada B`, `Vada C`, `Vada D` (typ vady).

### PMP — Požadavek metodické podpory

Tickety pro **úpravy existujícího IS s finanční kalkulací**:

- Úprava existující funkčnosti
- Změna business logiky
- Optimalizace

PMP má v `HOT_KALKULACE` 1 souhrnný řádek (hodiny × sazba × cena) plus N
řádků v `HOT_KALKULACE_PMP` (per-zaměstnanec rozpis). PM Tracker kalkulaci
read-only zobrazuje.

Termín: `HOT_ZAZNAMY.dat_res_t` (termín pro řešitele).

### PNF — Požadavek nové funkcionality

Tickety pro **rozšíření IS o novou funkčnost** (s kalkulací):

- Nová feature
- Nový modul
- Nová integrace

PNF má v `HOT_KALKULACE` jeden souhrnný řádek (bez per-zaměstnanec rozpisu —
narozdíl od PMP).

Subtypy: `PNF I`, `PNF II`, `Pozadavek`.

Termín: `HOT_ZAZNAMY.dat_res_t`.

## Interní typ — Úkol

Vlastní typ projektu **bez vazby na ServiceDesk**. Slouží pro:

- Interní úkoly týmu (např. "připravit prezentaci", "review architektury")
- Plánování / milníky bez SD ticketu
- Tasks které nemají přímé propojení s IS provozem

Úkol nemá externí vazbu, neexistuje v `HOT_ZAZNAMY`. Existuje jen v PM Trackeru.

## Volba typu při vytváření záznamu

V editoru záznamu je dropdown **Typ**. Po vybrání:

| Volba | Co aplikace očekává |
|---|---|
| **NES / PMP / PNF** | Vyplnit **externí vazbu** (6-cifer ticket id) — povinné? Záleží na konfiguraci |
| **Úkol** | Externí vazba není potřeba |

### Co když vyberu NES ale ticket neexistuje

Aplikace ti **inline ohlásí chybu** "Ticket #X v HOT_ZAZNAMY neexistuje."
Pole je validační error → záznam neuložíš dokud ho nesprávně vyplníš nebo
nezměníš typ na *Úkol*.

## Změna typu po vytvoření

V editoru záznamu **lze typ změnit**. Konsekvence:

- **NES → PMP / PNF** — externí vazba zůstává, jen se mění význam
- **Externí typ → Úkol** — externí vazba se ne smaže (zůstává), jen se
  záznam přestane filtrovat jako externí v dashboardu
- **Úkol → externí typ** — musíš vyplnit externí vazbu, jinak chyba

## Klasifikace harvest predikátem

Pro **externí záznamy** (NES / PMP / PNF) aplikace načítá vyjádření z SD
a klasifikuje je harvest predikáty:

- **K3** — Odeslání zadání PMP (typicky pro PMP záznamy)
- **K4 / K7** — Dodání řešení (pro všechny externí)
- **K6** — Odeslání požadavku
- **K10** — Nasazení / archivace
- **Plán dodání** — explicitní text "termín dodání"

Klasifikace pak řídí auto-fill harmonogramu. Detail:
[Auto-fill skutečnosti](../../integrace/servicedesk/auto-fill-skutecnosti.md).

## Filtrace v přehledu Záznamy

V hlavičce přehledu je dropdown filtru typu — můžeš zobrazit jen NES, jen
PMP, jen PNF, jen úkoly nebo všechny.

## Pro koho

Všichni uživatelé. Volba typu je obvykle dána **business kontextem** záznamu,
ne rolí uživatele.

## Související

- [Externí vazba na SD](externi-vazba-sd.md)
- [Tickety PMP/PNF/NES (SD pohled)](../../integrace/servicedesk/tickety-pmp-pnf-nes.md)
- [Slovníček](../../slovnicek.md)
