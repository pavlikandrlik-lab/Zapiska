---
title: Tickety PMP / PNF / NES
description: Typy ticketů ze ServiceDesku — co znamenají a jak je PM Tracker používá.
---

# Tickety PMP / PNF / NES

ServiceDesk eviduje různé typy ticketů. PM Tracker se zajímá primárně o tři:

| Zkratka | Plný název | Kdy v PM Trackeru |
|---|---|---|
| **NES** | Nesrovnalost | Incident / vada IS. Záznam typu NES s vazbou. Dashboard panel "v prodlení" filtruje NES s `sla_deadline < dnes` |
| **PMP** | Požadavek metodické podpory | Úprava IS s **kalkulací** (hodiny × sazba). Záznam typu PMP s vazbou |
| **PNF** | Požadavek nové funkcionality | Rozšíření IS s kalkulací. Záznam typu PNF s vazbou |

### Subtypy v `HOT_ZAZNAMY.subsystem`

- **NES**: `NES I`, `NES II`, `NES III` (úroveň závažnosti), `Vada A`, `Vada B`, `Vada C`, `Vada D` (typ vady)
- **PMP / PNF**: `PMP`, `PNF I`, `PNF II`, `Pozadavek`

### Termíny

| Typ | Sloupec termínu | Vysvětlení |
|---|---|---|
| **NES** | `sla_deadline` | Termín dle SLA dodavatele. Když `< dnes`, ticket je v prodlení |
| **PMP** | `dat_res_t` | "Termín pro řešitele" — primární termín pro PMP |
| **PNF** | `dat_res_t` | Stejně jako PMP |

> **Pouze NES má vyplněné `sla_deadline`.** Pro PMP/PNF je `sla_deadline`
> v DB systematicky `NULL`.

### Kalkulace (HOT_KALKULACE)

| Typ | `HOT_KALKULACE` | `HOT_KALKULACE_PMP` |
|---|---|---|
| **NES** | Typicky žádná (servis v rámci smlouvy) | — |
| **PMP** | 1 řádek — souhrn (hodiny × sazba × cena) | N řádků — per-zaměstnanec rozpis |
| **PNF** | 1 řádek — souhrn | — |

## Jak se ticket propojí se záznamem PM Trackeru

### V editoru záznamu

1. Otevři *Editor záznamu* → tab **Externí vazby**
2. Vyplň **6-ciferné `id` ticketu** (např. `363139`)
3. Aplikace hned (debounced ~300ms po vyplnění) ověří existenci v `HOT_ZAZNAMY`
4. Pokud ticket **existuje**:
   - Vyplní se **typ záznamu** (NES / PMP / PNF) podle SD
   - Povolí se tlačítko **Otevřít chat**
5. Pokud **neexistuje**:
   - Inline chyba "Ticket #X v HOT_ZAZNAMY neexistuje"
   - Vazba se neuloží

### Po uložení záznamu

Když záznam s vazbou uložíš, na pozadí se spustí **reaktivní harvest** —
sync služba načte vyjádření tohoto ticketu, klasifikuje je predikáty a
připraví je pro chat modal a auto-fill harmonogramu.

## URL na detail ticketu v SD

```
https://servicedesk.fis.acr/Hotline/Ticket/Details/{ticketId}
```

Tlačítko *Otevřít v ServiceDesk* na záznamu vede na tuto URL. Otevře se
v novém okně / tabu.

## Co PM Tracker s ticketem dělá

### 1. Zobrazí odkaz

Externí vazba na záznamu má klikací odkaz na detail ticketu v SD.

### 2. Načte vyjádření do chat modalu

Vyjádření z `HOT_VYJADRENI` se zobrazí jako **chronologický chat** uvnitř
PM Trackeru. Detail: [Chat vyjádření](chat-vyjadreni.md).

### 3. Klasifikace harvest predikáty

Každé vyjádření se klasifikuje podle textu:

- **K3** — Odeslání zadání PMP
- **K4 / K7** — Dodání řešení
- **K6** — Odeslání požadavku
- **K10** — Nasazení / archivace
- **Plán dodání** — explicitní text "termín dodání"
- **None** — nematchuje žádný predikát

Klasifikace pak řídí [auto-fill harmonogramu](auto-fill-skutecnosti.md).

### 4. NES → projektový dashboard

Pokud je ticket typu **NES** a má překročený termín dodání, zobrazí se
v [NES panelu](../../projekty/projektovy-dashboard/nes-v-prodleni/) projektu
(pokud má projekt propojení na IS daného ticketu).

## Důležitá business pravidla

### Ticket bez `id` = mimo scope

Z [memory `feedback_sd_ticket_id_required`](../../slovnicek.md):

> User zadává ticket výhradně přes 6-ciferné `HOT_ZAZNAMY.id`. Záznam bez `id`
> ignorovat (nikdy fallback na 0).

To znamená:

- Pokud ticket v SD nemá vyplněné `id`, PM Tracker ho **nikdy nemapuje**
- Pole *Externí ticket* musí být **6 cifer** — kratší / delší / non-numeric
  selže validace
- Aplikace nikdy nepoužije fallback `id = 0`

### Více vazeb na záznam

Záznam PM Trackeru může mít **N externích vazeb** — typicky jednu primární
(odpovídá typu záznamu) a případně další referenční (pro křížové odkazy).

### Read-only

PM Tracker do SD nezapisuje. Nový ticket se zakládá výhradně v SD UI.
Vyjádření se přidávají v SD UI. PM Tracker je při dalším syncu jen načte.

## Speciální field: `pid`

Ticket v `HOT_ZAZNAMY` má `id` (6-ciferné) **a** `pid` (řetězec, např.
`A111111`). PM Tracker pracuje primárně s `id`, ale `pid` se používá pro:

- Join `HOT_ZAZNAMY` × `HOT_VYJADRENI` — vyjádření jsou vázaná **přes pid**,
  ne přes id
- Zobrazení v UI (jako lidsky čitelný identifier)
- Některé filtry v SD systému

## Související

- [Záznamy → Externí vazba na SD](../../projekty/zaznamy/externi-vazba-sd.md)
- [Chat vyjádření](chat-vyjadreni.md)
- [Auto-fill skutečnosti](auto-fill-skutecnosti.md)
- [Slovníček](../../slovnicek.md)
