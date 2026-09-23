# Dokumentace, wiki knihovna, known issues, changelog

> **Stav: 🟢 mapa harvestu hotová; wiki staví P9.**

## Co modul v Zápisce dělá

Markdown dokumentace uložená v repozitáři je **renderovaná přímo v aplikaci** pod vlastní
routou. Uživatel čte nápovědu v aplikaci, vývojář ji edituje jako soubory v gitu —
jeden zdroj pravdy, žádná druhá kopie v databázi.

Wiki má navigační strom podle složek a stránky se strukturou „začátek → oblasti → pomoc".

## Zdroje k harvestu ze Zápisky

| Soubor | Obsah |
|---|---|
| `PmTracker.Web/Services/Documentation/IDocumentationService.cs` | Rozhraní |
| `PmTracker.Web/Services/Documentation/MarkdownDocumentationService.cs` | Render markdownu, navigace |
| `PmTracker.Web/Views/Dokumentace/` | Obrazovky (v Číselnících se přepíší do Reactu) |
| `docs/wiki/` | Struktura wiki knihovny — vzor členění |
| `docs/known-issues/` | Formát evidence známých problémů |
| `docs/changelog/releases/` + `scripts/generate-changelog.sh` | Generovaný changelog |
| `scripts/check-docs-quality.sh` | Kontrola mrtvých odkazů a povinných sekcí |
| `PmTracker.Tests.Api/Controllers/DocumentationNavigationTests.cs` | Testy navigace |
