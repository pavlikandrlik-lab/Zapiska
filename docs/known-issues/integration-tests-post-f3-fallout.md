# Integration testy — 5 zbývajících selhání po per-action authz redesignu (F1–F8)

**Vytvořeno:** 2026-04-23
**Kontext:** Po F8 A1 fix (DI + PermissionSeeder ve fixture) prošlo 61/66 integration testů.
Zbývajících 5 selhání NENÍ DI regrese — jde o designové otázky po F3.1/F3.7 redesignu.

> **VYŘEŠENO 2026-06-16.** Skupina 1 opravena (rozhodnutí uživatele: vícevrstvá autorizace
> „klíč + omezení na subsystém"). Viz sekce „Řešení skupiny 1" níže. Skupina 2 trvá.

## Skupina 1 — Subsystem lead scope restrikce v komentářích (4 testy)

**Testy:**
- `CommentCreateAuthorizationDataStoreTests.AddComment_ShouldAllowSubsystemLead_ForAssignedSubsystemOnly`
- `CommentCreateAuthorizationDataStoreTests.AddComment_ShouldAllowSubsystemDeputyLead_ForAssignedSubsystemOnly`
- `CommentCreateAuthorizationDataStoreTests.AddComment_ShouldDenySubsystemLeadSpecialPermission_InOpenMeeting`
- `MeetingAndCommentDataStoreTests.SubsystemLeaderPermission_ShouldAllowOwnCommentCrudOnlyInDraft_AndBlockOthersInOpen`

**Očekávání testu:** VEDOUCI_SUBSYSTEMU subsystému A nesmí komentovat záznam v subsystému B.

**Realita po F3.1:** VEDOUCI_SUBSYSTEMU má `comments.add` s `ScopeMode.All` (per matici).
`AuthorizationSnapshot.PerProjectPermissions` slévá subsystem-scoped granty do project-scope
(MEDIUM-1 fix, 2026-04-22). Výsledek: `HasPermission(CommentsAdd, projektId)` = true pro
subsystem leada na libovolném záznamu projektu → `CanAddComment` vrací true → komentář prochází.

**Designová otázka:** Má být `comments.add` pro subsystem leada scope-restricted (jen vlastní
subsystém) nebo otevřený na projekt? Matice (Excel) aktuálně říká ALL. Scope restrikce by
vyžadovala buď:
1. Změnu matice (subsystem lead → ne `comments.add` vůbec; nahrazeno jen `meetings.notes.subsystemlead`)
2. Explicitní subsystem-lead-equivalent gate v `CanAddComment` (přidat check nad rámec `CommentsAdd`)
3. Rozdělit resolver tak, aby subsystem-scoped granty byly výhradně per-subsystem

### Řešení skupiny 1 (2026-06-16)

Příčina: `AuthorizationSnapshot` přes MEDIUM-1 propaguje subsystémové granty (vč. `comments.add`)
do `PerProjectPermissions`, takže `CanAddComment` se ukončil na project-scope kontrole dřív, než
se dostal k subsystémovému omezení. Vedoucí tak komentoval libovolný subsystém.

Oprava (vícevrstvá autorizace „klíč + omezení na subsystém"):
- `AuthorizationSnapshot` má novou (volitelnou) sadu `PerProjectDirectPermissions` = oprávnění
  POUZE z přímých projektových rolí (bez MEDIUM-1 propagace) + metodu `HasDirectProjectPermission`.
  `AuthorizationSnapshotBuilder` ji plní z `projectRows`. Existující efektivní sada beze změny
  (nulová regrese pro dashboard/UI flagy/atd.).
- `CanAddComment` rozhoduje třístupňově: (1) přímá projektová/globální `comments.add` → kdekoli
  („vyšší bere", pokrývá i kombinaci projektová role + vedoucí subsystému); (2) vedoucí/zástupce
  subsystému → jen vlastní subsystém v DRAFT; (3) ostatní (např. METODIK přes zděděný grant) →
  zachované chování.
- `CanModifyComment`: vedoucí subsystému smí upravit/smazat VLASTNÍ komentář svého subsystému
  v DRAFT i bez samostatného `comments.edit.own` (implikovaný CRUD).
- `CommentService.AddCommentAsync` počítá „je osoba lead-equivalent některého subsystému projektu"
  z `GetLeadEquivalentOsobaIdsBySubsystemAsync` a předává do policy.

Pozn. METODIK_SUBSYSTEMU: nemá `meetings.notes.subsystemlead` ani není lead-equivalent (lead-equiv
= jen Lead/Deputy), proto je u něj zachováno dosavadní chování (komentuje přes zděděný `comments.add`).
Pokud by se měl i METODIK omezit jen na vlastní subsystém, je to samostatný follow-up (vyžaduje
mapování globální `SubsystemId` ↔ `ProjektSubsystemId` v subsystémové dimenzi snapshotu).

## Skupina 2 — DashboardPriority.SaveRecord + Collaboration

> **VYŘEŠENO 2026-06-16 — byl to REÁLNÝ produkční bug, ne test-setup.** Původní vysvětlení níže
> (async hosted service) bylo MYLNÉ: `SaveRecord` volá `RebuildForRecordAsync` **synchronně** (ř. 248).

**Test:** `DashboardPriorityDataStoreTests.SaveRecord_ShouldCreateAndRemovePriorityRow_WhenCollaborationChanges`

**Skutečná příčina:** `ReplaceRecordCollaborationAsync` (RecordService.SaveRecord.cs) přidá/odebere
`ZaznamSpoluprace` jen do change trackeru bez `SaveChangesAsync`. Následný `RebuildForRecordAsync`
čte spolupráci přes `ZaznamSpoluprace.AsNoTracking()` = dotaz do DB; EF Core před dotazem neflushuje
pending změny → rebuild viděl zastaralý stav (nový spolupracovník bez priority row, smazaný ji
neztratil) až do příštího full rebuildu.

**Oprava:** `await dbContext.SaveChangesAsync(innerCt)` PŘED `RebuildForRecordAsync` (uvnitř vnější
transakce → atomické). Test je správný; chyba byla v pořadí flushe. Scoring algoritmus je v pořádku
(collaborator má roleWeight 40).

---

*(Původní mylné vysvětlení, ponecháno pro historii:)*
~~Priority matrix queue processor je hosted service, který ve fixture neběží~~ — NEPLATÍ, rebuild
je synchronní.

## Doporučení

- Skupina 1: před řešením odsouhlasit s produkt ownerem semantiku subsystem lead komentářů.
  Aktuální chování (neomezené) odpovídá matici; testy reprezentují pre-redesign záměr.
- Skupina 2: upravit test (volat rebuild explicitně) nebo rozšířit fixture o priority queue.

Tyto otázky NEBLOKUJÍ deploy per-action redesignu (F1–F8). Production chování je konzistentní
s autoritativní maticí `authz-target-matrix.xlsx`.
