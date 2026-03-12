# PM Tracker - Technická dokumentace 10: Troubleshooting a recovery

## 1. Účel
Dokument definuje standardní postupy pro diagnostiku incidentů, rozhodnutí o rollbacku a bezpečnou obnovu služby.

## 2. Publikum a role
- Ops on-call: primární incident response.
- DBA on-call: databázové incidenty.
- Vývoj on-call: aplikační bugfix diagnostika.

## 3. Závislosti a předpoklady
- Přístup k IIS serveru a SQL serveru.
- Přístup k poslednímu stabilnímu release artefaktu.
- Přístup k release changelogu.

## 4. Vstupy a výstupy
### Vstupy
- Chybové hlášky z UI nebo monitoringu.
- HTTP status kódy a čas incidentu.
- Poslední deploy metadata.

### Výstupy
- Klasifikovaný incident se zvolenou recovery strategií.
- Ověřená obnova provozu.
- Incident report s root cause summary.

## 5. Detailní postup
### 5.1 Triáž incidentu
1. Urči rozsah: všechny uživatele / vybraná role / vybraný modul.
2. Urči dopad: outage / partial degradation / data inconsistency.
3. Zaznamenej timestampy a poslední změny (deploy, config, DB patch).

### 5.2 Nejčastější incidenty
- `500.30` po deployi:
  - zkontroluj appsettings + DB konektivitu + hosting bundle.
- `403` pro legitimního uživatele:
  - zkontroluj mapování role/permission/scope.
- Selhání exportu:
  - validuj vstupní data a export pipeline.

### 5.3 Rozhodovací strom recovery
- Pokud je problém konfigurační a rychle reverzibilní:
  - rollback konfigurace + recycle app poolu.
- Pokud je problém release binárky:
  - rollback publish balíčku.
- Pokud je problém v DB změně:
  - DBA restore z backup podle schváleného postupu.

### 5.4 Post-recovery validace
- Ověř klíčové route (`/Projekty`, `/Jednani`, `/Ciselniky`, `/Nastaveni`).
- Ověř kritické use-cases (create/edit + export).
- Ověř dokumentaci/changelog konzistenci vůči nasazené verzi.

### 5.5 Incident report minimum
- Incident ID
- UTC timeline
- Detekce, triáž, akce, recovery, root cause
- Preventivní opatření

## 6. Verifikace
- Obnovená dostupnost aplikace potvrzená z více klientů.
- Žádné otevřené kritické chyby po recovery.
- Incident report uzavřen a schválen vlastníkem služby.

## 7. Rollback
- Preferuj nejmenší bezpečný rollback (config -> binary -> DB).
- Každý rollback krok zaznamenej v incident timeline.

## 8. Troubleshooting
- Incident není reprodukovatelný:
  - validuj časové okno a environment drift.
- Sporadická chyba oprávnění:
  - porovnej efektivní práva pro konkrétního uživatele a projekt.
- Sporadická DB chyba:
  - validuj kompatibilitu DB schema se spuštěnou verzí aplikace.

## 9. Audit a traceability
- Release notes: `/Users/Pavel.Andrlik/Documents/PM Tracker/CHANGELOG.md`
- Verzované release podklady: `/Users/Pavel.Andrlik/Documents/PM Tracker/docs/changelog/releases/`
- Dokumentační routing: `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/Controllers/DokumentaceController.cs`
