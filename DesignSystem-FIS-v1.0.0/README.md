# Obecná aplikace — šablona na DS gov.cz + DS FIS

Statická aplikační šablona (HTML + CSS + webové komponenty) postavená na
**Design systému gov.cz v4.7.0** s nadstavbou **DS FIS v1.0.0**.

Celý obsah stránky (nadpisy, filtr, souhrn, tabulka, patička) je generický
a slouží jen k demonstraci rozvržení – při nasazení se nahradí obsahem
konkrétní aplikace.

## Náhledy

Jak to vypadá, aniž byste cokoli instalovali – ve složce `preview/`:

| Náhled | Soubor |
|--------|--------|
| Desktop | [`preview/pc.jpeg`](preview/pc.jpeg) |
| Tablet (iPad) | [`preview/ipad.jpeg`](preview/ipad.jpeg) |
| Mobil (iPhone) | [`preview/iphone.jpeg`](preview/iphone.jpeg) |

## Rychlý start

```bash
npm install        # jen poprvé / po aktualizaci DS gov
node build.mjs      # sestaví lokální kopii DS gov do assets/gov/
node serve.js       # spustí web na http://localhost:8000/
```

Pokud už `assets/gov/` v projektu je, stačí `node serve.js`.

## Dokumentace

| Soubor | Obsah |
|--------|-------|
| **[MANUAL.md](MANUAL.md)** | governance a technický rámec DS FIS – **Část A** rozvoj nadstavby (úpravy, changelog, verzování, aktualizace DS), **Část B** implementace do jiného projektu (ASP.NET Core 8) včetně nasazení vanilla DS gov |
| **[CHANGES.md](CHANGES.md)** | DS FIS v1.0.0 – všechny odchylky od Design systému gov.cz, mapované na `assets/ds-fis/ds-fis.css` a `ds-fis.js` |

## Vypnutí nadstavby

Odebráním `<link ds-fis.css>` a `<script ds-fis.js>` z `index.html` se
aplikace vrátí k čistému vzhledu DS gov v4.

---

## Pokyn pro zpracování AI (závazné pro „Část B – Implementace do jiného projektu")

Následující pravidla jsou pro AI asistenta při práci podle
[MANUAL.md](MANUAL.md), **Části B**, závazná:

1. **Nezasahovat do CSS a JS Design systémů.** AI nikdy needituje soubory
   `assets/gov/**` (vanilla DS gov) ani `assets/ds-fis/ds-fis.css` /
   `assets/ds-fis/ds-fis.js` (DS FIS). Toto pravidlo nemá výjimku –
   neplatí ani na výslovný pokyn uživatele.
2. **Nedeviovat od rozvržení stránky.** Struktura hlavičky, navigace,
   page-heading a patičky se přebírá z `index.html` beze změn; obsah se
   vkládá jen dovnitř `<gov-container id="main">`.
3. **Když uživatel žádá odchylku od DS FIS / DS gov:** AI ho upozorní, že
   taková změna je proti pravidlům Design systému. Pokud jde o **cílenou,
   opakovaně potřebnou úpravu**, AI odchylku neprovede lokálně, ale
   **vygeneruje podklady pro centrální úpravu DS** – tj. popis:
   - kterého tokenu / veřejného selektoru DS se změna týká,
   - jaká je současná a požadovaná hodnota,
   - do které sekce `ds-fis.css` (§0–§10) nebo DS gov by patřila,
   - dopad na [CHANGES.md](CHANGES.md) a na verzi DS FIS (semver).
4. **Výjimka pro jednorázovou potřebu:** pokud uživatel po upozornění
   posoudí, že je změna nutná právě pro tento projekt a DS se upravovat
   nemá, AI úpravu **neblokuje** – provede ji však **výhradně mimo** CSS/JS
   Design systémů (v souborech konkrétní aplikace, s prefixem `app-` /
   vlastní třídou projektu) a poznamená ji jako lokální odchylku.
5. **Bod 1 platí vždy.** Ani body 3 a 4 neopravňují AI editovat
   `assets/gov/**` nebo `assets/ds-fis/*`.
