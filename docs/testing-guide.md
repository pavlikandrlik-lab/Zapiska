# Zápiska Testing Guide

## Obsah
1. [Cíl](#cil)
2. [Vrstva testů](#vrstva-testu)
3. [Předpoklady](#predpoklady)
4. [Rychlý start](#rychly-start)
5. [Jednotlivé skripty](#jednotlive-skripty)
6. [Výstupy a reporty](#vystupy-a-reporty)
7. [Coverage gate](#coverage-gate)
8. [Mutation testing](#mutation-testing)
9. [E2E režim](#e2e-rezim)
10. [Nejčastější problémy](#nejcastejsi-problemy)

## Cíl
Tento dokument popisuje lokální testovací pipeline pro PM Tracker bez CI.

Pipeline pokrývá:
- `Unit` testy logiky a oprávnění,
- `Integration` testy data layer nad SQL Serverem,
- `API` testy controllerů přes `WebApplicationFactory`,
- `E2E` smoke scénáře v browseru (Playwright).

## Vrstva testů
- `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Tests.Unit`
- `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Tests.Integration`
- `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Tests.Api`
- `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Tests.E2E`

Sdílené helpery:
- `/Users/Pavel.Andrlik/Documents/PM Tracker/tests/Common`

## Předpoklady
- `.NET SDK` (net8 build target)
- `Docker`/`Colima` spuštěný
- internet pro první restore NuGet balíčků

Kontrola před spuštěním:
```bash
/Users/Pavel.Andrlik/Documents/PM\ Tracker/scripts/test-prereq-check.sh
```

## Rychlý start
Spusť celý pipeline jedním příkazem:
```bash
/Users/Pavel.Andrlik/Documents/PM\ Tracker/scripts/test-all.sh
```

Kroky v pipeline:
1. prerequisite check
2. restore + build
3. unit
4. integration
5. api
6. e2e
7. coverage aggregation + threshold check
8. mutation report (report-only)

## Jednotlivé skripty
- Unit:
```bash
/Users/Pavel.Andrlik/Documents/PM\ Tracker/scripts/test-unit.sh
```
- Integration:
```bash
/Users/Pavel.Andrlik/Documents/PM\ Tracker/scripts/test-integration.sh
```
- API:
```bash
/Users/Pavel.Andrlik/Documents/PM\ Tracker/scripts/test-api.sh
```
- E2E:
```bash
/Users/Pavel.Andrlik/Documents/PM\ Tracker/scripts/test-e2e.sh
```
- Mutation (non-blocking):
```bash
/Users/Pavel.Andrlik/Documents/PM\ Tracker/scripts/test-mutation.sh
```

## Výstupy a reporty
- TRX + raw coverage:
  - `/Users/Pavel.Andrlik/Documents/PM Tracker/artifacts/test-results/`
- Agregovaná coverage:
  - `/Users/Pavel.Andrlik/Documents/PM Tracker/artifacts/coverage/report/`
  - `/Users/Pavel.Andrlik/Documents/PM Tracker/artifacts/coverage/summary.txt`
- Mutation report:
  - `/Users/Pavel.Andrlik/Documents/PM Tracker/artifacts/mutation/`

## Coverage gate
Aktuální (fáze 1):
- line coverage >= `75%`
- branch coverage >= `65%`

Pokud limit neprojde, `test-all.sh` skončí chybou.

## Mutation testing
`test-mutation.sh` běží v režimu report-only:
- generuje report,
- neblokuje pipeline (`test-all.sh`) při non-zero výsledku,
- cílem je sběr baseline před zpřísněním gate ve fázi 2.

Výchozí běh je omezen mutate filtrem na menší sadu souborů, aby byl lokálně použitelný.
Pokud chcete vlastní scope, nastavte:
```bash
MUTATION_MUTATE="**/Services/Data/SqlServerDataStore.cs" /Users/Pavel.Andrlik/Documents/PM\ Tracker/scripts/test-mutation.sh
```

## E2E režim
Default je headless Chromium.

Pro debug (headed):
```bash
E2E_HEADED=1 /Users/Pavel.Andrlik/Documents/PM\ Tracker/scripts/test-e2e.sh
```

E2E fixture:
- vytvoří SQL DB přes Testcontainers,
- nabootstrapuje DB skripty dle `/docs/db-bootstrap.md`,
- spustí aplikaci lokálně na `http://127.0.0.1:5188`,
- provede browser scénáře,
- ukončí proces aplikace a container.

## Nejčastější problémy
1. Docker neběží
- Chyba: `Docker daemon is not available`
- Řešení: spustit Docker Desktop nebo Colima.

2. Playwright browser není nainstalovaný
- `test-e2e.sh` se pokusí nainstalovat `chromium` automaticky (`playwright.sh`, `playwright.ps1+pwsh`, nebo bundled node CLI fallback).

3. Pomalé první spuštění
- první běh stahuje NuGet balíčky, Playwright browser a Docker image.

4. SQL bootstrap chyba
- zkontroluj pořadí a existenci SQL souborů podle `/Users/Pavel.Andrlik/Documents/PM Tracker/docs/db-bootstrap.md`.
