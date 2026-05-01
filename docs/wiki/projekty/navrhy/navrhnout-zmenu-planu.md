---
title: Navrhnout změnu plánu
description: Jak vytvořit návrh změny harmonogramu.
---

# Navrhnout změnu plánu

Návrh = **formální žádost o změnu plánu** harmonogramu. Slouží pro situace
kdy:

- **Dodavatel posunul termín** — nový plán musí být schválený, ne tichá oprava
- **Změna scope** — krok se prodloužil kvůli novým požadavkům
- **Slip + recovery plan** — přiznáme zpoždění a navrhneme nový plán

Bez návrhu by změna plánu byla **netransparentní** — žádný audit kdo a proč
změnil termín.

## Předpoklady

- Permission `proposals.create` na projektu
- Existující záznam s harmonogramem
- Krok harmonogramu **není uzamčený** jiným otevřeným návrhem
  (`pendingScheduleProposalLock`)

## Postup

1. Otevři detail záznamu → tab *Harmonogram*
2. U konkrétního kroku → tlačítko **Navrhnout změnu** (nebo z hlavního tabu
   *Návrhy* tlačítko *Nový návrh*)
3. Otevře se modal s formulářem
4. Vyplň pole
5. Ulož

## Pole formuláře

| Pole | Povinné | Popis |
|---|---|---|
| **Záznam** | ✓ | Předvyplněné, lze změnit dropdownem |
| **Krok** | ✓ | HS01 / HS02 / ... |
| **Aktuální plánovaná hodnota** | (read-only) | Pro kontext |
| **Navrhovaná nová hodnota** | ✓ | Číslo dní (HS0X_DURATION) |
| **Odůvodnění** | ✓ | Volný text — proč navrhuješ změnu |
| **Volitelné: dopad na další kroky** | — | Popis kaskádových efektů |
| **Volitelné: požadovaný schvalovatel** | — | Lze cílit na konkrétního leadera |

## Po uložení

### 1. Návrh se vytvoří

V `dbo.navrhy` (nebo ekvivalentní tabulka) se vytvoří záznam ve stavu
**Vytvořen**.

### 2. Krok se uzamkne

`pendingScheduleProposalLock` na kroku — ostatní uživatelé nemohou:

- Přímo editovat skutečnost (auto-fill běží dál ale výsledek nepřepisuje
  baseline)
- Toggle Auto/Ručně
- Manuálně zadat hodnotu

Tooltip na zamčené buňce vysvětluje "Probíhá schvalování návrhu #X".

### 3. Audit log

```
TYPE: PROPOSAL_CREATED
PROPOSAL: 42
RECORD: 123
KROK: HS04
BEFORE: { duration: 5 }
AFTER:  { duration: 7 }
REASON: "Dodavatel oznámil posun termínu o 2 dny"
ACTOR: jan.novak
```

### 4. Notifikace schvalovatele

V aktuální verzi aplikace **nemá email notifikace**. Schvalovatel se musí:

- Pravidelně dívat na tab *Návrhy* projektu
- Mít info z jiného kanálu (mail / chat) že návrh existuje

## Co dál

Návrh přejde do schvalovacího workflow. Schvalovatel:

- **Schválí** → návrh ve stavu *Schválen*, čeká na aplikaci
- **Zamítne** → návrh ve stavu *Zamítnut* (terminal), zámek se uvolní
- **Vrátí k přepracování** → autor návrhu může upravit

Detail: [Schválení návrhu](schvaleni-navrhu.md).

## Stažení návrhu

Pokud po vytvoření **změníš názor**, můžeš návrh stáhnout (před schválením):

1. Detail návrhu → tlačítko **Stáhnout**
2. Confirm dialog
3. Návrh přejde do stavu *Stažen* (terminal)
4. **Krok se odemkne**, lze normálně editovat

Permission `proposals.withdraw` (typicky autor sám může).

## Vztah k záznamu

Návrh **nemění samotný záznam** — jen jeho harmonogram. Záznam zůstává v
původním stavu, ostatní pole (název, popis, vlastník) se nemění.

Pro úpravu jiných polí záznamu (mimo plán harmonogramu) použij běžný
[editor záznamu](../zaznamy/upravit-zaznam.md).

## Více návrhů na jeden krok

Aplikace **brání paralelním návrhům** — krok může mít **nejvýš jeden
otevřený návrh** (Vytvořen + Schválen, ale ještě ne Aplikován). Pokus o
další návrh hodí chybu:

```
Pro krok HS04 už existuje otevřený návrh #42 (Schválen, čeká na aplikaci).
Buď ho vyřeš (Aplikuj / Zamítni), nebo počkej na uzavření.
```

To brání chaosu kdy by se schválily dva protichůdné návrhy.

## Permissions

- `proposals.view` — vidět tab Návrhy
- `proposals.create` — vytvořit
- `proposals.withdraw` — stáhnout vlastní (typicky implicitní s create)

## Související

- [Schválení návrhu](schvaleni-navrhu.md) — workflow schvalovatele
- [Plán vs. skutečnost](../harmonogram/plan-vs-skutecnost.md)
- [Manuální skutečnost](../harmonogram/manualni-skutecnost.md)
