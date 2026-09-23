# A5 — Profil: konsolidace „Moje role a práva" + „Odvozená práva" do jedné struktury

**Datum:** 2026-07-08 · **Stav:** schváleno uživatelem (analýza 2026-07-08)

## Problém a kontext okolí

[Views/Profil/Index.cshtml](../../../PmTracker.Web/Views/Profil/Index.cshtml) má dnes:
1. **Identita** — `dt Role / dd = výčet RoleKody` (duplicitní s sekcí níže).
2. **Moje role a práva** — per aplikační role rozklikávací `<details class="profile-role-card">` (summary: název + kód + počet akcí) s tabulkou `Akce | Název | Přístup | Rozsah`. Tenhle vzor se userovi líbí a je cílový.
3. **Odvozená práva z projektu a subsystému** — plochá tabulka (`Akce | Název | Přístup | Rozsah | Zdroj`), `Zdroj` je text z `BuildGrantSourceSummary` („Projektová role: KOD (PROJEKT)").

Datový kontext (`ProfileService.PageQueries.cs`, `SecurityViewModels.cs:528`):
- `PermissionGrantViewModel`: `PermissionKey, ScopeLevel, ScopeMode, IsAllowed, ProjectIds, SourceType (APP_ROLE|PROJECT_ROLE|SUBSYSTEM_ROLE), SourceRoleCode, SourceProjectId` — **strukturované**, ale:
  - chybí **název role** (jen kód) pro projektové/subsystémové role,
  - chybí **identifikace subsystému** u SUBSYSTEM_ROLE grantů (user chce sloupec „Subsystém: OdEIP"),
  - všechny klíče jsou tytéž `PermissionKeys` jako u aplikačních rolí (potvrzuje proveditelnost jednotné struktury; filtry typu „jen můj záznam / subsystém" jsou v `ScopeSummary`).

## Řešení

**Jedna sekce „Moje role a práva"** — karta per **instance role** (role × projekt × subsystém), jednotný `<details>` vzor:

1. **Hlavička karty (summary):** `Typ role · Název role (kód) · Projekt · Subsystém · N akcí`.
   - Aplikační: `Aplikační · Super administrátor (SUPERADMIN) · — · — · 42 akcí`
   - Projektová: `Projektová · Projektový manažer (PM) · EIS · — · 18 akcí`
   - Subsystémová: `Subsystémová · Zástupce vedoucího subsystému (…) · EIS · OdEIP · 6 akcí` (userův příklad)
2. **Tělo karty:** stávající tabulka `Akce | Název | Přístup | Rozsah` (Rozsah dál nese filtry typu INCLUDE/vlastní záznam).
3. **Builder:** rozšířit projekci grantů o `SourceRoleName`, `SourceSubsystemKod/Nazev` (join `ciselnik_roli_projektu`/`ciselnik_roli_subsystemu` + `projekt_subsystemy`→`subsystemy`; pozor na SubsystemId↔ProjektSubsystemId mapping — viz memory subsystem-lead). Grupovat granty podle (SourceType, SourceRoleCode, SourceProjectId, SourceSubsystemId) → instance rolí. `BuildGrantSourceSummary` zaniká (nahrazeno strukturou); sekce „Odvozená práva" se smaže.
4. **Identita:** `dd` = **počet** („5 rolí") s odkazem-kotvou na `#moje-prava`.
5. Řazení karet: Aplikační → Projektová → Subsystémová; uvnitř dle projektu, subsystému, názvu role.

## Dotčené soubory
- `PmTracker.Web/Services/Profile/ProfileService.PageQueries.cs` — projekce + grupování na instance rolí
- `PmTracker.Web/Models/ViewModels/ProfilViewModels.cs` — `ProfilRolePravaViewModel` rozšířit (TypRole, Projekt, Subsystem) nebo nový `ProfilRoleInstanceViewModel`; smazat `ProfilOdvozenePravoViewModel`
- `PmTracker.Web/Models/ViewModels/SecurityViewModels.cs` — grant + `SourceRoleName`, `SourceSubsystem*` (ověřit dopad na ostatní konzumenty grantů — grep `PermissionGrantViewModel`)
- `PmTracker.Web/Views/Profil/Index.cshtml` — jednotná sekce; Identita počet
- `PmTracker.Web/wwwroot/css/site.css` — summary meta sloupečky (drobné)

## Akceptační kritéria
- Jedna sekce; každá karta = jedna instance role s hlavičkou Typ/Role/Projekt/Subsystém; rozklik ukáže akce.
- Subsystémová role ukazuje správný subsystém (userův příklad EIS/OdEIP ekvivalent na dev datech).
- Identita: počet rolí, ne výčet.
- Superadmin vidí totéž co dřív (žádná ztráta informace vůči dnešním dvěma sekcím).

## Testy
- Integration/Api: uživatel s projektovou + subsystémovou rolí → render obou karet se správnými hlavičkami.
- Unit: grupovací logika instancí (mapper) — kombinace APP/PROJECT/SUBSYSTEM grantů.

## Mimo scope
Změny výpočtu permission grantů (AuthorizationSnapshot); Nastavení ▸ role admin UI.
