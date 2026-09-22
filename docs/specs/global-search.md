# Globální vyhledávání — specifikace

> **Aktuální stav (2026-09-17):** tenhle dokument popisoval zrušenou dvojí
> implementaci (DbSuggest + OpenSearch/FullText nad `dbo.SearchIndex`).
> Vyhledávání bylo přestavěno na záznamocentrické hledání přímo nad ostrými
> tabulkami přes `LIKE`. Indexová vrstva, OpenSearch, embeddings i reindex
> byly odstraněny.

Zdroj pravdy pro nové vyhledávání:

- **Návrh:** [`docs/superpowers/specs/2026-09-17-vyhledavani-prestavba-design.md`](../superpowers/specs/2026-09-17-vyhledavani-prestavba-design.md)
- **Plán implementace:** [`docs/superpowers/plans/2026-09-17-vyhledavani-prestavba.md`](../superpowers/plans/2026-09-17-vyhledavani-prestavba.md)
- **Uživatelská dokumentace:** [`docs/wiki/uzivatelsky-dashboard/globalni-vyhledavani.md`](../wiki/uzivatelsky-dashboard/globalni-vyhledavani.md)
- **Kontext (proč LIKE, ne fulltext):** produkční SQL Server nemá komponentu
  Full-Text Search a mít nebude; viz návrh, sekce collation.

## Ve zkratce

- Jedna služba `RecordSearchService` sestaví EF Core dotaz nad `projektove_zaznamy`
  s `EXISTS` na `vyjadreni` a `zaznam_externi_odkazy`.
- Jednotkou výsledku je vždy **záznam**, i když shoda padla ve vyjádření nebo
  v čísle externího odkazu.
- Autorizace je součástí dotazu (`WHERE projekt_id IN (…)`), ne post-filtr —
  `ProjectVisibilityResolver` zrcadlí `CurrentUserContextViewModel.CanAccessProject`.
- Každý `LIKE` má `COLLATE Latin1_General_CI_AI` (akcent-necitlivé porovnání nad
  databází s `Czech_CI_AS`).
- Pole v hlavičce zůstává gov (`gov-form-search`), dropdown i stránka výsledků
  kreslí aplikace (`app-search-*`) — viz [`docs/known-issues/ds-fis-odchylky.md`](../known-issues/ds-fis-odchylky.md).
