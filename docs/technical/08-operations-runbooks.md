# PM Tracker - Technická dokumentace 08: Provozní runbooky

## 1. Účel
Dokument konsoliduje opakovatelné provozní SOP postupy pro denní správu, release cutover a incident response.

## 2. Publikum a role
- Ops administrátor: infrastruktura, restart, dostupnost.
- Aplikační administrátor: práva a provozní data.
- QA: post-release smoke validace.

## 3. Závislosti a předpoklady
- Přístup do IIS Manageru.
- Přístup do SQL Serveru.
- Přístup k release balíčku a changelogu.

## 4. Vstupy a výstupy
### Vstupy
- Deploy artefakt.
- SQL skripty.
- Tento runbook.

### Výstupy
- Provedený release bez regresí.
- Auditní stopa provedených kroků.

## 5. Detailní postup
### 5.1 Runbook A: Standardní restart služby
```powershell
iisreset
```
Kroky:
1. Informuj uživatele o krátké nedostupnosti.
2. Proveď restart.
3. Ověř `/Projekty` a `/Jednani`.

### 5.2 Runbook B: Release cutover
1. Potvrď dostupnost DB backupu.
2. Nasad publish balíček.
3. Aplikuj DB upgrade skripty (pokud jsou součást release).
4. Restartuj IIS.
5. Proveď smoke test:
  - create/edit projektu,
  - create/edit jednání,
  - add/edit vyjádření,
  - PDF/Word export.
6. Zapiš výsledek do provozního logu.

### 5.3 Runbook C: Onboarding prvního provozního admina
1. Ověř, že AD účet existuje, ale počítej s tím, že se do aplikace nepropíše automaticky.
2. Založ osobu v `dbo.osoby` se správným `Guid_AD`.
3. Přidej osobu do `authz.superadmins` nebo přiřaď admin roli.
4. Ověř přístup do `Nastavení`.

Kontrolní SQL:
```sql
SELECT id, jmeno, prijmeni, Guid_AD FROM dbo.osoby WHERE Guid_AD = '<GUID_Z_AD>';
SELECT osoba_id FROM authz.superadmins WHERE osoba_id = <ID_Z_TABULKY_OSOBY>;
```

### 5.4 Runbook D: Obnova po selhání deploye
1. Aktivuj rollback podle sekce 7.
2. Vrať předchozí publish balíček.
3. Ověř základní route a exporty.
4. Eskaluj incident s detailním timeline.

### 5.5 Runbook E: Porelease validační checklist
- [ ] Dokumentace a changelog odpovídají deploynuté verzi.
- [ ] Aplikační verze v patičce odpovídá release.
- [ ] Žádné 500/403 regresní chyby pro provozní role.
- [ ] Ověřeny přístupy minimálně na 2 reálných účtech.

## 6. Verifikace
- Každý runbook krok musí mít stav `provedeno/neprovedeno`.
- Po cutover musí být potvrzeno funkční UI + export + authz.
- Incident runbook musí mít zaznamenaný UTC timestamp a osobu, která krok provedla.

## 7. Rollback
- Aplikace: návrat na poslední funkční publish složku.
- Konfigurace: návrat `appsettings.Production.json`.
- DB: restore poslední ověřené backup, pokud patch nelze bezpečně vrátit.

## 8. Troubleshooting
- Smoke test failuje jen pro vybrané role:
  - kontrola authz mapování a role assignments.
- UI funguje, export nefunguje:
  - kontrola export endpointů, datové konzistence a logů aplikace.
- Dokumentace neodpovídá release:
  - regeneruj `CHANGELOG.md` a aktualizuj markdown zdroje.

## 9. Audit a traceability
- Changelog generator: `/Users/Pavel.Andrlik/Documents/PM Tracker/scripts/generate-changelog.sh`
- Release notes source: `/Users/Pavel.Andrlik/Documents/PM Tracker/docs/changelog/releases/`
- Produkční readiness checklist (tento soubor + sekce 5.5)
