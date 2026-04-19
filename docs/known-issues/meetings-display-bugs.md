# Bugy v zobrazení seznamu jednání

> **Status 2026-04-19 (po Fázi 2D Task 7):** Pravděpodobně opraveno před nahlášením.
>
> Revizní audit zjistil, že oba reportované bugy měly opravné commity **před** datem nahlášení (2026-04-18):
> - **Bug 1 (šipka):** opravena commitem [`1886862`](https://github.com/) — 2026-04-17 `fix: šipka rozbalovače roku jednání podle skutečné viditelnosti karet`
> - **Bug 2 (render logika):** implementována commitem [`db2560d`](https://github.com/) — 2026-04-17 `feat: rozdílné výchozí chování let jednání mezi aplikační a projektovou záložkou`
>
> Aktuální stav kódu odpovídá [docs/specs/meetings-year-grouping.md](../specs/meetings-year-grouping.md):
> - `CSS site.css:4491-4512` — default šipka ↓, open/preview-bez-skrytých → ↑
> - `JS meetingOverview.js` — toggle collapsed↔open, preview→open při skrytých kartách
> - `_ProjectMeetingsTab.cshtml` — historické roky state="open"
> - `_Jednani/Index.cshtml` — historické roky state="collapsed", aktuální "preview"
> - Unit testy `MeetingsYearGroupingTests.cs` + E2E `MeetingOverviewYearGroupingScenariosTests.cs` pokrývají spec.
>
> **Žádost:** zkontrolovat v aktuálním buildu (hard-refresh CTRL+F5 / Shift+Reload, protože JS může být cachovaný). Pokud bug přetrvává, upřesnit reprodukční kroky a přidat novou sekci níže.

**Historický kontext** (původní bug report, zachován pro dohledání):

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
