# Spec: ServiceDesk integrace — Výzvy + kalkulace (use-case C)

**Stav:** návrh (čeká na schválení)
**Datum:** 2026-04-20
**Autor:** Ing. Pavel Andrlík + Claude (brainstorming)
**Scope:** Use-case C z plánované ServiceDesk integrace. Use-case A (vytěžování vyjádření) a B (dashboard prodlení NES/PMP/PNF) budou samostatné specy.
**Navazuje na:** [ticketing-integration.md](../../specs/ticketing-integration.md) — sdílí infrastrukturu čtení z `intranetNEW` (read-only DbContext, servisní účet).

---

## 1. Kontext a cíl

Výzva je objednávkový dokument pro dodavatele podle § 134 ZZVZ (rámcová dohoda). Skládá se z PNF ticketů, které existují v ServiceDesku (databáze `intranetNEW.dbo.HOT_*`) a jsou provázány se záznamy v PM Trackeru přes externí vazby.

**Dnes:**
- `CiselnikVyzva` je tenký číselník (Id, Kod, Nazev, Rok, IsLocked). Dnes prázdný.
- `ZaznamExterniOdkaz.Vyzva` drží FK na výzvu. Ruční přiřazení.
- Žádné generování Wordu. Žádné čtení kalkulací z `HOT_KALKULACE`.

**Cíl:**
- Automatizovat sestavení výzvy z PNF externích vazeb.
- Generovat Word dokument podle fixní šablony (vzor `20260206_N_8201_Vyzva_c_2_2026_EIS.docx`).
- Zavést life-cycle výzvy (`Priprava` / `Odeslano` / `Zruseno`).
- Zavést buffer = virtuální seznam PNF čekajících na zařazení do výzvy.
- Číst APTI kalkulace z `HOT_KALKULACE` ve stavu „Akceptováno".

## 2. Rozsah

**✅ V scope:**
- Úpravy schématu `Vyzva`, `ZaznamExterniOdkaz`, `Projekt`.
- UI záložka „Výzvy" na dashboardu projektu.
- Switch „Zařadit do další výzvy" na externí vazbě PNF.
- Buffer logika + auto-přiřazení do aktivní výzvy.
- Auto-číslování per `CisloRamcoveSmlouvy` + rok.
- Drag & drop přeřazování PNF mezi bufferem a neodeslanými výzvami v rámci projektu.
- Globální unikátnost: PNF (6-místné číslo z `HOT_ZAZNAMY.id`) nesmí být ve více výzvách napříč projekty současně.
- Generování Wordu z fixní šablony.
- Read-through z `intranetNEW.dbo.HOT_*` (bez sync jobu, bez cache).
- Fallback číselník pro SuperAdmin/app_admin.

**❌ Mimo scope:**
- Use-case A (vytěžování vyjádření, rule engine, tagy).
- Use-case B (dashboard prodlení NES/PMP/PNF).
- Hangfire sync joby (budou potřeba až pro A/B).
- Editace textu PNF, kalkulací nebo čehokoliv v `HOT_*` (vše je read-only).

## 3. Datový model

### 3.1 `Projekt` — nová pole
Do `ProjektEntity` / editace projektu:

| Pole | Typ | Popis |
|---|---|---|
| `MistoPlneni` | nvarchar(500) nullable | např. „FIS (EIS): VZ 8201, Tychonova 1, 160 01 Praha 6" |
| `CisloRamcoveSmlouvy` | nvarchar(100) nullable | např. „23106000271" |

Nullable: ne všechny projekty budou generovat výzvy. Při pokusu o založení výzvy jsou obě pole vyžadována — validace na UI + server side.

**ACL editace těchto polí:**
- `app_admin` + `SuperAdmin` — plný přístup (`SuperAdmin` = `app_admin` + právo na zamknuté číselníky, přiděluje se loginem před prvním AD přihlášením)
- Ostatní — read-only

### 3.2 `Vyzva` — přejmenovat z `CiselnikVyzva`

Tabulka `CiselnikVyzva` je dnes prázdná → bezpečná migrace bez dat. Přejmenovat entitu + tabulku na `Vyzva`, `DbSet<Vyzva> Vyzvy`.

