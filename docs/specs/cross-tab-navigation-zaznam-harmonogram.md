# Překlik záznam ⇄ harmonogram (cross-tab navigace)

**Přidáno:** 2026-06-29 · **Modul:** `wwwroot/js/modules/crossTabNav.js`

## Co to dělá

Na detailu projektu se dá jedním klikem přeskočit mezi kartou záznamu (záložka
**Záznamy**) a jeho lištou v **Harmonogramu**, aniž by se stránka reloadovala. Cílová
záložka se přepne, případně lazy-loadne, sjede se na příslušnou kartu a ta se na 1,6 s
zvýrazní (`.cross-nav-highlight`), aby ji uživatel hned našel.

Dvě tlačítka:

| Směr | Hook | Ikona | Kde |
|------|------|-------|-----|
| Záznam → Harmonogram | `data-goto-schedule="<id>"` | `calendar-date` | karta záznamu (Záznamy) |
| Harmonogram → Záznam | `data-goto-record="<id>"` | `list` | karta harmonogramu (Harmonogram) |

## Komu to je dostupné

Není za žádným zvláštním oprávněním — vidí to **každý, kdo má přístup na příslušnou
záložku projektu** (Záznamy / Harmonogram). Jediné omezení je věcné:

- **Záznam → Harmonogram** se zobrazí jen když má záznam **vyplněnou hodnotu harmonogramu**
  (aspoň jeden krok plán nebo skutečnost — `summary.MaHarmonogramHodnotu`,
  predikát `HarmonogramKrokPredicates.MaVyplnenouHodnotu`). Bez toho by v harmonogramu
  neexistovala cílová karta, takže tlačítko nemá kam odkázat.
- **Harmonogram → Záznam** je na každé kartě harmonogramu (cílový záznam vždy existuje).

## Jak to funguje

`crossTabNav.js` má jeden document-level click listener (přežije lazy-load i refresh
karet). Po kliknutí volá `navigateToTab(tab, cardSelector)`:

1. `setActiveTab(tab)` — přepne aktivní záložku/panel (z `projectTabs.js`),
2. `syncTabQuery(tab)` — upraví `?tab=` v URL (bez reloadu),
3. `await ensureProjectTabLoaded(tab)` — když je cílová záložka jen lazy placeholder,
   dofetchne a vloží její HTML,
4. `scrollToCard(selector)` — cíl (`.schedule-card[data-schedule-record-id]` /
   `.record-card[data-record-id]`) nemusí být hned v DOM (lazy-load), proto retry přes
   `requestAnimationFrame` (až 20×); pak `scrollIntoView({ block: "start" })` + zvýraznění.

### Odsazení scrollu pod sticky header

Header je sticky o výšce `--app-header-h` (~110 px). Aby scroll nezajel „příliš nahoru" a
vršek karty nezmizel za headerem, jsou scroll cíle odsazené: `html { scroll-padding-top }`
a `.record-card`/`.schedule-card { scroll-margin-top }` = `calc(var(--app-header-h) + 4px)`
(navázáno na reálnou výšku headeru, ne pevná konstanta — ta časem zastarala na 92 px).

## Implementační gotcha — label tlačítka „Rozpad" (gov-button)

Tlačítko **Rozpad / Skrýt rozpad** na kartě harmonogramu je `<gov-button>` (Stencil web
komponenta se slot-relokací). **Label se NESMÍ psát přes `textContent` na host během initu.**
gov-button renderuje label do interního `<button class="element">`; zápis `textContent` na
host ten uzel smaže, a když komponenta ještě není dohydratovaná (čerstvě lazy-loadnutý
panel — přesně cross-tab „Zobrazit v harmonogramu" cesta), následný hydratační render
slotovaný text **zdvojí → „RozpadRozpad"**.

Řešení (`schedule/index.js`, `syncScheduleExpandButton`): server renderuje kroky vždy
sbalené s korektním labelem, takže **init label nepíše** (`updateLabel=false`, jen srovná
`aria-expanded`). Label se zapíše až na **skutečný user toggle** (`updateLabel:true`), kdy
je tlačítko dávno dohydratované a zápis je bezpečný. Stejný anti-pattern viz i
`comments.js` (comment-sort toggle).
