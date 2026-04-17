# Specifikace — výběr času začátku jednání (meeting-time-picker)

Dokumentuje povolený rozsah časů v modalu „Nové jednání" a „Upravit jednání".

---

## Požadované chování

Při zakládání a úpravě jednání (modal `NewMeetingModal.cshtml`) uživatel vybírá
**čas začátku jednání** přes custom time picker. Nabídka času musí být omezena
na pracovní dobu.

### Povolený rozsah

| Parametr | Hodnota |
| --- | --- |
| **První nabízený čas** | `06:00` |
| **Poslední nabízený čas** | `22:45` |
| **Krok** | 15 minut |

To znamená, že nabízené hodnoty jsou: `06:00, 06:15, 06:30, …, 22:30, 22:45`.

### Důvod

Jednání se konají v pracovní době. Neomezený rozsah 00:00–23:45 zahlcoval
dropdown nerelevantními hodnotami a uživatelé se stížností, že vidí noční časy.

---

## Implementace

### JS custom picker

Soubory:

- Zdroj: [PmTracker.Web/wwwroot/js/modules/pickers.js](../../PmTracker.Web/wwwroot/js/modules/pickers.js)
- **Bundle** (production loaded): [PmTracker.Web/wwwroot/js/site.bundle.js](../../PmTracker.Web/wwwroot/js/site.bundle.js)

> ⚠️ **POZOR**: Aplikace načítá `site.bundle.js`, ne zdrojové moduly. Při úpravě
> logiky v modulu (`modules/pickers.js`) musíš **vždy** synchronizovat bundle
> (`site.bundle.js`), jinak se změna nepromítne do aplikace.

Renderovací smyčka generující grid časových voleb:

```js
for (let hour = 6; hour <= 22; hour += 1) {
    for (let minute = 0; minute < 60; minute += 15) {
        // … render button with timeText "HH:mm"
    }
}
```

### Kde se picker používá

| Místo | Soubor | Pole |
| --- | --- | --- |
| Nové jednání | [Views/Projekty/NewMeetingModal.cshtml](../../PmTracker.Web/Views/Projekty/NewMeetingModal.cshtml) | `CasZacatek` |
| Upravit jednání | stejný modal (reuse) | `CasZacatek` |

Picker se aktivuje na libovolném `[data-app-time-field]` elementu. Kontrakt:

```html
<div class="app-time-field" data-app-time-field>
    <input data-app-time-display readonly />
    <input type="hidden" data-app-time-value name="…" />
    <button data-app-time-open>…</button>
    <div data-app-time-panel>
        <div data-app-time-grid role="listbox"></div>
    </div>
</div>
```

---

## Edge cases

1. **Uložená hodnota mimo rozsah** — pokud má existující jednání uloženo např.
   `05:00`, input `[data-app-time-display]` hodnotu zobrazí, ale v gridu ji
   uživatel nenajde. Po otevření pickeru a výběru jiného času se zapíše
   nová hodnota v rozsahu 06:00–22:45.
2. **Manuální editace** display inputu je `readonly`, uživatel nemůže zadat
   čas mimo rozsah.
3. **Serverová validace** `SaveMeetingCommand.CasZacatek` přijímá libovolný
   `TimeOnly`. Pokud chceš tvrdě vynutit rozsah i na serveru, přidej
   validační atribut — aktuálně je to pouze UX omezení.

---

## Pravidla pro úpravy

1. **Změnu rozsahu proveď na obou místech** — v `pickers.js` (zdroj) i v
   `site.bundle.js` (bundle). Pokud je v projektu build krok, raději ho spusť.
2. **Konzistence se všemi time pickery aplikace** — pokud přibude další
   (např. čas konce jednání), rozsah nechej stejný (6–22), pokud není důvod jinak.
3. **Dokumentuj důvod změny** zde.

---

## Pokrytí testy

- [PmTracker.Tests.Unit/Meetings/MeetingTimePickerRangeTests.cs](../../PmTracker.Tests.Unit/Meetings/MeetingTimePickerRangeTests.cs) —
  kontroluje, že zdroj i bundle používají rozsah 6–22.
