# Spec: ServiceDesk — Vytěžování vyjádření + chat modal + propojení s harmonogramem

**Stav:** návrh (čeká na schválení)
**Datum:** 2026-04-21
**Autor:** Ing. Pavel Andrlík + Claude (brainstorming)
**Scope:** Use-case A z plánované ServiceDesk integrace (vytěžování vyjádření). Rozšiřuje stávající architekturu návrhů (`ZaznamNavrhEntity`) a harmonogramu.
**Navazuje na:**
- [2026-04-20-servicedesk-integrace-vyzvy-design.md](2026-04-20-servicedesk-integrace-vyzvy-design.md) — Fáze 1 ServiceDesk sdílí read-only DbContext.
- [record-proposal-editor.md](../../specs/record-proposal-editor.md) — `ZaznamNavrhEntity` + `payload_json` workflow.

---

## 1. Kontext a cíl

### 1.1 Problém
Harmonogramové kroky projektových záznamů (PMP, PNF, NES) dnes uživatel vyplňuje ručně — počet dní „plán" a „skutečnost (odchylka)" per krok, bez vazby na realitu v ServiceDesku. To vede k:
- nekonsistentním termínům (uživatelské odhady místo doložitelných faktů),
- administrativní zátěži (manuální přepisování termínů),
- slepé stopě — kde se termín vzal, není dohledatelné.

### 1.2 Cíl
1. **Automatizovat vyplnění skutečnosti** harmonogramu podle textů v `HOT_VYJADRENI` (ServiceDesk).
2. **Umožnit ruční korekci** automatu přes chat-like modal s drag & drop stepperu na vyjádření.
3. **Zaznamenat doložitelnou vazbu** mezi kroky harmonogramu a konkrétními vyjádřeními (kdo, kdy, text — auditovatelné).
4. **Smazat krok „Fakturace" (#11)** úplně z aplikace i DB.
5. **Synchronizovat datumy externí vazby** (Datum objednání / dodání / převzetí) automaticky ze ServiceDesku.

