# Projektový dashboard — přestavba: prodlení podle penalizačního algoritmu, nový vzhled, Statistiky

**Stav:** návrh k odsouhlasení (2026-10-15, revize 2026-10-19: zdroj předávek = databáze ServiceDesku, rozhodnutí §16 uzavřena)
**Navazuje na:** [2026-06-28 Základní report](2026-06-28-projekt-dashboard-zakladni-report-design.md) (provider pattern + ECharts), [dashboard-prodleni.md](../../specs/dashboard-prodleni.md) (původní definice prodlení — **touto specifikací nahrazena**)
**Referenční zdrojáky** (root repa, v `.gitignore`): `Hotline.cs` = BusinessLayer, výpočet `PenaltyCalculation.TicketPenaltyCalculator` ř. 1327–1967; `Hotline (2).cs` = DataLayer, třída `DataLayer.Hotline` (zdroj předávek a SLA, `MOSD_External`, `TicketExternal_SlaCache`); `Details.cshtml` = detail ticketu

---

## 1. Cíl

1. Záložku **NES v prodlení** pohání stejný algoritmus, jakým počítá penále starý ServiceDesk. Dnešní naivní rozdíl data od termínu se nahradí.
2. Celý projektový dashboard má vypadat jako hotový produkt, který jde ukázat vedení: souhrnný pás nahoře, čisté záložky, karty, barvy z gov DS a funkční tmavý režim.
3. Záložka **Statické informace** se přejmenuje na **Statistiky** a stane se z ní report pro projektového manažera ve stylu Power BI: KPI dlaždice, průběh práce, termíny, lidé a ServiceDesk s dodavateli.

Záložky **Záznamy** a **NES v prodlení** zůstávají pod stejnými názvy, mění se jejich obsah a vzhled.

## 2. Výchozí stav (co jsem našel v kódu)

| Oblast | Stav | Důsledek |
|---|---|---|
| NES v prodlení | `SqlInformacniSystemQueryService.GetProdleneAsync`: NES `sla_deadline < dnes`, PMP/PNF `dat_res_t < dnes`, dny = kalendářní rozdíl | Nepočítá s obrátkami u dodavatele, se zastavením hodin po vrácení, pracovními dny, svátky ani schváleným prodloužením. Čísla neodpovídají penále ze ServiceDesku. |
| NES v prodlení — vzhled | Třídy `.pm-nes-panel*` nemají v CSS žádné pravidlo | Holá HTML tabulka |
| Statické informace | `_StatisticsPanel.cshtml` + `BuildStatisticsPanelAsync` | Natvrdo `Zruseno = 0`, `VProdleni = 0` per subsystém, `IsServiceDeskIntegrated = false`. KPI karty mají třídy `.dashboard-kpi-number/-label`, CSS ale definuje `.kpi-value/.kpi-label`, takže jsou nestylované. |
| Základní report (ECharts) | Hotový framework: `Services/ProjectDashboard/Zakladni/` (9 providerů, `ChartData`, `echartsRender.js`), endpoint `zakladni-report` | **Není zapojený do žádné záložky.** Nikdo ho nevidí. |
| Záložky | Vlastní `<button class="dashboard-tab">` + `projectDashboard.js` | Jiný vzhled než systémový `pm-tabs` jinde v aplikaci |
| Grafy | ECharts s výchozí paletou | Barvy mimo gov DS, tmavý režim se nepřebarví, chybí tabulkový pohled |

## 3. Rozsah

**V rozsahu**
- Port penalizačního algoritmu z `Hotline.cs` (NES, PMP, PNF) do PM Trackeru jako čistá, testovatelná doménová služba.
- Čtení workflow událostí U1–U11 a SLA hodin z databáze ServiceDesku (§6), druhý read-only connection string.
- Přestavba shellu dashboardu: hlavička, souhrnný pás, `pm-tabs`.
- Nový obsah a vzhled záložek Záznamy, NES v prodlení a Statistiky.
- Rozšíření Excel exportu záložky NES v prodlení o penalizační sloupce.
- Odstranění legacy statistik (`_StatisticsPanel`, `BuildStatisticsPanelAsync` a jejich VM, CSS a JS).
- Aktualizace dokumentace, která dosud tvrdí, že penalizace je trvale mimo rozsah.

