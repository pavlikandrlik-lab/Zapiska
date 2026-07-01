# Offline-first nasazení — závazné pravidlo

## Shrnutí

**Aplikace PM Tracker MUSÍ být kompletně spustitelná bez přístupu na internet.**

Produkční server běží v uzavřeném prostředí (intranet, bez veřejného internetu).
Žádný requirement v runtime / pageloadu nesmí záviset na externí službě nebo CDN.

## Co to znamená konkrétně

### Zakázané praktiky

1. **CDN odkazy v HTML/CSS** — žádné `<link href="https://cdn...">`, `<script src="https://unpkg..."`,
   `@import url("https://...")` atd. Výjimka: komentář/metadata URL (např. sourcemap hint),
   který nezpůsobí runtime request.
2. **Web fonts z Google Fonts** nebo jiných externích hostů. Pokud potřebujeme font, stáhneme
   `.woff2` do `PmTracker.Web/wwwroot/lib/fonts/` a servírujeme přes `@font-face` s lokálním URL.
3. **Externí API callbacks** za runtime (gravatar, analytika, tracking pixely). Cokoliv, co
   browser v default instalaci zavolá z cizí domény.
4. **npm packages loaded přes CDN ESM imports** — např. `import from 'https://esm.sh/...'`.
5. **Automatické aktualizace** komponent přes síť (např. SW se self-update mechanismem sahající
   na cizí endpoint).

### Povinné praktiky

1. **Všechny JS/CSS knihovny jsou v `PmTracker.Web/wwwroot/lib/<nazev-knihovny>/`** a servírují
   se přes `asp-append-version="true"` pro cache-busting.
2. **Obrázky, ikony, SVG** v `PmTracker.Web/wwwroot/images/` nebo `~/lib/<knihovna>/...`.
3. **NuGet balíčky** (backend) jsou součást `dotnet publish` output — offline server je nepotřebuje
   dalšího stažení.
4. **Docker images** pokud se používají — build na stroji s internetem, push do privátního registry
   dostupného offline serveru.

## Kontrolní seznam při code review

Každá PR, která přidává novou knihovnu nebo asset, musí projít:

- [ ] `grep -rn "https://" PmTracker.Web/Views PmTracker.Web/wwwroot/css` — vrací jen komentáře
      a data references (ne `<link>`, `<script>`, `@import`).
- [ ] `grep -rn "cdn\." PmTracker.Web/Views` — 0 matches.
- [ ] Všechny nové assety jsou v `PmTracker.Web/wwwroot/lib/...` nebo `PmTracker.Web/wwwroot/images/`.
- [ ] Knihovna má README/LICENSE ponechaná vedle (respektuj open source licence).
- [ ] `.gitignore` nevylučuje novou složku v `wwwroot/lib/` (jinak chybí v repo/publishi).

## Testy

- `PmTracker.Tests.Unit/Layout/OfflineAssetsTests.cs` — ověřuje:
  - `_Layout.cshtml` neobsahuje `cdn.` / `https://cdn`
  - `~/lib/gov-design-system/` assety fyzicky existují v repozitáři
  - `site.css` neobsahuje `@import url("https://...")`

## Historie / aktuální knihovny

| Knihovna              | Lokace                                   | Verze  | Zdroj                                                               |
|----------------------|------------------------------------------|--------|---------------------------------------------------------------------|
| Quill rich text      | `~/lib/quill/`                           | (viz)  | npm `quill`                                                         |
| gov-design-system    | `~/lib/gov-design-system/`               | 4.2.9  | npm `@gov-design-system-ce/components`, `@gov-design-system-ce/styles` 4.2.7 |
| Apache ECharts       | `~/lib/echarts/` (`echarts.esm.min.js`)  | 5.5.1  | npm `echarts` (dist ESM); grafy základního projektového reportu     |

## Publish workflow

`dotnet publish PmTracker.Web -c Release -o ./publish` zkopíruje **celý** `wwwroot/lib/` obsah
do výsledné složky. Po publishi ověř:

```bash
ls publish/wwwroot/lib/gov-design-system/dist/core/ | wc -l   # min 89
ls publish/wwwroot/lib/gov-design-system/styles/lib/tokens.min.css
```

Pokud některý soubor chybí, offline nasazení selže (search pole se vůbec nezobrazí).

## Výjimky

**Žádné.** Pokud je nový use case, který zdánlivě potřebuje internet (mapa, externí validace,
atd.), diskutuj s uživatelem — buď najdeme offline alternativu, nebo se feature odmítá.
