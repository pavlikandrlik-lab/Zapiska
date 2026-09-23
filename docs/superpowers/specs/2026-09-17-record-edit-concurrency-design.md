# Souběžná editace záznamu — zámek karty a record guard

**Datum:** 2026-09-17
**Stav:** návrh k odsouhlasení (revize 2 — po ověření dosažitelnosti kolize uživatel × automat)
**Trigger:** pilot/předpilot se dvěma uživateli — save záznamu padá hláškou „Harmonogram byl mezitím upraven jiným uživatelem. Načtěte záznam znovu." i v situacích, kdy uživatel harmonogram vůbec needituje.

---

## 1. Problém

### 1.1 Co se děje dnes

Editor záznamu nese skryté pole `ScheduleVersion` = `MAX(UpdatedAt)` všech řádků `ZaznamHarmonogramKroky` (fyzicky `dbo.zaznam_harmonogram_krok`) daného záznamu, serializované jako `Ticks.ToString("X16")`:

- render: `ProjectService.RecordEditorComposition.cs:216-227`
- přenos: `Views/Shared/_ScheduleBlock.cshtml:44-46` (hidden input)
- kontrola: `RecordService.SaveRecord.cs:699-720`

Při uložení se hodnota přepočítá a porovná. Neshoda → `RecordValidationIssue` s rule `schedule_stale_data` → vyhodí se `RecordValidationException` → **spadne celý save**, včetně názvu, popisu, stavu, externích vazeb a všeho ostatního.

### 1.2 Proč to padá i bez druhého uživatele

`UpdatedAt` na krocích posouvá i automatika, bez jakéhokoli člověka:

| Zdroj | Místo |
|---|---|
| Harvest vyjádření ze ServiceDesku | `Services/ServiceDesk/VyjadreniHarvestService.cs:527` |
| Rebalance vazeb | `Services/ServiceDesk/BindingRebalanceService.cs:357` |
| Tlačítko re-harvest | `Controllers/HarmonogramController.cs:141` |
| Otevření editoru (proaktivní harvest) | `Controllers/ZaznamyController.cs:106` → `ScheduleHarvestForRecordAsync` |

Poslední řádek je zákeřný: **otevření editoru samo naplánuje harvest**, který může v průběhu editace posunout `UpdatedAt` a tím zneplatnit verzi, se kterou se ta samá stránka vyrenderovala.

### 1.3 Kolize uživatel × automat je nedosažitelná — ověřeno

Rozdělení zápisů mezi uživatele a automat:

| Pole řádku kroku | Zapisuje uživatel (Save) | Zapisuje automat (ApplyPlan) |
|---|---|---|
| `PlanDatum` (kroky 1–10) | **ano** | ne |
| `SkutecnostDatum`, kroky {2,5,8,9} (manuální) | **ano** | ne — `ComputePlanAsync` manuální kroky přeskakuje |
| `SkutecnostDatum`, kroky {1,3,4,6,7,10} | **ne** | **ano**, pokud řádek není `SkutecnostRezim = Manual` |
| `SkutecnostZdroj` | odvozeně u manuálních kroků | **ano** |
| `SkutecnostRezim` (master switch Auto/Manual) | **ano** | ne |
| `PreferredExterniOdkazId` | ne — Save ho nezapisuje vůbec | **ano** |

Klíčový řádek je třetí. Do skutečnosti auto-eligible kroků {1,3,4,6,7,10} se uživatel nedostane **ani v ručním režimu**:

- V režimu Automatika je pole v UI zamčené.
- V ručním režimu UI input dokonce vyrenderuje (`Views/Shared/_ScheduleTable.cshtml:120-122`, `allowManualForAutoEligible`), ale server ho zahodí — `RecordService.SaveRecord.cs:878-882` filtruje `if (!IsManual(mk.Poradi)) continue;`.

A i kdyby se ta cesta otevřela, konflikt se sám zavírá: přepnutí do ručního režimu nastaví `SkutecnostRezim = Manual` a automat od té chvíle řádek **trvale přeskakuje** (`HarmonogramSkutecnostSyncService.cs:76-83`, `SkippedManualRezim`). Uživatelova akce má na vrch konstrukcí, ne dohodou.

**Závěr: 100 % konfliktů, které dnes pilot vidí, je falešných.** Kontrola `ScheduleVersion` nechrání před ničím reálným.

### 1.4 Co dnes uživatel na obrazovce vidí

