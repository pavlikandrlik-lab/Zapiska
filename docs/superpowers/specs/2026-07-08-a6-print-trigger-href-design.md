# A6 — Tisk: nová záložka předbíhá dialog volby formátu (gov-button href)

**Datum:** 2026-07-08 · **Stav:** schváleno uživatelem (analýza 2026-07-08; doplněk: zapamatování formátu musí fungovat tam, kde se nabízí)

## Problém a kontext okolí (reprodukováno)

Print triggery v aplikaci (`data-print-trigger`, 4 místa):

| Místo | Element | Chování |
|---|---|---|
| Karta jednání (`_MeetingCard.cshtml`) | nativní `<a class="icon-btn">` | ✅ newTabs=0, dialog čeká |
| Řádek záznamu (`_ZaznamPartial.cshtml:80`) | nativní `<a class="icon-btn">` | ✅ |
| **Tisk projektu** (`Projekty/Detail.cshtml`, tabs-actions) | `pm-button` → `<gov-button href target=_blank>` | ❌ **newTabs=1 + dialog se vykreslí zároveň** |
| **Tisk** na detailu jednání (`Jednani/Detail.cshtml`) | `pm-button` → `<gov-button href target=_blank>` | ❌ dtto |

Root cause: delegovaný click handler ([bootstrap.js:177](../../../PmTracker.Web/wwwroot/js/modules/bootstrap.js)) volá `preventDefault()` + `handlePrintTriggerClick`, což u nativního `<a>` navigaci zruší. `gov-button` s `href` ale renderuje **anchor uvnitř svého Stencil renderu** a aktivuje ho vlastní logikou — vnější `preventDefault` jeho navigaci nezastaví → otevře se tab **a zároveň** náš chooser.

Chooser logika ([ui/print.js](../../../PmTracker.Web/wwwroot/js/modules/ui/print.js)):
- `resolvePrintUrl(trigger, format)` čte `data-print-word-url` / `data-print-pdf-url`, `href` je až **fallback** pro PDF → odstranění `href` nic nerozbije.
- Flow „zapamatovat formát": uložená preference (`pmtracker.print.preferredFormat`) → přímé `openPrintUrl` bez dialogu; checkbox „Zapamatovat" v chooseru ukládá. **Tisk projektu s aktivními filtry** má vlastní scope-chooser („Použít aktuální filtry?") — volba scope se **neukládá** (jednorázová, dle poznámky v UI), uložený formát se respektuje. Toto chování je požadavek (user doplněk) a nesmí se změnit.
- `pm-button` TagHelper (`TagHelpers/PmButtonTagHelper.cs:65`): `href` atribut vydá jen když je nastaven → bez `Href` žádný anchor, gov-button je čisté tlačítko (`native-type="button"`).

## Řešení

1. Z obou rozbitých `pm-button` print triggerů odstranit `href` + `target` + `rel` (data-print-* URL zůstávají). Klik pak obslouží výhradně delegovaný handler: uložený formát → rovnou tab; jinak chooser → tab až po volbě.
2. Nativní `<a>` triggery (karta jednání, řádek záznamu) **beze změny** — href tam slouží jako middle-click/ctrl-click fallback a funguje.
3. Regresní pin: test „print trigger nesmí být gov-button s href".

## Invarianty (kontrola po implementaci)
- Uložená preference formátu → 1 klik = 1 nový tab správného formátu (žádný dialog) — všechna 4 místa.
- Bez preference → dialog, žádný tab; tab až po volbě; checkbox „Zapamatovat" uloží.
- Tisk projektu s aktivními filtry → scope-chooser; volba scope se nikdy neukládá; s uloženým formátem jde rovnou tisk zvoleného scope v uloženém formátu.
- Hover quick-chooser (2 s, jen s uloženou preferencí) funguje dál.

## Dotčené soubory
- `PmTracker.Web/Views/Projekty/Detail.cshtml`, `PmTracker.Web/Views/Jednani/Detail.cshtml` — odebrat `href/target/rel` z print pm-buttonů
- `PmTracker.Tests.Unit` — source-assertion pin

## Testy
- Unit: oba view soubory — print trigger bez `href` na pm-button; data-print-pdf/word-url přítomné.
- Playwright ověření: klik bez preference → newTabs=0 + chooser; volba PDF → newTabs=1; s preferencí → newTabs=1 bez chooseru; projekt s filtrem → scope chooser. (Durable E2E: minimálně první scénář.)

## Mimo scope
Změny print.js logiky (funguje správně); Export controller.
