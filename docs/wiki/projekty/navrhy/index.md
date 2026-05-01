---
title: Návrhy
description: Návrhy změn projektového plánu — workflow vytvoření, schválení, aplikace.
---

# Návrhy

Návrh = **proposal na změnu plánu projektu**. Typický scenario: dodavatel
informuje, že termín se posouvá, a leader chce **formálně zaznamenat** tuto
změnu plánu — ne přímo přepsat hodnotu, ale projít workflow vytvoření →
schválení → aplikace.

## Proč návrhy existují

Bez návrhů by změna plánu byla **nelegitimní úprava DB**:

- Žádný audit *kdo* a *proč* změnil plán
- Žádné schválení od leadera / sponsora
- Hodnoty plánu by byly nestabilní

S návrhy:

- Změna **má vlastní záznam** s odůvodněním
- Schvalovatel **podepíše** změnu (audit)
- **Lock kroku** brání přímé editaci paralelně s otevřeným návrhem

## Workflow

```
┌─────────┐    ┌──────────┐    ┌──────────────────┐    ┌───────────┐
│Vytvoření│ ─▶ │Schválení │ ─▶ │ Aplikace na plán │ ─▶ │ Závěrečné │
│ návrhu  │    │          │    │ (přepis baseline)│    │ uzavření  │
└─────────┘    └──────────┘    └──────────────────┘    └───────────┘
                    ▲
                    │ alternativně
                    │
                ┌───┴────┐
                │Zamítnut│
                └────────┘
```

## Stavy návrhu

| Stav | Popis | Dále možno |
|---|---|---|
| **Vytvořen** | Návrh existuje, čeká na schválení | Schválit / Zamítnout / Stáhnout |
| **Schválen** | Schválen, připraven k aplikaci | Aplikovat / Zamítnout |
| **Zamítnut** | Schvalovatel ho zamítl s důvodem | (terminal) |
| **Aplikován** | Plán už je přepsaný novou hodnotou | (terminal) |
| **Stažen** | Autor ho stáhl před schválením | (terminal) |

## Layout záložky Návrhy

V detailu projektu → tab *Návrhy*:

### Filtry

- **Stav** (Vytvořené / Schválené / Aplikované / Zamítnuté / Stažené / Všechny)
- **Autor** (já / kdokoliv)
- **Krok harmonogramu** (HS01, HS02, …)

### Tabulka návrhů

Sloupce:

- **ID návrhu**
- **Záznam** (klikací odkaz)
- **Krok**
- **Aktuální plán → navrhovaná hodnota**
- **Stav**
- **Autor** + datum
- **Akce** (Schválit / Zamítnout / Aplikovat — podle stavu a permissions)

### Tlačítko v hlavičce

- **Nový návrh** (`proposals.create`)

## Co je v této sekci wiki

- [Navrhnout změnu plánu](navrhnout-zmenu-planu.md) — workflow autora
- [Schválení návrhu](schvaleni-navrhu.md) — workflow schvalovatele

## Pendingscheduleproposallock

Když existuje návrh ve stavu *Vytvořen* nebo *Schválen* pro konkrétní krok:

- **Buňka skutečnosti** v harmonogramu je **disabled** (read-only)
- Tooltip vysvětluje "Probíhá schvalování návrhu #X, počkej na uzavření"
- Auto-fill běží dál ale jeho výsledky nejsou na buňce viditelné

Lock se uvolní:

- Po **aplikaci** návrhu (nový plán = navrhovaná hodnota)
- Po **zamítnutí** návrhu (plán zůstává)
- Po **stažení** návrhu (autor vzal zpět)

## Pro koho

| Persona | Co může |
|---|---|
| **Member** | Vytvořit návrh (`proposals.create`) |
| **Leader** | + Schválit / Zamítnout (`proposals.approve`) |
| **Admin** | Vše + překročit lock |

## Permissions

- `proposals.view` — vidět tab Návrhy
- `proposals.create` — vytvořit
- `proposals.approve` — schválit / zamítnout
- `proposals.apply` — aplikovat schválený návrh (typicky leader / admin)

## Související

- [Harmonogram → Plán versus skutečnost](../harmonogram/plan-vs-skutecnost.md)
- [Harmonogram → Manuální skutečnost](../harmonogram/manualni-skutecnost.md)
- [Záznamy → Upravit záznam](../zaznamy/upravit-zaznam.md)