Save jde přes AJAX (`wwwroot/js/modules/ajax.js`), takže se nad formulář vloží `gov-message color="error"`:

```
Záznam nelze uložit. Opravte označená pole v jednotlivých záložkách. | Harmonogram byl mezitím upraven jiným uživatelem. Načtěte záznam znovu.
Kód chyby: RECORD_VALIDATION_FAILED | TraceId: …
▸ Diagnostický log   [Kopírovat log] [Uložit log chyby]
```

Navíc `ScheduleVersion` nemapuje na žádnou záložku (`recordEditor/form.js:775-793`), takže editor nepřepne na Harmonogram, inline hláška se přilepí ke skrytému inputu a tlačítko **Obnovit stránku** se nevykreslí (je vyhrazené kódům `SESSION_STALE_CLIENT_BLOCK` / `SESSION_EXPIRED`, `ajax.js:232-246`).

### 1.5 Co to *není*

Není to vypršení session. Aplikace nemá cookie session ani idle timeout — autentizace je Windows Auth přes IIS (`Program.cs:33`). Vypršet může jen antiforgery token a na to existuje keep-alive koordinátor (`wwwroot/js/modules/session.js`, `Controllers/AppController.cs:27-59`) s vlastními hláškami. Ten zůstává beze změny — jen se na něj přibalí heartbeat zámku (§4.2).

### 1.6 Druhá, dosud neevidovaná díra

Souběh neřeší jen editor. **Schválení návrhu změny harmonogramu** zapisuje kroky přímo, mimo editační cestu: `RecordProposalService.DecisionCommands.cs:343-358` (`ApplyApprovedScheduleProposalAsync`). Zámek karty na tuhle cestu nedosáhne, takže ji musí pokrýt druhá vrstva (§5).

---

## 2. Cíl

1. Uživatel, kterého ruší jen automatika, nesmí být nikdy zablokován.
2. Souběh dvou lidí nad jedním záznamem se **řeší prevencí** — druhý člověk se do editace nedostane a vidí, kdo záznam upravuje.
3. Cizí lidský zápis, který zámek nezachytí (schválení návrhu, vypršelý zámek, dva taby), se musí při uložení poznat a pojmenovat.
4. Žádná tichá ztráta cizí práce.

### 2.1 Odsouhlasená rozhodnutí

| # | Otázka | Rozhodnutí |
|---|---|---|
| R1 | Kolize uživatel × uživatel | **Pesimistický zámek karty** — druhého do editace nepustit, zobrazit „Tento záznam upravuje jiný uživatel: Příjmení Jméno". |
| R2 | Fallback, když zámek nestačí | Tvrdé odmítnutí při uložení, ale **se jménem** toho, kdo mezitím uložil. |
| R3 | Přepnutí master switche Auto → Manual při změně od automatu | **Není kolize.** Save datum nepřepisuje, jen zmrazí stav; automat pak řádek trvale přeskakuje. |
| R4 | Dialog „Uložit bez skutečnosti / Zahodit / Vrátit se" | **Zrušen.** Větev, kterou měl obsluhovat, je nedosažitelná (§1.3). Kdyby vznikla, platí pravidlo *uživatelova akce vyhrává*. |
| R5 | Kontrola `ScheduleVersion` | **Smazat celou**, ne zjemňovat. |
| R6 | Oprava ručního režimu u auto-eligible kroků | **Součást fáze 1** — dnes se zadané datum tiše zahodí. |

---

## 3. Architektura řešení

Dva pilíře. Každý se dá nasadit samostatně.

```
   ┌────────────────────────────────────────────────┐
P1 │ Pesimistický zámek karty (prevence)            │  uživatel × uživatel
   │ GET Edit → acquire │ heartbeat │ release       │  v editoru
   └────────────────────────────────────────────────┘
                  ↓ nepokryje: schválení návrhu, vypršelý zámek, dva taby
   ┌────────────────────────────────────────────────┐
P2 │ Record guard se jménem (detekce)               │  uživatel × uživatel
   │ Verze (id posledního auditu) v formuláři       │  mimo editor
   └────────────────────────────────────────────────┘

   Automat žádnou vrstvu nespouští — kontrola ScheduleVersion se maže (§6).
```

---

## 4. P1 — Pesimistický zámek karty

### 4.1 Datový model

Nová tabulka `dbo.zaznam_edit_zamek`:

