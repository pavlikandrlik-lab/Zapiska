# Serverové generování PDF pro tisk — design

**Datum:** 2026-09-04
**Stav:** návrh ke schválení
**Vychází z:** požadavku „Při tisku nejsou stránky očíslovány, dej číslování na spodek stránky doprostřed"
**Souvisí:** Word export (`OpenXmlWordExportService`) — zápatí „Strana X z Y" hotové a ověřené na reálném exportu, drženo v pracovním stromu k ruční kontrole

## 1. Cíl

Tisková tlačítka mají vracet **skutečné PDF s patičkou „Strana X z Y" dole uprostřed**, místo dnešní HTML stránky, která si sama vyvolá tiskový dialog prohlížeče.

Vzhled dokumentu se **nemění** — použije se stávající šablona i stávající tiskové CSS.

## 2. Proč to nejde jednodušeji

CSS umí číslovat stránky přes `@page { @bottom-center { content: counter(page) } }`, ale **Chromium (a tedy i Edge na i15) tyto margin boxy neimplementuje**. Ověřeno experimentálně: vygenerované třístránkové PDF neobsahovalo slovo „Strana" vůbec.

Ani `position: fixed` prvek to nespraví — v tisku se sice zopakuje na každé stránce, ale `counter(page)` je mimo `@page` neplatný, takže by nesl statický text bez čísla.

Číslo stránky tedy může vzniknout jen tam, kde se stránky skutečně lámou — v generátoru PDF.

## 3. Rozhodnutí uživatele (diskuse 2026-09-04)

| # | Rozhodnutí |
|---|---|
| U1 | **Serverové generování PDF**, ne zapnutí vlastní patičky prohlížeče v tiskovém dialogu. Uživatel: „jiné řešení než druhá varianta pro mě není". |
| U2 | Motor = **headless Edge už nainstalovaný na serveru**, ne přepsání sazby do C# (QuestPDF). Zachovává dnešní vzhled a nevyžaduje licenční rozhodnutí. |
| U3 | Platí pro **všechny tři tisky** — projekt, jednání, záznam. |
| U4 | PDF se **otevře v prohlížečce PDF** v nové kartě (`Content-Disposition: inline`), nestahuje se do Stažených souborů. |
| U5 | V patičce je **jen „Strana X z Y"** — nic dalšího. Hlavička dokumentu už nese název, projekt, kdo a kdy generoval. |

## 4. Experimentální ověření (provedeno před sepsáním specu)

Průzkumný program (zahazovací, ve scratchpadu) prokázal celou cestu:

| Ověřeno | Výsledek |
|---|---|
| PuppeteerSharp umí řídit **nainstalovaný Edge** (`ExecutablePath`), nestahuje vlastní Chromium | ano |
| Patička s `class="pageNumber"` / `class="totalPages"` | vysázena, na 1. straně **„Strana 1 z 3"** |
| Zalomení do 3 stran, číslo odpovídá skutečnosti | ano |
| Česká diakritika (`příliš žluťoučký kůň úpěl ďábelské ódy`) | vysázena správně |
| Stopa v publish outputu | **~7 MB** (PuppeteerSharp 4 MB + WebDriverBiDi 2,5 MB), **žádné node ani Chromium binárky** |

**Proč ne `Microsoft.Playwright`** (byť ho máme v E2E testech): jeho node driver má v build outputu **125 MB** (změřeno na `PmTracker.Tests.E2E/bin`). Do publish balíku, který se nosí přes RDP, nepatří. PuppeteerSharp mluví s prohlížečem přes CDP přímo z .NET.

**Proč ne CLI `msedge.exe --print-to-pdf`:** přepínač neumí vlastní šablonu patičky, jen ji celou zapnout/vypnout. Číslo stránky vlastním textem tudy nedostaneme.

## 5. Výchozí stav

`Tisk` dnes vrací HTML stránku, nikde nevzniká PDF soubor:

- `Views/Export/PdfTemplate.cshtml` (102 ř.) + partials `_PdfRecordRow`, `_PdfRolesBlock`, `_PdfAttendanceBlock` (celkem 243 ř.)
- `wwwroot/css/pdf-export.css` (530 ř.) — **soběstačné**, žádné `url()`, `@import` ani `@font-face`
- jediný `<script>` v šabloně je `window.print()` pod podmínkou `Model.AutoPrint`
- tři akce sdílí stejnou šablonu: `ExportController.ProjektTisk` (:69), `JednaniTisk` (:132), `UkolTisk` (:171)

Word export už dnes konzumuje **stejný view model** (`IWordExportService.BuildDocument(PdfExportTemplateViewModel)`). Nová PDF služba sedne přesně vedle něj — bez nových dotazů do databáze.