**Mimo rozsah**
- Penále v **korunách**: ukazujeme penalizované dny, ne částky.
- PDF/tiskový export reportu (zůstává jen tisk přes prohlížeč a `@media print`).
- Historie snímků prodlení v čase (sparkline „vývoj za 30 dní"). Engine ale umí datovat penalizované dny, takže měsíční graf penalizovaných dnů v období je v rozsahu (§11).
- Úpravy osobního dashboardu (`/Dashboard`), který je jiná stránka.

## 4. Změna dřívějších rozhodnutí

| Dřívější rozhodnutí | Kde | Nově |
|---|---|---|
| „Penalizační logika (pracovní dny, svátky, U1–U11) je **trvale mimo scope** PM Trackeru" (2026-04-23) | `docs/specs/dashboard-prodleni.md` §Definice prodlení, `docs/technical/12-servicedesk-schema-reference.md` §5.5, §5.8, §7 | Penalizace **dnů** je v rozsahu, penále v Kč zůstává mimo. Dokumenty se upraví a starý spec dostane banner „nahrazeno". |
| Metrika prodlení = `DATEDIFF(DAY, termín, dnes)` | tamtéž | Metrika = počet **penalizovaných dnů** podle algoritmu (§5) |
| Statistiky = roční KPI + čtvrtletní tabulka | `_StatisticsPanel` | Statistiky = Základní report rozšířený o nové grafy (§11) |
| Strukturovaná workflow data ServiceDesku (`TransferType` 1–11) nepoužívat, spolehlivé jsou jen texty vyjádření | spec 2026-04-21 §8.1, §11 | Pro **výpočet penále** se `ticket_external_workflow` používá, protože z ní počítá penále i starý ServiceDesk a cílem je shoda 1:1. Vytěžování vyjádření do harmonogramu zůstává textové beze změny. |

## 5. Penalizační algoritmus (popis pro lidi)

### 5.1 Události (workflow ticketu)

| Kód | Událost | Parametr |
|---|---|---|
| U1 | Založení ticketu u dodavatele | u PMP/PNF termín plnění |
| U2 | Předání ticketu dodavateli | u PMP/PNF termín plnění |
| U3 | Převzetí ticketu od dodavatele (konec obrátky) | — |
| U5 | Archivace u dodavatele | — |
| U10 | Žádost dodavatele o prodloužení termínu | — |
| U11 | Schválení změny termínu projektovým manažerem MO | nový termín |

**Obrátka** = úsek od U1/U2 do nejbližšího následujícího U3. Bez U3 je ticket stále u dodavatele a obrátka končí „teď".

### 5.2 Pravidla podle typu

**NES — řízení časem SLA, potom dny**
- Ticket dostane SLA hodiny podle `dulezitost` z tabulky `ticket_external_sla` (§6.1, §5.4 body 5–6).
- Každá obrátka odečte ze zbývajícího SLA odpracovaný čas. Počítají se celé pracovní dny (24 h), víkendy a státní svátky se přeskakují. Když je ticket zpátky u nás, hodiny stojí.
- Když obrátka SLA vyčerpá, deadline je okamžik vyčerpání. Penalizují se **pracovní dny** od deadlinu do konce obrátky a ticket přechází do režimu „řízen dny".
- V režimu „řízen dny" se každá další obrátka penalizuje celá, od dne předání do dne vrácení.

**PMP — termín plnění, kalendářní dny**
- Deadline obrátky = termín plnění z U1/U2. Penalizují se **kalendářní dny** od deadlinu do konce obrátky.

**PNF — termín plnění, pracovní dny**
- Stejně jako PMP, ale penalizují se **pracovní dny**.

**Společná pravidla**
- Započítává se i započatý den („i započatý pracovní den" podle smlouvy), jeden den se nepenalizuje dvakrát.
- Termín prodloužený do půlnoci (23:59:59) se penalizuje až od dalšího dne.
- **Prodloužení:** U11 posouvá deadline obrátky na schválený termín; když mu předchází U10 v rámci obrátky, platí varianta T11/T12.
- **Prominutí (T14):** na konci výpočtu se smažou všechny penalizované dny ≤ nejvyššímu schválenému termínu z U11.
- **Více dodavatelů:** když se v událostech střídají dodavatelé, workflow se rozdělí a penále se počítá pro každého dodavatele zvlášť (T00).

### 5.3 Odůvodnění (kódy T)

Engine ke každému ticketu vrací strukturovaný seznam důvodů a UI z nich skládá česká souvětí:

| Kód | Význam |
|---|---|
| T00 | Ticket přešel mezi dodavateli, počítá se zvlášť |
| T01 | NES obrátka proběhla v rámci SLA (zbývalo X h, spotřebováno Y h) |
| T02 | Obrátka nedoběhla, ticket je u dodavatele, počítá se do teď |
| T03 | Obrátka proběhla včas |
| T04 | Obrátka nebyla včas, penalizováno od data předání (bez termínu nebo po vyčerpání SLA) |
| T05 | Obrátka nebyla včas, penalizováno od deadlinu |
| T06/T07 | Prodloužení bez žádosti: včas / pozdě |
| T08 | Deadline pro výpočet prodloužení nastaven na datum předání |
| T11/T12 | Prodloužení po žádosti: konec obrátky včas / pozdě |
| T13 | Neznámá kombinace parametrů (chyba dat) |
| T14 | Prominutí do schváleného termínu |

T09/T10 (dodavatel nestihl požádat o prodloužení včas) je v originálu **vypnuté** a zůstane vypnuté.

### 5.4 Věrnost portu

Port je **1:1** včetně zvláštností originálu. Odchylky jsou jen ty vyjmenované v §16. Zvláštnosti, které přenášíme beze změny:
1. PMP počítá kalendářní dny, NES a PNF pracovní.
2. Hledání prodloužení (`GetProlongationSegment`) najde U11 jen tehdy, když po obrátce existuje U3. U ticketu, který je stále u dodavatele, prodloužení uplatní až závěrečné prominutí T14.
3. U NES s parametrem `beforebound = true` se U11 hledá i před aktuální obrátkou.
4. „Pracovní čas" znamená celý pracovní den 00:00–24:00, ne pracovní doba 8–16.
5. SLA hodiny se hledají **přesnou shodou** textu `HOT_ZAZNAMY.dulezitost` s `ticket_external_sla.sla_name` (bez ořezu mezer, rozlišují se velká a malá písmena).
6. SLA hodiny (`double`) se převádějí na celé hodiny přes `Convert.ToInt32`, tedy zaokrouhlením na nejbližší celé číslo, při .5 na sudé (2,5 → 2; 3,5 → 4).

Technické změny, které chování nemění: `DateTime.Now` se nahradí injektovaným `TimeProvider`, string důvody strukturovanými záznamy, celočíselné dělení v textu T01 správným výpočtem hodin (týká se jen zobrazení) a mutace přes `ref` čistými návratovými hodnotami. Statická SLA cache originálu (načte se jednou za běh aplikace) se nahradí `IMemoryCache` s platností 1 hodina.

## 6. Zdroj workflow událostí a SLA hodin (uzavřeno 2026-10-19)

Zjištěno z datové vrstvy starého ServiceDesku (`Hotline (2).cs`, třída `DataLayer.Hotline`, v rootu repa a v `.gitignore`). ServiceDesk používá **dvě databáze**:

| Connection string starého SD | Databáze | Co z ní čte |
|---|---|---|
| `HotlineDB` | `intranetNEW` | tickety `HOT_*`, už napojeno přes `TicketingReadOnly` |
| `ServiceDeskEntities` (EF6) | vlastní databáze ServiceDesku | předávky a SLA hodiny |

V `intranetNEW` workflow tabulka není (discovery `SD_servicedesk/01_discovery.sql` §7, 399 tabulek).

### 6.1 Tabulky v databázi ServiceDesku

| Tabulka | Sloupce, které použijeme | Význam |
|---|---|---|
| `ticket_external` | `id`, `external_id`, `sla_name`, `supplier_tag` | Ticket synchronizovaný s dodavatelem. `external_id` = `HOT_ZAZNAMY.id` jako text, páruje se po `Trim()` (`MOSD_External.ExtendingTicketsWithExternal`). |
| `ticket_external_workflow` | `date`, `supplier`, `transfertype`, `dt_param` + cizí klíč na `ticket_external` | Předávky U1–U11. `dt_param` je termín plnění (U1/U2 u PMP/PNF) nebo schválený nový termín (U11). |
| `ticket_external_sla` | `sla_name`, `sla_hours` | SLA hodiny podle důležitosti. `sla_name` obsahuje hodnoty `HOT_ZAZNAMY.dulezitost` (NES I, NES II, NES III, Vada A–D). |

Přesné názvy a typy sloupců, hlavně cizího klíče ve `ticket_external_workflow`, se ověří dotazem do `sys.columns` (plán, Task 0), protože zdroják mapuje přes EF `.edmx`, který nemáme.

### 6.2 Konfigurace a přístup

- Nový connection string `ConnectionStrings:ServiceDeskExternalReadOnly` v `appsettings.json` a nastavení `Ticketing:ExternalConnectionStringName`. Hodnotu i čtecí právo loginu PM Trackeru na tři tabulky zajišťuje správce. PM Tracker se k serveru nijak neváže.
- Bez connection stringu nebo při `Ticketing:Enabled = false` se použije `Disabled*` fallback a záložka NES ukáže placeholder (§13).
- Kontext `ServiceDeskExternalReadOnlyDbContext` je read-only stejně jako `TicketingReadOnlyDbContext` (`SaveChanges` vyhodí výjimku).

### 6.3 Které tickety se hodnotí

- **Jen tickety synchronizované s dodavatelem**, tedy ty, které mají řádek v `ticket_external`. Ostatní se neřeší a **nikde se nezobrazují** (rozhodnutí 2026-10-19).
- Ticket patří do IS přes moduly (`HOT_MODULY.id_IS`), stejně jako dnes.
- Bez `HOT_ZAZNAMY.id` je ticket mimo rozsah.

## 7. Architektura

```
HOT_ZAZNAMY (intranetNEW)                 ticket_external + _workflow + _sla (DB ServiceDesku)
   │  TicketingReadOnlyDbContext              │  ServiceDeskExternalReadOnlyDbContext
   └──────────────┬───────────────────────────┘
                  ▼
   IProdleniQueryService (ServiceDesk.Sql)   ← tickety IS spárované s ticket_external, jejich předávky, SLA katalog
                  ▼
   PenaltyCalculator (port TicketPenaltyCalculator)   ← čistá funkce: ticket + události + SLA hodiny + čas
       └─ ICeskeSvatky (port MOSD_External.GetCzechHolidays: pevné svátky + Velký pátek od 2016 + Velikonoční pondělí)
                  ▼
   ProdleniService (Web)                     ← skládá, třídí, cache 10 min per (IS, den)
       ├─ NES v prodlení záložka (seznam, hrozí, nelze vyhodnotit, souhrn per dodavatel)
       ├─ Souhrnný pás dashboardu (počty)
       ├─ Statistiky — sekce ServiceDesk (penalizované dny v období)
       └─ Excel export
```

**Umístění**
- `PmTracker.ServiceDesk.Contracts`: `IProdleniQueryService`, DTO `ProdleniTicketDto`, `WorkflowUdalostDto`, `SlaKatalogDto`, `AkceptovanaKalkulaceDto`.
- `PmTracker.ServiceDesk.Sql`: `ServiceDeskExternalReadOnlyDbContext` + entity `TicketExternalEntity`, `TicketExternalWorkflowEntity`, `TicketExternalSlaEntity`; `SqlProdleniQueryService` + `DisabledProdleniQueryService`.
- `PmTracker.Web/Services/ServiceDesk/Prodleni/`: `WorkflowUdalost`, `PenaltyCalculator`, `PenaltyResult`, `PenaltyReason`, `PenaltyReasonFormatter`, `CeskeSvatky`, `ProdleniService`, `ProdleniPrehled`.

**Výkon**
- Tickety: z `intranetNEW` nearchivované tickety IS (pro Statistiky i archivované v období), z databáze ServiceDesku `ticket_external` s `external_id` v této množině. Spojení proběhne v paměti, protože jde o dvě databáze.
- Předávky: jeden dotaz `WHERE <FK> IN (…chunk ≤ 1 000…)`.
- SLA katalog: celá tabulka `ticket_external_sla` (desítky řádků), cache 1 h.
- Výsledek `ProdleniPrehled` se cachuje v `IMemoryCache` po dobu 10 minut, klíč je `(isId, referenční den, rozsah)`. Projekty sdílející IS sdílejí cache.

**Referenční čas:** lokální čas serveru přes `TimeProvider` (stejně jako `ProjectDashboardController`). Originál pracuje s `DateTime.Now` i s daty z obou databází jako s lokálním časem. Ověří se na vzorku: `ticket_external_workflow.date` předání proti času odpovídajícího vyjádření v `HOT_VYJADRENI`.

## 8. Dashboard — celková podoba

```
┌──────────────────────────────────────────────────────────────────────────────┐
│ Projekty › EIS › Dashboard                                                    │
│ Dashboard – Ekonomický informační systém         [Realizace] [IS: EIS]  ← Zpět │
├──────────────────────────────────────────────────────────────────────────────┤
│ ┌───────────────┐ ┌───────────────┐ ┌───────────────┐ ┌───────────────┐      │
│ │▌ 7            │ │▌ 4            │ │▌ 5   · 38 dní │ │▌ 3            │      │
│ │  Úkoly po     │ │  Čeká na      │ │  Tickety v    │ │  Hrozí        │      │
│ │  termínu   ⚠  │ │  skutečnost   │ │  prodlení  ⚠  │ │  prodlení     │      │
│ └───────────────┘ └───────────────┘ └───────────────┘ └───────────────┘      │
├──────────────────────────────────────────────────────────────────────────────┤
│  Záznamy │ NES v prodlení │ Statistiky                    (pm-tabs)           │
│ ─────────┴────────────────┴──────────────────────────────────────────────── │
│  … obsah záložky (lazy-load) …                                               │
└──────────────────────────────────────────────────────────────────────────────┘
```

- **Hlavička:** název projektu, stav jako `gov-tag`, chip s napojeným IS (nebo „bez napojení na IS" s odkazem), tlačítko Zpět. Breadcrumbs zůstávají.
- **Souhrnný pás:** čtyři dlaždice s aktuálním provozním stavem, nezávislým na období. Načítá se lazy z nového endpointu `GET /projekty/{id}/dashboard/souhrn`.
  - Dlaždice nese velké číslo, popisek a ikonu stavu (nikdy jen barvu) a vlevo akcentní pruh ve stavové barvě. Když je hodnota 0, zobrazí se neutrálně s fajfkou.
  - Klik na dlaždici přepne záložku a nastaví filtr (např. „Úkoly po termínu" otevře Záznamy s filtrem V prodlení).
  - Dlaždice se zobrazí jen tomu, kdo má oprávnění k příslušné záložce. Bez napojení na IS se ticketové dlaždice nevykreslí.
- **Záložky:** systémové `pm-tabs` (`persist="storage"`, `persist-key="dashboard-{projektId}"`). Lazy-load obsahu na `pm-tab-change`. Odkaz `?dashTab=` funguje dál.

## 9. Záložka Záznamy

Obsah zůstává (úkoly s problematickými kroky harmonogramu: V prodlení / Čeká na skutečnost / Blíží se termín), mění se vzhled a ovládání:
- **Filtrační chipy s počty:** „Vše 12 · V prodlení 5 · Čeká na skutečnost 4 · Blíží se termín 3". Prázdná kategorie je zašedlá.
- **Tabulka v kartě:** sticky hlavička. Řádek má vlevo 3px pruh ve stavové barvě kategorie a vedle `gov-tag` s textem. Odchylka je tučně „+14 dní" a stejnou barvou jako kategorie.
- **Rozbalení řádku:** detail kroků zůstává a přibydou odkazy „Otevřít záznam" (`/Zaznamy/Detail/{id}`) a „Harmonogram".
- **Prázdný stav:** ikona fajfky a věta „Všechny úkoly jsou v termínu" místo šedého textu.
- Bez změny backendu, kromě počtů pro chipy a dlaždice.

## 10. Záložka NES v prodlení

```
┌ KPI ───────────────────────────────────────────────────────────────────────┐
│ [5 v prodlení u dodavatele] [38 penalizovaných dnů] [3 hrozí prodlení] [1 nelze] │
└────────────────────────────────────────────────────────────────────────────┘
┌ Penalizované dny podle dodavatele ──────┐ ┌ V prodlení podle typu ────────┐
│ Dodavatel A ████████████████ 24         │ │ NES ██████ 3                   │
│ Dodavatel B ███████ 11                  │ │ PMP ██ 1                       │
│ Dodavatel C ██ 3                        │ │ PNF ██ 1                       │
└─────────────────────────────────────────┘ └────────────────────────────────┘
[Vše] [NES] [PMP] [PNF]                                            [Export do Excelu]
┌────────────────────────────────────────────────────────────────────────────┐
│ Ticket  Typ         Stručně        Dodavatel  U dodavatele od  Termín  Dny ▼│
│ #123456 NES · Vada B Chyba výpočtu  Dod. A     01.10. 09:12     02.10.  12   │
│   ▸ Proč: 1) Předáno 01.10. 09:12, SLA 16 h vyčerpáno 02.10. …            │
│          2) Prodloužení schváleno do 10.10. — dny do 10.10. prominuty     │
│          [časová osa: U2 ●──────────● dnes]                               │
└────────────────────────────────────────────────────────────────────────────┘
▸ Hrozí prodlení (3)       ▸ Nelze vyhodnotit (1)
```

**Kdo je „v prodlení"** (rozhodnutí 2026-10-19): synchronizovaný ticket IS, který je **právě u dodavatele** (poslední předávka je U1/U2 bez následného U3) a má penalizované dny > 0. Ticket vrácený zpět k nám v prodlení není, i když mu z minulých obrátek penále zůstalo. Pravidlo platí pro NES i PMP/PNF, protože algoritmus počítá dny jen za dobu u dodavatele.
- Zobrazený počet dnů = všechny penalizované dny ticketu u daného dodavatele (i z dřívějších obrátek), stejně jako sloupec „Dní penále" ve ServiceDesku.

**Hrozí prodlení:** ticket je u dodavatele a ještě nemá penále. Pro NES zbývá ≤ 48 h SLA, pro PMP/PNF zbývá do termínu ≤ 5 dní. Prahy jsou převzaté z filtrů starého ServiceDesku (`GetCounterHodinyKReseni`, `GetCounterDnyKReseni`).

**Nelze vyhodnotit:** synchronizovaný ticket u dodavatele, u kterého engine vrátil chybu: „nemá iniciační událost (U1)", „důležitost nemá záznam v `ticket_external_sla`" (§16) nebo „neznámý typ". Je vidět s důvodem, aby chyba dat nezmizela potichu.

**Sloupce:** ticket (odkaz do ServiceDesku), typ + důležitost (`gov-tag`), stručně, dodavatel, u dodavatele od (datum poslední předávky), termín (SLA deadline nebo termín plnění, případně poznámka „prodlouženo do"), penalizované dny s nápovědou pracovní/kalendářní.
- Řadí se podle dnů sestupně.
- Více dodavatelů na ticketu = jeden řádek na dodavatele, s hvězdičkou a vysvětlivkou T00 (stejně jako Excel ze ServiceDesku).
- Rozbalení řádku ukáže **časovou osu obrátek** (předání → převzetí, prodloužení, dnešek) a **odůvodnění** složené z kódů T.

**Grafy:** horizontální sloupce v jednom odstínu, seřazené sestupně. Dva malé grafy vedle sebe, na úzké obrazovce pod sebou.

**Excel export** přidá sloupce: Penalizované dny, Typ dní (pracovní/kalendářní), U dodavatele od, Odůvodnění (komentář buňky), Penalizované dny (seznam dat v komentáři), Chyba výpočtu.

**Bez napojení na IS:** placeholder zůstává, jen v novém vzhledu (karta s ikonou a odkazem na nastavení IS).

## 11. Záložka Statistiky

Základ je existující **Základní report** (`ZakladniReportBuilder`, providery, `ChartData`, ECharts). Legacy panel se maže. Nahoře je výběr období (rok, kvartál) a vedle něj popisek „Srovnání s: předchozí období stejné délky".

### 11.1 Rozvržení

```
┌ Období: [2026 ▾] [celý rok ▾] ───────────────────────────────────────────────┐
├ PŘEHLED ─────────────────────────────────────────────────────────────────────┤
│ [Otevřené úkoly 42  z toho 7 po termínu] [Dokončeno 63  ▲12 %] [Včasnost 71 % ▼4 b.] │
│ [Ø posun termínu 9,4 dne ▲1,2]  [Tickety v prodlení 5 · 38 dní]  [Rozpočet IS 64 %]   │
├ PRŮBĚH PRÁCE ────────────────────────────────────────────────────────────────┤
│ ┌ Burn-up: založeno vs. dokončeno (kumulativně) ┐ ┌ Stav úkolů (donut) ─────┐│
│ └───────────────────────────────────────────────┘ └─────────────────────────┘│
│ ┌ Stav úkolů podle subsystému (skládaný sloupec) — celá šířka ─────────────┐ │
├ TERMÍNY ─────────────────────────────────────────────────────────────────────┤
│ ┌ Dodržení termínů podle subsystému (původní vs. poslední, %) ┐ ┌ Ø posun ┐  │
│ ┌ Stáří otevřených úkolů (0–30 / 31–90 / 91–180 / 181–365 / >365 dní) ┐      │
├ LIDÉ A SPOLUPRÁCE ───────────────────────────────────────────────────────────┤
│ ┌ Vytížení vlastníků: otevřené úkoly v termínu / po termínu (top 10) ┐ ┌ Účast na jednáních ┐ │
│ ┌ Vyjádření podle subsystému ┐                                               │
├ SERVICEDESK A DODAVATELÉ  (jen s napojením na IS) ───────────────────────────┤
│ ┌ Otevřené tickety podle typu: u dodavatele / u nás ┐ ┌ Penalizované dny podle dodavatele ┐ │
│ ┌ Penalizované dny po měsících ┐ ┌ Akceptované kalkulace po měsících (Kč) ┐   │
└──────────────────────────────────────────────────────────────────────────────┘
```

Karty grafů jdou dvě vedle sebe (`flex: 1 1 420px`), na úzké obrazovce pod sebou. Grafy s mnoha kategoriemi jsou přes celou šířku.

### 11.2 Katalog vizuálů

„Nový" = nový provider, „existující" = beze změny výpočtu, jen nový vzhled. Všechny úkolové grafy pracují **jen s kategorií Úkol** (`RecordCategoryCodes.TaskShort/Task`). Dnešní dataset bere i Informace a Rozhodnutí, které nemají stav úkolu a zkreslují „Nezahájeno". Je to vědomá oprava, čísla stávajících grafů se tím změní.

| # | Vizuál | Forma | Sdělení | Data | Stav |
|---|---|---|---|---|---|
| 1 | Otevřené úkoly | KPI | Kolik práce leží, kolik po termínu | non-final úkoly k dnešku; po termínu = `DatumUkonceni < dnes` | nový |
| 2 | Dokončeno v období | KPI + trend | Tempo | `DatumDokonceni` v období vs. předchozí období | nový |
| 3 | Včasnost | KPI + trend | Plní se původní plán? | % dokončených v období do původního termínu (`DeadlineDiscipline`) | nový (sdílí výpočet) |
| 4 | Ø posun termínu | KPI + trend | Jak moc se posouvá | z posunutých úkolů dokončených v období | nový (sdílí výpočet) |
| 5 | Tickety v prodlení | KPI | Stav dodavatelů dnes | `ProdleniPrehled` | nový, jen s IS |
| 6 | Rozpočet IS | KPI s ukazatelem | Kolik je vyčerpáno | `GetRozpocetAsync` (limit/čerpání) | nový, jen s IS |
| 7 | Burn-up | Čára, 2 série, 1 osa | Stíháme dokončovat, co přibývá? | kumulativně založené vs. dokončené úkoly po měsících období | nový |
| 8 | Stav úkolů | Donut + součet uprostřed | Poměr hotovo / rozpracováno / nezahájeno / zrušeno | `RecordStatusPieProvider` | existující |
| 9 | Stav podle subsystému | Skládaný sloupec | Kde se práce hromadí | `RecordStatusBreakdownProvider` | existující |
| 10 | Dodržení termínů podle subsystému | Seskupený sloupec, 2 série, % | Kdo plní původní / poslední termín | sloučení `DeadlineOriginalCompliancePerSubsystem` + `DeadlineCompliancePerSubsystem` (stejná jednotka %, jedna osa) | sloučení existujících |
| 11 | Ø posun podle subsystému | Sloupec | Kde se nejvíc posouvá | `DeadlineAvgShiftDaysPerSubsystemProvider` | existující |
| 12 | Stáří otevřených úkolů | Sloupec (5 košů) | Co hnije | dnes − `DatumZalozeni` non-final úkolů | nový |
| 13 | Vytížení vlastníků | Horizontální skládaný sloupec, top 10 + „Ostatní" | Kdo je přetížený, kdo nestíhá | non-final úkoly podle vlastníka, v termínu / po termínu | nový |
| 14 | Účast na jednáních | Sloupec % po měsících | Chodí lidé na jednání? | `Jednani` + `Ucast` (stav PRESENT) | nový |
| 15 | Vyjádření podle subsystému | Sloupec | Kde se komunikuje | `VyjadreniPerSubsystemProvider` | existující |
| 16 | Otevřené tickety podle typu | Skládaný sloupec NES/PMP/PNF × u dodavatele / u nás | Kde tickety leží | kandidáti + poslední událost | nový, jen s IS |
| 17 | Penalizované dny podle dodavatele | Horizontální sloupec | Kdo nejvíc prodlévá | `PenaltyResult.PenalizedDays` v období | nový, jen s IS |
| 18 | Penalizované dny po měsících | Sloupec | Zlepšuje se to? | datované `PenalizedDays` agregované po měsících | nový, jen s IS |
| 19 | Akceptované kalkulace po měsících | Sloupec (Kč) | Kolik se objednává | `HOT_KALKULACE` tickety IS, `datum_akceptace` v období, akceptace podle zákazového seznamu (`SqlTicketingQueryService`) | nový, jen s IS |

**Ze Statistik mizí:** `DeadlineDisciplineStatsProvider` (karty pokryjí KPI 3 a 4) a `VyjadreniStatsProvider` (ø vyjádření na záznam, nízká vypovídací hodnota). Jejich výpočetní logika zůstává sdílená v `DeadlineDiscipline`.

**Trend:** šipka ▲/▼, rozdíl a slovní hodnocení barvou *a* textem („lépe" / „hůř"). Každá metrika má danou polaritu, např. u „Ø posunu" je růst špatně.

**Sekce ServiceDesk bez napojení na IS:** jedna informační karta s odkazem na nastavení IS, žádné prázdné grafy.

### 11.3 Rozšíření datového modelu reportu

- `DatasetRecord` + `KategorieId`, `VlastnikId`.
- `ZakladniDataset` + `Vlastnici` (Id → jméno), `Jednani` (Id, datum) + `Ucasti` (JednaniId, StavKod), `TaskCategoryIds`.
- `ZakladniDataset.ServiceDesk` (`ServiceDeskSlice?`, null bez IS nebo při `Ticketing:Enabled=false`): `ProdleniPrehled` pro období, `IsRozpocetDto`, akceptované kalkulace `(datum, cena)`.
- `Obdobi.Predchozi()`: předchozí období stejné délky (rok → předchozí rok, kvartál → předchozí kvartál, rozsah → rozsah stejné délky těsně před).
- Providery dál **nesahají do DB**. Všechno načte `ZakladniDatasetLoader` jedním průchodem (PM DB + SD služby).

### 11.4 Rozšíření `ChartData`

- `ChartKind` + `Donut`, `HorizontalBar`, `StackedHorizontalBar`, `Kpi`. `Bar` s více sériemi = seskupený.
- `StatCard` + `Trend? (double Delta, string Text, TrendPolarity GoodWhen)`, `Status? (Good|Warning|Critical|Neutral)`, `Progress? (0–1)` pro rozpočet.
- `ChartData` + `Span (Half|Full)`, `Unit` (např. „%", „dní", „Kč") pro osy a tooltipy, `EmptyMessage`.
- `ChartSeries.ColorToken` se začne používat: hodnota je název CSS tokenu, ne hex.

## 12. Vizuální systém

- **Barvy výhradně přes gov DS tokeny** (paměť `feedback_dark_mode_gov_tokens`). Do `:root` v `site.css` přibudou grafové aliasy `--pm-chart-1…4`, `--pm-chart-status-good/warning/critical/neutral`, `--pm-chart-grid`, `--pm-chart-axis-text` namapované na gov rampy (`--color-primary-*`, `--color-secondary-*`, `--color-neutral-*`, `--color-success/warning/error-*`). Žádný hex, žádné nové `data-theme="dark"` pravidlo.
- **Paleta se validuje skriptem** `dataviz/scripts/validate_palette.js` pro světlý i tmavý povrch (CVD ΔE ≥ 8 na sousedních párech). Gov DS má jen modrou, jantarovou a neutrální rampu, proto grafy používají **nejvýš 3 kategorické barvy**. Víc kategorií (dodavatelé, vlastníci) se kreslí jedním odstínem a seřazením, nikdy ne duhou.
- **Stavové barvy** (success/warning/error) jsou vyhrazené pro stav (V prodlení, Hotovo…) a vždy je doplňuje ikona nebo text.
- **ECharts téma:** `echartsRender.js` při inicializaci přečte vyřešené hodnoty tokenů (`getComputedStyle`) a postaví z nich téma. `MutationObserver` na `<html data-theme>` grafy po přepnutí tématu přebarví.
  - Tenké značky, zaoblené konce sloupců 4px, mezery 2px mezi segmenty, čáry 2px.
  - Mřížka a osy tlumené, tooltip na každém grafu, legenda od 2 sérií, přímé popisky jen u donutu a horizontálních sloupců.
  - Jedna osa Y, nikdy dvě.
- **Tabulkový pohled:** každá karta grafu má `<details>` „Zobrazit data" se serverově vykreslenou tabulkou z `ChartData`. Slouží pro přístupnost, tisk i ověření čísel.
- **Karty:** pozadí `--pm-color-background`, okraj `--pm-color-border`, radius `--pm-radius-m`, padding `--pm-spacing-l`. Nadpis 14px semibold, pod ním jednořádkový insight v `--pm-color-text-muted`.
- **Edge na i15** (paměť `feedback_i15_edge_css_compat`): nosný layout jen přes flex a flex-wrap, žádné `:has`/`max-content` jako nosné. CSS jde do nového `wwwroot/css/components/project-dashboard.css`, které se servíruje s `charset=utf-8`.
- **Tisk:** `@media print`: karty `break-inside: avoid`, souhrnný pás a záložky se skryjí, aktivní panel se vytiskne.

## 13. Chyby a degradace

| Situace | Chování |
|---|---|
| `Ticketing:Enabled=false`, chybí connection string `ServiceDeskExternalReadOnly` nebo projekt bez IS | Ticketové dlaždice se nevykreslí. Záložka NES ukáže placeholder s odkazem. Sekce ServiceDesk ve Statistikách = jedna informační karta. |
| `intranetNEW` nebo databáze ServiceDesku nedostupná, vyprší čas, chybí čtecí právo | Záložka NES ukáže chybovou kartu „ServiceDesk teď neodpovídá — zkuste to za chvíli" s tlačítkem Zkusit znovu. Souhrnný pás ukáže u ticketových dlaždic „—" s tooltipem. Statistiky vykreslí ostatní sekce. Chyba se loguje s isId. |
| Ticket bez iniciační události (U1) nebo s důležitostí, která nemá řádek v `ticket_external_sla` | Sekce „Nelze vyhodnotit" s důvodem. Chybějící důležitost se navíc zaloguje jednou za načtení katalogu, s přesnou hodnotou `dulezitost`. |
| Ticket bez řádku v `ticket_external` (nesynchronizovaný) | Neřeší se a nezobrazuje (§6.3) |
| Ticket bez `HOT_ZAZNAMY.id` | Mimo rozsah, ignoruje se (paměť `feedback_sd_ticket_id_required`) |

## 14. Oprávnění

Beze změny klíčů: `dashboard.view`, `dashboard.records.view`, `dashboard.nes.view`, `dashboard.statistics.view` (popisek „Záložka Statistiky" už v seedu je).
- Nový endpoint `souhrn` má policy `dashboard.view`. Jednotlivé dlaždice se filtrují podle oprávnění k příslušné záložce.
- `statistics-panel` endpoint vrací nový report místo legacy panelu. URL zůstává, `zakladni-report` endpoint se sloučí do něj.
- `projektId` je v route všech endpointů (paměť `feedback_permission_policy_needs_projektid_in_route`).

## 15. Testování

- **Engine (unit, golden):** scénáře T01–T14 pro každý typ, víc dodavatelů (T00), prominutí T14, půlnoc 23:59:59, víkend a svátek uvnitř obrátky, ticket stále u dodavatele (fixní `TimeProvider`).
- **České svátky:** Velký pátek a Velikonoční pondělí 2016–2030 proti tabulce a pevné svátky.
- **Dotazová služba:** párování `external_id` s mezerami na `HOT_ZAZNAMY.id`, ticket bez `ticket_external` vyřazen, chunkování předávek, CHAR(5) typ s mezerami (InMemory kontexty obou databází).
- **Parita se ServiceDeskem (akceptační):** vyexportuješ ze starého ServiceDesku sestavu „Dní penále" (`GetListOfOverallSupplierDueTicketsToExcel`) pro 20–30 reálných ticketů. PM Tracker musí dát stejné penalizované dny. Protože engine čte stejná data jako ServiceDesk, každý rozdíl je chyba portu a opraví se. Reálná data nejdou do gitu, porovnání proběhne lokálně.
- **Providery Statistik:** golden-vektor testy (vstup dataset, výstup `ChartData`) bez DB, stejně jako u stávajících providerů.
- **Api render testy:** kotvení na atributy a ASCII, ne na český text (paměť `feedback_razor_encodes_diacritics_in_html_tests`).
- **Reálný prohlížeč:** Playwright proti lokální app (paměť `feedback_verify_ui_in_real_browser`): proklikání všech záložek a dlaždic, snímky ve světlém i tmavém režimu, úzká šířka. Snímky dostaneš ke kontrole.

## 16. Rozhodnutí (stav 2026-10-19)

| # | Otázka | Rozhodnutí |
|---|---|---|
| R1 | Zdroj předávek | **Databáze ServiceDesku** (`ticket_external*`), stejná data jako starý ServiceDesk (§6). Textová rekonstrukce z `HOT_VYJADRENI` odpadá. |
| R2 | SLA hodiny podle důležitosti | Z tabulky `ticket_external_sla`, ne z konfigurace PM Trackeru. |
| R3 | Které tickety vstupují do seznamu | Jen synchronizované (`ticket_external`) a právě u dodavatele (§10). Nesynchronizované se neřeší a neukazují. Archivované jen do statistik za období. |
| R4 | Prahy „hrozí prodlení" | NES ≤ 48 h SLA, PMP/PNF ≤ 5 dní, převzato ze ServiceDesku. *Zatím jsi nepotvrdil, platí, dokud neřekneš jinak.* |
| R5 | Předání PMP/PNF bez termínu | Odpadá: termín je ve sloupci `dt_param`, žádné parsování textu. Chová se 1:1 jako originál (T04). |
| R6 | Důležitost bez řádku v `ticket_external_sla` | Originál by přes `Convert.ToInt32(null)` dosadil 0 h SLA a penalizoval od prvního dne. Podle tebe důležitost u NES nikdy není prázdná, takže tahle situace znamená chybu dat nebo katalogu → **„Nelze vyhodnotit"** s důvodem, žádné falešné penále. |
| R7 | Connection string a přístup | Vyplňuje správce v `appsettings.json` (`ConnectionStrings:ServiceDeskExternalReadOnly`), čtecí právo zajišťuje také správce. |
