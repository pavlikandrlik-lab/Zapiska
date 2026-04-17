# Specifikace — spouštění testů lokálně (macOS + Colima)

Dokumentuje, jak spustit celou testovací sadu (unit, api, integration) lokálně
na macOS s Colima jako Docker backendem.

---

## Prerekvizity

1. **Colima běží** s Docker enginem:
   ```bash
   colima status
   # měl by vypisovat "colima is running"
   ```
   Pokud ne: `colima start`.

2. **Docker socket** existuje na:
   ```
   /Users/<user>/.colima/default/docker.sock
   ```

3. **SQL container** již běží (výstup `docker ps` zahrnuje např.
   `mcr.microsoft.com/azure-sql-edge:latest`).
   Testy si ale stejně startují vlastní testcontainer přes
   Testcontainers.MsSql, takže přítomnost pmtracker-sql containeru nevadí
   (jen zabírá port 1433 pro dev).

---

## Environment proměnné pro test běh

Testcontainers na Colima vyžaduje:

```bash
export DOCKER_HOST=unix:///Users/$USER/.colima/default/docker.sock
export TESTCONTAINERS_RYUK_DISABLED=true
```

- `DOCKER_HOST` — bez toho Testcontainers hledá `/var/run/docker.sock`,
  který na Colima nevzniká.
- `TESTCONTAINERS_RYUK_DISABLED=true` — Ryuk (resource reaper) selhává
  na Colima, protože potřebuje bind-mount `docker.sock` do containeru
  a Colima VM socket nepodporuje bind. Bez Ryuka se containery jen nečistí
  automaticky; lze je ručně smazat `docker rm -f $(docker ps -aq -f ancestor=mcr.microsoft.com/azure-sql-edge)`.

---

## Spuštění testů

### Unit testy (rychlé, bez SQL)

```bash
dotnet test PmTracker.Tests.Unit
```

Očekávaný počet: 221+ testů, trvá ~1 s.

### API testy (pomalé, s SQL testcontainerem)

```bash
DOCKER_HOST=unix:///Users/$USER/.colima/default/docker.sock \
TESTCONTAINERS_RYUK_DISABLED=true \
dotnet test PmTracker.Tests.Api
```

Očekávaný počet: 282+ testů, trvá ~13 s (první běh déle kvůli pull image).

### Integration testy

```bash
DOCKER_HOST=unix:///Users/$USER/.colima/default/docker.sock \
TESTCONTAINERS_RYUK_DISABLED=true \
dotnet test PmTracker.Tests.Integration
```

Očekávaný počet: 77+ testů, trvá ~80 s.

### Všechno najednou

```bash
DOCKER_HOST=unix:///Users/$USER/.colima/default/docker.sock \
TESTCONTAINERS_RYUK_DISABLED=true \
dotnet test
```

---

## Co testcontainer dělá

1. Spustí nový container `mcr.microsoft.com/azure-sql-edge:latest` s heslem
   `PmTracker!Test2026`, mapuje volný port na 1433.
2. Počká na připravenost SQL (2min timeout).
3. Vytvoří novou databázi `api_<timestamp>_<guid>` nebo
   `pmtracker_test_<timestamp>_<guid>` (unikátní per test run).
4. Spustí SQL skripty dle `docs/technical/06-database-bootstrap-migrations.md`
   sekce 5.1 — baseline + upgrade patche + `db_seed_dev_admin.sql` (dev role admin).
5. Test suite sdílí jednu instanci containeru (xUnit collection).
6. Po dokončení testů container disposed (Testcontainers).

Implementace: [tests/Common/SqlServerTestDatabaseManager.cs](../../tests/Common/SqlServerTestDatabaseManager.cs),
[tests/Common/RepositoryPaths.cs](../../tests/Common/RepositoryPaths.cs),
[tests/Common/SqlScriptRunner.cs](../../tests/Common/SqlScriptRunner.cs).

---

## Kritické upgrade skripty pro testcontainer

Testcontainer buildí DB od nuly, takže MUSÍ projít celá sekvence 5.1
z dokumentu 06. Pokud přidáváš nový upgrade patch:

1. Přidej `.sql` do root repozitáře.
2. Zaregistruj ho v `docs/technical/06-database-bootstrap-migrations.md`
   sekce 5.1, pořadí dle závislosti na předchozích patchích.
3. Spusť api testy — pokud projdou, seed je OK.

### Známé skriptové problémy (historicky)

- **db_upgrade_0_4_membership_subsystems.sql** obsahoval INSERT…SELECT se
  sloupcem `dbo.subsystemy.vedouci_osoba_id`, který v baseline chyběl
  (v produkci byl přidán ručně). T-SQL parse-time name resolution
  vyhazoval `Invalid column name` i když se `IF COL_LENGTH(...)` vyhodnocovalo
  jako false. Fix: přepsat INSERT do dynamického SQL (`sp_executesql`),
  které se parsuje až v runtime. **Opraveno** v commitu [TBD].

- **Role PROJ_MAN a GEST** chyběly v baseline/upgrade. Přidány v
  `db_upgrade_1_1_6_project_roles_manager_gestor.sql`.

---

## CI environment

Pokud v GitHub Actions / Azure Pipelines:

- Docker daemon bývá dostupný na `/var/run/docker.sock` bez úprav —
  `DOCKER_HOST` neexportovat.
- Ryuk bývá funkční — `TESTCONTAINERS_RYUK_DISABLED` neexportovat.

---

## Pravidla

1. **Pro každou změnu serverové logiky** spusť api testy lokálně před commit.
2. **Neslibuj „poběží v CI" bez ověření** — integrace může selhat na
   novém upgrade skriptu, změně seed, chybějícím sloupci apod.
3. **Při přidání SQL skriptu** aktualizuj dokument 06 a ověř test run.
