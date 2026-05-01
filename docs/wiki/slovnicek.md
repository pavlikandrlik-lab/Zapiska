---
title: Slovníček pojmů
description: Klíčové business pojmy používané v PM Trackeru a v této dokumentaci.
---

# Slovníček

Pojmy seřazené abecedně. Každá položka má krátkou definici a odkaz na detail.

## A

**Active Directory (AD)** — firemní adresářová služba, ze které PM Tracker
převzímá identitu přihlášeného uživatele a přes [AD picker](integrace/active-directory/ad-picker.md)
vyhledává osoby pro přiřazování do týmů.

**AD picker** — UI komponenta pro vyhledávání osoby v AD podle jména, loginu
nebo emailu. Používá se v týmu projektu, při editaci záznamu (collaborator), atd.

**Audit log** — záznam o tom kdo a kdy provedl změnu. Zapisuje se automaticky
u všech mutujících akcí (vytvoření, úprava, smazání, schválení návrhu, …).

## B

**Buffer card** — placeholder karta v panelu Výzvy. Vizuálně označuje slot pro
budoucí výzvu která ještě nebyla zadána.

## C

**Collaborator** — spolupracující osoba na záznamu. Není primárním vlastníkem,
ale má k záznamu přístup a může na něm pracovat.

## E

**Externí vazba** — propojení záznamu PM Trackeru s konkrétním ServiceDesk
ticketem (přes 6-ciferné `id`). Spustí harvest vyjádření a auto-fill harmonogramu.
Detail: [Externí vazba na SD](projekty/zaznamy/externi-vazba-sd.md).

## F

**FIS** — Finanční informační systém (ServiceDesk: `HOT_IS.ID = 1`). Jeden ze
dvou IS, ke kterým může být projekt přiřazen. Hotline: `973 200 840`.

## H

**Harmonogram** — plán a skutečnost projektových kroků (HS01, HS02, …). Eviduje
plán (kolik dní krok trvá), skutečnost (co reálně proběhlo) a delay (odchylku).
Detail: [Harmonogram](projekty/harmonogram/).

**Harvest predikát** — pravidlo klasifikace SD vyjádření podle textu. Aplikace
rozlišuje 5 typů (case-insensitive `Contains` match na `HOT_VYJADRENI.popis`):

| Klíč | Hledaná fráze | Typ tiketu | Krok |
|---|---|---|---|
| **K3** | "Záznam byl založen a předán dodavateli k řešení pod značkou:" | PMP | 3 |
| **K4_K7** | "Dodavatel přidal řešení" | PMP / PNF | 4 / 7 |
| **K6** | "Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována." | PNF | 6 |
| **K10** | "Záznam byl převeden do archivu." | PMP / PNF | 10 |
| **Plán dodání** | "předal záznam dodavateli :" + "s termínem plnění dodavatele" | různé | různé |

Klasifikace řídí [auto-fill harmonogramu](integrace/servicedesk/auto-fill-skutecnosti.md).
Konkrétní mapping řeší [HarmonogramKrokDatumMapping](projekty/harmonogram/kroky-a-faze.md).

**HS01, HS02, … HS0N** — kódové označení kroků harmonogramu. Každý krok má
typ duration (kolik dní krok trvá) a typ delay (odchylka skutečnosti).

Auto-fill mapping per typ tiketu (z `HarmonogramKrokDatumMapping.cs`):

| Typ | HS03 | HS04 | HS06 | HS07 | HS10 |
|---|---|---|---|---|---|
| **NES** | — | — | — | — | — |
| **PMP** | K3 | K4_K7 | — | — | — |
| **PNF** | — | — | K6 | K4_K7 | K10 |

Kroky 1, 2, 5, 8, 9 nejsou auto-fill plněné — buď ruční (typ Úkol), nebo
mimo aktivní sadu typu. NES nemá žádný auto-fill krok, jen `K1` = datum
založení (manuální).

## I

**Identifikátor jednání** — číslo jednání ve formátu typicky "8201". Unikátní v
rámci projektu. Detail: [Identifikátor jednání](projekty/jednani/identifikator-jednani.md).

