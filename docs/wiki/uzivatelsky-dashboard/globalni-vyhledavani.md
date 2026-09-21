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
- **Speciální znaky** (`%`, `_`) se berou doslova, regex podporovaný není
- **Hledání jen v jednom projektu** — globální search neumí scope na projekt;
  pro to použij filtr v záznamech projektu

## Performance

Vyhledávací endpoint má **rate limiter** (typicky 30 dotazů / 10 sekund per
user). Pokud děláš hodně rychlých dotazů (např. live-search při psaní), můžeš
narazit na 429 Too Many Requests.

## Pro koho

Pro každého přihlášeného uživatele. Výsledky filtrované jeho přístupy.

## Když vyhledávání nevrací nic

Tabulku indexu **nezakládá aplikace** — vzniká databázovým skriptem
`db_upgrade_1_4_4_search_index.sql`, který spouští správce databáze pod účtem
s `db_owner`. Účet, pod kterým aplikace běží, na zakládání tabulek právo nemá.

Když hledání vrací prázdno pro cokoli, podívej se do logu (`logs/pmtracker-*.log`)
— aplikace při startu píše konkrétní důvod:

| Stav v logu | Co to znamená | Co s tím |
|---|---|---|
| `TableMissing` | Chybí tabulka `dbo.SearchIndex` | Spustit migrační skript |
| `Unavailable` | Databáze neodpovídá | Zkontrolovat connection string a práva účtu aplikace |

Skript je idempotentní, jde spustit opakovaně. Po doběhnutí **restartuj aplikaci** —
index se naplní při startu. Stav vrací i endpoint `/Search/Status` (pole `state`
a `stateDetail`), na kterém stojí karta vyhledávání v Profilu s tlačítkem pro ruční
přegenerování, a offline ho ukáže `db_check_applied_upgrades.sql`.

## Jak hledání funguje

Hledá se přes `LIKE` nad indexovou tabulkou, **ne fulltextem** — produkční SQL Server
nemá nainstalovanou komponentu Full-Text Search (rozhodnutí 2026-09-17). Prakticky to
znamená:

- **Na diakritice ani velikosti písmen nezáleží** — „zalohovani“ najde „Zálohování“.
  (Dotaz si vynucuje collation `Czech_CI_AI`; samotná databáze má `Czech_CI_AS`,
  tedy akcent-citlivou.)
- **Hledá se na podřetězce, ne na slovní tvary** — „záznam“ najde „záznamu“
  i „záznamech“, ale „záznamu“ **nenajde** „záznam“. Zkus kratší tvar nebo kořen slova.
- **Víceslovný dotaz vyžaduje všechna slova** (v názvu, klíčových slovech nebo textu),
  ne nutně vedle sebe. Bere se prvních 6 slov.
- **Řazení**: shoda v názvu váží nejvíc, pak klíčová slova, pak text; celá fráze
  v názvu jde nahoru.
- **`%` a `_` se berou doslova** — hledání „50 %“ hledá opravdu „50 %“.

## Související

- [Moje priority](moje-priority.md) — aktivní pracovní pohled (ne hledání)
- [Projekty](../projekty/) — listing s filtry, alternativa k vyhledávání

## Hledání bez diakritiky

Hledá se bez ohledu na diakritiku a velikost písmen — „zalohovani" najde „Zálohování",
„rizeni" najde „Řízení". Platí to i pro písmena s háčkem (`č ř š ž`), která čeština
bere jako samostatná písmena abecedy; kvůli nim se porovnává přes
`COLLATE Latin1_General_CI_AI`, ne přes českou collation.

Jestli to na konkrétní databázi opravdu funguje, ukáže `db_check_search_collation.sql` —
spustí se proti produkci, nic nemění a ve sloupci *Verdikt* má mít samé `OK`.
