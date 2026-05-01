---
title: Základní ovládání
description: Společné UI vzorce — modaly, taby, ukládání, AJAX patterns, klávesové zkratky.
---

# Základní ovládání

PM Tracker používá jednotné UI vzorce napříč aplikací. Když pochopíš tyhle
základy, ovládání ostatních oblastí ti půjde intuitivně.

## Modální dialogy

Modal je dialogové okno, které překryje hlavní stránku a zaměří fokus na jednu
úlohu. Aplikace ho používá pro:

- **Vytvoření / úpravu krátkých entit** (nový projekt, nové jednání, přidání
  člena týmu)
- **Potvrzení akce** (smazat projekt, smazat záznam)
- **Picker** (výběr osoby z AD, výběr datumu)

### Otevření modalu

Tlačítka, která modal otevírají, mají označení *Nový*, *Upravit*, *Přidat*
nebo *Smazat*. Po kliku se otevře modal s formulářem.

### Zavření modalu

| Způsob | Funguje na |
|---|---|
| Křížek (X) v rohu | Většinou ano |
| Klik mimo modal | Někdy ano (záleží na typu) |
| `Esc` klávesa | Většinou ano |
| Tlačítko *Zrušit* / *Zpět* | Vždy |

**Některé modaly mají dirty-check** — pokud máš neuložené změny, aplikace tě
upozorní před zavřením. Typicky editor záznamu.

## Editor záznamu — modal vs. full-page

Editor záznamu je výjimka — máš dvě varianty:

- **Modal** — rychlejší pro krátké úpravy, hlavní stránka zůstane v pozadí
- **Full-page** — celá stránka, lepší pro dlouhé záznamy, zachovává URL
  (sdílení odkazu, browser history)

V profilu si můžeš nastavit výchozí volbu. Detaily: [Upravit záznam](../projekty/zaznamy/upravit-zaznam.md).

## Taby v detailu

Detail projektu / záznamu / jednání má víc tabů. Aktivní tab si aplikace
**pamatuje při návratu z editace**:

- Otevřeš detail projektu → tab Záznamy je default
- Klikneš na *Tab Harmonogram* → uvidíš harmonogram
- Otevřeš editor záznamu → ulož → vrátí tě zpět na detail projektu, **stále
  na tabu Harmonogram** (ne defaultně na Záznamech)

## Ukládání (AJAX form submit)

Většina formulářů ukládá **přes AJAX** — bez reloadu celé stránky:

1. Klikneš *Uložit*
2. Aplikace pošle data na server
3. Server vrátí výsledek (JSON)
4. UI se aktualizuje:
   - **Úspěch** → zavře modal, refresh listu, zobrazí toast notifikaci
   - **Chyba validace** → zobrazí chyby u polí inline
   - **Server chyba (500)** → toast s textem a Trace-Id

## Validace

- **Klient-side** — při focus-loss pole nebo při submitu se ověří povinná pole,
  formát, rozsah
- **Server-side** — vždy proběhne i když klient validaci přeskočí
- Chyby validace se zobrazí **u konkrétního pole**, ne v jedné horní hlášce

## Lazy-scroll

Dlouhé seznamy (záznamy projektu, projekty v přehledu) používají **lazy-scroll**:
aplikace dotahuje další položky když scrolluješ na konec. Žádné stránkování
("strana 3 z 10").

## Klávesové zkratky

| Zkratka | Akce |
|---|---|
| `Esc` | Zavřít modal (kde povolené) |
| `Ctrl+S` | Uložit aktuální formulář (kde podporované) |
| `Tab` / `Shift+Tab` | Navigace mezi poli |
| `Enter` v textovém poli | Default action formuláře (typicky submit) |

Aplikace nemá komplexní keyboard-driven workflow — primárně se ovládá myší.

## Filtry a persistence

Filtry v přehledech (Skrýt smazané, filtry stavu, …) se ukládají do
`localStorage` prohlížeče. Při dalším přihlášení tě aplikace **vrátí do stavu**
v jakém jsi naposled byl.

Reset filtrů: Profil → uživatelské nastavení → tlačítko *Vymazat uložené filtry*.

## Toasty / notifikace

Aplikace komunikuje výsledky přes **toasty** — krátké notifikace v rohu
obrazovky:

- **Zelený** — úspěch (uloženo, smazáno, …)
- **Červený** — chyba (s textem a Trace-Id pro debug)
- **Žlutý** — varování (např. "data přepsala jiná osoba")

Toast zmizí sám po několika sekundách, lze ho zavřít kliknutím.

## Téma

V profilu lze přepnout **světlé / tmavé téma**. Volba se ukládá do
`localStorage`, není sdílená napříč zařízeními.

## Související

- [Profil → Uživatelské nastavení](../profil/nastaveni-uzivatele.md)
- [Pomoc → Řešení problémů](../pomoc/reseni-problemu.md)
