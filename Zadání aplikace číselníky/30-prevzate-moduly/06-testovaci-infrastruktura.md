# Testovací infrastruktura

> **Stav: 🟢 mapa harvestu hotová; fixture a podvržená identita vloženy do P1.**

## Co se přebírá

Ne jednotlivé testy, ale **infrastruktura, která je drží** — fixtures, testovací
autentizace, izolace databáze, spouštěcí skripty a governance vizuálních testů.

## Zdroje k harvestu ze Zápisky

| Soubor | Obsah |
|---|---|
| `PmTracker.Tests.Api/TestInfrastructure/PmTrackerWebAppFactory.cs` | Spuštění aplikace v testu |
| `PmTracker.Tests.Api/TestInfrastructure/TestAuthHandler.cs` | Podvržená identita — jak testovat aplikaci s Windows Auth |
| `PmTracker.Tests.Api/TestInfrastructure/NoOpAntiforgery.cs` | Vypnutí ochrany proti CSRF v testu |
| `PmTracker.Tests.Api/TestInfrastructure/ApiSqlFixture.cs` | Databáze pro HTTP testy |
| `PmTracker.Tests.Integration/TestInfrastructure/SqlIntegrationFixture.cs` | Izolovaná databáze pro integrační testy |
| `PmTracker.Tests.Integration/TestInfrastructure/IntegrationTestDataStore.cs` | Testovací datová vrstva |
| `PmTracker.Tests.E2E/TestInfrastructure/E2ETestFixture.cs` | Playwright fixture |
| `scripts/test-prereq-check.sh` | Kontrola předpokladů před během |
| `scripts/test-all.sh`, `test-unit.sh`, `test-integration.sh`, `test-api.sh`, `test-e2e.sh`, `test-mutation.sh` | Spouštěcí skripty vrstev |
| `docs/technical/11-visual-testing-governance.md` | Governance vizuálních testů, testovací matice, pasti |
| `PmTracker.Tests.Unit/Architecture/` | Architektonické testy — hlídají pravidla, ne chování |

## Poučení, která musí být v zadání explicitně

- **Playwright a web komponenty.** Host gov komponenty je pro Playwright „neviditelný",
  vysoká karta „nestabilní". Klikat se musí dispatchem události, čekat na hydrataci
  komponenty, viditelnost ověřovat počtem prvků. Screenshot vysokého prvku vyprší.
- **Assertace na český text v HTML testech selhávají** — šablonovací vrstva kóduje diakritiku
  na HTML entity. Kotvit se na atributy a ASCII; textové hlášky testovat na úrovni modelu.
- **Architektonický test je levnější než code review.** Pravidlo, které lze vyjádřit testem,
  se vyjádří testem.
- Regresní test musí **před opravou selhat**. Test, který projde i na rozbité verzi, nic nedrží.

## Rozhodnuto

- **T1** Vitest + React Testing Library · **T2** lokální instance SQL Server · **T3** Playwright v .NET