## 6. Architektura

```
ExportController.{ProjektTisk|JednaniTisk|UkolTisk}
        |
        | PdfExportTemplateViewModel  (beze změny)
        v
   IViewRenderer  ---- vyrenderuje PdfTemplate.cshtml do stringu
        |
        | HTML
        v
   IPdfRenderer (ChromiumPdfRenderer)
        |  +-- přilepí wwwroot/css/pdf-export.css
        |  +-- spustí headless Edge, Page.printToPDF s patičkou
        v
   PdfRenderResult
        |
        +-- Bytes != null  -->  File(bytes, "application/pdf") + inline disposition
        |
        +-- Bytes == null  -->  View(PdfTemplate)   [dnešní chování, záchranná brzda]
```

### 6.1 Soubory

| Akce | Soubor | Odpovědnost |
|---|---|---|
| Vytvořit | `Services/Export/IPdfRenderer.cs` | rozhraní + `PdfRenderRequest` + `PdfRenderResult` |
| Vytvořit | `Services/Export/ChromiumPdfRenderer.cs` | PuppeteerSharp implementace, patička, brána souběhu |
| Vytvořit | `Services/Export/PdfExportOptions.cs` | vazba na `appsettings`, autodetekce cesty k Edgi |
| Vytvořit | `Services/Export/IViewRenderer.cs` + `RazorViewRenderer.cs` | Razor view → HTML string |
| Změnit | `Controllers/ExportController.cs` | tři akce vrací PDF; společný privátní helper |
| Změnit | `Program.cs` | registrace služeb + options |
| Změnit | `PmTracker.Web.csproj` | `PuppeteerSharp` 25.8.0 |
| Změnit | `appsettings.example.json` | nová sekce `Export:Pdf` |
| Změnit | `docs/technical/*` deployment checklist | co ověřit na serveru |
| **Nemění se** | `Views/Export/*.cshtml`, `pdf-export.css` | zdroj PDF zůstává beze změny |

### 6.2 Kontrakt generátoru

```csharp
public sealed record PdfRenderRequest
{
    public required string Html { get; init; }
    public string? StylesheetPath { get; init; }   // absolutní cesta k pdf-export.css
}

public sealed record PdfRenderResult(byte[]? Bytes, string? FailureReason)
{
    public bool Succeeded => Bytes is not null;
}

public interface IPdfRenderer
{
    Task<PdfRenderResult> RenderAsync(PdfRenderRequest request, CancellationToken ct);
}
```

Selhání se **nevyhazuje výjimkou** — vrací se jako `FailureReason`. Fallback je tak řízený tok, ne odchyt výjimky, a dá se otestovat bez prohlížeče.

### 6.3 Patička

```html
<div style="width:100%;font-family:Arial,sans-serif;font-size:9pt;color:#111;text-align:center;">
  Strana <span class="pageNumber"></span> z <span class="totalPages"></span>
</div>
```

Třídy `pageNumber` a `totalPages` plní Chromium sám při sazbě. Hlavička se vypne prázdným `<div></div>` — jinak Chromium vysází svou výchozí (název + URL).

### 6.4 Okraje

Dnešní CSS má `@page { size: A4 portrait; margin: 5mm; }`. Generátor okraje nastavuje sám (nahoře/vlevo/vpravo 5 mm, **dole 14 mm** kvůli místu pro patičku).

**Ověřeno měřením 2026-09-04:** okraje se **nesčítají**. Chromium respektuje `@page` z šablony a hodnoty předané generátoru ignoruje — naměřeno na matici (CSS 5 mm × generátor 5 mm/0 mm dává shodnou polohu textu 34,6 bodu; teprve změna CSS na 0 mm ji posune na 20,4 bodu). Pás pro patičku si Chromium rezervuje sám: na plné stránce zbývá mezi spodkem obsahu a patičkou ~3,5 mm, tedy bez překryvu. **`pdf-export.css` se nemění.**

### 6.5 Názvy souborů

| Varianta | Vzor |
|---|---|
| Projekt | `Zapiska_<zkratka>_projekt_2026-09-04.pdf` |
| Jednání | `Zapiska_<zkratka>_jednani-12_2026-09-04.pdf` |
| Záznam | `Zapiska_<zkratka>_zaznam-901-1_2026-09-04.pdf` |

Zkratka projektu se očistí od znaků nepovolených v názvu souboru. Disposition je `inline` s vyplněným `filename*` — Edge dokument zobrazí a při „Uložit jako" nabídne správný název.

## 7. Konfigurace

```json
"Export": {
  "Pdf": {
    "Enabled": true,
    "BrowserExecutablePath": "",
    "TimeoutSeconds": 60,
    "MaxConcurrent": 2
  }
}
```