| Sloupec | Typ | Poznámka |
|---|---|---|
| `zaznam_id` | `INT` | PK, FK → `projektove_zaznamy(id)` ON DELETE CASCADE |
| `osoba_id` | `INT` | FK → `osoby(id)`, držitel zámku |
| `ziskano_at` | `DATETIME2(3)` | UTC, čas získání (zobrazuje se v hlášce) |
| `heartbeat_at` | `DATETIME2(3)` | UTC, poslední projev života |

Jeden řádek na záznam. Zámek je **advisory** — aplikační, ne databázový; `sp_getapplock` ani SQL zámky se nepoužívají (nepřežily by request).

Migrace: `db_upgrade_1_4_5_record_edit_lock.sql`, idempotentní, ve stylu `db_upgrade_1_4_4_search_index.sql`, plus otisk v `db_check_applied_upgrades.sql`. **EF Migrations se nepoužívají** — nasazení je offline, schéma řídí ruční skripty.

### 4.2 Životní cyklus

| Událost | Chování |
|---|---|
| **Acquire** — `GET /Zaznamy/Edit/{id}`, po autorizaci a před načtením modelu | Jeden atomický `MERGE`: zámek dostane ten, kdo řádek drží, nebo jehož `heartbeat_at` je starší než TTL. Vlastní zámek téhož `osoba_id` je re-entrantní (druhý tab projde a jen obnoví heartbeat). |
| **Heartbeat** | **Přibalí se na existující keep-alive**, který už běží každých 5 minut — `/App/KeepAlive` dostane volitelný `zaznamId`, editor stránka ho pošle. Žádný nový časovač, žádný nový endpoint. |
| **Release — uložení** | Po úspěšném `Save`. Best-effort: selhání release nesmí shodit save. |
| **Release — odchod** | `navigator.sendBeacon` na `POST /Zaznamy/EditLock/Release` z `pagehide` + napojení na close-guard cestu (Zrušit / breadcrumb / back). |
| **Expirace** | TTL **15 minut** bez heartbeatu (= 3 zmeškané keep-alive cykly). Vyhodnocuje se při acquire, žádný úklidový job. |

Ruční „převzít zámek" (force takeover) **není součástí v1** — TTL to řeší samo.

### 4.3 Chování pro blokovaného uživatele

`GET Edit` u cizího živého zámku **nevrací editor**. Redirect na detail záznamu s `gov-message color="warning"`:

> **Tento záznam upravuje jiný uživatel: Novák Jan**
> Úpravy začaly v 14:05. Zkuste to prosím později.
> [Zkusit znovu]

Jméno formátuje sdílený helper `BuildDisplayName` (`ProjectService.RecordComposition.cs:182-195`) — stejný tvar jako všude jinde v UI.

Tlačítko Upravit na kartě záznamu zůstává viditelné; stav zámku se do výpisů netahá (byl by to dotaz navíc nad celým seznamem). Uživatel se o zámku dozví až při kliknutí — vědomý kompromis ve prospěch výkonu výpisů.

### 4.4 Rozsah

- Zámek se bere **jen na přímé editační cestě** (`Zaznamy/Edit`, tedy `records.edit` i `records.schedule.edit`).
- Návrhový workflow (`NavrhyController`) zámek nebere ani nerespektuje — návrhy nic nepřepisují, kolizi jejich schválení řeší P2.
- `Zaznamy/Create` zámek nebere (záznam ještě neexistuje).

---

## 5. P2 — Record guard se jménem

### 5.1 Pravidlo, na kterém to celé stojí

> **Verze záznamu se posune při každém uživatelském zápisu — a nikdy při zápisu automatu.**

Tím jedním pravidlem je pokryto všechno podstatné: automat si píše svoje a nikoho neblokuje, zatímco cizí *lidský* zásah editor spolehlivě zachytí.

### 5.2 Mechanismus — verze je id posledního auditního zápisu

**Oprava proti revizi 2 (ověřeno při implementaci 2026-09-17):** dřívější znění tvrdilo, že `ProjektovyZaznamEntity` má namapovaný `RowVersion`. **Nemá.** Ten řádek v `RecordEntityConfiguration.cs:97` patří konfiguraci **návrhů** (`zaznam_navrhy`), která je ve stejném souboru níž; tabulka `projektove_zaznamy` sloupec `row_version` vůbec neobsahuje.