| Pole | Typ | Popis |
|---|---|---|
| `Id` | int PK | |
| `ProjektId` | int FK | Výzva patří ke konkrétnímu projektu |
| `Kod` | nvarchar(20) | např. „2/2026", generováno automaticky |
| `PoradoveVRoce` | int | Inkrementální číslo v rámci `CisloRamcoveSmlouvy + Rok` |
| `Rok` | int | např. 2026 |
| `Stav` | tinyint (enum `VyzvaStav`) | `1=Priprava`, `2=Odeslano`, `3=Zruseno` |
| `DatumZalozeni` | datetime2 | |
| `ZalozilOsobaId` | int FK → Osoba | |
| `DatumOdeslani` | datetime2 nullable | Vyplněno při přechodu do `Odeslano` |
| `OdeslalOsobaId` | int? FK → Osoba | |
| `MistoPlneniSnapshot` | nvarchar(500) | Snapshot z projektu v čase založení |
| `CisloRamcoveSmlouvySnapshot` | nvarchar(100) | Snapshot |

**Zrušit:** `Nazev` (redundantní s `Kod`), `IsLocked` (nahradí `Stav=Odeslano`).

**Index:** `(CisloRamcoveSmlouvySnapshot, Rok, PoradoveVRoce)` pro auto-generování dalšího čísla a unikátnost.
**Constraint:** `UNIQUE(CisloRamcoveSmlouvySnapshot, Rok, PoradoveVRoce)`.

### 3.3 `ZaznamExterniOdkaz` — úpravy

| Pole | Změna | Popis |
|---|---|---|
| `Vyzva` → `VyzvaId` | Rename | FK na `Vyzva.Id`, nullable |
| `ZaradidDoVyzvy` | **new** bool default 0 | Switch „čeká na výzvu", jen pro PNF |

**Validace:**
- `ZaradidDoVyzvy` má smysl jen pro PNF (`TypOdkazuId` = PNF). Pro NES/PMP se na UI skryje; na server side validace odmítne ON pro jiný typ.
- Pokud je `VyzvaId` NOT NULL, `ZaradidDoVyzvy` musí být true (invariant).
- **Globální unikátnost PNF ve výzvách:** databázový filtered unique index na `(Cislo) WHERE VyzvaId IS NOT NULL` — PNF (6-místné číslo z `HOT_ZAZNAMY.id`) nemůže být současně ve dvou výzvách napříč projekty.

## 4. Buffer — definice

Buffer **není fyzická tabulka**. Je to virtuální filtr:

```
PNF buffer projektu X = {
  ZaznamExterniOdkaz ev
  JOIN ProjektovyZaznam z ON ev.ZaznamId = z.Id
  JOIN CiselnikTypuExternichOdkazu t ON ev.TypOdkazuId = t.Id
  WHERE z.ProjektId = X
    AND t.Kod = 'PNF'
    AND ev.ZaradidDoVyzvy = true
    AND ev.VyzvaId IS NULL
}
```

**Typ externí vazby** (`PNF`) se detekuje přes `CiselnikTypuExternichOdkazu`. Podle dohody je mapování 1:1 s `HOT_ZAZNAMY.typ_zaznamu` (`PMP` / `PNF` / `NES`) — konektor ze ServiceDesku do Zápisky píše přímo tento typ.

## 5. Life-cycle výzvy

### 5.1 Stavy

