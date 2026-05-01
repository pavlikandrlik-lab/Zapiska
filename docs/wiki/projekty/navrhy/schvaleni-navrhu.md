---
title: Schválení návrhu
description: Workflow schvalovatele — co může a co dělá.
---

# Schválení návrhu

Návrhy [změny plánu](navrhnout-zmenu-planu.md) prochází schvalovacím workflow.
Schvalovatel **(typicky leader projektu)** návrh přečte, zhodnotí a rozhodne:

- **Schválit** → návrh přejde do stavu *Schválen*, připraven k aplikaci
- **Zamítnout** → návrh ve stavu *Zamítnut* (terminal), zámek se uvolní
- **Vrátit k přepracování** → autor může upravit a podat znovu

## Předpoklady

- Permission `proposals.approve` na projektu (typicky leader)

## Cesta k návrhu

V detailu projektu → tab *Návrhy* → seznam návrhů s filtrem (default
*Otevřené*, tj. Vytvořené + Schválené).

Klik na řádek → **detail návrhu**:

```
Návrh #42

Záznam:                  #123 — "Implementace REST API"
Krok:                    HS04 (Dodání řešení)
Aktuální plán:           5 dní
Navrhovaná nová hodnota: 7 dní (+2)

Odůvodnění:
  Dodavatel oznámil, že kvůli rozšíření scope o 2 entity
  potřebuje ještě 2 dny navíc. Datum dodání se posouvá z
  30.4.2026 na 2.5.2026.

Dopad na další kroky:
  HS05 (Akceptace) se posouvá o 2 dny → nový termín 9.5.2026
  HS06 (Nasazení) zůstává — lze dohnat zkrácením HS05

Vytvořil:    jan.novak (24.4.2026 14:30)
Stav:        Vytvořen
```

## Akce schvalovatele

### Schválit

Tlačítko **Schválit**:

1. Confirm dialog ("Opravdu schválit?")
2. Po potvrzení návrh přejde do stavu *Schválen*
3. Audit log: `PROPOSAL_APPROVED`
4. **Stále zamčený krok** — čeká na aplikaci

Schválený návrh zatím **nezměnil plán**. Aplikace = další krok.

### Zamítnout

Tlačítko **Zamítnout**:

1. Otevře se modal s povinným polem **Důvod zamítnutí**
2. Vyplň důvod (volný text)
3. Po potvrzení návrh přejde do stavu *Zamítnut* (terminal)
4. **Krok se odemkne** (lock se uvolní)
5. Audit log: `PROPOSAL_REJECTED` s důvodem

Plán zůstává původní hodnotou. Autor návrhu vidí důvod a může:

- Vytvořit **nový návrh** s upravenými parametry
- Akceptovat zamítnutí

### Vrátit k přepracování

Tlačítko **Vrátit autorovi**:

1. Otevře se modal s polem **Komentář pro autora**
2. Vyplň poznámku
3. Návrh přejde zpátky do stavu *Vytvořen* s flagem "vráceno k přepracování"
4. Autor uvidí komentář v detailu návrhu
5. Autor může upravit pole a podat **znovu k schválení**

Lock zůstává.

## Aplikace schváleného návrhu

Po schválení návrhu nastává poslední krok — **aplikace na harmonogram**:

### Manuální aplikace

V detailu schváleného návrhu → tlačítko **Aplikovat**:

1. Confirm dialog ("Tím přepíšeš plán kroku. Pokračovat?")
2. Po potvrzení:
   - **Plán kroku** (`HS0X_DURATION`) se přepíše novou hodnotou
   - **Skutečnost** (`HS0X_DELAY`) se **přepočítá z nové baseline**
   - Návrh přejde do stavu *Aplikován* (terminal)
   - **Krok se odemkne**
3. Audit log: `PROPOSAL_APPLIED`

Permission `proposals.apply`.

### Automatická aplikace

Některé verze aplikace mají **auto-apply** — schválený návrh se
automaticky aplikuje hned po Schválení (jeden klik = oba kroky). Záleží na
konfiguraci.

Default je **manuální** aplikace — leader musí ještě explicitně potvrdit.

## Konsekvence aplikace

Po aplikaci návrhu:

- **Plán** kroku má novou hodnotu
- **Skutečnost** se přepočítá z nové baseline (delay se může změnit)
- **Cumulative delay** dalších kroků se přepočítá
- **Auto-fill** kroků dál běží na nových hodnotách

## Stavy návrhu — recap

| Stav | Co může schvalovatel | Lock kroku |
|---|---|---|
| **Vytvořen** | Schválit / Zamítnout / Vrátit | Ano |
| **Schválen** | Aplikovat / Zamítnout (i schválený lze ještě zamítnout) | Ano |
| **Zamítnut** | (terminal — nic) | Ne |
| **Aplikován** | (terminal — nic) | Ne |
| **Stažen** | (terminal — autor stáhl) | Ne |

## Audit chain

Všechny akce na návrhu se logují s timestampem a aktérem:

```
PROPOSAL_CREATED      jan.novak    24.4. 14:30
PROPOSAL_APPROVED     marie.svoboda 24.4. 16:00
PROPOSAL_APPLIED      marie.svoboda 24.4. 16:05
```

Lze zpětně dohledat **komplet historii** návrhu.

## Vztah ke kalkulaci (PMP)

Pokud má záznam typ **PMP** s kalkulací (`HOT_KALKULACE`), prodloužení
plánu **nemění finanční rozpis** — kalkulace v SD je fixní. Pokud chceš i
změnu kalkulace, musí to udělat dodavatel přímo v ServiceDesku.

## Permissions

| Akce | Klíč |
|---|---|
| Vidět návrhy | `proposals.view` |
| Schválit | `proposals.approve` |
| Zamítnout | `proposals.approve` (stejný klíč jako schválit) |
| Vrátit autorovi | `proposals.return` |
| Aplikovat | `proposals.apply` |

## Související

- [Navrhnout změnu plánu](navrhnout-zmenu-planu.md)
- [Plán vs. skutečnost](../harmonogram/plan-vs-skutecnost.md)
- [Manuální skutečnost](../harmonogram/manualni-skutecnost.md)