Verzí záznamu je proto **id posledního řádku v `authz.audit_log` pro danou entitu**. Důvod, proč to funguje přesně podle pravidla §5.1:

- Audit s `entity_type = 'zaznam'` píší **výhradně čtyři uživatelské cesty** — `RecordService.SaveRecord`, `RecordService.DeleteRecord`, `RecordService.MeetingIdentifier` a schválení návrhu v `RecordProposalService.DecisionCommands`.
- **Automatika neaudituje nic** — `Services/Schedules/` ani `Services/ServiceDesk/` neobsahují jediné volání `IAuditWriteService`. Její zásahy do kroků tedy verzi neposunou.
- Dotaz jede po existujícím indexu `IX_authz_audit_log_entity_type_entity_id`.

Zavádí se:

1. Hidden pole `RecordVersion` v `Views/Projekty/_EditZaznamForm.cshtml`, plněné z `ZaznamEditViewModel.RecordVersion`.
2. `SaveRecordCommand.RecordVersion`.
3. `RecordVersionQuery.ResolveVersionTokenAsync` — `MAX(id)` auditních řádků záznamu.
4. Kontrola v `SaveRecord` hned po načtení záznamu: liší-li se token, save se odmítne kódem `RECORD_STALE`. Prázdný token (nový záznam, formulář z doby před nasazením) kontrolu přeskočí.

**Žádný bump se nikam nedoplňuje.** Audit se zapisuje i při uložení, které mění pouze harmonogram, takže se verze posune sama — původně plánovaný trik s `Entry(entity).Property(...).IsModified` je tím bezpředmětný a byl ze zadání vypuštěn. Stejně tak odpadá jakákoli změna schématu: **fáze 1 je nasaditelná bez SQL skriptu.**

Cenou je jediná závislost: každá budoucí uživatelská zápisová cesta musí auditovat. Hlídá to integrační test `AutomatScheduleWrite_ShouldNotChangeRecordVersion` spolu s dvojicí testů na odmítnutí a průchod čerstvé verze.

### 5.3 Hláška

> Záznam mezitím uložil jiný uživatel: **Novák Jan** (17. 9. 2026 14:12). Vaše změny nelze uložit přes cizí verzi — načtěte záznam znovu.
> [Obnovit stránku]

Jméno a čas se dotáhnou z auditní tabulky `AuthzAuditLog` (`Services/Audit/AuditModels.cs:104-126`): poslední řádek s `EntityType = "zaznam"`, `EntityId = {id}`, `Action IN ('update','approve')`, seřazeno podle `CreatedAt` sestupně. Když se autor nedohledá, hláška jméno vynechá — nesmí kvůli tomu spadnout.

Kód `RECORD_STALE` se přidá do seznamu, u kterého `ajax.js:232-246` vykresluje tlačítko **Obnovit stránku**.

### 5.4 Obnova tokenu po uložení není potřeba — ověřeno

Nabízela se obava, že po AJAX uložení zůstane v otevřeném editoru starý `RecordVersion` a druhé uložení spadne na vlastní předchozí zápis. **Nestane se to:** `BuildSaveAjaxSuccessResult` vrací `refreshScope: "page"` (`ZaznamyController.Commands.cs:158-180`) a klient na to reaguje plnou navigací — `refreshPageScope` → `window.location.assign(refreshUrl)` (`wwwroot/js/modules/recordRefresh.js:351-375`). Stránka se přenačte a token se vyrenderuje čerstvý.

Guard test to přesto pohlídá: po úspěšném save musí odpověď nést `refreshScope = "page"`. Kdyby se editor někdy překlopil na in-place refresh, test spadne a obnova tokenu se doplní.

> **Doplněno 2026-09-23:** úvaha výše platí jen pro **Uložit**. V editoru je ještě tlačítko „Doplnit identifikátor z jednání“, po kterém se editor nepřenačte (`refreshScope: "record-card"` na stránce editoru žádnou kartu nenajde). Řeší to nález N5 v §11 — tahle akce verzi neposouvá.

---

## 6. Odstranění falešných konfliktů

### 6.1 Smazat `ScheduleVersion`

Ruší se celý mechanismus:

