---
title: Globální vyhledávání
description: Full-text vyhledávání napříč projekty, záznamy a jednáními.
---

# Globální vyhledávání

PM Tracker má **fulltextové vyhledávání** napříč entitami:

- **Projekty** (jméno, zkratka, popis)
- **Záznamy** (název, popis, externí ticket ID)
- **Jednání** (číslo, místo, body programu)
- **Osoby** (jméno, login, email)

## Jak otevřít

- **Vyhledávací pole na dashboardu** (hlavní stránka po přihlášení)
- **Globální search v hlavičce** aplikace (vždy dostupné)
- Klávesová zkratka (pokud je aktivní v konfiguraci)

## Jak funguje

Aplikace používá **SQL Server Full-Text Search** nebo (pokud je nakonfigurované)
externí Elasticsearch. Detaily jsou mimo scope wiki — pro uživatele stačí vědět:

- **Toleruje překlepy** v určité míře
- **Ignoruje stop words** (a, je, na, …)
- **Vrací výsledky relevantní k dotazu**, ne přesný match

## Filtrace přístupovými právy

Aplikace **filtruje výsledky podle tvých přístupových práv**. Záznam ze
projektu, kam nemáš přístup, **se ti nezobrazí** ani když by textově odpovídal.

To znamená dvě věci:

- Bezpečnost — fulltext nemůže "uniknout" data mezi projekty
- Diagnostika — pokud vyhledávání nenajde co očekáváš, ověř že máš přístup
  k danému projektu

## Tipy pro efektivní hledání

### Hledej pod číslem ticketu

Pokud znáš 6-ciferné `id` ticketu (např. `363139`), zadáním získáš:

- Přímý odkaz na záznam(y) PM Trackeru s touto externí vazbou
- Detail ticketu v ServiceDesku (link)

### Hledej část názvu

Stačí část — aplikace dělá prefix-matching i fulltext. "FIS" najde projekt
"FIS-EIS Modernizace".

### Hledej jméno osoby

Funguje na vlastníky, collaboratory, účastníky jednání, autory komentářů.
Můžeš hledat "Novák" nebo "jan.novak" (login).

### Hledej kódy

Identifikátory jednání ("8201"), zkratky projektů ("FIS-EIS"), kódy subsystémů
("R_EIS").

## Limity

- **Velmi krátký dotaz** (1-2 znaky) může vrátit příliš mnoho výsledků nebo
  být zamítnut
- **Speciální znaky** (`%`, `_`, regex) nejsou podporované
- **Hledání jen v jednom projektu** — globální search neumí scope na projekt;
  pro to použij filtr v záznamech projektu

## Performance

Vyhledávací endpoint má **rate limiter** (typicky 30 dotazů / 10 sekund per
user). Pokud děláš hodně rychlých dotazů (např. live-search při psaní), můžeš
narazit na 429 Too Many Requests.

## Pro koho

Pro každého přihlášeného uživatele. Výsledky filtrované jeho přístupy.

## Související

- [Moje priority](moje-priority.md) — aktivní pracovní pohled (ne hledání)
- [Projekty](../projekty/) — listing s filtry, alternativa k vyhledávání
