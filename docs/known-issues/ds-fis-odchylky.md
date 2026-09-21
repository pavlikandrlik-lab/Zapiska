# Lokální odchylky od Design systému FIS

Evidence podle pravidla 4 v `DesignSystem-FIS-v1.0.0/README.md`: odchylka se
nedělá v souborech Design systému, ale v souborech aplikace s prefixem `app-`,
a poznamená se sem.

## 1. Dropdown vyhledávání (2026-09-17)

**Čeho se týká:** `gov-form-autocomplete` v hlavičce.

**Co DS předepisuje:** vyhledávací pole s komponentou `gov-form-autocomplete`,
která si seznam výsledků vykresluje sama. Plní se přes property `options`
plochými řetězci (`[{ name: "text" }]`).

**Proč se odchylujeme:** komponenta nemá API pro vlastní vykreslení položky.
Zadání vyžaduje dvouřádkovou položku se zkratkou subsystému a číslem jednání
zarovnanými vpravo a se zvýrazněnou shodou — to se do plochého řetězce nevejde.

**Jak je odchylka provedena:** vyhledávací **pole** zůstává gov
(`gov-form-search` + `gov-form-input`). **Seznam výsledků** kreslí aplikace
v `.app-search-dropdown` / `.app-search-item*` (soubory `wwwroot/js/global-search.js`
a `wwwroot/css/site.css`). Soubory `assets/gov/**` ani `assets/ds-fis/*` se needitují.

**Podklad pro případnou centrální úpravu DS:** `gov-form-autocomplete` by
potřebovala slot nebo callback pro vykreslení položky (obdoba `renderOption`),
aby šlo zobrazit víceřádkovou položku s metadaty. Patřilo by to do DS gov
(komponenta), ne do nadstavby DS FIS.
