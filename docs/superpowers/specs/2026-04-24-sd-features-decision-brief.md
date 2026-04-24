# ServiceDesk features — decision brief

> **STATUS 2026-04-24: VŠECHNA ROZHODNUTÍ UZAVŘENA.** Finální sumarizace níže; původní Q&A s odpověďmi zachovány jako history.
> Implementační plány: 5 samostatných plánů v [docs/superpowers/plans/](../plans/) s prefixem `2026-04-24-sd-*`.

## Finální rozhodnutí (shrnutí)

| Q | Výsledek | Pozn. |
|---|---|---|
| A-Q1 | Admin-only (`permission:settings.sd.view`) | zachovat |
| A-Q2 | `/SDConnector/Inspect` podstránka + standardní shell | budoucí `/SDConnector/State` zmíněno, nyní mimo scope |
| A-Q3 | Button → reálný modal | reuse existing chat modal komponenty |
| B-Q1 | Custom `<pm-chat-stepper>` dědící z [gov-stepper](https://designsystem.gov.cz/komponenty/stepper.html) | gov styly + vlastní chování (drag, buffer, chronologie) |
| B-Q2 | Verify + implement dle popisu | bubliny fixní pořadí; horní krok posune spodní, ne opačně; buffer 5 slotů (3 pevné + 2 add-on) |
| C-Q1 | Switch „Auto z SD ⇄ Ručně" per záznam | default Auto; v harmonogramu vpravo u sloupce Skutečnost |
| C-Q2 | Default MAX datum + dropdown výběr pro 2+ kandidátů | chevron hidden při 1 hodnotě |
| C-Q3 | Enum 4 hodnoty (Neznámo/Automat/Manual/Historicka) | Manual jen switch-Ručně+explicit input |
| C-Q4 | Nové vyjádření přebíjí user binding | Re-harvest zahazuje user bindings; manual datum přes switch NENÍ dotčen |
| C-Q5 | Kaskádové volání binding→Skutečnost | bez periodic sync |
| C-Q6 | Matice z [vytezovani-vyjadreni spec §3/3.1](2026-04-21-servicedesk-vytezovani-vyjadreni-design.md) — hardcoded C# |
| G-Q1 | Pořadí: **B → A → D → C → Sprint B** | D přidán (viz U10) |
| G-Q2 | **5 samostatných PR** | (původně G=b=4, +1 za D) |
| **U10** | Externí vazba create **vyžaduje validaci 6-cifer ID proti SD** | samostatný PR (Feature D) |

---

## Implementační plány

- **Plán 1 (B):** [2026-04-24-sd-plan-1-b-stepper-chronologie.md](../plans/2026-04-24-sd-plan-1-b-stepper-chronologie.md) — `<pm-chat-stepper>` custom element, buffer 5 slotů, chronology drag&drop
- **Plán 2 (A):** [2026-04-24-sd-plan-2-a-inspector.md](../plans/2026-04-24-sd-plan-2-a-inspector.md) — `/SDConnector/Inspect` raw vs styled panels + button → modal
- **Plán 3 (D):** [2026-04-24-sd-plan-3-d-externi-vazba-validace.md](../plans/2026-04-24-sd-plan-3-d-externi-vazba-validace.md) — hard constraint: 6-cifer ID ověřené vůči `HOT_ZAZNAMY` při create externí vazby
- **Plán 4 (C):** [2026-04-24-sd-plan-4-c-harmonogram-auto-fill.md](../plans/2026-04-24-sd-plan-4-c-harmonogram-auto-fill.md) — switch Auto/Ručně, SkutecnostZdrojEnum, kaskádové volání, dropdown alternativ
- **Plán 5 (Sprint B):** [2026-04-24-sd-plan-5-sprint-b-is-dashboard.md](../plans/2026-04-24-sd-plan-5-sprint-b-is-dashboard.md) — projekt→IS vazba + NES dashboard aktivace

---

## Historie otázek a odpovědí

> Zachováno pro audit trail. Finální rozhodnutí viz tabulka výše.

Tři features mají docs + částečnou implementaci. Před napsáním implementačního plánu potřebuji tvé rozhodnutí na 13 otázek níže.

---

## Feature A — `/SDConnector` ticket inspector

Stávající `/SDConnector` má jen harvest overview (KPI + tabulka vazeb + re-harvest). Spec ([Task 19](plans/2026-04-21-chat-modal-harvest-core.md#L1887)) ale chtěl navíc **ticket inspector** — zadáš 6-ciferné ID, vedle sebe uvidíš raw data ze SD (HTML vyjádření) a zpracovaný výstup (sanitizovaný plain text + klasifikace na kroky). Admin debug tool: „tenhle ticket harvest rozpoznal špatně, podívám se proč" bez nutnosti procházet celým flow editoru záznamu.

### A-Q1: Authz gate
Aktuálně gate `permission:settings.sd.view` (admin). Spec říká veřejná.
- **(a)** Admin-only (zachovat current state)
- **(b)** Veřejná — každý login user vidí
- **(c)** Admin + VLASTNIK_PROJEKTU
-----A


**Doporučení (a)** — SD data citlivá (jména, částky, dodavatelé), admin tool. Spec z 2026-04-21 byl před authz redesignem.

### A-Q2: Umístění inspectoru
Inspector je nová sekce (input + 2 panely). Stávající overview zůstane. Otázka jen kde inspector umístit.
- **(a)** Rozšířit `Index.cshtml` — 3 sekce stacked: stav spojení, inspector, overview
- **(b)** Samostatná podstránka `/SDConnector/Inspect`
- **(c)** Tabs „Přehled" / „Inspector" na 1 URL

**Doporučení (a)** — 1 URL, vše na dosah, nejjednodušší kód.
-----podstránka, podobně slepá jako je StyleGuide, dostupné pro admina a před odkaz, to stačí, dokonce mi to vyhoduje, pokud je /SDConnector volné, tak sem, jinak ještě tvoje rozšíření /Inspect, v budoucnu se třeba rozhodnu pro statistiky a nějaké system info na /SDConnector/State... ale zatím ne a nikam to nepiš

### A-Q3: Modal preview v pravém panelu
Spec navrhuje dashed 1200×800 rámeček simulující chat modal pro dev ověření rozměrů.
- **(a)** Simulovaný rámeček dle spec (dashed 1200×800)
- **(b)** Vynechat — jen karty bez rámečku
- **(c)** Button „Otevřít v reálném modalu" — klik spustí opravdový chat modal

**Doporučení (c)** — reuse existing modal kódu, zero duplicate CSS, admin vidí reálný produkční zážitek.
-----chtěl jsem původně modal který není modal ale je vytisknutý přímo na stránce pro lepší porovnávatelsnost raw a stylizaci do vyjádření, ale modal bude jednodušší, dej to do modalu... 
---

## Feature B — vyjádření + stepper napojení

Chat modal má timeline bublin vlevo + stepper kroků vpravo + drag & drop. Funguje ze ~90%. Dvě drobnosti nejasné.

### B-Q1: `gov-stepper` vs `pm-chat-step`
Spec říkala `gov-stepper` (gov komponenta), realita má custom `pm-chat-step` CSS. Vizuálně stejné, rozdíl je v maintenance.
- **(a)** Nechat `pm-chat-step` (funguje)
- **(b)** Refactor na `gov-stepper` (pokud existuje v gov kit v4)
- **(c)** Ověřit gov kit v4 — pak rozhodnout

**Doporučení (c)** — 15 min check Figma kitu, evidence-based rozhodnutí.
-----asi C, ale chápu, že gov komponenty u sebe mají i javascipt který definuje chování a tady to chování je výrazně jiné, asi jsem ok s variant vytvořit vlastní objekt který bude dědit vše od gov stepper komponenty sle nevyhovující věci si upraví, tím se zajistí že v budocnu při případné změně GOV stylu (globálně) se update stylu se provede ale chování zůstane.... koncepční a chytré řešení... 

### B-Q2: Chronologie drag & drop
Spec §7.4: „bublina nemůže drop nad předchozí krok". Možná implementováno v JS bundlu, možná ne — nevidím v Razor view.
- **(a)** Verify existing JS první, pak rozhodnout
- **(b)** Reimplement from scratch
- **(c)** Vynechat validaci — binding rebalance to stejně sesbírá

**Doporučení (a)** — grep před kódem. Pokud existuje → nic, jinak implementovat.
-----a, popis chování stepperu bych chtěl aby bubliny nemohli měnit pořadí, to je základ, výjimkou jsou nepovinné kroky které může uživatel s příslušným právem přidat, bylo popsáno a někde v dokumentaci musí být, a sponější krok nepřesune horní krok, pouze horní krok může posunout spodní krok, dále kroky mají mít úplně dole o několik míst více než je délka vyjádření aby tam mohl být buffer nepřidělených kroků a místo kam odložit kroky ke kerý zatím neexistuje vyjádření a nejdou tedy na nic zatím napojit, poslední vyjádření ale může mít jeden z kroků a ostatní další kroky by se neměly v tu chvíli kam vejít, kdyby prostor pro kroky nebyl delší než prostor vyjádření.... 
---

## Feature C — harmonogram auto-fill z ticketů ⭐ největší

Projektový záznam má harmonogram (Plán | Skutečnost). Dnes Skutečnost ručně. **C doplní auto-fill**: datum K3/K4/K10 vyjádření z ticketu → `SkutecnostDatum` příslušného kroku. DB fields existují (`DatumObjednani/Dodani/Prevzeti`, tabulka `zaznam_harmonogram_vyjadreni_vazba`), ale pipeline → harmonogram **vůbec neexistuje**.

### C-Q1: Záznam bez vazby na ticket (spec H2)
Některé záznamy nejsou napojené na SD (interní práce). Jak zobrazit jejich Skutečnost?
- **(a)** Badge „off-ticket" + ruční edit dovolen
- **(b)** Jen ruční edit, žádný badge (status quo)
- **(c)** Force vazbu — bez ticketu nelze uložit
- **(d)** Skrýt Skutečnost sloupec

**Doporučení (a)** — transparentní, flexibilní, track zdroje přes enum.

-----Netuším jak to myslím že některé záznamy nejsou napojené na SD, jaké záznamy. Záznamy projektové v aplikaci jsou jaké jsou, ty napojení nemají. Napojení mají záznamy externí, které jsou na záznam projektový napojeny. Záznam externí je definován a jeho vytvoření je pomíněné zadáním šestimístného id které se musí najít v SD, jinak externí záznam nemůže být založen a nemůže existovat, nebude uložen do db. Toto je podmínka která možná chybí a je potřeba to dodělat.
Pokud to myslíš tak že Projektováý záznam nemusí mít vazbu na externí záznam, tak to ano, to může nastat a může to být běžné. V takovém případě skutečnost bude nulová a zatím než se management rozhodne že to bude jinak bude taková skutečnost svítit jako nulová, resp nebude skutečnost existovat. Nevím jak by bylo pracné rozšíření že by se skutečnost u projektových záznamů které nemají a nebudou mít žádné napojení na PMP ani PNF zadávala ručně, to by jedině co mě napadá muselo být ošetřené někde u skutečnost harmonogramu kde by byl switch získávat automaticky nebo zadat ručně, výchozí volba by byla "Automaticky z SD" a možnost přepnout by byla na "Ručně". Toto se mi i jeví jako docela dobrý nápad a mohl by se klidně zapracovat když se to bude stejně předělávat. Takže ano, toto rozšíření přes switch doplň. tlačítko umísti někam vpravo nahoru, to je místo kam se snažím umitťovat další možnost okna a objetů v něm takže to bude koncepční. Přesnou polohu vymysli ve spolupráci se skillem /frontend-design

### C-Q2: Víc ticketů na 1 záznam (spec H3)
Záznam má 2+ externí vazby. Které datum se propíše?
- **(a)** MIN — nejranější
- **(b)** MAX — nejpozdější
- **(c)** First-Active — ticket s nejnižším Id
- **(d)** Last-updated — nejnovější harvest

**Doporučení (a)** — MIN = „kdy se to konečně stalo", intuitivní sémantika.
-----výchozí stav bude že se pro píše to nejpozdější to znamená max, uživatel s příslušným oprávněním editovat harmonogram bude mít možnost vybrat z automaticky nalezených variant v případě že bude switch přepnutý na automatické získávání i jiný termín než tento výchozí nastavení. Výběr bude pomocí dropdown komponenty.

### C-Q3: `SkutecnostZdrojEnum` hodnoty
Nová DB column pro audit, odkud datum přišlo. Určuje UI badge + sync logiku.
- **(a)** 4 hodnoty: Neznámo / Automat / Manual / Historicka (migrovaná pre-auto-fill data)
- **(b)** Víc — uveď které (např. AutoOverridden, BulkImport)
- **(c)** Jen 3: Neznámo / Automat / Manual — Historicka → fallback na Manual

**Doporučení (a)** — Historicka cenný audit po roce.
-----A

### C-Q4: Override — user přepíše auto-filled hodnotu
Scénář: krok 3 = 15.3. (Zdroj=Automat). User přepíše na 16.3. Další sync tick — co?
- **(a)** Zamknout — Zdroj flipne na Manual, sync tuto řádku skip (respect user intent)
- **(b)** Přepsat zpět s warning — user data ztracená
- **(c)** Konflikt prompt „toto je auto, chceš zamknout?"

**Doporučení (a)** — user change drží. Když chce refresh, klikne „Re-harvest" (existuje).
-----Pokud se bavíme o oblasti vyjádření a párování stepper na vyjádření z čehož by se získávaly termíny, tak v automatickém režimu se zkouší pouze nová vyjádření jestli sedí na nějaký krok a pokud ano tak se ten krok použije pro nové vyjádření a i když nastavil uživatel krok k jinému předchozímu vyjádření tak má smůlu protože nové vyjádření to přebíjí. V případě že ale uživatel stiskne tlačítko Re-harvest, tak se všechny uživatelská přiřazení zahazují.  Tento systém by měl zamezit konfliktu protože jednou vytěžené vyjádření se už znovu nevytěžuje tudíž by němělo dojít k problému automati přepisuje uživatele.

### C-Q5: Kdy auto-fill běží
- **(a)** Periodický sync job (přes `SyncHostedServiceBase`, 30 min)
- **(b)** On-demand při save bindingu — immediate feedback po drag&drop
- **(c)** Oba — immediate + periodický fallback
- **(d)** Jen při Re-harvest batch (minimal — user musí klikat)

**Doporučení (c)** — instantní feedback (b) + chycení background events (a). Na stávající sync infra stavět levné.
-----Auto-fill čeho? Vyjáření? Potom přes komponentů nebo ručně uživatelem a akcemi které vyvolají auto-fill. Termínů do harmonogramu? pokaždé když je změna, kaskádové volání, jsou to interní data. Pokud jsme se nepochopili tak dej dotaz znovu s více informacemi.

### C-Q6: Krok ↔ datum matice — **potřebuji ověřit** ⚠️

Moje chápání ze [spec vyteovani-vyjadreni-design §1.3-5](2026-04-21-servicedesk-vytezovani-vyjadreni-design.md):

| Typ | Krok 1 „Objednáno" | Krok 3 „Dodáno" | Krok 5 „Archivováno" |
|---|---|---|---|
| **NES** | — (NES nemá objednávku) | K4/K7 (dodání) | K10 (archiv) |
| **PMP** | K3 (odeslání dodavateli) | K4/K7 | K10 |
| **PNF** | K6 (kalkulace akceptována) | K4/K7 | K10 |

- **(a)** Matice OK, hardcoded C# switch (build+deploy při změně)
- **(b)** Matice OK, konfigurovatelné v appsettings (JSON)
- **(c)** **Matice špatně — oprav níže:**
  ```
  Pokud (c), piš opravu:
  - NES krok X má brát datum Y ne Z
  - PMP krok ...
  ```

**Doporučení (a)** za předpokladu že matice je správně. Bez tvé validace implementace mapuje blbě.
-----A, matice je v bodě 3 i 3.1 napsaná dobře, tak neser se vezmi to správně jak je, jsou tak popsané kroky i jaký krok patří k čemu, k jakému typu záznamu, ten blábol co jsi sem vypdal jako diplmomaticky přehlédnu

---

## Globální

### G-Q1: Pořadí sprintů
Odložené kusy: Feature A (2-3 h), B (1-2 h), C (1-2 dny), Sprint B z dřívější diskuse = projekt→IS vazba + NES dashboard (1 den).
- **(1)** B → A → C → Sprint B — doporučené
- **(2)** Sprint B → A → B → C — user value first
- **(3)** A → B → Sprint B → C — dev tools first

**Doporučení (1)** — rychlé winy napřed, C dodá data pro Sprint B dashboard. Sprint B jako finále.
-----netuším, třeba, jestli to bude blbě nakopi ti prdel

### G-Q2: PR strategie
- **(a)** 1 velký PR — všechno dohromady, atomický rollback
- **(b)** 4 samostatné PR — granulární review + rollback
- **(c)** 2 skupiny — (B+A) + (C+Sprint B)

**Doporučení (b)** — každý testovatelný samostatně.
-----B

---

## Formát odpovědi

```
A-Q1: _   A-Q2: _   A-Q3: _
B-Q1: _   B-Q2: _
C-Q1: _   C-Q2: _   C-Q3: _   C-Q4: _   C-Q5: _   C-Q6: _ (+ opravy matice pokud c)
G-Q1: _   G-Q2: _

Poznámky: …
```

Po odpovědi napíšu implementační plán → ty schválíš → pak se teprve programuje.