| Co | Kde |
|---|---|
| Kontrola | `RecordService.SaveRecord.cs:699-720` (blok „F-11 soft concurrency check"; zbytek `ValidateScheduleValuesAsync` — pořadí 1–10, duplicity, chronologie — zůstává) |
| Výpočet verze | `ProjectService.RecordEditorComposition.cs:216-227`, `ProjectService.ScheduleBlockComposition.cs:37` |
| Hidden input | `Views/Shared/_ScheduleBlock.cshtml:44-46` |
| Pole view modelu | `Models/ViewModels/Projekty/ProjektHarmonogramTabViewModels.cs:74` |
| Pole commandu | `Models/ViewModels/Commands/RecordCommands.cs` (`ScheduleVersion`) |
| Prefix v close-guardu | `wwwroot/js/modules/recordEditor/form.js:337` — vypustit `"ScheduleVersion"` ze `scheduleSnapshotKeyPrefixes` |
| Klon v návrzích | `Services/RecordProposalService.Queries.cs:524` — s polem zaniká i evidovaná chyba *„ScheduleVersion se nepřenáší v CloneScheduleBlock"* z bugfix sprintu 2026-04-16 |

### 6.2 Opravit ruční režim u auto-eligible kroků (R6)

Dnes uživatel v ručním režimu vyplní datum u kroku 1/3/4/6/7/10, uloží — a hodnota se tiše zahodí (§1.3).

Oprava v `RecordService.SaveRecord.cs:878-882`: filtr `if (!IsManual(mk.Poradi)) continue;` se nahradí podmínkou vázanou na master switch:

```
var isManualStep = HarmonogramManualSteps.IsManual(mk.Poradi);
if (!isManualStep && !rezimManual) continue;   // Auto režim: auto-eligible kroky patří automatu
```

Důsledky, které jsou žádané:
- Zapsané datum nastaví u řádku `SkutecnostRezim = Manual` → automat ten krok **trvale přeskakuje**, dokud uživatel nepřepne zpět na Automatiku.
- Přepnutí zpět na Automatiku vrátí řádky do `Auto` a automat je při dalším harvestu přepíše — což je přesně to, co uživatel tím přepnutím žádá.
- V režimu Automatika se odeslaná manuální data dál defenzivně ignorují (klient je neposílá).

---

## 7. Testovací strategie

| Vrstva | Co pokrýt |
|---|---|
| **Unit** | TTL a re-entrance zámku (`FakeTimeProvider`). Ruční režim: datum u kroku 4 se uloží a nastaví `Manual`; v Auto režimu se ignoruje. |
| **Integration** | Zámek proti reálné DB: dvě `osoba_id`, atomicita `MERGE`, převzetí po expiraci. **Verze se posune** při uživatelském uložení a **neposune se** po zásahu automatu; stará verze je odmítnuta, čerstvá projde. |
| **Api** | `GET Edit` u cizího zámku vrací redirect a hlášku se jménem — assertovat na atributy/ASCII, **ne na český text** (Razor kóduje diakritiku na entity). `RECORD_STALE` payload. |
| **E2E (Playwright)** | Dva uživatelé → druhý se do editoru nedostane. Gov komponenty: klikat `DispatchEventAsync("click")`, čekat na třídu `hydrated`, viditelnost přes `ToHaveCount`/atribut, ne `ToBeVisible`. |

**Regresní test, který musí selhat před opravou:** uložení změny pouze v názvu záznamu poté, co automat sáhl do harmonogramu. Dnes skončí `schedule_stale_data`, po opravě musí projít.

---

## 8. Analýza pracnosti

**Jednotka: hodiny inline exekuce Opus 5** v hlavní session — čtení kódu, psaní, iterace na build/test failech. Mimo odhad zůstává ruční ověření na cílovém Edge/i15 a schvalovací brány; to je práce na tvé straně.

Sloupec *kód* = editace zdrojů, *testy* = psaní testů včetně `dotnet build`/`dotnet test` cyklů. Druhý sloupec je u .NET dominantní — proto je vypsaný zvlášť, aby šel odhad kontrolovat, ne jen věřit.

### 8.1 Fáze 1 — odstranění falešných konfliktů + guard (bez SQL skriptu)

| Položka | Soubory | Kód | Testy | Celkem |
|---|---|---|---|---|
| Smazat `ScheduleVersion` (§6.1) | 7 míst dle tabulky | 0,25 | 0,5 | **0,75 h** |
| Record guard přes audit id (§5) | `_EditZaznamForm.cshtml`, `ZaznamEditViewModels.cs`, `ProjectService.RecordEditorComposition.cs`, `RecordCommands.cs`, `RecordService.SaveRecord.cs`, `RecordLastWriterQuery.cs` (nový), `RecordValidationContracts.cs`, `BaseController.Commands.cs`, `ajax.js` | 0,75 | 0,5 | **1,25 h** |
| Oprava ručního režimu (§6.2) | `RecordService.SaveRecord.cs` | 0,25 | 0,5 | **0,75 h** |
| **Součet fáze 1** | | | | **2,75 h** |

Fáze 1 **nevyžaduje žádnou změnu schématu databáze** — verzí je id posledního auditního zápisu (§5.2). Nasaditelná samostatně.

### 8.2 Fáze 2 — pesimistický zámek

| Položka | Soubory | Kód | Testy | Celkem |
|---|---|---|---|---|
| SQL migrace + otisk v diagnostice | `db_upgrade_1_4_5_record_edit_lock.sql` (nový), `db_check_applied_upgrades.sql` | 0,5 | — | **0,5 h** |
| Entita + EF konfigurace + DbSet | 3 soubory | 0,25 | — | **0,25 h** |
| Service (acquire/heartbeat/release) s atomickým `MERGE` | `Services/Records/RecordEditLockService.cs` (nový) | 0,75 | — | **0,75 h** |
| Napojení na editor + blokovaná cesta + hláška | `ZaznamyController.cs`, `ZaznamyController.Commands.cs`, `_RecordEditLocked.cshtml` (nový) | 0,75 | — | **0,75 h** |
| Heartbeat na keep-alive + release beacon | `AppController.cs`, `session.js`, `recordEditor/editLock.js` (nový), `bootstrap.js` (side-effect import — bez něj tiše spadne celé UI) | 0,75 | — | **0,75 h** |
| Testy (Unit + Integration + Api + E2E dva uživatelé) | 4 testovací projekty | — | 1,25 | **1,25 h** |
| **Součet fáze 2** | | | | **4,25 h** |

E2E se dvěma identitami je tu nejdražší položka — potřebuje fixture pro druhého uživatele a gov komponenty se v Playwrightu chovají neposlušně.

### 8.3 Průřezově

| Položka | Odhad |
|---|---|
| Dokumentace (`docs/technical`, `docs/user-guide.md`), CHANGELOG | 0,25 h |
| Publish balíček (`dotnet publish` + `publish.zip`) | 0,25 h |
| **Součet** | **0,5 h** |

### 8.4 Celkem

| Fáze | Odhad |
|---|---|
| Fáze 1 — falešné konflikty + guard + ruční režim | **2,75 h** |
| Fáze 2 — pesimistický zámek | **4,25 h** |
| Průřezově | **0,5 h** |
| **Celkem** | **7,5 h inline exekuce** |

Oproti revizi 1 (17,75 h) je to necelá polovina — zrušený dialog, zrušená baseline a smazání kontroly místo jejího zjemňování.

### 8.5 Kalibrace odhadu — naměřeno 2026-09-17

**Fáze 1 odhadnuta na 2,75 h, skutečnost 1,05 h (62 min 56 s).** Odhad byl nadsazený **2,6×**.

Co v tom čase bylo: kompletní implementace všech tří částí, 10 nových testů (1 architekturní sada, 3 unit, 6 integračních + 2 Api), ověření, že regresní test bez opravy skutečně padá, a tři plné testovací sady (Unit 1802, Integration 104, Api 371).

Kde odhad ujel:
- **Psaní kódu i testů je výrazně rychlejší, než odhad počítal.** Řádově minuty, ne desetiny hodiny.
- **Testovací cykly opravdu dominují, ale méně, než jsem čekal**: cílený integrační běh 7–10 s, plná integrační sada 4 min 13 s, Api sada 26 s, Unit sada 1 s.
- **Největší jednotlivá položka byla neplánovaná** — chyba v samotné specifikaci (§5.2 tvrdila, že `projektove_zaznamy` má `row_version`; nemá) si vyžádala ověření, návrh náhradního řešení a rozhodnutí uživatele.

Poučení pro fázi 2: **odhad dělit ~2,5** u položek typu „napsat kód + testy". Nekrátit u položek, které čekají na externí běh (plná integrační sada, Playwright) a u položek s neznámou — u zámku je neznámou chování `MERGE` pod souběhem a E2E se dvěma identitami. Realistický odhad fáze 2 po kalibraci: **1,7–2,0 h** místo původních 4,25 h.

### Fáze 2 — naměřeno

**Odhadnuto 4,25 h, po kalibraci 1,7–2,0 h, skutečnost 0,44 h (26 min 9 s).** Původní odhad nadsazený **9,7×**, i překalibrovaný odhad ještě **4×**.

V tom čase: migrace + entita + mapování, `RecordEditLockService` (atomický `MERGE`, TTL, re-entrance), brána v `GET /Zaznamy/Edit` se stránkou „upravuje jiný uživatel", heartbeat přibalený na keep-alive, uvolnění beaconem, sjednocení formátu jména, 20 nových testů (9 architekturních, 6 integračních, 5 Api, 2 E2E) a dokumentace.

Proč byl i kalibrovaný odhad vedle:
- **Obavy se nepotvrdily.** `MERGE` pod souběhem i E2E se dvěma identitami — obojí napoprvé správně. Rezerva na „neznámé" byla zbytečná.
- **Zdržely jiné věci, než se čekalo**: E2E fixture nezakládá žádné záznamy (musel je test vyrobit SQL) a druhá osoba v seedu neexistuje. Dvě kola oprav ≈ 6 minut.
- **Repo se bránilo samo a ušetřilo čas**: `AuthorizationPolicyEnforcementTests` okamžitě chytila, že `ReleaseEditLock` nemá policy ani odůvodnění v allowlistu.

**Pravidlo pro příště: u práce tohoto typu (známý repo, jasná spec, TDD) odhaduj kolem 0,5 h na fázi o 4–5 tascích.** Dominantní položkou zůstává plná integrační sada (5 min 23 s), ne psaní kódu.

---

## 9. Mimo rozsah

- **Ruční převzetí zámku** („Převzít úpravy" pro admina) — TTL 15 min problém řeší; doplnitelné později bez změny schématu.
- **Indikace zámku ve výpisech záznamů** — dotaz navíc nad celým seznamem.
- **Zámek v návrhovém workflow** — kolizi schválení řeší P2.
- **Kolize v jiných entitách** (jednání, komentáře, projekt) — stejný vzor by šel použít, není součástí zadání.

## 10. Rizika

| Riziko | Dopad | Ošetření |
|---|---|---|
| Zaseknutý zámek po pádu prohlížeče | Záznam nejde 15 min editovat | TTL + hláška ukazuje, odkdy zámek běží |
| Dva taby téhož uživatele | Druhý tab projde zámkem (re-entrance) a spadne až na P2 | Správné chování, ale hláška musí být srozumitelná i pro „sám sobě" |
| Budoucí uživatelská zápisová cesta bez auditu | Guard ji neuvidí, cizí zápis se tiše přepíše | Audit je v tomto kódu povinný u všech zápisů do záznamu; hlídá integrační trojice testů (§7) |
| Nasazení bez spuštění SQL skriptu (fáze 2) | Editor spadne na chybějící tabulce | **Fail-fast místo původně zamýšleného fail-soft:** `SqlStartupValidatorHostedService` tabulku vyžaduje a hláška jmenuje `db_upgrade_1_4_5_record_edit_lock.sql`. Aplikace nenastartuje s polovičním schématem — to je v souladu se zbytkem guardu a operátor se to dozví hned, ne až od uživatele |
| Ruční režim po opravě zmrazí krok natrvalo | Uživatel se diví, že automat přestal doplňovat | Je to žádané chování (R3); patří do uživatelské dokumentace |

---

## 11. Review po implementaci — 2026-09-18

Zpětná kontrola hotového kódu. Čtyři nálezy, všechny opravené a pokryté testem; pátý (N5) přibyl 2026-09-23.

### N1 — Kontrola příslušnosti k projektu přestala platit (vážné)

Guard verze se ve `ValidateSaveRecordAsync` vložil doprostřed existujícího
`if / else if` řetězu a odsunul kontrolu `record_project_mismatch` do větve, která se
při vyplněné verzi nikdy nevyhodnotí. Tahle kontrola je přitom **jediná vazba mezi `Id`
záznamu z formuláře a `ProjektId`, proti kterému controller ověřuje oprávnění** — bez ní
by právo `records.edit` na jednom projektu otevřelo záznamy všech ostatních. Částečnou
záchranou zůstávala validace subsystému, ta ale padá, jakmile oba projekty sdílejí subsystém.

Oprava: mismatch větev je zpátky **před** kontrolou verze a v kódu ji drží komentář o pořadí.
Regrese: `SaveRecord_ShouldRejectForeignProject_EvenWhenRecordVersionIsCurrent`
(bez opravy selže na chybějícím `record_project_mismatch`).

### N2 — Hláška mohla pojmenovat nesprávného člověka

`RecordVersionQuery` a `RecordLastWriterQuery` četly auditní řádky každý jinak (jiný filtr
akcí, jiné řazení), takže hláška mohla jmenovat autora jiného řádku, než který byl verzí —
nebo nikoho.

Oprava: obě místa berou řádky ze sdíleného `RecordVersionQuery.VersionRows` a poslední podle
`id`. Jméno formátuje sdílený `PersonDisplayNameQuery`. Regrese (po N5 přepsaná):
`StaleMessage_ShouldNameAuthorOfVersionRow_NotLaterMeetingAssign`.

### N3 — Chybějící migrace se projevila až za běhu

Riziko z §10 zůstalo neošetřené. Místo původně zamýšleného fail-soft je teď fail-fast
ve startovním guardu, s hláškou, kterou operátor zkopíruje do sqlcmd.
Test: `StartovniGuard_VyzadujeTabulkuZamku_APojmenujeSkript`.

### N4 — Drobnosti

- **Heartbeat shazoval keep-alive.** Volání v `AppController.KeepAlive` nebylo ošetřené;
  selhání zámku by uživateli zobrazilo „relace vypršela". Teď best-effort s logem —
  následek neúspěchu je jen vypršení zámku po TTL.
- **PK zámku bral EF jako IDENTITY.** Konvence EF označí celočíselný klíč za
  store-generated; sloupec ale IDENTITY není. Doplněno `ValueGeneratedNever()` + test.
- **Stránka zamčeného záznamu neměla rám ani styly.** Doplněn `card` wrapper jako u editoru
  a tři pravidla do `site.css` (třídy v šabloně existovaly, v CSS ne).
- **Zbytkové komentáře o `row_version`** z opuštěné varianty přepsány na audit-id.

### N5 — Doplnění identifikátoru zablokovalo vlastní uložení (nalezeno 2026-09-23)

Nalezeno při sepisování ručních testů. Tlačítko „Doplnit identifikátor z jednání“ je **uvnitř
editoru**; akce zapíše audit `assign`, a ten posouval verzi. Editor se po ní nepřenačte
(`refreshScope: "record-card"` na stránce editoru nic nenajde), takže držel starou verzi a další
Uložit skončilo hláškou „uložil jiný uživatel: (uživatel sám)“ — přesně ten falešný konflikt,
kvůli kterému celá práce vznikla. Zavedla to Fáze 1.

Oprava: `assign` verzi neposouvá. Je to bezpečné, protože uložení editoru identifikátor
nepřepíše — doplňuje ho jen když je prázdný a entitu čte čerstvě z DB. Pravidlo pro budoucí
akce je záměrně asymetrické: vyřadit se smí jen akce, jejíž zápis editor prokazatelně
nepřepíše; cokoli jiného verzi posouvá, horší případ je tak zbytečný konflikt, ne ztracená data.
Regrese: `SaveRecord_ShouldSucceed_AfterMeetingIdentifierWasAssignedFromOpenEditor` (jde přes
skutečnou službu a hlídá i předpoklad, že identifikátor uložení přežije).

Mimo rozsah (starší chování, nezaviněné touto prací): editor po doplnění dál ukazuje staré
číslo i tlačítko „Doplnit identifikátor“, dokud se stránka nepřenačte.

### Co review prověřilo a nechalo být

- **Atomicita `MERGE … WITH (HOLDLOCK)`** a filtr na `osoba_id` u heartbeatu i uvolnění —
  cizí zámek nelze prodloužit ani smazat.
- **Fail-open, když řádek zámku mezi `MERGE` a čtením zmizí** — správná volba, doplněn komentář.
- **`Save` zámek needitorsky nekontroluje** — záměrně: backstopem je guard verze. Ověřeno,
  že uvolnění po uložení nemůže sebrat cizí zámek a že klient po uložení z editoru odchází
  (`refreshScope: "page"`), takže uvolnění nepřijde uprostřed rozdělané práce.
- **Chybějící antiforgery na `ReleaseEditLock`** — aplikace token nevaliduje globálně a akce
  maže výhradně vlastní zámek volajícího.

