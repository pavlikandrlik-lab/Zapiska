---
title: Nové jednání
description: Jak založit jednání v projektu.
---

# Nové jednání

## Předpoklady

- Permission `meetings.create` na projektu
- Jsi v detailu projektu

## Postup

1. Detail projektu → tab **Jednání** → tlačítko **Nové jednání**
2. Otevře se modal
3. Vyplň povinná pole + volitelná
4. Ulož

## Pole formuláře

| Pole | Povinné | Popis |
|---|---|---|
| **Číslo jednání** | ✓ | Unikátní v rámci projektu (např. `8201`) |
| **Datum** | ✓ | Datum konání |
| **Čas** | — | Čas zahájení |
| **Místo** | — | Volný text (např. "Místnost 105", "Online MS Teams") |
| **Téma / Předmět** | — | Krátký popis o čem jednání bylo |
| **Svolavatel** | — | Default = ty (přihlášený user). Lze změnit AD pickerem |
| **Stav** | — | Default `OTEVRENO`. Možnosti: OTEVRENO, CLOSED |

## Validace

### Unikátnost čísla

Při uložení aplikace ověří `číslo + projekt_id` proti existujícím jednáním.
Pokud již existuje (i archivované), vyhodí chybu:

```
Jednání s číslem 8201 v projektu FIS-EIS již existuje
(vytvořeno 2026-04-15).
```

Vyřešení: zvolit jiné číslo nebo otevřít existující jednání.

### Formát čísla

Aplikace nemá pevný formát čísla (závisí na zvyklostech projektu). Některé
projekty používají:

- `8201` (sekvenční rok-pořadí)
- `2026-04-25` (datum-založený)
- `R_DAN-001` (subsystém-pořadí)

Konvenci si určuje **leader projektu** podle business pravidel.

## Po uložení

- Jednání se vytvoří v `dbo.jednani`
- Audit log: `MEETING_CREATED`
- Aplikace tě **přesměruje na detail nového jednání**, kde můžeš:
  - Přidat účastníky (AD picker)
  - Zapsat body programu
  - Přidat záznamy z jednání
  - Vyjádření a závěry

## Co po uložení **nevytvoří**

- Žádné záznamy ani body programu — to vše vytváříš ručně po vytvoření
  jednání

## Spojení s identifikátorem 8201

Pokud má projekt zapnutý toggle **Používat identifikátor záznamů podle jednání**,
záznamy vytvořené *během* tohoto jednání budou mít v UI / exportech
zobrazený identifikátor podle čísla jednání. Detail:
[Identifikátor jednání](identifikator-jednani.md).

## Pro koho

- **Member** — typicky nemá `meetings.create` (záleží na konfiguraci)
- **Leader** — typicky vytváří jednání
- **Admin** — full

## Související

- [Identifikátor jednání](identifikator-jednani.md)
- [Účastníci](ucastnici.md)
- [Uzavřené jednání](uzavrene-jednani.md) — co s jednáním na konci
