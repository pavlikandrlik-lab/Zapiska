# Bugy v zobrazení seznamu jednání

**Status:** Nahlášeno 2026-04-18, k opravě při migraci Views v rámci Fáze 2D (nebo dříve, pokud se Jednání migrují v 2A/2B/2C).

## Bug 1 — Projektový dashboard, záložka Jednání: chybná orientace šipky

**Umístění:** [PmTracker.Web/Views/Projekty/_ProjectMeetingsTab.cshtml](../../PmTracker.Web/Views/Projekty/_ProjectMeetingsTab.cshtml)

**Popis:** U roku, který má `data-meeting-year-state="collapsed"` (zabalený), ikona šipky ukazuje **nahoru** (symbolika "zabalit"), přestože kliknutí provede **rozbalení**. Grafika šipky tedy neodpovídá volané funkci.

**Očekávané chování:**
- `state="collapsed"` → šipka míří **dolů** (symbolika "rozbalit dolů")
- `state="expanded"` → šipka míří **nahoru** (symbolika "zabalit")

**Podezření:** Atribut `name` u `<gov-icon>` nebo CSS transformace je prohozená oproti stavu.

## Bug 2 — Globální záložka Jednání: seznam jednání se nevykresluje správně

**Očekávané chování (globální pohled):**

Největší karty = **projekty**. Uvnitř karty projektu:

- Sekce **aktuální rok** je rozbalená a zobrazuje **právě jeden řádek jednání** (první řádek karet jednání)
- Pokud je **jednání pro aktuální rok dostatek na 2+ řádky** → v pravém horním rohu karty je šipka **dolů** (rozbalit další řádky)
- Pokud je **jednání pro aktuální rok jen na 1 řádek** → šipka **není** (nic k rozbalení)
- **Ostatní roky (předchozí) nejsou viditelné vůbec** — ani jako zabalený proužek
- V pravém horním rohu karty jednání (nad aktuálním rokem) je **separátní šipka pro přepnutí na starší roky**
- Po kliknutí na tuto šipku se zobrazí proužky ostatních roků (collapsed), které jdou jednotlivě rozkliknout

**Očekávané chování (projektová záložka):**

Stejné jako globální, s výjimkou: **všechny roky jsou vidět** pod sebou (jako proužky, collapsed stav) od začátku. Aktuální rok je defaultně rozbalený s prvním řádkem jednání, ostatní proužky jsou collapsed.

**Aktuální stav:** Vykreslování neodpovídá výše popsané logice v obou záložkách (projektová i globální).

## Kdy opravit

- **Nejdřív:** až se v rámci Fáze 2 sahá na tento soubor (pravděpodobně 2D migrace Views, nebo 2C pokud se zapojuje pm-dialog do modálů jednání).
- **Při opravě:** současně opravit obě části (šipka + render logika).
- **Test:** Playwright E2E test scénář s 1 řádkem jednání (bez šipky), 2+ řádky jednání (s šipkou dolů), po rozbalení šipka nahoru; ověřit, že ostatní roky jsou hidden a přepínají se přes vrchní šipku karty.