| Stav | Význam | PNF přidávání | Word export | Editace PNF switche |
|---|---|---|---|---|
| `Priprava` | aktivní rozpracovaná | ano | ano (s vodoznakem „NÁVRH") | ano |
| `Odeslano` | uzamčená finální | ne | ano (bez vodoznaku) | ne (read-only switch) |
| `Zruseno` | storno | ne | ne (tlačítko disabled) | n/a — PNF už ve výzvě nejsou |

### 5.2 Přechody stavu

Povolené přechody (ruční, jen `proj_man`, `adm_proj` projektu, `app_admin`, `SuperAdmin`):

- `— → Priprava` — založení výzvy
- `Priprava → Odeslano` — odeslání (povinná potvrzovací modálka)
- `Priprava → Zruseno` — storno → PNF vrací se do bufferu (`VyzvaId=NULL`, `ZaradidDoVyzvy=true`)
- `Odeslano → Priprava` — reaktivace (používá se pro přesouvání PNF mezi výzvami, viz 5.5)
- `Odeslano → Zruseno` — storno i po odeslání
- `Zruseno → Priprava` — jen SuperAdmin/app_admin fallback (oprava chyby)

### 5.3 Zařazování PNF — algoritmus

Když uživatel přepne **switch=ON** na PNF externí vazbě:

1. Zjisti seznam výzev projektu ve stavu `Priprava`, setříděný podle `PoradoveVRoce ASC`.
2. **0 výzev v `Priprava`** → PNF zůstává v bufferu (`VyzvaId=NULL`).
3. **≥1 výzva v `Priprava`** → PNF přiřazen do **první** (nejnižší `PoradoveVRoce`).

Když uživatel přepne **switch=OFF**:

- PNF je v bufferu (`VyzvaId=NULL`) → záznam se jen odmaže (`ZaradidDoVyzvy=false`).
- PNF je ve výzvě `Priprava` → odebere se (`VyzvaId=NULL`, `ZaradidDoVyzvy=false`).
- PNF je ve výzvě `Odeslano` → UI switch je disabled, akce se neprovede.

### 5.4 Zakládání výzvy

Kliknutí na **„Založit výzvu z bufferu"** (v záložce Výzvy):

1. Validace: projekt má vyplněný `MistoPlneni` a `CisloRamcoveSmlouvy`. Jinak chyba.
2. Validace: buffer projektu není prázdný.
3. Auto-generování `PoradoveVRoce`:
   ```sql
   SELECT ISNULL(MAX(PoradoveVRoce), 0) + 1
   FROM Vyzva
   WHERE CisloRamcoveSmlouvySnapshot = @CisloRamcoveSmlouvy
     AND Rok = YEAR(@Now)
   ```
4. Vytvoř `Vyzva` (`Stav=Priprava`, snapshot dat z projektu).
5. Všechny PNF z bufferu projektu nastav `VyzvaId = NewVyzva.Id` (a `ZaradidDoVyzvy` zůstává true).
6. UI přepne na detail nově vytvořené výzvy.

### 5.5 Scénář: víc `Priprava` výzev v projektu

Může nastat, pokud uživatel vrátí odeslanou výzvu do `Priprava`, zatímco má jinou `Priprava` výzvu.

- Nové zařazování (switch=ON) jde do výzvy s **nejnižším `PoradoveVRoce`**.
- PNF už zařazené v některé z těchto výzev se **nepřesouvají automaticky**.
- Ruční přeskupení: drag & drop v záložce Výzvy (viz 6.2).

### 5.6 Číslování výzev

- Scope unikátnosti: `(CisloRamcoveSmlouvySnapshot, Rok)`.
- 1. ledna nový rok → reset na 1 (nová kombinace `Rok`).
- Tři projekty sdílející smlouvu „23106000271" → sdílená řada 1, 2, 3, …
- Projekt se smlouvou „12345678" → vlastní řada 1, 2, 3, …

## 6. UI

### 6.1 Záložka „Výzvy" na dashboardu projektu (`/Projekty/{id}/Vyzvy`)

**Layout:** dvousloupcový, bez samostatné detail stránky.

- **Levý postranník (~25 %):** seznam položek projektu
  - Na vrchu **„Buffer"** (badge s počtem PNF)
  - Pod ním **výzvy** setříděné `DatumZalozeni DESC` (nejnovější nahoře)
  - Každá položka: `Kod`, `Stav` (badge), počet PNF
- **Pravý hlavní obsah (~75 %):** detail vybrané položky

**Detail bufferu:**
- Nadpis „Buffer projektu — čeká na zařazení"
- Seznam PNF (Cislo, Název z HOT, předpokládaná cena, link na záznam)
- Tlačítko **„Založit výzvu z bufferu"** (disabled pokud buffer prázdný nebo chybí `MistoPlneni`/`CisloRamcoveSmlouvy` na projektu)

**Detail konkrétní výzvy:**
- Hlavička: `Kod`, `Stav` (s dropdownem pro změnu stavu), datum založení, kdo založil, datum odeslání (pokud je)
- Tabulka PNF zařazených ve výzvě: pořadí (a/b/c…), Č. úkolu VP (= `ProjektovyZaznam.CisloViditelne`), Č. HTL (= HOT_ZAZNAMY.id = `ZaznamExterniOdkaz.Cislo`), název, APTI celková cena
- Tlačítko **„Stáhnout Word"** (v `Priprava` = návrh s vodoznakem, v `Odeslano` = finální, v `Zruseno` = disabled)
- Tlačítko **„Upravit přiřazení PNF do výzev"** → modal s drag & drop (viz 6.2)
- U každého PNF řádku inline tlačítko **„Odebrat z výzvy"** (jen ve stavu Priprava)
- Tlačítko **„Změnit stav"** (povolené přechody dle 5.2)

### 6.2 Modal „Upravit přiřazení PNF do výzev" (drag & drop)

- Scope: PNF **v rámci jednoho projektu**, mezi bufferem a všemi výzvami projektu ve stavu `Priprava`.
- Sloupce:
  - Buffer (čekající PNF)
  - Výzva N/YYYY (Priprava)
  - … (další Priprava výzvy projektu, pokud existují)
- Odeslané výzvy (Odeslano) se v modalu **neukazují** (nelze z nich přesouvat).
- Drag & drop přesouvá PNF mezi sloupci → po puštění AJAX update `VyzvaId`.
- Gov komponenty pro vizuální styl; DnD knihovna doplněna v implementačním plánu.

### 6.3 Switch na externí vazbě PNF

V editoru záznamu, karta Externí vazby, u každého řádku PNF:

- Gov switch „Zařadit do další výzvy"
- Read-only text vedle switche:
  - `VyzvaId IS NULL` + `ZaradidDoVyzvy=false` → žádný text
  - `VyzvaId IS NULL` + `ZaradidDoVyzvy=true` → „Čeká se"
  - `VyzvaId IS NOT NULL` → „Zařazeno do výzvy N/YYYY" (link na detail výzvy)
- Switch disabled pro:
  - NES, PMP (nepovolený typ)
  - PNF ve výzvě ve stavu `Odeslano`

### 6.4 Číselník výzev (`/Ciselniky/Vyzvy`)

- Přístup: pouze `SuperAdmin` a `app_admin`.
- Plný CRUD na `Vyzva` + ruční úprava `ZaznamExterniOdkaz.VyzvaId`.
- Používá se jen pro opravy chyb, které si způsobí uživatelé přeřazováním stavů.
- Varování v UI: „Tato stránka je určena pro opravy chyb. Standardně používejte záložku Výzvy v projektu."

## 7. Generování Word dokumentu

### 7.1 Šablona

- Jedna fixní `.docx` šablona v aplikaci, zdrojový vzor: `20260206_N_8201_Vyzva_c_2_2026_EIS.docx`.
- Soubor uložen v `PmTracker.Web/Resources/Templates/Vyzva.docx` (final cestu určí implementační plán).
- Šablonové texty (§ 134 ZZVZ, preambule, adresy, ředitel Záborec, cílový systém „FIS") **napevno v šabloně**. Změna = release aplikace.
- Tečkovaná pole pro ruční doplnění (po stažení): `Čj.` (formát „NNNNNN/YYYY-SSSS"), `V Praze dne` (`DD.MM.YYYY` bold placeholder).

### 7.2 Dynamická pole (placeholdery)

| Placeholder | Zdroj |
|---|---|
| Pořadové číslo výzvy (titulek) | `Vyzva.Kod` |
| Místo plnění | `Vyzva.MistoPlneniSnapshot` |
| Termín plnění (např. „do: 31. 3. 2026") | otevřeno — viz 10.2 (nový atribut výzvy vs. `HOT_KALKULACE.termin`) |
| Číslo rámcové smlouvy | `Vyzva.CisloRamcoveSmlouvySnapshot` |
| Vodoznak „NÁVRH" | Přidat jen pro `Stav=Priprava` |

### 7.3 Řádky tabulky požadavků

Pro každý PNF zařazený ve výzvě (`Vyzva.Id = X AND ZaznamExterniOdkaz.VyzvaId = X`):

| Sloupec | Zdroj |
|---|---|
| Pořadové písmeno (a, b, c…) | Auto z pořadí podle `ZaznamExterniOdkaz.Id` ASC (stable pořadí přidání) |
| Č. úkolu VP | `ProjektovyZaznam.CisloViditelne` (via `ZaznamExterniOdkaz.ZaznamId`) |
| Název požadavku | `HOT_ZAZNAMY.strucne` (via `ZaznamExterniOdkaz.Cislo = HOT_ZAZNAMY.id`) |
| Č. HTL | `ZaznamExterniOdkaz.Cislo` (= `HOT_ZAZNAMY.id`) |
| Popis | `HOT_ZAZNAMY.popis` |
| „Vazba na PMP č. …" | Součást textu `HOT_ZAZNAMY.popis` (psáno ručně uživatelem v ServiceDesku; **PM Tracker to neextrahuje ani nedopisuje**) |

### 7.4 APTI tabulka per PNF

Filtr: `HOT_KALKULACE.pid = HOT_ZAZNAMY.pid AND HOT_ZAZNAMY.id = ZaznamExterniOdkaz.Cislo AND HOT_KALKULACE.akceptace = N'Akceptováno'`.

*(Potvrzeno uživatelem 2026-04-21: **HOT_KALKULACE.pid = HOT_ZAZNAMY.pid přímo**. Tabulka `HOT_PID` je v produkční DB přítomná, ale obsahuje stejný `pid` jako `HOT_ZAZNAMY` a nemá dodatečnou obchodní hodnotu — do mappingu se nezahrnuje.)*

| Sloupec Wordu | Zdroj |
|---|---|
| Kód A (Analýza) | `pracnost_a`, `sazba_a`, `cena_a` |
| Kód B (Programové úpravy / Rekonfigurace) | `pracnost_p`, `sazba_p`, `cena_p` — název řádku „B" je v šabloně pevný per PNF? Nebo se řídí něčím v HOT? → **otevřeno (sekce 10)** |
| Kód C (Testování) | `pracnost_t`, `sazba_t`, `cena_t` |
| Kód D (Implementace) | `pracnost_i`, `sazba_i`, `cena_i` |
| CELKEM | `cena` |

Pokud PNF nemá žádnou kalkulaci s `akceptace='Akceptováno'` → tabulka prázdná / chyba generování → **fail-fast** s chybovou hláškou a seznam problematických PNF.

### 7.5 Sumační tabulky

- Agregace všech řádků výzvy: součet bez DPH, DPH 21 %, s DPH.
- Licenční rozšíření: agregace `cena_l`, `sazba_l`, `pocet_l`, `rozpad_licence` — zobrazit, jen pokud aspoň jeden PNF má `cena_l > 0`.

### 7.6 Technika

- Knihovna: **OpenXML SDK** (DocumentFormat.OpenXml) — bez MS Word COM interop. Knihovna je ověřená, MIT, běží v IIS bez instalace Office.
- Generuje se streamem, vrací se přímo v HTTP response jako stažení (`application/vnd.openxmlformats-officedocument.wordprocessingml.document`).
- Název souboru: `YYYYMMDD_{Kod s nahrazením / za _}_Vyzva_projekt_{Zkratka}.docx`.

## 8. ServiceDesk integrace — datový tok

### 8.1 Použité tabulky z `intranetNEW.dbo`

**Primárně:**
- `HOT_ZAZNAMY` — `id`, `typ_zaznamu` (filtr na PNF), `strucne`, `popis`
- `HOT_KALKULACE` — APTI rozpad + cena, filtr `akceptace='Akceptováno'`
- ~~`HOT_PID`~~ — **nepoužívat** (potvrzeno 2026-04-21, tabulka existuje ale `pid` je stejný jako `HOT_ZAZNAMY.pid`, žádná přidaná hodnota)

**Kontextově (pro zobrazení seznamu):**
- `HOT_IS`, `HOT_SUBSYSTEM`, `HOT_MODULY`, `HOT_DODAVATEL` — jen kdyby bylo potřeba zobrazit v UI detail PNF (např. dodavatel)

**Nepoužito v use-case C:**
- `HOT_VYJADRENI`, `HOT_VYJADRENI_TEXT` (patří do use-case A)
- `HOT_IS_LIMIT`, `HOT_TYMY` (zatím nepotřeba)

### 8.2 Technický přístup

- **Druhý DbContext** `TicketingReadOnlyDbContext` v `PmTracker.Web/Data/` (podle existující spec `ticketing-integration.md`).
- Read-only: bez `SaveChanges`, `AsNoTracking` default, `AutoDetectChangesEnabled=false`.
- Servisní účet s rolí `db_datareader` na `intranetNEW`.
- Samostatný connection string `ConnectionStrings:TicketingReadOnly`.
- **Čtení on-demand**: bez Hangfire, bez cache tabulek v PM Tracker DB. Kalkulace se čtou při každém zobrazení výzvy a při generování Wordu.
- Důvod: use-case C nepotřebuje výkon velkého objemu (desítky PNF per výzva, několik výzev denně). Hangfire + cache jsou potřeba pro A/B, ne pro C.

### 8.3 Kontraktová izolace

- Entity `HOT_*` mapovány na samostatné DTO v `PmTracker.Web/Services/Ticketing/Contracts/` (nebo podobná struktura).
- Doménová vrstva (Services.ProjectDashboard, RecordService…) **nesmí** vidět přímé HOT entity — jen DTO.
- Chráníme se proti budoucí výměně ServiceDesku.

## 9. Bezpečnost a audit

- Změny stavu výzvy logovány do nové tabulky `VyzvaHistorieStavu` (Id, VyzvaId, PuvodniStav, NovyStav, DatumZmeny, ZmenilOsobaId) — paralela k `ZaznamHistorieStavu*`.
- CRUD v číselníku výzev (fallback) → standardní audit log aplikace.
- Connection string `TicketingReadOnly` přes secret manager / Azure Key Vault, nikdy v repu.
- Switch akce na externí vazbě — logovat v existujícím audit mechanismu záznamu.
- Globální unikátnost PNF ve výzvách vynucena DB constraintem — nemůže být obejita chybou UI.

## 10. Otevřené body (k doladění v implementačním plánu)

1. **Název kódu B** („Programové úpravy" vs. „Rekonfigurace"): ve vzorovém Wordu se střídá per PNF. Drží to HOT (nějaký sloupec `HOT_KALKULACE.popis`?) nebo je to volba proj_mana v UI výzvy? Default v implementaci: **pevně „Programové úpravy"**, přejmenování až na požadavek.
2. **Termín plnění** ve Wordu (např. „do: 31. 3. 2026"): je to atribut výzvy, nebo se odvozuje z `HOT_KALKULACE.termin`? Dnes předpokládám nový atribut `Vyzva.TerminPlneniSnapshot` s hodnotou doplněnou při zakládání nebo odesílání výzvy.
3. ~~Mapping HOT_PID~~ — **UZAVŘENO 2026-04-21:** `HOT_KALKULACE.pid = HOT_ZAZNAMY.pid` přímo, HOT_PID se nepoužívá.
4. **Přesné Gov komponenty DnD**: Gov design system má drag & drop? Pokud ne, doplnit externí knihovnu kompatibilní s Gov styly.
5. **Vodoznak „NÁVRH"**: technika v OpenXML — watermark header nebo overlay text. Upřesníme v implementaci.

## 11. Akceptační kritéria

**Must-have pro dokončení use-case C:**

1. ✅ Projekt lze upravit: pole `MistoPlneni` a `CisloRamcoveSmlouvy` editovatelné rolí `app_admin`/`SuperAdmin`.
2. ✅ PNF externí vazba má funkční switch „Zařadit do další výzvy" s read-only indikátorem stavu.
3. ✅ Switch je skryt/disabled pro NES/PMP a pro PNF ve výzvě `Odeslano`.
4. ✅ Databázový constraint zajišťuje, že PNF nemůže být ve dvou výzvách napříč projekty.
5. ✅ Záložka Výzvy projektu zobrazuje postranník (Buffer + výzvy) + detail.
6. ✅ Zakládání výzvy automaticky generuje `Kod` per (smlouva, rok), nasype buffer, přepne UI na novou výzvu.
7. ✅ Stavové přechody fungují podle tabulky v 5.2, s ACL.
8. ✅ Modal „Upravit přiřazení PNF" s drag & drop mezi bufferem a Priprava výzvami projektu.
9. ✅ Word export funguje pro Priprava (s vodoznakem) i Odeslano (bez).
10. ✅ Word obsahuje všechny sekce dle vzorového dokumentu; APTI data načtena z `HOT_KALKULACE` (akceptace=Akceptováno).
11. ✅ Číselník výzev `/Ciselniky/Vyzvy` je přístupný jen `SuperAdmin`/`app_admin` jako fallback.
12. ✅ Audit: změny stavů logovány v `VyzvaHistorieStavu`.
13. ✅ Testy: unit tests pro buffer logiku, auto-číslování, přechody stavu. Integration test pro Word generování (snapshot přes Verify).

## 12. Zodpovědnost

- **Technický návrh:** Claude + implementace
- **Vzor Wordu + business pravidla:** Ing. Andrlík
- **Schéma `intranetNEW.HOT_*`:** Ing. Andrlík (ne DBA, zatím přes ručně dodané SELECTy — viz `hotline.txt` v kontextu konverzace)
- **Přístup k servisnímu účtu + connection string:** IT (před deploymentem)

---

## Návrh implementace (vysoký pohled)

**Důležité:** Toto NENÍ implementační plán. Je to jen hrubá kostra pro odhad velikosti a dekomposici, podrobný plán vznikne v dalším kroku (`writing-plans` skill).

### Fáze 1 — schéma a backend core (bez UI)
1. EF migrace: `Projekt.MistoPlneni`, `Projekt.CisloRamcoveSmlouvy`.
2. EF migrace: rename `CiselnikVyzva` → `Vyzva` + nová pole + enum stav.
3. EF migrace: `ZaznamExterniOdkaz.ZaradidDoVyzvy` + rename sloupce `Vyzva` → `VyzvaId` + filtered unique index.
4. Migrace: `VyzvaHistorieStavu` tabulka.
5. `TicketingReadOnlyDbContext` + entity `HotZaznam`, `HotKalkulace` + mapping + DTO. *(HotPid odstraněno 2026-04-21 — nepoužívá se, viz §10.)*
6. `VyzvaService` — založení výzvy, přechody stavu, přeřazování PNF, auto-číslování.
7. `TicketingQueryService` — dotazy do HOT_KALKULACE, HOT_ZAZNAMY.
8. Unit testy pro buffer/auto-číslování/přechody.

### Fáze 2 — UI
9. Editace projektu: nová pole `MistoPlneni`, `CisloRamcoveSmlouvy`.
10. Switch na externí vazbě PNF v `_EditZaznamForm.cshtml`.
11. Záložka Výzvy `/Projekty/{id}/Vyzvy` (postranník + detail buffer/výzva).
12. Modal „Upravit přiřazení" (drag & drop).
13. Číselník výzev `/Ciselniky/Vyzvy` (fallback).
14. Playwright testy UI.

### Fáze 3 — Word export
15. Šablona `Vyzva.docx` v `Resources/Templates/`.
16. `VyzvaWordGenerator` (OpenXML).
17. Vodoznak „NÁVRH" pro `Priprava`.
18. Verify snapshot test vygenerovaných Wordů.

### Fáze 4 — integrace a akceptace
19. Connection string `TicketingReadOnly` v secret manageru.
20. Nasazení na test DB + manuální akceptační test se vzorovou výzvou.
21. Uzavření use-case C, předání do vedení k schválení.

---

**Konec specu.** Pokud je v pořádku a schvaluješ, navazujícím krokem je invokace skillu `superpowers:writing-plans` pro vytvoření podrobného implementačního plánu fáze 1.