### 1.3 Princip vytěžování
Automat čte `HOT_VYJADRENI.popis` a textovými predikáty detekuje specifické fráze, které vygeneroval ServiceDesk při určitých akcích (např. „Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována."). Na datum toho vyjádření se přichytí konkrétní krok harmonogramu.

---

## 2. Rozsah

### ✅ V scope
- 10 harmonogramových kroků (1–10), fakturace (#11) pryč.
- Nové tlačítko **„Vyjádření a termíny"** (ikona chat bubliny) u každé externí vazby v editoru záznamu.
- **Modal** s dvousloupcovým layoutem 80/20 (chat + stepper).
- **Automat** vytěžování vyjádření — trigger při vytvoření externí vazby a při příchodu nového vyjádření.
- **Re-harvest** tlačítko (ruční spuštění).
- **Drag & drop** přiřazení bublin ke krokům + dropdown „Přidat ruční krok".
- Sloupce `DatumObjednani`, `DatumDodani`, `DatumPrevzeti` na kartě externí vazby — **automaticky synchronizované**.
- DB cleanup fakturace.
- Rozšíření `CreateRecordProposalPayload` a `SchedulePlanProposalPayload` o ruční datumy pro kroky 2/5/8/9 + vazby vyjádření.
- Přejmenování tlačítka „Odebrat vazbu" na ikonu 🗑.
- Přejmenování labelu switche na **„Výzva"**.
- Kompletně **nový layout karty externí vazby**: Číslo → Typ (read-only) → Cena → Výzva switch → 🗑 + 💬.

### ❌ Mimo scope
- Use-case C (Výzvy + Word export) — samostatný spec `2026-04-20-servicedesk-integrace-vyzvy-design.md`.
- Use-case B (Dashboard prodlení NES/PMP/PNF) — budoucí spec.
- Mobilní layout (< 768 px).
- Editace textu vyjádření (ServiceDesk zůstane read-only).
- Přímý zápis do `HOT_*` tabulek.
- Rule engine / ML klasifikace vyjádření — pro první iteraci **jen textové LIKE predikáty**.

---

## 3. Harmonogramové kroky — kanonický katalog

Kompletní katalog 10 kroků (index z `HarmonogramTypEntity.KrokPoradi`). Fakturace (#11) **smazaná**.

| # | Název kroku | NES | PMP | PNF | Zdroj datumu (automat) |
|---|---|:-:|:-:|:-:|---|
| 1 | příprava zadání dodavateli | ✓ | ✓ | ✓ | datum založení záznamu (`HOT_ZAZNAMY.datum_zalozeni` nebo ekvivalent) |
| 2 | konzultace termínů s dodavatelem | — | dropdown | — | **ruční** |
| 3 | odeslání zadání dodavateli | — | ✓ | — | vyjádření obsahuje „Záznam byl založen a předán dodavateli k řešení pod značkou:" |
| 4 | dodání návrhu řešení | — | ✓ | — | poslední vyjádření obsahuje „Dodavatel přidal řešení" |
| 5 | vypořádání připomínek | — | dropdown | — | **ruční** |
| 6 | odeslání požadavku na výrobu | — | — | ✓ | vyjádření obsahuje „Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována." |
| 7 | dodání funkcionality dodavatelem | — | — | ✓ | poslední vyjádření obsahuje „Dodavatel přidal řešení" |
| 8 | připomínkování | — | — | dropdown | **ruční** |
| 9 | testování | — | — | dropdown | **ruční** |
| 10 | nasazení do provozu | — | — | ✓ | vyjádření obsahuje „Záznam byl převeden do archivu." |
| ~~11~~ | ~~fakturace~~ | — | — | — | **SMAZÁNO z aplikace + DB (skript `db_upgrade_1_1_9_fakturace_cleanup.sql`)** |

### 3.1 Aktivní kroky per typ tiketu

- **NES:** `{1}` — jediný povinný, dropdown prázdný.
- **PMP:** `{1, 3, 4}` automaticky; dropdown `{2, 5}`. **PMP nemá krok 10** (PMP se nedává do provozu, končí dodáním návrhu).
- **PNF:** `{1, 6, 7, 10}` automaticky; dropdown `{8, 9}`.

### 3.2 Datum archivace pro PMP

PMP nemá krok 10, ale přesto má datum archivace ServiceDesku významnou vazbu — zapíše se **automaticky** do `ZaznamExterniOdkazEntity.DatumPrevzeti`. Tzn. krok 10 se na PMP nevyskytuje, ale datum převzetí na externí vazbě se naplní.

### 3.3 Chronologie kroků

Přiřazení bublin ke krokům **musí respektovat chronologický pořádek**. Pořadí kroků 1 → N musí chronologicky vzestupně odpovídat datumům bublin, ke kterým jsou připnuty. Porušení (např. krok 2 na novějším datumu než krok 3) je v UI blokováno — pokus o drop dostane toast „Porušení chronologie: krok 3 je přiřazen k dřívější bublině."

---

## 4. Datový model

### 4.1 Existující sloupce — **netřeba upravovat**

**`ZaznamNavrhEntity` (`zaznam_navrhy`):**
- `payload_json nvarchar(max)` — **už existuje**. Jen se rozšíří deserializační model o nová pole.

**`ZaznamExterniOdkazEntity` (`zaznam_externi_odkazy`):**
- `DatumObjednani datetime2?` — **už existuje**.
- `PlanDodani datetime2?` — **už existuje**.
- `DatumDodani datetime2?` — **už existuje**.
- `DatumPrevzeti datetime2?` — **už existuje**.
- `Cislo varchar` — 6místný identifikátor tiketu v ServiceDesku (= `HOT_ZAZNAMY.id`).

### 4.2 Nové sloupce na `ZaznamExterniOdkazEntity`

| Sloupec | Typ | Účel |
|---|---|---|
| `LastHarvestedAt` | `datetime2?` | Kdy naposledy proběhl auto-harvest vyjádření pro tuto vazbu. UI ukazuje „Vytěženo před 2 dny". |

### 4.3 Nová tabulka `zaznam_harmonogram_vyjadreni_vazba`

```sql
CREATE TABLE dbo.zaznam_harmonogram_vyjadreni_vazba
(
  id                  INT IDENTITY PRIMARY KEY,
  zaznam_id           INT NOT NULL
    FOREIGN KEY REFERENCES dbo.projektove_zaznamy(id) ON DELETE CASCADE,
  krok_key            UNIQUEIDENTIFIER NOT NULL,
  externi_odkaz_id    INT NOT NULL
    FOREIGN KEY REFERENCES dbo.zaznam_externi_odkazy(id) ON DELETE CASCADE,
  hot_vyjadreni_id    VARCHAR(50) NOT NULL,
  datum_vyjadreni     DATETIME2 NOT NULL,
  source              TINYINT NOT NULL,  -- 1=Auto 2=Manual
  stav                TINYINT NOT NULL,  -- 1=Active 2=Superseded 3=Deleted
  created_at          DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
  created_by_osoba_id INT NULL
    FOREIGN KEY REFERENCES dbo.osoby(id),
  deleted_at          DATETIME2 NULL,
  deleted_by_osoba_id INT NULL
    FOREIGN KEY REFERENCES dbo.osoby(id)
);

CREATE INDEX ix_zhvv_zaznam_krok_stav
  ON dbo.zaznam_harmonogram_vyjadreni_vazba(zaznam_id, krok_key, stav);
CREATE INDEX ix_zhvv_externi_odkaz
  ON dbo.zaznam_harmonogram_vyjadreni_vazba(externi_odkaz_id);
```

**Pole `krok_key`** = `HarmonogramTypEntity.KrokKey` (GUID; pár DURATION+DELAY má stejný KrokKey).

**Kardinalita 1:1 (Active):** v libovolný moment max 1 řádek s `stav=1` (Active) pro kombinaci `(zaznam_id, krok_key)`. Nové přiřazení supersedne starší (`stav=2`). Nemažeme, držíme historii pro audit.

### 4.4 Skutečnost kroku — **nová tabulka se NEDĚLÁ**

Skutečnost zůstává uložená v `ZaznamHarmonogramHodnotaEntity` jako počet dnů odchylky (`HS0X_DELAY.HodnotaInt`). Konverze datum → odchylka proběhne při zápisu vazby:

```
odchylka_dnu = datum_vyjadreni - plan_end_date(krok)
```

`plan_end_date(krok)` se spočítá stávajícím `BuildHarmonogramVypocetCore(datum_zalozeni, typy, hodnoty)` z `HarmonogramService`.

**Důvod:** stávající UI harmonogramu (`_ScheduleBlock.cshtml`, Gantt, `_EditZaznamSchedulePanel.cshtml`) čte `HS0X_DELAY` hodnoty jako dnešek. Ponecháním modelu nerozbijeme stávající zobrazení. Nová tabulka `zaznam_harmonogram_vyjadreni_vazba` slouží jen pro **vazbu na zdrojové vyjádření** (audit + re-harvest + tie-break).

### 4.5 Rozšíření `CreateRecordProposalPayload`

Stávající: `ExterniVazby[]`, `HarmonogramHodnoty[]`.

**Nová pole:**
- `HarmonogramVazby[]: Array<{ krokKey, externiOdkazIndex, hotVyjadreniId, datumVyjadreni }>` — vazby vyjádření na kroky, které se při schválení propíšou do nové tabulky.
- `ManualActualKroky[]: Array<{ krokKey, absolutniDatum }>` — ruční datumy pro kroky 2/5/8/9 (absolutní datum, konverze na odchylku proběhne při schválení).
- `ExterniVazby[i].HotVytezenoAt: datetime2?` — kdy byl tiket synchronizován. Při schválení se použije už vytěžená hodnota (nedotazujeme znova), jen se spustí lehký check na nová vyjádření, která mezitím přibyla.

### 4.6 Rozšíření `SchedulePlanProposalPayload`

Stávající: `PlannedHarmonogramHodnoty[]`, `ActualHarmonogramHodnoty[]`.

**Nové pole:**
- `ManualActualKroky[]: Array<{ krokKey, absolutniDatum }>` — stejné jako schéma 3.

**Beze změny:** externí vazby se ve schématu 2 **neupravují** (jde o úpravu harmonogramu; externí vazby jsou doménou schéma 1 přímo nebo schéma 3 přes nový záznam).

---

## 5. Tři workflow schémata

### 5.1 Schéma 1 — Přímá úprava

**Platí pro uživatele s klíčem `records.edit`.**

| Akce | Persistence |
|---|---|
| Sync karty externí vazby (zadání 6místného čísla → ServiceDesk lookup) | Přímý zápis do `ZaznamExterniOdkazEntity` |
| Drag & drop bubliny ve chat modalu | Přímý zápis do `zaznam_harmonogram_vyjadreni_vazba` + přepočet `HS0X_DELAY` hodnoty |
| Ruční datum pro krok 2/5/8/9 ve chat modalu | Přímý zápis `HS0X_DELAY.HodnotaInt` (bez vazby na vyjádření) |
| Re-harvest | Přímá akce (audit logovaná) |
| Smazání externí vazby | Přímý DELETE (CASCADE smaže i vazby na vyjádření) |

**Žádná schvalovačka, žádný návrh.** Uživatel s `records.edit` je autoritou.

### 5.2 Schéma 2 — Úprava harmonogramu přes návrh

**Platí pro uživatele s klíčem `records.schedule.edit` (vedoucí subsystému / zástupce), kteří nemají `records.edit`.**

- Jde přes existující `SCHEDULE_PLAN_CHANGE` workflow (`NavrhyController.SubmitScheduleProposal`).
- Rozšíření: payload obsahuje `ManualActualKroky[]` pro ruční kroky 2/5/8/9.
- **Externí vazby se NEDOTÝKAJÍ** — v tomto workflow nejsou součástí.
- Schválení provádí PM/ADM projektu přes existující `ApproveProposal`. `ApplyApprovedScheduleProposalAsync` rozšířené o zpracování `ManualActualKroky[]`.
- **Chat modal pro uživatele ve schéma 2 je read-only** — vidí přiřazení, ale nemůže je měnit bez návrhu.

### 5.3 Schéma 3 — Nový záznam přes návrh

**Platí pro vedoucí subsystému (subsystem lead), kteří zakládají nový záznam.**

- Jde přes existující `CREATE_RECORD` workflow.
- Rozšíření: payload obsahuje `HarmonogramVazby[]` a `ManualActualKroky[]`.
- Vedoucí subsystému v editoru návrhu:
  1. Přidá externí vazbu (zadá 6místné číslo → auto-sync vyplní Typ + datumy).
  2. V chat modalu přiřadí bubliny ke krokům — tyto vazby se uloží do payload JSON.
  3. Uloží návrh.
- PM/ADM při schválení:
  - `SaveRecordAsync` vytvoří záznam se záznamy v `ZaznamExterniOdkazEntity` a `ZaznamHarmonogramHodnotaEntity`.
  - Dodatečně vytvoří řádky v `zaznam_harmonogram_vyjadreni_vazba` podle payloadu.
  - **Check** na nová vyjádření v ServiceDesku, která přibyla mezi submitem a schválením — pokud jsou relevantní, zobrazí se PM/ADM jako varování s možností je dodat do záznamu před finálním schválením.

### 5.4 Přehled — kdo do čeho zapisuje

| Role | Schéma 1 | Schéma 2 | Schéma 3 |
|---|:-:|:-:|:-:|
| `records.edit` (PM, ADM projektu) | ✓ | (návrh schvaluje) | (návrh schvaluje) |
| `records.schedule.edit` bez `records.edit` | — | ✓ | — |
| Subsystem lead (vedoucí subsystému) | — | — | ✓ |

---

## 6. UI — karta externí vazby (v editoru záznamu)

### 6.1 Rozložení karty

```
┌──────────────────────────────────────────────────────────────────────────────┐
│  [Číslo: ______]  [Typ: PNF]  [Cena: ____ Kč]  [◉ Výzva]              [🗑]  │
│                                                                         [💬] │
│  Datum objednání: 12.3.2026  Datum dodání: 28.3.2026  Datum převzetí: —     │
└──────────────────────────────────────────────────────────────────────────────┘
```

### 6.2 Horní řádek (zleva doprava)

| Prvek | Komponenta | Chování |
|---|---|---|
| **Číslo** | `gov-form-input type="text"` + pattern `[0-9]{0,6}` + maxlength 6 | Povinný. Po 6 cifrách auto-trigger `POST /Zaznamy/ExterniOdkaz/Sync` s `cislo`. |
| **Typ** | `gov-form-input readonly` | Vyplněno ze syncu (`HOT_ZAZNAMY.typ_zaznamu`). Pokud nenalezeno, „—" + varování pod kartou. |
| **Předpokládaná cena** | `gov-form-input type="number"` | Ruční vstup, Kč formát. |
| **Výzva** | `gov-switch` s labelem **„Výzva"** | Disabled pro NES/PMP. Viz Fáze 1 spec. |
| **🗑 Odebrat** | `gov-button variant="secondary" size="s"` + `gov-icon trash` | Tooltip „Odebrat vazbu". Confirm dialog před akcí. |
| **💬 Vyjádření a termíny** | `gov-button variant="secondary" size="s"` + `gov-icon comment` | Tooltip „Zobrazit komunikaci a termíny". Disabled dokud sync nedohledal ticket. |

**🗑 + 💬 jsou nad sebou** úplně vpravo na pravém okraji karty.

### 6.3 Dolní řádek — tři datumy

Formát `dd.MM.yyyy`. `gov-text--secondary`, label bold.

| Pole | Zdroj | Fallback |
|---|---|---|
| Datum objednání | vyjádření „Kalkulace byla akceptována" | „—" |
| Datum dodání | poslední vyjádření „Dodavatel přidal řešení" | „—" |
| Datum převzetí | vyjádření „Záznam byl převeden do archivu" | „—" + muted „Záznam byl převeden do archivu." |

Pokud žádný datum není vyplněn, **celý dolní řádek se skryje**.

### 6.4 ACL

Čtení karty + otevření modalu + přímá úprava ve schémat 1: **`records.edit`**. Pro `records.schedule.edit` bez `records.edit` je modal **read-only**.

---

## 7. UI — chat modal

### 7.1 Layout

- `gov-dialog size="xl"`.
- Šířka: výchozí **1600 px**, při `< 1200 px` zmenší na `95vw` + změna poměru na **70/30**.
- Výška: `min(100vh - 64px, 960px)`.
- **Hlavička:**
  - Titulek: „Vyjádření k tiketu **{Cislo}** — {Typ} — {Strucne}"
  - Subtitle: počet vyjádření + rozsah datumů.
  - Vpravo: `gov-button secondary` **„+ Přidat ruční krok ▾"** (dropdown).
  - Vpravo: `gov-button secondary` **„Spustit vytěžení znovu"** (confirm modal).
  - Vpravo: × close.
- **Tělo (grid):**
  - Levý sloupec: **80 %** (nebo 70 % pod 1200 px) — timeline.
  - Pravý sloupec: **20 %** (nebo 30 % pod 1200 px) — stepper.
- **Patka:** status autosave („Uloženo ✓" / „Ukládání…" / „Chyba — opakovat").

### 7.2 Levý sloupec — timeline vyjádření

**Komponenta pro bublinu:** `gov-card` s custom třídou (inspirace z `SD_servicedesk/Details.cshtml`, ale přeložená do gov komponent).

**Klasifikace vyjádření podle `HOT_VYJADRENI.typ_komentare` (předpoklad — ověří Fáze 4 mapping):**

| typ_komentare | Vizuál | Přiřaditelné ke kroku? |
|---|---|---|
| `"05"` nebo `"25"` (systémová hláška) | centrovaná plaketka `gov-banner variant="info" size="s"`, autor + datum v `title` tooltipu | **Ne**, s jedinou výjimkou: **bublina „Záznam byl převeden do archivu."** (= povinná pro krok 10 PNF / `DatumPrevzeti` u PMP). Technicky rozlišeno serverem přes predikát na `popis`. |
| `"16"` (odkaz na kalkulaci) | malá karta s ikonou 💰, bez obsahu | Ne |
| ostatní | plná bublina s avatarem + autorem + textem | Ano |

**Kategorie autora (ikona):**

| Kategorie | Ikona | Popisek | Gov barva |
|---|---|---|---|
| `ZP` | `user` | Zadavatel | neutral |
| `VS` | `user-tie` | Vedoucí subsystému | primary |
| `DO` | `industry` | Dodavatel | secondary |
| `PM` | `stamp` | Projektový manažer | warning |
| `RT` | `people-group` | Řešitelský tým | info |

**Struktura bubliny:**
- Avatar vlevo (ikona + tooltip kategorie).
- Jméno autora + datum vpravo nahoře.
- Tělo (`white-space: pre-wrap`, bez HTML rendering).
- Footer: pokud je na bublině aktuálně upnutý krok, viditelná ikona „📌 Krok N: Název" (chip).

**Řazení:** vzestupně podle data (nejstarší nahoře, nejnovější dole). Autoscroll dolů při otevření. Sticky date header.

**Pod 1200 px:** max 3 řádky + tlačítko „[Více]".

### 7.3 Pravý sloupec — vertikální stepper

**Komponenta:** vlastní vertikální stepper (založen na `gov-card`/`gov-stepper`). 6 slotů maximálně (dle typu tiketu).

**Výchozí obsah:**

| Typ | Výchozí body |
|---|---|
| NES | `[1]` |
| PMP | `[1, 3, 4]` |
| PNF | `[1, 6, 7, 10]` |

**Dropdown „+ Přidat ruční krok":**

| Typ | Dostupné ruční kroky |
|---|---|
| NES | — (dropdown skrytý) |
| PMP | `[2]`, `[5]` |
| PNF | `[8]`, `[9]` |

Přidaný krok se zařadí na správnou chronologickou pozici (podle indexu 1–10).

**Ruční krok lze odebrat ze stepperu** (× vedle názvu). Automatické kroky × nemají.

**Kroky bez přiřazeného vyjádření** padají do **bufferu** — parking zóna pod posledním vyjádřením (`gov-bg--background-neutral-subtle`), viditelně oddělené.

### 7.4 Drag & drop chování

**Pravidla:**
- Tah **jen po ose Y** (horizontální fixace).
- **Plynulý pohyb** (CSS transition), snap na Y-center bubliny při uvolnění.
- **Nahoru** — bod se nemůže dostat nad bod předchozího kroku (chronologie). Taktéž nemůže přetáhnout přes obsazenou bublinu (uživatel musí odpojit jiný krok).
- **Dolů** — libovolně. Spodní body se posouvají, aby se neporušila chronologie.
- **Magnet na úrovně bublin** — bod se vždy zasekne na Y-center nejbližší bubliny.
- **1:1 striktní** — jedna bublina = jeden krok. Pokus o drop na obsazenou bublinu → toast „Bublina již má přiřazený krok {K}. Odpojte ho nejdřív."

**Archivní bublina (krok 10 PNF):** poslední vyjádření „Záznam byl převeden do archivu." je pro krok 10 **povinně přiháknutá**. Tah je blokovaný. Pokud tiket není v archivu, krok 10 leží v bufferu.

### 7.5 Visual linie (hover-based)

- **Default:** žádné linie.
- **Hover na chip v bublině** nebo **hover na řádek v kroku** → SVG křivka spojující.
- **Toggle „Zobrazit všechny linie"** (checkbox nad pravým sloupcem) → nakreslí všechny permanentně.

---

## 8. Automat — pravidla vytěžování

### 8.1 Textové predikáty

**Tabulka `HOT_VYJADRENI` (intranetNEW.dbo):**

| Krok / pole | SQL predikát (LIKE, case-insensitive dle collation) |
|---|---|
| Krok 1 (všichni) | datum založení tiketu z `HOT_ZAZNAMY` (nezávisí na vyjádření) |
| Krok 3 (PMP) | `popis LIKE N'%Záznam byl založen a předán dodavateli k řešení pod značkou:%'` → první ASC |
| Krok 4 (PMP) | `popis LIKE N'%Dodavatel přidal řešení%'` → **poslední** DESC |
| Krok 6 (PNF) | `popis LIKE N'%Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována.%'` → první ASC |
| Krok 7 (PNF) | `popis LIKE N'%Dodavatel přidal řešení%'` → **poslední** DESC |
| Krok 10 (PNF) | `popis LIKE N'%Záznam byl převeden do archivu.%'` → jediný |
| `DatumObjednani` (PMP + PNF) | stejné jako Krok 6 (first match) |
| `DatumDodani` (PMP + PNF) | poslední „Dodavatel přidal řešení" |
| `DatumPrevzeti` (všichni) | „Záznam byl převeden do archivu." |

**Plán dodání** (zatím mimo scope, ale pozn.): `popis LIKE N'%předal záznam dodavateli : %s termínem plnění dodavatele%'` + regex na `dd.mm.yyyy` v textu.

### 8.2 Lifecycle vytěžování — triggery

Automat běží v **reakci na uživatelské události**, ne trvale. Primární strategie: **proaktivní harvest ve chvíli, kdy se uživatel chystá s daty pracovat**. Hangfire periodický job je jen záložní síť pro tickety, které se dlouho neotevřely.

#### 8.2.1 Tabulka triggerů

| # | Událost | Rozsah | Frekvence | Sync/Async |
|---|---|---|---|---|
| **T1** | Uživatel napsal 6 cifer do pole `Číslo` | 0 tiketů (jen lookup `HOT_ZAZNAMY`, žádný harvest vyjádření) | Debounce 400 ms | Sync |
| **T2** | Uložení formuláře záznamu s novou / upravenou externí vazbou | 1 tiket (nově vložený nebo s změněným `Cislo`) | Hned po commit | **Async** (background) |
| **T3** | Uživatel klikne „Spustit vytěžení znovu" v chat modalu | 1 tiket | On-demand | Sync (spinner) |
| **T4** | Hangfire periodický batch job — **pouze non-archivované tickety** | N tiketů (všechny s `LastHarvestedAt < now - interval`) | Konfigurovatelné v admin panelu, **default 1 hodina** | Async |
| **T5** | Otevření editoru projektového záznamu (`EditZaznamModal` / `EditZaznamPage`) | N tiketů (všechny externí vazby daného záznamu, které nejsou v archivu) | 1× na load | **Async** (editor se zobrazí okamžitě, harvest doběhne na pozadí; UI si data při refresh / při otevření tabu stáhne) |
| **T6** | Otevření chat modalu | 0 tiketů pokud `LastHarvestedAt < 5 min`; jinak 1 tiket (harvest před zobrazením dat) | On-demand | Sync (krátký spinner, jinak okamžitě) |
| **T7** | Schválení návrhu `CREATE_RECORD` s externími vazbami | M tiketů | Po schválení | Async |
| **T8** | Otevření záložky **Externí vazby** nebo **Harmonogram** v editoru záznamu (pokud T5 ještě neproběhl) | N tiketů (lazy fallback) | Poprvé když tab aktivován | Async |

#### 8.2.2 Rozdíl lookup vs. harvest

- **Lookup** (T1) = rychlý dotaz `SELECT TOP 1 ... FROM HOT_ZAZNAMY WHERE id=?`. Vrací Typ a Strucne. **Nezapisuje** do PM Tracker DB.
- **Harvest** (T2, T3, T4, T5, T7, T8) = průchod všech vyjádření tiketu (`SELECT ... FROM HOT_VYJADRENI WHERE hot_zaznam_id=? AND datum > @last_harvested_at`), aplikace LIKE predikátů, zápis do `zaznam_harmonogram_vyjadreni_vazba` + `HS0X_DELAY` + update `LastHarvestedAt`.

#### 8.2.3 Primární proaktivní strategie — T5

Otevření editoru záznamu je **nejsilnější signál**, že uživatel se chystá se záznamem pracovat (harmonogram, externí vazby, vyjádření). Proto se právě v tu chvíli spustí harvest **všech** externích vazeb daného záznamu, které:

- mají `Cislo IS NOT NULL` (napojené na ServiceDesk),
- NEjsou v archivním stavu v ServiceDesku (`HOT_ZAZNAMY.stav != 'archivováno'`),
- mají `LastHarvestedAt < NOW - 5 minut` (není čerstvě harvestnuté).

Harvest běží **async** — editor se uživateli zobrazí okamžitě, data harmonogramu jsou vidět z poslední DB hodnoty, nové hodnoty z ServiceDesku dorazí typicky do 1–3 sekund (pokud má záznam 3 vazby). Když uživatel prokliká na záložku harmonogram za 5 s, už tam má aktuální data. Pokud ne, Plán C zajistí SignalR push / polling v editoru (mimo tento spec).

#### 8.2.4 Záložní síť — T4 Hangfire periodický job

Hangfire job pokrývá případ, kdy se k tiketu dlouho nikdo nevrátil, ale:
- Dashboard prodlení (Use-case B) nebo reporty se opírají o aktuální datumy bez otevření editoru.
- Uživatel zapomněl otevřít záznam, do kterého ServiceDesk poslal nová vyjádření.

**Default interval: 1 hodina.** Konfigurovatelné v admin nastavení (viz §8.5) — mezi hodnotami `15min / 30min / 1h / 3h / 6h / 12h / disabled`. Doporučení pro produkci: 1–3 hodiny.

**Filtr kandidátů pro Hangfire job:**
```sql
SELECT id FROM zaznam_externi_odkazy
WHERE cislo IS NOT NULL
  AND (last_harvested_at IS NULL
       OR last_harvested_at < DATEADD(minute, -@interval_minutes, SYSUTCDATETIME()))
  -- Vyloučit archivované tickety (poslední znám hodnota v PM Tracker DB)
  AND (datum_prevzeti IS NULL
       OR datum_prevzeti > DATEADD(day, -7, SYSUTCDATETIME()))
       -- 7denní grace window: právě archivované tickety ještě můžou dostat
       -- pozdní vyjádření (např. uzavření SLA), takže je harvestujeme ještě týden;
       -- starší archivy přeskočíme, protože se stejně nic nezmění
```

**Throttle:** max 4 paralelní HOT DB requesty.

**Zrušení kompletně:** admin nastaví interval = `disabled`. Hangfire job se nespustí. Zůstanou jen reaktivní triggery (T2, T3, T5, T6, T7, T8).

### 8.3 Automat vítězí nad uživatelem (nové vyjádření)

Když automat najde nové vyjádření `V_nove` pro krok `K` (= nový text v `HOT_VYJADRENI` spustí patternový predikát pro krok `K`):

1. **Akce:** supersedne předchozí Active vazbu (pokud existuje, auto nebo manual) a vytvoří novou Active vazbu s `V_nove`.
2. **Důvod:** dodavatel může posílat revize (např. původně blbý textový popis opravy, později řádně dodané řešení); novější vyjádření je autoritativní.
3. **Audit log:** „Krok K přiřazen automatem k novějšímu vyjádření (ID {V_nove.id}, datum {datum}). Původní vazba (source={source}, datum {puvodni_datum}) archivována."
4. **UI notifikace** uživateli při příštím otevření modalu: `gov-message variant="info"` „Automat upravil přiřazení kroků podle nových vyjádření."

**Když automat NEpřepisuje:**
- Pokud žádné nové vyjádření nesplňuje pattern pro krok K → automat nesaná do existující vazby (ať byla manual či auto).
- Uživatelova ruční volba zůstává platnou skutečností, dokud se neobjeví nové matching vyjádření. Typický scénář: dodavatel komunikoval volným textem → automat ho nezachytí → uživatel manuálně přiřadí → později dodavatel pošle řádnou zprávu → automat ji matchne a přepíše.

### 8.4 Chronologie — algoritmus rebalance

Když automat přepíše krok `K`, musí ověřit, že se neporušila chronologie (krok K+1 nesmí být dříve než krok K).

```
on harvest event for K with new bubble V_K:
  ACTIVE_K = find_active_binding(zaznam_id, K)

  # Kontrola horní hranice (K-1)
  ACTIVE_PREV = find_active_binding(zaznam_id, K-1)
  if ACTIVE_PREV exists AND V_K.datum < ACTIVE_PREV.datum:
    # Nové vyjádření je dřívější než předchozí krok — neplatná vazba
    log_warning("Nové vyjádření V_K porušuje chronologii, krok K není obnoven")
    return

  # Kontrola spodní hranice (K+1)
  ACTIVE_NEXT = find_active_binding(zaznam_id, K+1)
  if ACTIVE_NEXT exists AND V_K.datum >= ACTIVE_NEXT.datum:
    # Nové vyjádření je pozdější než následující krok — následující se musí posunout
    shift_downstream_steps(K, V_K.datum)

  # Nahradit vazbu
  supersede_binding(ACTIVE_K)
  create_active_binding(K, V_K, source=Auto)

shift_downstream_steps(startKrok, minDatum):
  for each krok in downstream(startKrok):
    candidates = find_bubbles_for_step(krok, datum > minDatum)
    if candidates not empty:
      supersede_active_binding(krok)
      create_active_binding(krok, candidates.first, source=Auto)
      minDatum = candidates.first.datum
    else:
      park_to_buffer(krok)  # žádné relevantní vyjádření není
```

**Buffer = parking zone:** krok, pro který automat nenajde relevantní vyjádření, je v UI viditelný v bufferu pod poslední bublinou.

### 8.5 Re-harvest — co zahazuje

Uživatelsky spuštěný re-harvest (trigger T3):

- **Zahazuje všechny vazby** (auto + manual) pro **automatické kroky** (1, 3, 4, 6, 7, 10).
- **Nezahazuje** vazby pro **ruční kroky** (2, 5, 8, 9) — automat je stejně neřeší.
- Confirm: „Všechna přiřazení automatických kroků budou smazána a nahrazena podle aktuálního ServiceDesku. Ruční kroky zůstanou beze změny."

### 8.6 Admin nastavení synchronizace — nová sekce `/Nastaveni?section=servicedesk-sync`

Nové admin rozhraní pro řízení Hangfire periodického jobu (T4) a celkového chování synchronizace. Dostupné jen pro role **`app_admin`** a **`SuperAdmin`**.

#### 8.6.1 Konfigurovatelné hodnoty

| Klíč | Typ | Default | Rozsah | Popis |
|---|---|---|---|---|
| `servicedesk.sync.hangfire.interval` | enum | `1h` | `disabled`, `15min`, `30min`, `1h`, `3h`, `6h`, `12h` | Perioda T4 batch jobu. |
| `servicedesk.sync.hangfire.maxParallelism` | int | `4` | `1`–`16` | Počet paralelních HOT DB requestů v jednom ticku. |
| `servicedesk.sync.hangfire.archiveGraceDays` | int | `7` | `0`–`30` | Počet dní po archivaci tiketu, kdy ho T4 ještě harvestuje (grace window pro pozdní vyjádření). Potom se přeskočí. |
| `servicedesk.sync.editor.freshnessMinutes` | int | `5` | `0`–`60` | T5/T8: pokud `LastHarvestedAt < NOW - N min`, harvest se přeskočí (tiket je „čerstvý"). 0 = harvestuj vždy. |
| `servicedesk.sync.timeout.seconds` | int | `30` | `5`–`120` | Timeout per tiket per harvest. |
| `servicedesk.sync.enabled` | bool | `true` | | Master killswitch — když `false`, žádný harvest (T2, T3, T4, T5, T6, T7, T8) neběží. Lookup (T1) zůstává. |

#### 8.6.2 Datový model — tabulka `servicedesk_sync_settings`

Klíč-hodnota tabulka pro tyto konfigurace. Jednoduchá `nvarchar` storage + runtime parsing. Důvod: nepotřebujeme strukturované sloupce, rozšíření nových klíčů v budoucnu je triviální.

```sql
CREATE TABLE dbo.servicedesk_sync_settings
(
  klic             NVARCHAR(200) NOT NULL PRIMARY KEY,
  hodnota          NVARCHAR(MAX) NOT NULL,
  updated_at       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
  updated_by_osoba_id INT NULL FOREIGN KEY REFERENCES dbo.osoby(id)
);
```

Seed při spuštění skriptu naplní 6 výchozích klíčů.

#### 8.6.3 UI — karta „Synchronizace ServiceDesku" v Nastavení

Přidá se do navigace `/Nastaveni` (levý sloupec sekcí) nová položka **„Synchronizace ServiceDesku"** (section key `servicedesk-sync`), viditelná jen pro `app_admin` / `SuperAdmin`.

Layout:

```
┌─ Synchronizace ServiceDesku ───────────────────────────────┐
│                                                            │
│ Master kill-switch                                         │
│ ○ Zapnuto    ○ Vypnuto                                     │
│                                                            │
│ Periodický harvest (Hangfire)                              │
│ Interval:  [1 hodina ▾]                                    │
│   Možnosti: Vypnuto / 15 min / 30 min / 1h / 3h / 6h / 12h │
│                                                            │
│ Max paralelních requestů:  [ 4 ]                           │
│ Grace window po archivaci (dny):  [ 7 ]                    │
│                                                            │
│ Proaktivní harvest při otevření editoru (T5 / T8)          │
│ „Čerstvost" tiketu (min.):  [ 5 ]                          │
│   (pokud je tiket harvestnutý do N minut, přeskočí se)     │
│                                                            │
│ Timeout per tiket (s):  [ 30 ]                             │
│                                                            │
│ [Uložit změny]  [Obnovit výchozí]                          │
│                                                            │
│ ─── Stav synchronizace ─────────────────────────────────── │
│ Poslední spuštění Hangfire jobu:  21.4.2026 14:32          │
│ Zpracováno tiketů:  12                                     │
│ Chyby:  0                                                  │
│ Průměrná doba harvestu per tiket:  185 ms                  │
│ [Spustit nyní]  [Zobrazit log]                             │
│                                                            │
└────────────────────────────────────────────────────────────┘
```

Komponenta: **`gov-card` s `gov-form-input` / `gov-select` / `gov-switch`**. Žádná vlastní CSS.

Akce v patce karty (jen `SuperAdmin`):
- **Spustit nyní** — force trigger Hangfire jobu mimo interval.
- **Zobrazit log** — otevře modal s posledních 50 Hangfire-job runs (datum, počet zpracovaných, chyby).

#### 8.6.4 API endpointy

| Method | Route | Popis |
|---|---|---|
| GET | `/Nastaveni/ServiceDeskSync` | Render card view (přístup jen pro admin role). |
| POST | `/Nastaveni/ServiceDeskSync/Save` | Uložit hodnoty. |
| POST | `/Nastaveni/ServiceDeskSync/RunNow` | Force spuštění Hangfire jobu. |
| GET | `/Nastaveni/ServiceDeskSync/Log` | JSON seznam posledních runs. |

#### 8.6.5 Runtime integrace

- Služba `IServiceDeskSyncSettings` — cached reader, refresh cache při každém save (pub/sub přes `IMemoryCache.Remove` hook).
- Hangfire job při každém ticku čte aktuální interval → pokud se změnil, pře-registruje cron.
- Editor controller (`ZaznamyController.Edit`) v T5 volá `IHarvestScheduler.ScheduleHarvestForRecord` s `freshnessMinutes` parametrem.

---

## 9. UI — localStorage + autosave

### 9.1 localStorage strategie

Chat modal pracuje primárně s **klientským state** v localStorage. Do DB se sahá **jen** při:

1. **Autosave** (debounced 1 000 ms po poslední akci).
2. **Explicitní save** při zavírání modalu (confirm dialog).
3. **Beforeunload** — `sendBeacon` best-effort.

**localStorage klíč:** `pm.chat-modal.{zaznamId}.{externiOdkazId}`

**Payload:**
```json
{
  "snapshot_ts": "2026-04-21T14:32:18Z",
  "server_ts": "2026-04-21T14:30:00Z",
  "bindings": [
    { "krok_key": "abc-123", "hot_vyjadreni_id": "V12345", "source": "Manual" },
    ...
  ],
  "manual_steps": [
    { "krok_key": "def-456", "datum": "2026-03-15" }
  ],
  "dropdown_kroky_pridane": ["step-2-guid", "step-5-guid"]
}
```

### 9.2 Konfliktní detekce

Při otevření modalu:
1. Načti z DB (`server_state`).
2. Zkontroluj `localStorage[key]`.
3. Pokud `localStorage.snapshot_ts > localStorage.server_ts` → **unsaved changes**. Zobraz `gov-message variant="warning"` „Máte neuložené změny z {datum}. Použít / Zahodit?"
4. Jinak → zobraz čerstvý DB state.

### 9.3 Autosave UI

V patce modalu:
- Klidový stav: tiché „Uloženo ✓" na 2 s po úspěšném savu, pak zmizí.
- Aktivní sav: `gov-message variant="info" size="s"` s spinnerem + „Ukládání…".
- Chyba: `gov-message variant="error"` „Ukládání selhalo — zkuste znovu." + retry button.

### 9.4 Zavření s neuloženými změnami

- **Pokud jsou změny:** confirm dialog s třemi volbami:
  - **Uložit a zavřít** (default)
  - **Zavřít bez uložení** (destructive)
  - **Zůstat** (cancel)
- **Pokud nejsou změny:** zavře normálně.

### 9.5 beforeunload

- Pokud jsou změny → nativní `beforeunload` dialog prohlížeče.
- Paralelně `sendBeacon` POST s aktuálním snapshotem (best-effort, server ignoruje ve chvíli, kdy mu přijde pozdě).
- `gov-message variant="info"` se zobrazí **jednou při prvním otevření modalu** (persistováno v localStorage): „Změny se ukládají automaticky. Při zavření se vše uloží."

---

## 10. API endpointy (nové)

| Method | Route | Body | Popis |
|---|---|---|---|
| POST | `/ExterniOdkaz/Sync` | `{ cislo }` | **T1** — lookup 6místného čísla v ServiceDesku → vrátí `{ nalezeno, typ, strucne }`. |
| GET | `/Zaznamy/Vyjadreni/List` | `?externiOdkazId=X` | **T6** — vrátí seznam vyjádření pro modal (timeline). |
| POST | `/Zaznamy/HarmonogramVazba/Save` | celý localStorage snapshot | Bulk upsert — server udělá diff proti DB, aplikuje rozdíly, vrátí nový server_state. |
| POST | `/Zaznamy/HarmonogramVazba/ReHarvest` | `{ externiOdkazId }` | **T3** — smaže všechny auto kroky + spustí automat znovu (synchronně). |
| POST | `/Zaznamy/HarmonogramSkutecnost/ManualSet` | `{ zaznamId, krokKey, datum }` | Jen pro ruční kroky 2/5/8/9 bez vazby. |
| GET | `/Nastaveni/ServiceDeskSync` | — | **Admin** — render karty s nastavením synchronizace. |
| POST | `/Nastaveni/ServiceDeskSync/Save` | hodnoty ze form | **Admin** — uložit hodnoty klíčů do `servicedesk_sync_settings`. |
| POST | `/Nastaveni/ServiceDeskSync/RunNow` | — | **SuperAdmin** — force trigger Hangfire jobu. |
| GET | `/Nastaveni/ServiceDeskSync/Log` | `?limit=50` | **Admin** — JSON posledních Hangfire runs. |

### 10.1 Interní služby (ne HTTP, ale součást kontraktu)

| Rozhraní | Metoda | Popis |
|---|---|---|
| `IHarvestScheduler` | `ScheduleHarvestAsync(int externiOdkazId)` | **T2, T7** — fire-and-forget po uložení externí vazby. |
| `IHarvestScheduler` | `ScheduleHarvestForRecordAsync(int zaznamId)` | **T5, T8** — harvest všech vazeb daného záznamu (async). |
| `IVyjadreniHarvestService` | `HarvestTicketAsync(int externiOdkazId, CancellationToken ct)` | Skutečná implementace harvestu (synchronní uvnitř Hangfire jobu). |
| `IServiceDeskSyncSettings` | `GetAsync()` / `SaveAsync(settings)` | Cached config reader/writer pro §8.6 klíče. |

---

## 11. DB cleanup fakturace — skript `db_upgrade_1_1_9_fakturace_cleanup.sql`

```sql
-- 1. Spočítat typy fakturace před smazáním
DECLARE @FakturaceTypy TABLE (Id INT);
INSERT @FakturaceTypy (Id)
SELECT Id FROM dbo.ciselnik_harmonogram_typu
WHERE Kod LIKE 'HS11_%' OR Nazev LIKE N'%fakturace%';

SELECT 'Typy k smazání' AS Info, COUNT(*) AS Count_ FROM @FakturaceTypy;

-- 2. Smazat hodnoty (plány + skutečnosti)
DELETE FROM dbo.zaznam_harmonogram_hodnoty
WHERE TypId IN (SELECT Id FROM @FakturaceTypy);

-- 3. Smazat řádky ciselniku
DELETE FROM dbo.ciselnik_harmonogram_typu
WHERE Id IN (SELECT Id FROM @FakturaceTypy);

-- 4. Smazat pending návrhy, které odkazují na fakturaci (edge case)
-- (payload_json obsahuje TypId — pokud nějaký existuje, smazat celý návrh)
DELETE FROM dbo.zaznam_navrhy
WHERE stav = 'PENDING'
  AND payload_json LIKE N'%HS11_%';

-- 5. Verifikace
SELECT 'Pozůstalé řádky fakturace:' AS Check_, COUNT(*) AS Count_
FROM dbo.ciselnik_harmonogram_typu
WHERE Kod LIKE 'HS11_%' OR Nazev LIKE N'%fakturace%';
-- Expected: 0
```

**Aplikační kód — úpravy:**
- `HarmonogramService.EnsurePersistedActiveHarmonogramSchemaVersionAsync` + `HarmonogramCatalogService` — odstranit seed řádku pro HS11.
- `_ScheduleBlock.cshtml`, `_EditZaznamSchedulePanel.cshtml` — ověřit přes grep, že není nikde hardcoded HS11.
- Testy — pokud některý test kontroluje počet kroků = 11, upravit na 10.

**Nasazení:** Dev nejdřív → ruční ověření → Prod. Destruktivní operace.

---

## 12. Akceptační kritéria

### Must-have pro dokončení Use-case A

1. ✅ Tlačítko „Vyjádření a termíny" (ikonka chat bubliny) u každé externí vazby v editoru záznamu.
2. ✅ Tlačítko „Odebrat vazbu" je ikonka (koš), ne text.
3. ✅ Label switche na kartě externí vazby je „Výzva".
4. ✅ Nové pořadí prvků na kartě: Číslo → Typ (read-only) → Cena → Výzva → 🗑 + 💬.
5. ✅ Auto-sync po 6 cifrách — vyplní Typ, datumy, spustí vytěžování.
6. ✅ Tlačítko Vyjádření je disabled, dokud sync nedohledal ticket.
7. ✅ Datumy pod kartou (Objednání, Dodání, Převzetí) automaticky synchronizované.
8. ✅ Datum převzetí fallback „Záznam byl převeden do archivu." nebo „—".
9. ✅ Modal 80/20 (1600 px), pod 1200 px 70/30.
10. ✅ Levý sloupec timeline s gov-card/gov-banner bublinami, rozlišení systémových (05/25) a běžných.
11. ✅ Pravý sloupec s vertikálním stepperem (počet bodů = aktivní kroky pro typ tiketu).
12. ✅ Dropdown „+ Přidat ruční krok" s relevantními volbami pro typ tiketu.
13. ✅ Drag & drop s vertikální fixací + snap na bublinu + 1:1 striktní + chronologie.
14. ✅ Buffer pro kroky bez relevantního vyjádření — viditelný pod posledním vyjádřením.
15. ✅ Autosave debounced 1000 ms + localStorage strategie.
16. ✅ Re-harvest zahazuje auto + manual pro automatické kroky, zachovává ruční.
17. ✅ Automat vítězí nad uživatelem pro novější vyjádření + chronologie algoritmus.
18. ✅ Nová tabulka `zaznam_harmonogram_vyjadreni_vazba` + kardinalita 1:1 přes `stav=Active` flag.
19. ✅ `LastHarvestedAt` sloupec na externí vazbě.
20. ✅ DB cleanup fakturace.
21. ✅ Rozšíření `CreateRecordProposalPayload` a `SchedulePlanProposalPayload` o ruční datumy a vazby.
22. ✅ Integrace se třemi workflow schématy (přímá / úprava harmonogramu / nový záznam).
23. ✅ ACL: `records.edit` pro schéma 1; `records.schedule.edit` pro schéma 2; subsystem lead pro schéma 3.
24. ✅ Gov komponenty místo Bootstrap (překlad z `SD_servicedesk/Details.cshtml`).
25. ✅ Unit testy + Playwright visual smoke test na modal.
26. ✅ Triggery T1–T8 implementovány dle §8.2 tabulky.
27. ✅ Otevření editoru záznamu (T5) spouští async harvest všech jeho externích vazeb.
28. ✅ Otevření záložky Externí vazby / Harmonogram (T8) spouští lazy harvest, pokud T5 ještě neproběhl.
29. ✅ Hangfire batch job (T4) filtruje archivované tickety (7denní grace window).
30. ✅ Admin karta `/Nastaveni?section=servicedesk-sync` pro interval + parallelism + grace window + freshness + timeout + killswitch.
31. ✅ Tabulka `servicedesk_sync_settings` + `IServiceDeskSyncSettings` cached reader.
32. ✅ `IHarvestScheduler` stub v Plánu B, reálná implementace v Plánu C.

---

## 13. Otevřené body (k doladění v implementačním plánu)

1. **`HOT_VYJADRENI` schema** — přesné názvy sloupců (`typ_komentare`, `autor`, `popis`, `datum`) + CommentType hodnoty `"05"`, `"25"`, `"16"` (z `SD_servicedesk/Details.cshtml`) — ověřit mapování proti produkční DB (čeká na Fázi 4 ServiceDesk mapping).
2. **Přesné LIKE patterns** pro textové predikáty — diakritika, collation, lokalizace dodavatelů mohou ovlivnit matching.
3. **Hangfire polling vs. on-demand** — interval, zdroje datumu (kolik zaznamů lze dotázat naráz bez zátěže ServiceDesku).
4. **Vnitřní struktura localStorage payload** — schéma + verze (forward compat).
5. **Přesné gov komponenty** pro stepper (vertikální) a floating dropdown — ověřit v docs Gov Design System.
6. **UI indikátor „Vytěženo před 2 dny"** — formátování (relative time) + refresh.
7. **Integrace s dashboardem prodlení (Use-case B)** — jak se data z této fáze napojí na dashboard.

---

## 14. Vztah k jiným fázím a specům

- **Fáze 1 ServiceDesk** (`2026-04-20-servicedesk-integrace-vyzvy-design.md`) — sdílí read-only DbContext. Tento spec předpokládá, že `PmTracker.ServiceDesk.Sql` je funkční a obsahuje mapping pro `HOT_VYJADRENI` (rozšířit).
- **Fáze 4 ServiceDesk mapping** (parkovaný úkol) — závazně dokončit předtím, než se nasadí Use-case A do produkce.
- **Fáze 3 Word export** — mimo scope tohoto specu.
- **Use-case B Dashboard prodlení** — bude využívat data z `zaznam_harmonogram_vyjadreni_vazba` pro zobrazení plánu vs. skutečnosti.

---

## 15. Zodpovědnost

- **Technický návrh a implementace:** Claude
- **Business pravidla (kroky 1–10, PMP/PNF/NES):** Ing. Andrlík + `Návrhy úprav.docx`
- **Schéma `HOT_*`:** Ing. Andrlík (ručně přes reální data) — Fáze 4 ServiceDesk mapping
- **Přístup k servisnímu účtu + connection string `TicketingReadOnly`:** IT

---

**Konec specu.**