**Informační systém (IS)** — v kontextu PM Trackeru vždy odkazuje na konkrétní
IS ve ServiceDesku (`HOT_IS`). Aplikace má hardcoded katalog dvou: **FIS**
(`ID=1`) a **ISSP** (`ID=2`). Každý projekt může být přiřazený nejvýš k jednomu IS.

**ISSP** — Informační systém služebních poměrů (ServiceDesk: `HOT_IS.ID = 2`).
Druhý IS, ke kterému může být projekt přiřazen. Hotline: `973 225 500`.

## J

**Jednání** — projektová porada. Má unikátní číslo (per-project), datum, místo,
účastníky a může mít navázané záznamy / vyjádření. Detail: [Jednání](projekty/jednani/).

## K

**K3, K4_K7, K6, K10** — zkratky pro [harvest predikáty](#h) klasifikující SD
vyjádření.

**Kalkulace** — finanční rozpis PMP ticketu (počet hodin × sazba). PM Tracker
ji čte ze ServiceDesku (`HOT_KALKULACE`).

**Komentář** — interní vyjádření pod záznamem v PM Trackeru. Nezaměňovat
s [vyjádřením](#v) které je termín pro text v ServiceDesku.

## N

**Návrh (proposal)** — návrh změny harmonogramu projektu. Workflow: vytvoření →
schválení (nebo zamítnutí) → aplikace na harmonogram. Detail: [Návrhy](projekty/navrhy/).

**NES** — *Nesrovnalost*. Typ ticketu ve ServiceDesku — incident / vada
v provozovaném IS. NES tickety mají v `HOT_ZAZNAMY` vyplněný `sla_deadline`
(termín řešení dle SLA dodavatele). V PM Trackeru se zobrazují v projektovém
dashboardu **NES panelu** jako "v prodlení", pokud `sla_deadline < dnes`.

Subtypy zpravidla: `NES I`, `NES II`, `NES III`, `Vada A`, `Vada B`, `Vada C`,
`Vada D`.

NES typicky **nemá kalkulaci** — řeší se v rámci servisní smlouvy.

## O

**Osoba** — uživatelský záznam v `dbo.osoby`. Vázaný na AD `Guid_AD`. Aby se
uživatel mohl přihlásit, musí mít odpovídající záznam v této tabulce.

## P

**Pendingscheduleproposallock** — zámek harmonogramu kroku po vytvoření [návrhu](#n).
Brání ručnímu přepisu skutečnosti dokud není návrh schválen / aplikován / zamítnut.

**Permission key** — stringový identifikátor konkrétní akce v aplikaci, např.
`projects.edit`, `dashboard.nes.view`. Aplikace má per-action authz model
(redesign 2026-04-23): **76 klíčů** v 15 kategoriích. Konvence:
`<doména>.<entita>?.<akce>[.<scope>]`. Detail: [Akce / permissions](nastaveni-administrace/akce-permissions/).

**PMP** — *Požadavek metodické podpory*. Typ ticketu ve ServiceDesku pro úpravy
existujícího IS s **finanční kalkulací** (počet hodin × sazba). Pro PMP je v
SD vždy 1 řádek v `HOT_KALKULACE` (souhrnný) plus N řádků v `HOT_KALKULACE_PMP`
(per-zaměstnanec rozpis). PMP používá `dat_res_t` jako termín řešitele.

V PM Trackeru auto-fill skutečnosti používá **K3** (krok 3 = odeslání zadání)
a **K4** (krok 4 = dodání řešení) harvest predikáty.

**PNF** — *Požadavek nové funkcionality*. Typ ticketu ve ServiceDesku pro
rozšíření IS o novou funkčnost. Má kalkulaci (jeden souhrnný řádek). PNF
používá `dat_res_t` jako termín řešitele.

V PM Trackeru auto-fill skutečnosti používá **K6** (krok 6 = odeslání
požadavku/akceptace kalkulace), **K7** (krok 7 = dodání řešení, sdílí predikát
s K4 jako `K4_K7`) a **K10** (krok 10 = nasazení/archivace) harvest
predikáty.

Subtypy: `PMP`, `PNF I`, `PNF II`, `Pozadavek`.

**Projekt** — centrální entita PM Trackeru. Všechny ostatní entity (záznamy,
jednání, harmonogram, …) existují v kontextu konkrétního projektu.

**Projektový dashboard** — agregovaný pohled na projekt. Obsahuje 4 panely:
Záznamy, Statistiky, NES v prodlení, Výzvy. Detail: [Projektový dashboard](projekty/projektovy-dashboard/).

## R

**Re-harvest** — manuální spuštění aktualizace dat ze SD pro konkrétní externí
vazbu. K dispozici v [SD konektoru](integrace/servicedesk/sd-konektor-diagnostika.md)
nebo v [chat modalu](integrace/servicedesk/chat-vyjadreni.md).

**Režim Auto / Ručně** — přepínač u skutečnosti harmonogramu. **Auto** = sync
služba periodicky aktualizuje hodnotu. **Ručně** = uživatel zafixoval hodnotu,
sync ji nepřepíše. Detail: [Manuální skutečnost](projekty/harmonogram/manualni-skutecnost.md).

**Role** — sada [permission keys](#p). Uživatel má jednu nebo více rolí.
Role lze přiřadit globálně, per-projekt nebo per-subsystém scope. PM Tracker
má **11 systémových rolí** definovaných v `PermissionSeedConfiguration.cs`:

- **Globální** (3): `SUPERADMIN`, `APP_ADMIN`, `READ_ALL`
- **Projektové** (5): `VLASTNIK_PROJEKTU`, `ADM_PROJ`, `PROJ_MAN`, `HOST`, `GEST`
- **Subsystémové** (3): `VEDOUCI_SUBSYSTEMU`, `ZASTUPCE_VEDOUCIHO_SUBSYSTEMU`, `METODIK_SUBSYSTEMU`

Detail: [Nastavení → Role](nastaveni-administrace/role/).

## S

**ServiceDesk (SD)** — firemní hotline systém. PM Tracker z něj read-only
čte tickety a vyjádření. Detail: [Integrace → ServiceDesk](integrace/servicedesk/).

**Skutečnost** — reálný termín kroku harmonogramu (datum dodání, datum nasazení,
…). Pochází z auto-fill ze SD vyjádření nebo z ručního zadání. Vedle hodnoty je
[badge zdroje](#z).

**Soft-delete** — projekty a záznamy se fyzicky nemažou z DB. Označí se jako
*smazané* a skryjí z default přehledů. Lze obnovit zásahem v DB.

**Stepper / pm-chat-stepper** — custom HTML element v chat modalu vyjádření.
Renderuje 5-slot buffer harmonogramu na který lze drag-dropem přetáhnout
vyjádření a vytvořit vazbu krok ↔ vyjádření.

**Subsystém** — logická část projektu (modul, komponenta). Záznamy mohou být
přiřazené k subsystému. Detail: [Subsystémy](projekty/tym/subsystemy.md).

## T

**Tým projektu** — osoby přiřazené k projektu s konkrétními rolemi. Detail:
[Tým](projekty/tym/).

## U

**Úkol** — interní typ záznamu PM Trackeru bez vazby na ServiceDesk. Pro vlastní
plánování týmu.

## V

**Vyjádření** — text z `HOT_VYJADRENI` ze ServiceDesku. Zadává se přímo v SD,
PM Tracker ho čte read-only a zobrazuje v [chat modalu](integrace/servicedesk/chat-vyjadreni.md).
Nezaměňovat s [komentářem](#k) který je interní v PM Trackeru.

**Výzva** — plánovaná akce / dodávka od dodavatele. Detail: [Výzvy](projekty/projektovy-dashboard/vyzvy/).

## Z

**Záznam** — základní stavební blok projektu. Má typ (NES/PMP/PNF/úkol), stav,
vlastníka, termíny, volitelnou externí vazbu. Detail: [Záznamy](projekty/zaznamy/).

**Zdroj skutečnosti (badge)** — vizuální indikace odkud hodnota skutečnosti
pochází:

| Ikona | Zdroj | Význam |
|---|---|---|
| 🤖 | Automat | Auto-fill ze SD vyjádření |
| ✍️ | Manual | Uživatel zadal ručně |
| 📜 | Historicka | Migrovaná před auto-fill |
| — | Neznámo | Žádná skutečnost zatím |
