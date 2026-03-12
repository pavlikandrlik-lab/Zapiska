# PM Tracker - Technická dokumentace 09: Testování a kvalita

## 1. Účel
Dokument definuje testovací strategii, minimální quality gate a provozní pravidla pro validaci release.

## 2. Publikum a role
- QA: provedení test matrix a smoke scénářů.
- Vývojář: implementace a údržba testů.
- Ops: validace build artefaktu před nasazením.

## 3. Závislosti a předpoklady
- .NET SDK 8.
- Docker/Colima pro integrační/E2E vrstvu.
- Připravený lokální SQL container pro test běhy (dle test fixture).

## 4. Vstupy a výstupy
### Vstupy
- Test skripty v `/Users/Pavel.Andrlik/Documents/PM Tracker/scripts`.
- Test projekty `PmTracker.Tests.*`.
- Dokumentační quality gate skript.

### Výstupy
- Test reporty (`artifacts/test-results`, `artifacts/coverage`, `artifacts/mutation`).
- Potvrzení, že release splňuje minimální quality kritéria.

## 5. Detailní postup
### 5.1 Kompletní test pipeline
```bash
/Users/Pavel.Andrlik/Documents/PM\ Tracker/scripts/test-prereq-check.sh
/Users/Pavel.Andrlik/Documents/PM\ Tracker/scripts/test-all.sh
```

### 5.2 Jednotlivé vrstvy
```bash
/Users/Pavel.Andrlik/Documents/PM\ Tracker/scripts/test-unit.sh
/Users/Pavel.Andrlik/Documents/PM\ Tracker/scripts/test-integration.sh
/Users/Pavel.Andrlik/Documents/PM\ Tracker/scripts/test-api.sh
/Users/Pavel.Andrlik/Documents/PM\ Tracker/scripts/test-e2e.sh
/Users/Pavel.Andrlik/Documents/PM\ Tracker/scripts/test-mutation.sh
```

### 5.3 Coverage gate
- Min baseline:
  - line coverage >= 75 %
  - branch coverage >= 65 %

### 5.4 Dokumentační quality gate
```bash
/Users/Pavel.Andrlik/Documents/PM\ Tracker/scripts/check-docs-quality.sh
```
Kontroly:
- dead-link kontrola v `docs/` a `install.md`,
- povinné ISO/SOP sekce v `docs/technical/*.md`,
- konzistence základního názvosloví.

## 6. Verifikace
- Všechny test skripty končí `exit code 0`.
- Coverage summary splní threshold.
- Dokumentační quality gate projde bez chyb.

## 7. Rollback
- Pokud quality gate failne:
  - release je blokován,
  - oprav chyby a testy opakuj od kroku 5.1.

## 8. Troubleshooting
- Docker daemon není dostupný:
  - spusť Docker Desktop/Colima.
- E2E browser chybí:
  - spustí se automatická instalace Playwright; při failu proveď manuální bootstrap.
- Dokumentační link checker hlásí false-positive:
  - ověř relativní cesty a anchor syntax.

## 9. Audit a traceability
- Test skripty: `/Users/Pavel.Andrlik/Documents/PM Tracker/scripts`
- Test matrix (repo): `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Tests.*`
- Docs quality gate: `/Users/Pavel.Andrlik/Documents/PM Tracker/scripts/check-docs-quality.sh`
