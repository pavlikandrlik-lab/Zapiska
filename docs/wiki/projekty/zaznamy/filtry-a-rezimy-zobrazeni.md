---
title: Filtry a režimy zobrazení záznamů
description: Filtrace přehledu — typ, stav, vlastník; persistované volby v profilu.
---

# Filtry a režimy zobrazení

V záložce *Záznamy* můžeš tabulku **filtrovat a řadit** podle různých kritérií.
Aplikace **persistuje volby** v `localStorage` prohlížeče — když se vrátíš,
filtry zůstanou.

## Dostupné filtry

### Typ záznamu

Dropdown s volbami:

- Všechny (default)
- Jen NES
- Jen PMP
- Jen PNF
- Jen úkoly

### Stav

Dropdown stavů z číselníku. Můžeš zvolit konkrétní stav nebo *Všechny*.

### Vlastník

- Všichni (default)
- Já (filtruje na záznamy kde jsi vlastník)
- Konkrétní osoba (dropdown se members týmu)

### Subsystém

Pokud projekt má subsystémy, dropdown s nimi. Volba *Všechny* / *Bez subsystému* /
konkrétní subsystém.

### Skrýt dokončené

Toggle (default zapnutý). Skryje záznamy v koncových stavech (`HOTOVO`,
`ZRUSENO`, `ARCHIV`).

### Skrýt smazané

Toggle (default zapnutý). Skryje soft-deleted záznamy (`is_deleted=1`).

### Vyhledávání

Pole pro **fulltext** v názvu / popisu záznamu. Filtruje tabulku okamžitě
(debounced).

## Řazení

Klik na hlavičku sloupce → řazení vzestupně. Druhý klik → sestupně. Třetí
klik → odebrat řazení (default order).

Default řazení: **podle priority** (urgentnost + termín). Lze změnit:

- Datum vytvoření (nejnovější / nejstarší)
- Termín dodání
- Stav
- Vlastník (alfabeticky)
- Typ

## Persistence

Volby se ukládají v `localStorage` browseru:

```
pmtracker.records.{projektId}.filter.typ        = "PMP"
pmtracker.records.{projektId}.filter.stav       = "OTEVRENO"
pmtracker.records.{projektId}.filter.hide-done  = true
pmtracker.records.{projektId}.sort.column       = "termin"
pmtracker.records.{projektId}.sort.direction    = "asc"
```

Persistence je **per-projekt** — každý projekt má vlastní filtry.

## Reset

V **profilu** → tlačítko *Vymazat uložené filtry*:

- Smaže všechny `pmtracker.records.*` klíče
- Filtry se vrátí na defaulty

Nebo přímo v UI tabulky tlačítko *Resetovat filtry* (pokud existuje) —
smaže jen aktuální projekt.

## Lazy-scroll

Tabulka **nemá stránkování**. Místo toho používá **lazy-scroll**:

- Při načtení stránky se zobrazí ~50 záznamů (default)
- Po scroll na konec se dotahují další položky
- Žádné "strana 3 z 10"

Důvod: lepší UX — uživatel může plynule procházet, nemusí klikat *Další*.

### Limit při velmi velkém projektu

Pokud projekt má **tisíce záznamů**, scroll je pořád pohodlný (díky lazy load),
ale **fulltext** může být pomalejší. Aplikace má rate-limit na search endpoint —
pokud děláš live-search při psaní, narazíš na 429 (Too Many Requests).

## Kombinace filtrů

Filtry se **kombinují AND** — záznam se zobrazí, jen pokud splňuje všechny
nastavené filtry.

Příklad:

- Typ = PMP
- Stav = OTEVRENO
- Vlastník = Já
- Skrýt dokončené = ano

→ Tabulka ukáže jen otevřené PMP záznamy kde jsi vlastník.

## Export filtrované tabulky

Pokud existuje tlačítko **Export tabulky** (CSV / Excel), exportuje se
**aktuálně filtrovaný stav** — ne všechny záznamy.

## Pro koho

Všichni uživatelé. Filtry nemají vlastní permission keys — jsou součástí
zobrazení.

## Související

- [Profil → Uživatelské nastavení](../../profil/nastaveni-uzivatele.md) — reset filtrů
- [Globální vyhledávání](../../uzivatelsky-dashboard/globalni-vyhledavani.md) — alternativa pro hledání napříč projekty