- `Enabled: false` → tisk se okamžitě chová jako dnes (HTML + `window.print()`). Vypínač bez nasazení nové verze.
- `BrowserExecutablePath` prázdné → autodetekce ve známých cestách:
  - `C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe`
  - `C:\Program Files\Microsoft\Edge\Application\msedge.exe`
  - `/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge` (vývoj na macOS)
- `MaxConcurrent` → brána `SemaphoreSlim`; víc souběžných tisků čeká ve frontě, aby těžký dokument nezahltil server.

Produkční `appsettings.json` se na serveru udržuje ručně, proto sekce patří i do `appsettings.example.json`.

## 8. Záchranná brzda

Když `IPdfRenderer` vrátí neúspěch — Edge chybí, nejde spustit pod účtem aplikace, vyprší časový limit — akce **vrátí dnešní HTML stránku** a důvod zaloguje na úrovni `Warning`.

Tisk tedy nikdy nepřestane fungovat; v nejhorším případě se vrátí do dnešního stavu bez číslování.

Stojí to prakticky nic: HTML šablona zůstává, protože **ona sama je zdrojem toho PDF**. Stačí nemazat dnešní větev a ponechat parametr `autoPrint`, který má význam už jen na této cestě.

## 9. Provoz a nasazení

| Věc | Dopad |
|---|---|
| Proces prohlížeče | Edge běží pod účtem aplikačního poolu; potřebuje zapisovatelný dočasný adresář (`--user-data-dir` v `Path.GetTempPath()`, po doběhnutí se maže) |
| Přepínače | `--no-sandbox`, `--disable-dev-shm-usage` — pod účtem služby nelze zakládat sandbox |
| Doba generování | odhad 1–2 s na dokument (spuštění prohlížeče na požadavek) |
| Paměť | jedna instance Edge ≈ 200 MB po dobu generování; brána souběhu drží strop |
| Velikost balíku | +7 MB |
| Antivirus | spouštění prohlížeče serverovým procesem může být hlídané — patří do checklistu |

Do deployment checklistu přibude ověření: existuje Edge, aplikační pool ho smí spustit, dočasný adresář je zapisovatelný.

**Vědomé zjednodušení:** prohlížeč se spouští **na požadavek**, nedrží se živý mezi tisky. Je to pomalejší o cca sekundu, ale odpadá celá třída poruch (zmrtvěná instance, únik paměti, restart poolu). Při intranetovém provozu je to správná strana kompromisu. Kdyby se ukázalo, že je to pomalé, přidá se sdílená instance později.

## 10. Testovací strategie

| Vrstva | Test | Prohlížeč? |
|---|---|---|
| Unit | patička obsahuje `class="pageNumber"`, `class="totalPages"`, centrování, text „Strana … z …" | ne |
| Unit | očištění názvu souboru (diakritika, lomítka, mezery) | ne |
| Unit | autodetekce cesty k prohlížeči — nastavená hodnota má přednost před hledáním | ne |
| Api | tiskové akce s **podvrženým** `IPdfRenderer` → `application/pdf`, `inline`, správný název souboru | ne |
| Api | podvržený generátor hlásí selhání → vrátí se HTML stránka (fallback) | ne |
| Api | oprávnění u tří tiskových akcí zůstávají (stávající testy musí projít beze změny) | ne |
| Integration | **skutečné vygenerování**: třístránkové HTML → PDF má 3 strany a na druhé je text „Strana 2 z 3" | ano |

Poslední test je ten podstatný — bez něj by číslování nikdo neuhlídal. Text z PDF se vytáhne knihovnou **`UglyToad.PdfPig` 0.1.16** (MIT), přidanou **jen do testovacího projektu**; do aplikace se nedostane.

Test přeskočí sám sebe s jasnou hláškou, pokud na stroji není prohlížeč — aby nepadal u někoho, kdo Edge nemá.

## 11. Rizika

| Riziko | Ošetření |
|---|---|
| Na serveru není Edge / nesmí se spustit | fallback na dnešní tisk + vypínač v konfiguraci + položka v checklistu |
| Okraje z CSS a z generátoru se sečtou | ověří se měřením na skutečném výstupu, `@page` se případně vynuluje |
| Souběžné tisky vyčerpají paměť | brána `MaxConcurrent`, výchozí 2 |
| Uživatel ztratí automatické vyvolání tiskového dialogu | vědomá cena rozhodnutí U4; PDF se otevře v prohlížečce, tisk je jedno kliknutí |
| Zvětšení publish balíku | +7 MB, měřeno |

## 12. Co se nemění

Vzhled tištěného dokumentu, view modely, oprávnění tiskových akcí, filtry u tisku projektu, Word export, ani harmonogram a stránka záznamu.
