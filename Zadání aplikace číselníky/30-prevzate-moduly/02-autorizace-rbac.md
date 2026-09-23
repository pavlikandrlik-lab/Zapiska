# Autorizace — seed-only RBAC

> **Stav: 🟢 mapa harvestu hotová; model přenesen a zjednodušen v P2.**

Nejcennější převzatý modul. V Zápisce prošel kompletním redesignem na per-action klíče
a je provozně ověřený.

## Principy modelu

1. **Seed-only.** Role, oprávnění i jejich mapování jsou definované **výhradně v kódu**
   a verzované v gitu. Runtime UI pro skládání rolí neexistuje — zabraňuje tichým driftům
   mezi prostředími a zajišťuje auditní stopu v historii repozitáře.
2. **Jeden klíč = jedna mutující akce.** Ne hrubé „edit" klíče. Zápiska má cca 76 klíčů
   v kategoriích (záznamy, komentáře, jednání, tým, osoby, export, nastavení, číselníky…).
3. **Dvouúrovňový scope.** Na úrovni akce `GLOBAL` / `PROJECT`; na úrovni mapování
   role→akce `ALL` / `INCLUDE` (výčet konkrétních objektů).
4. **Per-request snapshot.** Z databáze se sestaví kanonická in-memory projekce efektivních
   práv osoby (globální + per-objekt) a všechny kontroly čtou z ní.
5. **Deklarativní kontrola.** Atribut s policy `permission:{klic}` na akci; handler přečte
   identifikátor objektu z routy a deleguje na snapshot.
6. **Architektonické testy jako pojistka.** Test selže, když klíč existuje v konstantách,
   ale chybí v seedu; když role nemá žádné mapování; když mutující akce nemá policy.

## Zdroje k harvestu ze Zápisky

| Soubor | Obsah |
|---|---|
| `PmTracker.Web/Services/Security/AuthorizationSnapshot.cs` | Projekce efektivních práv |
| `PmTracker.Web/Services/Security/AuthorizationSnapshotBuilder.cs` | Sestavení snapshotu z DB |
| `PmTracker.Web/Services/Security/AuthorizationService.cs` | Kanonická fasáda, per-request cache |
| `PmTracker.Web/Services/Security/PermissionAuthorizationHandler.cs` | Napojení na policy |
| `PmTracker.Web/Services/Security/PermissionRequirement.cs` | Requirement |
| `PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs` | **Jediný zdroj pravdy**: akce + role + mapování |
| `PmTracker.Web/Services/Security/PermissionSeeder.cs` | Aplikace seedu při startu |
| `PmTracker.Web/Services/Security/PermissionScopeLevel.cs`, `ScopeMode.cs`, `RoleScope.cs` | Enumy scope modelu |
| `PmTracker.Web/Services/Security/RoleCatalogLinker.cs` | Propojení číselníků rolí se seedem |
| `PmTracker.Web/Services/Security/ProjectAuthorizationQueryHelper.cs` | Filtrace dotazů dle práv |
| `PmTracker.Web/Services/Security/ForbiddenException.cs` | Jednotné odmítnutí |
| `PmTracker.Web/Extensions/AuthorizationPolicyExtensions.cs` | Registrace policy pro každý klíč |
| `PmTracker.Tests.Unit/Authorization/`, `PmTracker.Tests.Unit/Architecture/` | Architektonické testy |
| `docs/authorization.md` | Návod „jak přidat klíč" a „jak přidat roli" |
| `docs/technical/07-security-authz.md` | Bezpečnostní model, provozní postupy, rollback |
| `db_upgrade_1_3_8_authz_per_action_redesign.sql` | Migrace na per-action klíče |

## Poučení, která musí být v zadání explicitně

- **Deklarativní policy na objektově vázaný klíč potřebuje identifikátor objektu v routě
  nebo query.** Bez něj proběhne tichá globální kontrola a uživatel s dílčími právy dostane
  odmítnutí. Řešení je identifikátor v URL plus kontrola proti podvržení, ne kontrola z těla
  požadavku.
- **UI se řídí stejným seedem jako server.** Rozpad mezi tím, co UI nabízí, a tím, co server
  povolí, byl v Zápisce zdrojem chyb.
- **Právo číst zahrnuje právo tisknout a exportovat.**
- **Mrtvé klíče se mažou**, jakmile zanikne endpoint, který je používal.

## Co je nutné rozhodnout, než se kód vloží

- **A5** — přístup k datům určí, jak se přepíší dotazy sestavující snapshot.
- **A2** — jak se efektivní práva dostanou do Reactu, aby UI nenabízelo, co server odmítne.
