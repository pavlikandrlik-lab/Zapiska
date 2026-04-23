# Integration testy — 5 zbývajících selhání po per-action authz redesignu (F1–F8)

**Vytvořeno:** 2026-04-23
**Kontext:** Po F8 A1 fix (DI + PermissionSeeder ve fixture) prošlo 61/66 integration testů.
Zbývajících 5 selhání NENÍ DI regrese — jde o designové otázky po F3.1/F3.7 redesignu.

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

## Skupina 2 — DashboardPriority.SaveRecord + Collaboration

**Test:** `DashboardPriorityDataStoreTests.SaveRecord_ShouldCreateAndRemovePriorityRow_WhenCollaborationChanges`

**Očekávání:** Přidání collaboratora do `VybraniSpolupracovniciIds` při SaveRecord vytvoří
priority matrix row v `ZaznamPriorityUzivatelu`.

**Realita:** Priority matrix queue processor je hosted service, který ve fixture (ServiceCollection,
žádný aplikační host) neběží. `IPriorityMatrixRebuildQueue.Enqueue` zapíše do fronty, ale nikdo
ji nezpracuje → row nevznikne.

**Řešení:** Test musí po SaveRecord explicitně volat `FullRebuildPriorityMatrix()` (jak to dělá
jiné testy ve stejném souboru), nebo fixture musí spustit `PriorityMatrixQueuedRebuildHostedService`.
Není to authz regrese — je to issue testovacího setupu (priority matrix rebuild je async).

## Doporučení

- Skupina 1: před řešením odsouhlasit s produkt ownerem semantiku subsystem lead komentářů.
  Aktuální chování (neomezené) odpovídá matici; testy reprezentují pre-redesign záměr.
- Skupina 2: upravit test (volat rebuild explicitně) nebo rozšířit fixture o priority queue.

Tyto otázky NEBLOKUJÍ deploy per-action redesignu (F1–F8). Production chování je konzistentní
s autoritativní maticí `authz-target-matrix.xlsx`.
