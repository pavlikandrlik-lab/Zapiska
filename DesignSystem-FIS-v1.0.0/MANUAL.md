# DS FIS — governance a technický rámec

**DS FIS v1.0.0** je tenká nadstavba nad **Design systémem gov.cz v4.7.0**
(dále „DS gov" nebo „vanilla"). Tento dokument má dvě části:

- **Část A – Rozvoj DS FIS.** Jak se nadstavba udržuje: úpravy `ds-fis.css`
  / `ds-fis.js`, evidence změn, verzování, aktualizace verze DS gov.
- **Část B – Implementace do jiného projektu.** Jak DS FIS (a spolu s ní
  vždy i vanilla DS gov) vložit do cizí aplikace – na příkladu webové
  aplikace **ASP.NET Core 8**.

Referenční `index.html` („Obecná aplikace – Finanční informační systém")
slouží jen jako vzor rozvržení. Produkční aplikace nahradí obsah stránky a
řídí se pravidly níže.

---

# Část A — Rozvoj DS FIS

## A1. Účel a rozsah

| | |
|---|---|
| **Co DS FIS je** | množina odchylek od DS gov (barevná hlavička, širší obsah, vlastní mobilní hlavička, DS našeptávač, jemné orámování rozbalovacích boxů, menu uživatele, úpravy patičky) |
| **Kde odchylky žijí** | výhradně `assets/ds-fis/ds-fis.css` (vzhled) a `assets/ds-fis/ds-fis.js` (chování) |
| **Co DS FIS nemění** | žádný zdrojový soubor DS gov. Pracuje jen přes tokeny a veřejné selektory DS. |
| **Vratnost** | odebráním dvou řádků z `index.html` (`<link ds-fis.css>`, `<script ds-fis.js>`) se aplikace vrátí k čistému DS gov v4 |
| **Mimo rozsah** | obsah stránky, business logika, napojení na API, autentizace |

## A2. Role a odpovědnosti

| Role | Odpovědnost |
|------|-------------|
| **Správce DS FIS** | udržuje `ds-fis.css` / `ds-fis.js`, verzuje nadstavbu, vede [CHANGES.md](CHANGES.md), schvaluje nové odchylky |
| **Vývojář aplikace** | konzumuje nadstavbu beze změn, obsah dává do svých stránek, nové odchylky nepřidává mimo `ds-fis.*` |
| **Správce build/CI** | drží verzi DS gov, spouští `build.mjs`, po aktualizaci DS ověřuje shodu (A9) |

## A3. Zásady (pravidla nadstavby)

1. **Jeden CSS + jeden JS.** Veškeré vizuální odchylky jsou v `ds-fis.css`,
   veškeré doplňkové chování v `ds-fis.js`. Nikde jinde žádné vlastní CSS/JS.
2. **Jen tokeny a veřejné selektory DS.** Žádná úprava souborů v `assets/gov/`,
   `!important` jen tam, kde to DS vynucuje.
3. **Prefixy.** Nové třídy mají prefix `app-` (vzhled/struktura) nebo `js-`
   (háček pro skript, nikdy se na něj nestyluje).
4. **Rozměry a barvy do CSS.** V `index.html` nejsou žádné `style=""` ani
   rozměrové atributy pro vzhled (např. velikost `.app-logo` je jen v
   `ds-fis.css`). Výjimka: `padding` obalu navigace, který převzatá šablona
   DS drží inline.
5. **Offline.** Žádné CDN, žádné externí zdroje. Vše lokální v `assets/`.
6. **Vratnost.** Odebrání nadstavby nesmí rozbít markup – třídy `app-`/`js-`
   bez CSS/JS jsou neškodné.
7. **Verzování.** Každá změna odchylek = záznam v [CHANGES.md](CHANGES.md) a
   zvýšení verze DS FIS (A7). Verze se zobrazuje v patičce.
8. **Sledování DS.** Po každé aktualizaci DS gov se ověří, že se žádný
   selektor nadstavby nerozešel (A9).

## A4. Technické předpoklady

| Nástroj | Verze | K čemu |
|---------|-------|--------|
| **Node.js** | 22 LTS nebo novější | stažení a sestavení DS gov (`npm`, `build.mjs`) |
| **npm** | součást Node | instalace balíčků `@gov-design-system-ce/*` |
| **Editor** | VS Code *nebo* Visual Studio 2022 | editace, spuštění |
| **Prohlížeč** | Chrome / Edge / Firefox, aktuální | běh (WebComponents, ES moduly) |
| **Statický HTTP server** | přiložený `serve.js`, Live Server, … | běh při vývoji |

> **Proč server a ne dvojklik na `index.html`?**
> Webové komponenty DS gov jsou ES moduly a `gov-icon` si stahuje SVG přes
> `fetch()`. Z `file://` to prohlížeč zablokuje. Stejně to řeší i oficiální
> „vanilla" starter gov.cz (Vite). Proto vždy přes `http(s)://`.

Node.js je potřeba **jen k sestavení** lokální kopie DS gov. Samotný běh
žádný Node nevyžaduje.

## A5. Struktura projektu

```
projekt/
├─ index.html                 referenční stránka (vzor rozvržení)
├─ serve.js                   minimální statický server (jen vestavěné moduly Node)
├─ build.mjs                  sestavení assets/gov/ z balíčků @gov-design-system-ce
├─ package.json               závislosti (jen pro build)
├─ README.md · MANUAL.md · CHANGES.md
│
├─ assets/
│  ├─ ds-fis/
│  │  ├─ ds-fis.css           ← VŠECHNY vizuální odchylky (DS FIS)
│  │  └─ ds-fis.js            ← doplňkové chování (DS FIS)
│  ├─ logo_main_white.png     logo pro tmavou (modrou) hlavičku
│  ├─ logo_main_dark.png      logo pro světlý podklad
│  ├─ build-logo.py           Pillow skript, který obě barevné varianty vyrobí ze zdrojového loga
│  ├─ favicon.svg
│  └─ gov/                    ← LOKÁLNÍ KOPIE DS gov = „vanilla" (vytvoří build.mjs)
│     ├─ styles/  fonts/  icons/  components/  templates/
└─ node_modules/              jen pro build; k běhu není potřeba
```

Adresář **`assets/gov/` je samonosný** – po sestavení běží i bez
`node_modules`. Je vhodné držet ho ve verzování.

## A6. Postup – sestavení a spuštění pro vývoj

```bash
npm install          # stáhne @gov-design-system-ce/* + esbuild
node build.mjs        # zkopíruje CSS/písmo/ikony/komponenty do assets/gov/
node serve.js         # http://localhost:8000/  (port: PORT=3000 node serve.js)
```

Server posílá `Cache-Control: no-store`, takže v prohlížeči stačí **F5**.
VS Code: integrovaný terminál → `node serve.js`, nebo rozšíření *Live Server*.

Co dělá `build.mjs`:

1. `@gov-design-system-ce/styles/lib/*.css` → `assets/gov/styles/`
2. `@gov-design-system-ce/templates/lib/styles/{tokens,skip-links,index}.css`
   → `assets/gov/styles/` (jako `templates-tokens.css`, `skip-links.css`, `index.css`)
3. `@gov-design-system-ce/fonts/lib/roboto.css` + `*.woff2` → `assets/gov/fonts/`
4. `@gov-design-system-ce/icons/lib/{components,complex,colored}` → `assets/gov/icons/`
   (+ kopie `components` jako `templates`, kvůli markupu s `type="templates"`)
5. `@gov-design-system-ce/components/dist/core/*` → `assets/gov/components/`
6. `@gov-design-system-ce/templates/dist/scripts/scripts.js` → sbalí esbuildem
   do `assets/gov/templates/scripts.js`

## A7. Postup – aktualizace verze DS gov

```bash
npm i @gov-design-system-ce/{styles,templates,components,fonts,icons}@latest
node build.mjs
```

Poté projít kontrolní seznam A9.

## A8. Postup – vydání nové verze DS FIS

1. Změnu odchylky zanést **jen** do `ds-fis.css` / `ds-fis.js`.
2. Doplnit / upravit řádek v [CHANGES.md](CHANGES.md).
3. Zvýšit verzi DS FIS podle semver:
   - **patch** – oprava bez vizuální změny,
   - **minor** – nová odchylka nebo změna chování,
   - **major** – nekompatibilní změna (jiné třídy `app-`, jiná struktura markupu).
4. Aktualizovat verzi v patičce `index.html`
   (`Verze x.y.z · DS gov.cz 4.7.0 · DS FIS vx.y.z`) a v hlavičkách
   `ds-fis.css` / `ds-fis.js`.

### Přidání nebo změna odchylky (schvaluje Správce DS FIS)

- řeší se přes token / veřejný selektor DS, ne úpravou `assets/gov/`,
- patří do existující sekce `ds-fis.css` (§0–§10) nebo se založí nová,
- nová třída dostane prefix `app-` / `js-`,
- je zdokumentovaná v [CHANGES.md](CHANGES.md),
- ověří se, že vypnutí nadstavby nechá markup validní.

### Vypnutí nadstavby

Zakomentovat / smazat v `index.html`:

```html
<link rel="stylesheet" href="assets/ds-fis/ds-fis.css" />
<script src="assets/ds-fis/ds-fis.js" defer></script>
```

Aplikace pak vypadá jako čistý Design systém gov.cz v4.

## A9. Kontrola shody po aktualizaci DS gov

- [ ] `assets/gov/` se přegeneroval bez chyb, žádné 404 v konzoli.
- [ ] Hlavička je tmavě modrá, sjednocená s patičkou.
- [ ] Vyhledávací pole je na středu hlavičky, našeptávač se otevírá 6 px pod polem, neprůhledné pozadí, stejně vysoké řádky.
- [ ] Rozbalovací boxy (Sekce / Agendy) lícují se spodní hranou hlavičky, jemné modré orámování.
- [ ] Pod 1200 px: lupa + uživatel + hamburger v jedné řadě, navigace jako panel, podmenu se rozbaluje **pod** položkou.
- [ ] Menu uživatele neprobliká při F5.
- [ ] Projít [CHANGES.md](CHANGES.md) řádek po řádku – cílové selektory v nové verzi DS existují.

## A10. Nastavení a konfigurace

| Místo | Klíč | Význam |
|-------|------|--------|
| `index.html` (`<head>`, před loaderem) | `window.GOV_DS_CONFIG.iconsPath` | musí ukazovat na cestu k `…/gov/icons` |
| `index.html` | `GOV_DS_CONFIG.canValidateWcagOnRender` | `false` – vypnutá WCAG validace při renderu |
| `serve.js` | `PORT` (env) | port přiloženého serveru, výchozí `8000` |
| `ds-fis.css` §0 | `--fis-header-bg`, `--fis-header-breakpoint`, `--fis-popover-border`, … | tokeny nadstavby – jediné místo pro barevné/rozměrové doladění |
| `ds-fis.js` | `SEARCH_ITEMS` | ukázková data našeptávače – v produkci nahradit voláním API |

### Pořadí v `<head>` (NESMÍ se měnit)

```
1. window.GOV_DS_CONFIG            (iconsPath → …/gov/icons)
2. CSS DS gov                      tokens → templates-tokens → styles → layout →
                                   components → templates → animations → content →
                                   skip-links → index
3. fonts/roboto.css
4. ds-fis/ds-fis.css              ← nadstavba, POSLEDNÍ z CSS
5. <script type="module" components/core.esm.js>
6. <script defer templates/scripts.js> + initTemplateScripts()
7. <script defer ds-fis/ds-fis.js>   ← nadstavba, POSLEDNÍ ze skriptů
```

### Přeskakovací odkazy (`gov-skip-links`)

```html
<div class="gov-skip-links">
	<a href="#main-navigation">Přeskočit na navigaci</a>
	<a href="#main">Přeskočit na obsah</a>
</div>
```

Je to **standardní komponenta DS gov** (`skip-links.css` z balíčku
`templates`), ne nic vlastního. Odkazy jsou vizuálně skryté a zobrazí se
až při tabování z klávesnice. Umožní uživatelům klávesnice a čteček
obrazovky přeskočit opakující se hlavičku a skočit rovnou na navigaci
(`#main-navigation`) nebo obsah (`#main`). Jde o naplnění **WCAG 2.4.1
Bypass Blocks**. Blok nechte v markupu; cílová `id` (`main-navigation`,
`main`) musí na stránce existovat.

## A11. Časté potíže

| Projev | Příčina | Řešení |
|--------|---------|--------|
| Nestylovaná stránka, tlačítka jako čistý text | otevřeno přes `file://` | spustit přes server |
| 404 na `…/gov/...` | neproběhl `node build.mjs` | spustit build |
| Ikony se nenačítají | špatný `GOV_DS_CONFIG.iconsPath` | musí ukazovat na `…/gov/icons` |
| Našeptávač prázdný | ukázková data | `SEARCH_ITEMS` napojit na API |
| Po `npm i @…@latest` rozbité rozvržení | změna selektorů v nové verzi DS | porovnat s [CHANGES.md](CHANGES.md), doladit `ds-fis.css` |

---

# Část B — Implementace do jiného projektu (ASP.NET Core 8)

DS FIS nikdy nejde nasadit samostatně – vždy jede **na vanilla DS gov**.
Do cílového projektu se proto přenáší **obojí** a nasazuje se **zároveň**:

| Vrstva | Co to je | Odkud |
|--------|----------|-------|
| **vanilla** | `assets/gov/` – lokální kopie DS gov (CSS, písmo, ikony, komponenty, `scripts.js`) | vytvoří `build.mjs` z balíčků `@gov-design-system-ce/*` |
| **nadstavba** | `assets/ds-fis/ds-fis.css` + `ds-fis.js` | tento repozitář (DS FIS) |
| **vzor markupu** | `index.html` – hlavička, navigace, patička, page-heading | tento repozitář, přepíše se do Razor layoutu |

## B1. Zásada společného nasazení

- **Verzní pár.** V cílovém projektu se eviduje dvojice
  `DS gov x.y.z` + `DS FIS a.b.c`. Vanilla se nikdy neaktualizuje bez
  kontroly shody nadstavby (A9).
- **Stejný původ.** `assets/gov/` i `assets/ds-fis/` se servírují ze
  stejné aplikace (stejná doména), aby `fetch()` ikon nespadl na CORS.
- **Žádné CDN.** Obě vrstvy zůstávají lokální ve `wwwroot`.
- **Pořadí načítání** v Razor layoutu je stejné jako v `<head>` `index.html`
  (A10) – vanilla CSS → písmo → `ds-fis.css`; pak `core.esm.js` (module) →
  `scripts.js` (defer) + `initTemplateScripts()` → `ds-fis.js` (defer).

## B2. Umístění souborů ve `wwwroot`

```
wwwroot/
└─ assets/
   ├─ gov/            ← vanilla (celý výstup build.mjs)
   ├─ ds-fis/
   │  ├─ ds-fis.css
   │  └─ ds-fis.js
   ├─ logo_main_white.png   logo_main_dark.png
   └─ favicon.svg
```

## B3. Jak získat `assets/gov/` (vanilla)

Cílový projekt **nemusí** mít Node v runtime. `assets/gov/` se sestaví
jednou z tohoto repozitáře a zkopíruje, nebo se generuje v CI:

**Varianta 1 – ruční (jednorázově / při bumpu DS):**
```bash
# v repozitáři DS FIS
npm install
node build.mjs
# zkopírovat výsledek do cílového projektu
xcopy /E /I assets\gov  <cil>\wwwroot\assets\gov
copy assets\ds-fis\*    <cil>\wwwroot\assets\ds-fis\
```

**Varianta 2 – jako krok buildu cílového projektu** (`.csproj`), pokud je
Node k dispozici na build agentu:
```xml
<Target Name="BuildDsGov" BeforeTargets="Build"
        Condition="!Exists('$(MSBuildProjectDirectory)/wwwroot/assets/gov/components/core.esm.js')">
  <Exec Command="npm ci" WorkingDirectory="$(MSBuildProjectDirectory)/ds-fis-src" />
  <Exec Command="node build.mjs" WorkingDirectory="$(MSBuildProjectDirectory)/ds-fis-src" />
  <ItemGroup>
    <_DsGov Include="$(MSBuildProjectDirectory)/ds-fis-src/assets/gov/**/*" />
  </ItemGroup>
  <Copy SourceFiles="@(_DsGov)"
        DestinationFiles="@(_DsGov->'$(MSBuildProjectDirectory)/wwwroot/assets/gov/%(RecursiveDir)%(Filename)%(Extension)')" />
</Target>
```
(`ds-fis-src/` = tento repozitář jako submodul / podadresář; `node_modules`
a `assets/gov/` z něj patří do `.gitignore` cílového projektu.)

## B4. Zapojení v aplikaci (`Program.cs`)

```csharp
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseStaticFiles(new StaticFileOptions
{
    // .woff2 a další doplnit jen pokud je server sám nezná
    ContentTypeProvider = new FileExtensionContentTypeProvider
    {
        Mappings =
        {
            [".woff2"] = "font/woff2",
            [".mjs"]   = "text/javascript",
        }
    },
    OnPrepareResponse = ctx =>
    {
        // dlouhá cache pro neměnné assety vanilla + nadstavby
        if (ctx.File.Name.EndsWith(".woff2") || ctx.Context.Request.Path.StartsWithSegments("/assets/gov"))
            ctx.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
    }
});

app.MapRazorPages(); // nebo MVC / Blazor dle projektu
app.Run();
```

## B5. Razor layout (`_Layout.cshtml`, `<head>`)

```cshtml
<script>
    window.GOV_DS_CONFIG = { iconsPath: "/assets/gov/icons", canValidateWcagOnRender: false };
</script>

@* --- vanilla DS gov: pořadí nesmí měnit --- *@
<link rel="stylesheet" href="~/assets/gov/styles/tokens.css" />
<link rel="stylesheet" href="~/assets/gov/styles/templates-tokens.css" />
<link rel="stylesheet" href="~/assets/gov/styles/styles.css" />
<link rel="stylesheet" href="~/assets/gov/styles/layout.css" />
<link rel="stylesheet" href="~/assets/gov/styles/components.css" />
<link rel="stylesheet" href="~/assets/gov/styles/templates.css" />
<link rel="stylesheet" href="~/assets/gov/styles/animations.css" />
<link rel="stylesheet" href="~/assets/gov/styles/content.css" />
<link rel="stylesheet" href="~/assets/gov/styles/skip-links.css" />
<link rel="stylesheet" href="~/assets/gov/styles/index.css" />
<link rel="stylesheet" href="~/assets/gov/fonts/roboto.css" />

@* --- nadstavba DS FIS: vždy POSLEDNÍ z CSS --- *@
<link rel="stylesheet" href="~/assets/ds-fis/ds-fis.css" asp-append-version="true" />

<script type="module" src="~/assets/gov/components/core.esm.js"></script>
<script src="~/assets/gov/templates/scripts.js" defer></script>
<script>
    window.addEventListener("DOMContentLoaded", () => window.initTemplateScripts && window.initTemplateScripts());
</script>
<script src="~/assets/ds-fis/ds-fis.js" defer asp-append-version="true"></script>
```

Tělo layoutu (hlavička `gov-header`, `gov-skip-links`, navigace, patička)
se převezme 1:1 z `index.html`; do `@RenderBody()` jde jen obsah uvnitř
`<gov-container id="main">`.

## B6. Nasazení a provoz

- **Publikace:** `dotnet publish -c Release` – `wwwroot/assets/**` se
  publikuje automaticky jako statický obsah. Node na cílovém serveru
  není potřeba.
- **IIS / reverzní proxy:** ověřit MIME `font/woff2`, `image/svg+xml`,
  `text/javascript`; doporučeno HTTPS.
- **CSP:** povolit `script-src 'self'` a inline `initTemplateScripts`
  volání (nebo přesunout do souboru a přidat hash). `fetch` ikon jde na
  `'self'` – žádná výjimka pro cizí doménu.
- **Aktualizace DS:** provádí se v repozitáři DS FIS (A7), znovu se
  vygeneruje `assets/gov/`, projde kontrola A9 a teprve pak se nová
  dvojice `gov` + `ds-fis` zkopíruje do cílového projektu.
- **Verze v patičce** cílové aplikace drží stejný formát
  `Verze … · DS gov.cz x.y.z · DS FIS a.b.c`.

## B7. Kontrola po zapojení

- [ ] `/assets/gov/components/core.esm.js` i `/assets/ds-fis/ds-fis.css` vrací 200.
- [ ] V konzoli prohlížeče nejsou 404 (typicky ikony → špatný `iconsPath`).
- [ ] Hlavička modrá, našeptávač, rozbalovací boxy a mobilní chování dle A9.
- [ ] `gov-skip-links` cílí na existující `#main-navigation` a `#main`.
- [ ] Vypnutím `ds-fis.css` + `ds-fis.js` spadne aplikace na čistý DS gov (ne rozbité).
