# Vyjádření — kdo smí přidat, upravit a smazat: plán implementace

> **Pro agentické vykonavatele:** POVINNÝ SUB-SKILL: superpowers:executing-plans (uživatel: inline, žádní subagenti — ani na závěrečnou revizi, ta je self-review). Kroky mají checkboxy (`- [ ]`).

**Cíl:** Přidání, úprava a smazání vyjádření se řídí maticí ze spec R1. Ikony u vyjádření počítá server stejnou metodou, kterou kontroluje uložení.

**Architektura:**
- `CommentAuthorizationPolicy` dostane jeden vstup `CommentScope`: projekt, stav jednání a osoby subsystému záznamu (vedoucí, zástupce, metodik). Z něj odvodí `CanAddComment`, `CanEditComment` a `CanDeleteComment`. Vlastní vyjádření smí upravit a smazat ten, kdo by ho teď smel přidat (R2).
- `CommentService` scope skládá na jednom místě. Ze stejné politiky vrací pro panel příznaky „smí upravit / smí smazat“ (R6).
- Mrtvá cesta `_TaskItemPartial` se smaže (R8).

**Tech stack:** ASP.NET Core 8 MVC/Razor, EF Core (SQL Server, Unit testy EF InMemory), xUnit + FluentAssertions, Api testy přes Testcontainers (po restartu nejdřív `colima start`), Playwright .NET E2E.

**Spec:** `docs/superpowers/specs/2026-11-10-vyjadreni-uprava-podle-role-design.md`

## Globální omezení

- Seed rolí a klíčů se nemění (`PermissionSeedConfiguration.cs`). Zápis z jednání (`CanSaveMeetingNote`) se nemění.
- Zamčení u uzavřeného jednání (`EnsureMeetingAllowsCommentChangesAsync`) zůstává: uzavřené jednání = nikdo nic.
- Stav záznamu se do úpravy a smazání nepromítá (R9). Přidání k uzavřenému záznamu dál odmítá `AddCommentAsync`.
- Výjimka 2026-06-16 zůstává: vedoucí a zástupce (`meetings.notes.subsystemlead` + seznam vedoucích subsystému záznamu) smí v Přípravě přidat, upravit a smazat vlastní vyjádření i bez klíčů `comments.*`.
- Soubory s „Policy“ nebo „Authorization“ v názvu nesmí porovnávat kódy rolí (`NoHardcodedAuthzRolesTests`). Role se poznají jen podle seznamů osob ze `ProjectRoleCache`.
- Hlášky serveru zůstávají: „Nemáte oprávnění přidat vyjádření k tomuto záznamu.“, „Nemáte oprávnění upravit toto vyjádření.“, „Nemáte oprávnění smazat toto vyjádření.“
- Vzhled ikon a panelu se nemění. Mění se jen podmínka, kdy se vykreslí.
- Odpovědi a dokumentace česky. Po hotové změně se doplní `docs/k-otestovani.md`, hotové se mergne do lokálního `main`, push jen na pokyn.

## Na co se při revizi zaměřit

1. **Osoba se dvěma rolemi:** Gestor, který je zároveň metodikem jiného subsystému, se řídí rolí Gestora (R4), i v Otevřeno a u cizího subsystému. Test: Task 2, `Gestor_ktery_je_i_metodikem_se_ridi_gestorem`.
2. **Metodik, kterému role skončila** (`datum_odebrani`), ale snapshot oprávnění ještě nese `comments.add`. Nesmí nic. Testy: Task 1, `Odebrany_metodik_neni_v_seznamu`; Task 2, `Metodik_bez_role_v_subsystemu_nesmi_nic`.
3. **Vyjádření, jehož jednání chybí (orphan):** ikona koše jen pro SuperAdmina, tužka nikomu. To odpovídá serveru (`DeleteCommentAsync` orphan větev, `UpdateCommentAsync` hází výjimku). Test: Task 3, `Orphan_vyjadreni_smaze_jen_superadmin`.
4. **Klíč pro úpravu bez klíče pro mazání (R5):** tužka ano, koš ne, a server odpovídá stejně. Testy: Task 2, `Uprava_a_smazani_se_ridi_vlastnimi_klici`; Task 3, `Priznaky_respektuji_oddelene_klice`.
5. **Výjimka 2026-06-16 nesmí unést do Otevřeno:** vedoucí bez klíčů `comments.*` v Otevřeno nic. Testy: Task 2, `Vedouci_jen_s_klicem_zapisu_ma_vyjimku_jen_v_priprave`; Integration `SubsystemLeaderPermission_ShouldAllowOwnCommentCrudOnlyInDraft_AndBlockOthersInOpen` (beze změny, musí zůstat zelený).

---

### Task 1: `ProjectRoleCache` — osoby všech tří subsystémových rolí

**Soubory:**
- Upravit: `PmTracker.Web/Services/IProjectRoleCache.cs`, `PmTracker.Web/Services/ProjectRoleCache.cs`
- Test: `PmTracker.Tests.Unit/Services/ProjectRoleCacheTests.cs`

**Rozhraní:**
- Produkuje (pro Task 2, 3):
  - `Task<IReadOnlyDictionary<int, IReadOnlyList<int>>> GetSubsystemRoleOsobaIdsBySubsystemAsync(int projectId, CancellationToken ct = default)` — globální `SubsystemId` → osoby s aktivní rolí vedoucí, zástupce nebo metodik (seřazené, bez duplicit).
  - `Task<IReadOnlyList<int>> GetSubsystemRoleOsobaIdsAsync(int projectId, int subsystemId, CancellationToken ct = default)` — výřez pro jeden subsystém, prázdný list, když nikdo.
- Stávající `GetLeadEquivalentOsobaIds*` vrací dál jen vedoucího a zástupce a stejnou instanci při opakovaném volání. Používá je dashboard, návrhy a zápis z jednání, a ty se nemění.

- [ ] **Krok 1: Testy.** V `ProjectRoleCacheTests` nahradit `SeedLeadAssignmentAsync` obecným `SeedRoleAssignmentAsync(db, projectId, subsystemId, osobaId, string roleCode, DateTime? datumOdebrani = null)`. Číselník rolí dostane pevná id: Lead = 1, DeputyLead = 2, Methodik = 3. Stávající testy volají helper s `SubsystemRoleCodes.Lead`. Nové testy:

```csharp
[Fact]
public async Task Subsystemove_role_obsahuji_vedouciho_zastupce_i_metodika_jen_sveho_subsystemu()
{
    // 5001 vedoucí, 5002 zástupce, 5003 metodik subsystému 100; 5004 metodik subsystému 200
    (await cache.GetSubsystemRoleOsobaIdsAsync(10, 100)).Should().Equal(5001, 5002, 5003);
    (await cache.GetSubsystemRoleOsobaIdsAsync(10, 200)).Should().Equal(5004);
    (await cache.GetSubsystemRoleOsobaIdsAsync(10, 300)).Should().BeEmpty();
}

[Fact]
public async Task Seznam_vedoucich_dal_metodika_neobsahuje()
    // stejný seed → GetLeadEquivalentOsobaIdsAsync(10, 100) == [5001, 5002]

[Fact]
public async Task Odebrany_metodik_neni_v_seznamu()
    // metodik 5003 s datumOdebrani = včera → GetSubsystemRoleOsobaIdsAsync(10, 100) neobsahuje 5003

[Fact]
public async Task Subsystemove_role_druhe_volani_vrati_tutez_instanci()
    // jako Get_SecondCallSameProjekt_ReturnsCachedInstance, pro GetSubsystemRoleOsobaIdsBySubsystemAsync
```

- [ ] **Krok 2: Spustit testy.**
  - Spustit: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ProjectRoleCacheTests"`
  - Očekávám: build FAIL — `GetSubsystemRoleOsobaIds*` neexistuje.

- [ ] **Krok 3: Implementace.**
  - Doplnit obě metody do `IProjectRoleCache` (XML doc: „vedoucí, zástupce a metodik subsystému; pro vyjádření, spec 2026-11-10 R3“).
  - V `ProjectRoleCache` jeden dotaz načte řádky všech tří rolí (`Kod in Lead, DeputyLead, Methodik`) i s `RoleSubsystemuId`.
  - Per projekt se uloží `private sealed record ProjectSubsystemRoles(IReadOnlyDictionary<int, IReadOnlyList<int>> LeadEquivalent, IReadOnlyDictionary<int, IReadOnlyList<int>> AllRoles)`.
  - Filtry aktivního přiřazení a aktivního subsystému projektu (`DatumOdebrani`) zůstávají stejné jako dnes.

- [ ] **Krok 4: Spustit testy.**
  - Spustit: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ProjectRoleCacheTests"`
  - Očekávám: PASS (4 stávající + 4 nové).

- [ ] **Krok 5: Commit.**
  - `git commit -m "feat(vyjadreni): ProjectRoleCache — osoby všech subsystémových rolí včetně metodika"`

---

### Task 2: Pravidla a uložení — matice R1 na serveru

**Soubory:**
- Vytvořit: `PmTracker.Web/Services/Common/CommentScope.cs`
- Upravit: `PmTracker.Web/Services/Common/ICommentAuthorizationPolicy.cs`, `PmTracker.Web/Services/Common/CommentAuthorizationPolicy.cs`, `PmTracker.Web/Services/CommentService.cs`
- Upravit (fixture): `PmTracker.Tests.Api/TestInfrastructure/ApiSqlFixture.cs` — `AddCommentAsync` vrací `Task<int>` (id vloženého vyjádření). Stávající volání s `await` zůstanou funkční.
- Test: `PmTracker.Tests.Unit/Common/CommentAuthorizationPolicyTests.cs`, nový `PmTracker.Tests.Api/Controllers/CommentRoleMatrixServerTests.cs`

**Rozhraní:**
- Konzumuje: `IProjectRoleCache.GetSubsystemRoleOsobaIdsAsync`, `GetLeadEquivalentOsobaIdsAsync` (Task 1).
- Produkuje (pro Task 3):

```csharp
// PmTracker.Web/Services/Common/CommentScope.cs
/// Kde se vyjádření přidává nebo mění: projekt záznamu, stav jednání a osoby subsystému záznamu.
public sealed record CommentScope(
    int ProjektId,
    bool IsDraftMeeting,
    IReadOnlyCollection<int> SubsystemRoleOsobaIds,          // vedoucí, zástupce, metodik subsystému záznamu
    IReadOnlyCollection<int> SubsystemLeadEquivalentOsobaIds); // vedoucí, zástupce (výjimka 2026-06-16)

// ICommentAuthorizationPolicy — nahrazuje CanAddComment(…5 parametrů), CanModifyComment, CanDeleteComment(user, projektId, autor)
bool CanAddComment(CurrentUserContextViewModel currentUser, CommentScope scope);
bool CanEditComment(CurrentUserContextViewModel currentUser, CommentScope scope, int commentAuthorOsobaId);
bool CanDeleteComment(CurrentUserContextViewModel currentUser, CommentScope scope, int commentAuthorOsobaId);
// CanCommentAsSubsystemLeader a CanSaveMeetingNote beze změny.

// CommentService (private), jediné místo skládání scope:
Task<CommentScope> ResolveCommentScopeAsync(int projektId, int subsystemId, bool isDraftMeeting, CancellationToken ct);
```

**Pravidlo** (jádro spec, proto i tělo):

```csharp
public bool CanAddComment(CurrentUserContextViewModel u, CommentScope s)
{
    // R4: přímá projektová / globální role s comments.add → kdekoli v projektu, Příprava i Otevřeno.
    if (u.HasDirectProjectPermission(PermissionKeys.CommentsAdd, s.ProjektId)) return true;
    // R1: subsystémové role jen v Přípravě…
    if (!s.IsDraftMeeting) return false;
    // …a jen u záznamu svého subsystému (vedoucí, zástupce, metodik — R3).
    if (s.SubsystemRoleOsobaIds.Contains(u.OsobaId) && u.HasPermission(PermissionKeys.CommentsAdd, s.ProjektId)) return true;
    // Výjimka 2026-06-16: vedoucí / zástupce s meetings.notes.subsystemlead i bez comments.add.
    return CanCommentAsSubsystemLeader(u, s.ProjektId, s.SubsystemLeadEquivalentOsobaIds);
}

public bool CanEditComment(CurrentUserContextViewModel u, CommentScope s, int autor)
    => u.HasPermission(PermissionKeys.CommentsEditAny, s.ProjektId)
       || (IsOwn(u, autor) && CanChangeOwn(u, s, PermissionKeys.CommentsEditOwn));

public bool CanDeleteComment(CurrentUserContextViewModel u, CommentScope s, int autor)
    => u.HasPermission(PermissionKeys.CommentsDeleteAny, s.ProjektId)
       || (IsOwn(u, autor) && CanChangeOwn(u, s, PermissionKeys.CommentsDeleteOwn));

// R2: vlastní smí změnit, kdo by ho teď na tomtéž místě smel přidat + klíč akce; nebo výjimka 2026-06-16.
private bool CanChangeOwn(CurrentUserContextViewModel u, CommentScope s, string ownKey)
    => (CanAddComment(u, s) && u.HasPermission(ownKey, s.ProjektId))
       || (s.IsDraftMeeting && CanCommentAsSubsystemLeader(u, s.ProjektId, s.SubsystemLeadEquivalentOsobaIds));

private static bool IsOwn(CurrentUserContextViewModel u, int autor) => autor > 0 && u.OsobaId == autor;
```

Pozn.: dnešní „třetí vrstva“ (`HasPermission(comments.add)` bez subsystémové role) zaniká. Takový uživatel dnes existuje jen jako metodik a ten je od teď v seznamu osob subsystému. Kdo má `comments.add` jen zděděný a v seznamu není, nemá platnou roli a nesmí nic.

- [ ] **Krok 1: Api testy serveru, napsat jako první.**
  - Nový `CommentRoleMatrixServerTests`, seed podle vzoru `RecordCommentsFormAuthzRenderTests.SeedAsync`:
    - osoby Owner, Gestor (`ProjectRoleCodes.Gestor`), Pm (`ProjectRoleCodes.ProjectManager`), Lead (`SubsystemRoleCodes.Lead` v A), Metodik (`SubsystemRoleCodes.Methodik` v A);
    - záznamy v subsystému A a B;
    - jednání DRAFT a OPEN.
  - Vyjádření se seedují přes `_fixture.AddCommentAsync` (vrací id).
  - POSTy jako `ClosedMeetingCommentChangesTests`: `ApiTestHttpHelper.BuildAjaxPost` na `/Zaznamy/AddComment|UpdateComment|DeleteComment?asUser=…`, pole `projektId`, `uiContext=project`, `zaznamId` (u AddComment `ZaznamId`, `JednaniId`), `Id`, `Text`. Výsledek: `ReadModalResultAsync(...).Ok` a stav v DB.

  | Test | Kdo / kde | Očekávání |
  |---|---|---|
  | `Gestor_upravi_vlastni_v_priprave` | Gestor, A, DRAFT | Ok, text změněn |
  | `Gestor_smaze_vlastni_v_priprave` | Gestor, A, DRAFT | Ok, řádek pryč (**charakterizace**, viz Krok 2) |
  | `Gestor_upravi_a_smaze_vlastni_v_otevrenem` | Gestor, B, OPEN | Ok, Ok |
  | `Gestor_neupravi_cizi` | Gestor na vyjádření PM, A, DRAFT | Ok = false, text beze změny |
  | `Metodik_prida_upravi_smaze_v_priprave_u_sveho_subsystemu` | Metodik, A, DRAFT | Ok ×3 |
  | `Metodik_neprida_v_otevrenem` | Metodik, A, OPEN | Ok = false, žádný nový řádek |
  | `Metodik_neprida_u_ciziho_subsystemu` | Metodik, B, DRAFT | Ok = false |
  | `Metodik_neupravi_ani_nesmaze_starsi_vlastni_v_otevrenem` | vyjádření Metodika seedované do OPEN, A | Ok = false ×2, řádek beze změny |
  | `Vedouci_neupravi_ani_nesmaze_vlastni_v_otevrenem` | Lead, A, OPEN | Ok = false ×2 |
  | `Metodik_nabidka_jednani_jen_priprava_u_sveho_subsystemu` | GET `RecordCommentsPartial`, Metodik | A: `<option value="{DraftId}"` ano, `{OpenId}` ne; B: bez `action="/Zaznamy/AddComment"` |

- [ ] **Krok 2: Spustit Api testy na dnešním kódu a zapsat výsledek.**
  - Spustit: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~CommentRoleMatrixServerTests" 2>&1 | tail -40`
  - Očekávám (dnešní `CanModifyComment` v Přípravě vyžaduje roli vedoucího, mimo Přípravu jen `comments.edit.own`; třetí vrstva `CanAddComment` pouští metodika kamkoli):
    - FAIL:
      - `Gestor_upravi_vlastni_v_priprave`;
      - `Metodik_prida_upravi_smaze_v_priprave_u_sveho_subsystemu` (přidání projde, úprava ne);
      - `Metodik_neprida_v_otevrenem`;
      - `Metodik_neprida_u_ciziho_subsystemu`;
      - `Metodik_neupravi_ani_nesmaze_starsi_vlastni_v_otevrenem`;
      - `Vedouci_neupravi_ani_nesmaze_vlastni_v_otevrenem`;
      - `Metodik_nabidka_jednani_jen_priprava_u_sveho_subsystemu`.
    - PASS už dnes: `Gestor_upravi_a_smaze_vlastni_v_otevrenem`, `Gestor_neupravi_cizi`.
    - Podle kódu FAIL, ale **výsledek zapsat do ledgeru**: `Gestor_smaze_vlastni_v_priprave`. Spec kap. 2 tvrdí „odmítne“, `k-otestovani.md` §5 tvrdí „projde“. Skutečný výsledek se v Tasku 5 zapíše do dokumentace.
  - Jiný výsledek, než je tady uvedený, je nález: zapsat ho do ledgeru a před implementací vysvětlit.

- [ ] **Krok 3: Unit testy pravidla.**
  - V `CommentAuthorizationPolicyTests` smazat testy `CanAddComment_*` a `CanModifyComment_*`. Volají staré signatury a jeden (`…ShouldPreserve_NonLeadSubsystemRole…`) hlídá opak nového pravidla.
  - `CanSaveMeetingNote_*` a `CanCommentAsSubsystemLeader_*` zůstávají.
  - Nové testy (helper `BuildUser` beze změny):
    - profily: PM = přímé `comments.add/edit.own/edit.any/delete.own/delete.any`; Gestor = přímé `comments.add/edit.own/delete.own`; Vedoucí (osoba 12) a Metodik (osoba 13) = efektivní `comments.add/edit.own/delete.own`, Vedoucí navíc `meetings.notes.subsystemlead`, přímé `{}`; Host = nic;
    - scope: `new CommentScope(2, draft, vlastniSubsystem ? [osoba] : [99], vlastniSubsystem && jeVedouci ? [osoba] : [99])`; autor vlastního = osoba, cizího = 777.

```csharp
[Theory]
//           profil     příprava vlastníSub  přidat  upravitVl smazatVl upravitCizí smazatCizí
[InlineData("PM",       true,    true,       true,   true,     true,    true,       true)]
[InlineData("PM",       false,   false,      true,   true,     true,    true,       true)]
[InlineData("Gestor",   true,    false,      true,   true,     true,    false,      false)]
[InlineData("Gestor",   false,   false,      true,   true,     true,    false,      false)]
[InlineData("Vedouci",  true,    true,       true,   true,     true,    false,      false)]
[InlineData("Vedouci",  true,    false,      false,  false,    false,   false,      false)]
[InlineData("Vedouci",  false,   true,       false,  false,    false,   false,      false)]
[InlineData("Metodik",  true,    true,       true,   true,     true,    false,      false)]
[InlineData("Metodik",  true,    false,      false,  false,    false,   false,      false)]
[InlineData("Metodik",  false,   true,       false,  false,    false,   false,      false)]
[InlineData("Host",     true,    true,       false,  false,    false,   false,      false)]
public void Matice_R1(string profil, bool priprava, bool vlastniSubsystem,
    bool pridat, bool upravitVlastni, bool smazatVlastni, bool upravitCizi, bool smazatCizi)
```

  - Samostatné `[Fact]`:
    - `Vedouci_jen_s_klicem_zapisu_ma_vyjimku_jen_v_priprave` — jen `meetings.notes.subsystemlead`, ve vlastním seznamu vedoucích: Příprava přidat / upravit / smazat vlastní = true; Otevřeno = false ×3.
    - `Gestor_ktery_je_i_metodikem_se_ridi_gestorem` — přímé klíče Gestora, osoba v `SubsystemRoleOsobaIds` cizího subsystému, Otevřeno: přidat = true, upravit vlastní = true.
    - `Metodik_ve_dvou_subsystemech_smi_v_obou` — dva scope s osobou v seznamu = true, třetí bez ní = false.
    - `Metodik_bez_role_v_subsystemu_nesmi_nic` — efektivní `comments.add/edit.own/delete.own`, není v žádném seznamu, Příprava: false ×3.
    - `Uprava_a_smazani_se_ridi_vlastnimi_klici` (R5) — Gestor jen s `comments.add` + `comments.edit.own`: upravit vlastní = true, smazat vlastní = false. PM s `comments.edit.any` bez `comments.delete.any`: upravit cizí = true, smazat cizí = false.
    - `Smazany_projekt_nepovoli_pridat` — Gestor, `deletedProjectIds: [2]`: přidat = false.

- [ ] **Krok 4: Spustit Unit testy.**
  - Spustit: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~CommentAuthorizationPolicyTests"`
  - Očekávám: build FAIL — `CommentScope` a nové signatury neexistují.

- [ ] **Krok 5: Implementace.**
  - `CommentScope.cs`, rozhraní a pravidlo podle bloku výše. Staré `CanModifyComment` a `CanDeleteComment(user, projektId, autor)` smazat. Grep potvrdil, že je volá jen `CommentService` a testy.
  - Popis třídy (`<summary>`) přepsat na pravidlo R1/R2 s odkazem na spec 2026-11-10.
  - `CommentService`:
    - `AddCommentAsync`, `UpdateCommentAsync`, `DeleteCommentAsync` a `GetMeetingIdsAllowingNewCommentAsync` volají `ResolveCommentScopeAsync(record.ProjektId, record.SubsystemId, IsDraftMeeting(state), ct)`. U nabídky jednání se scope pro DRAFT a OPEN spočítá jednou a vybírá se podle `meeting.IsDraft`.
    - Update volá `CanEditComment`, Delete volá `CanDeleteComment`. Hlášky beze změny.
    - Smazat `AddCommentContext`, `ResolveAddCommentContextAsync` a `CanModifyCommentAsync`.
    - `SaveMeetingNotesBatchAsync` beze změny (dál `GetLeadEquivalentOsobaIdsBySubsystemAsync`).

- [ ] **Krok 6: Spustit testy.**
  - Spustit:
    - `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~CommentAuthorizationPolicyTests"`
    - `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~CommentRoleMatrixServerTests|FullyQualifiedName~RecordCommentsFormAuthzRenderTests|FullyQualifiedName~ClosedMeetingCommentChangesTests|FullyQualifiedName~MeetingContextCommentsTests"`
    - `dotnet test PmTracker.Tests.Integration --filter "FullyQualifiedName~MeetingAndCommentDataStoreTests|FullyQualifiedName~CommentCreateAuthorizationDataStoreTests"`
  - Očekávám: vše PASS. Integration testy vedoucího jen s `meetings.notes.subsystemlead` prochází díky výjimce 2026-06-16.

- [ ] **Krok 7: Commit.**
  - `git commit -m "feat(vyjadreni): pravidla přidání, úpravy a smazání podle role a stavu jednání (spec 2026-11-10 R1–R5, R7)"`

---

### Task 3: Ikony podle serveru — příznaky „smí upravit / smí smazat“

**Soubory:**
- Vytvořit: `PmTracker.Web/Services/CommentActions.cs` — `public sealed record CommentActions(bool CanEdit, bool CanDelete);`
- Upravit:
  - `PmTracker.Web/Services/ICommentService.cs`, `PmTracker.Web/Services/CommentService.cs`
  - `PmTracker.Web/Services/IRecordService.cs`, `PmTracker.Web/Services/RecordService.cs` (průchozí metoda jako `GetMeetingIdsAllowingNewCommentAsync`)
  - `PmTracker.Web/Models/ViewModels/Projekty/ProjektZaznamyTabViewModels.cs`:
    - `VyjadreniViewModel`: smazat `CanEditOwnAsSubsystemLeader`, přidat `public bool SmiUpravit { get; set; }`, `public bool SmiSmazat { get; set; }`;
    - smazat `CanCommentAsSubsystemLeader` ze `ZaznamCardSummaryViewModel`, `ZaznamCommentsPanelViewModel` a `ZaznamCardViewModel`;
    - smazat `ZaznamCardSummaryViewModel.AktualniSubsystemLeadEquivalentOsobaIds`, pokud ho grep po odstranění příznaků nikde nečte. `ZaznamCardViewModel.AktualniSubsystemLeadEquivalentOsobaIds` zůstává, je mimo rozsah.
  - `PmTracker.Web/Services/ProjectService.LazyQueries.cs` (panel ~387 a summary ~568, ~698) a `PmTracker.Web/Services/ProjectService.RecordCards.cs` (~162): přestat plnit smazané vlastnosti.
  - `PmTracker.Web/Controllers/ZaznamyController.Partials.cs` (`PrepareRecordCardShellPresentation`, `PrepareRecordCommentsPresentationAsync`) a `PmTracker.Web/Controllers/BaseController.RecordsTab.cs:83`: smazat výpočet `canCommentAsSubsystemLead`. `PrepareRecordCommentsPresentationAsync` doplní příznaky.
  - `PmTracker.Web/Views/Projekty/_ZaznamCommentsPartial.cshtml`: ikony podle příznaků.
  - Falešné `ICommentService` v Unit testech: `Records/RecordServiceDelegationTests.cs`, `Meetings/MeetingServiceSaveAttendanceBatchTests.cs`, `Meetings/MeetingServiceDelegationTests.cs`.
- Test: nový `PmTracker.Tests.Api/Controllers/CommentIconsByRoleRenderTests.cs`, `PmTracker.Tests.Unit/Records/RecordServiceDelegationTests.cs` (delegace nové metody).

**Rozhraní:**
- Konzumuje: `CommentScope`, `ICommentAuthorizationPolicy.CanEditComment/CanDeleteComment`, `ResolveCommentScopeAsync` (Task 2).
- Produkuje:

```csharp
// ICommentService i IRecordService (RecordService jen deleguje)
/// Pro každé vyjádření záznamu ze seznamu: smí ho uživatel upravit / smazat — týmž pravidlem
/// jako UpdateCommentAsync / DeleteCommentAsync (spec 2026-11-10 R6). Id, které k záznamu
/// nepatří, ve výsledku chybí.
Task<IReadOnlyDictionary<int, CommentActions>> GetCommentActionsAsync(
    int zaznamId, IReadOnlyCollection<int> commentIds, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
```

Implementace `GetCommentActionsAsync`, rozhodnutí, která signatura neurčí:
- vyjádření, jednání a stavy jednání načíst dávkou (žádný dotaz na vyjádření zvlášť);
- scope se počítá jen pro DRAFT a OPEN;
- uzavřené jednání (`MeetingStatePolicy.IsReadOnly`) = `(false, false)`;
- chybějící jednání (orphan) = `(false, currentUser.IsSuperAdmin)`, stejně jako server.

- [ ] **Krok 1: Api testy vykreslení.**
  - `CommentIconsByRoleRenderTests`, seed jako v Tasku 2 (vlastní marker a čísla jednání).
  - GET `/Zaznamy/RecordCommentsPartial?projektId=…&zaznamId=…&asUser=…`.
  - Ikony se ověřují v bloku daného vyjádření: najít `data-comment-id="{id}"` a hledat do dalšího `data-comment-item`. Hledá se `data-comment-edit-toggle` (tužka) a `action="/Zaznamy/DeleteComment"` (koš). Pomocná metoda v testu: `static string CommentBlock(string html, int commentId)`.

  | Test | Kdo / vyjádření | Tužka | Koš |
  |---|---|---|---|
  | `Gestor_vidi_ikony_u_vlastniho_v_priprave_i_otevrenem` | Gestor, vlastní v DRAFT a v OPEN | ano, ano | ano, ano |
  | `Gestor_nevidi_ikony_u_ciziho` | Gestor, vyjádření PM | ne | ne |
  | `Metodik_vidi_ikony_jen_v_priprave_u_sveho_subsystemu` | Metodik, vlastní DRAFT/A; vlastní OPEN/A | ano; ne | ano; ne |
  | `Vedouci_nevidi_ikony_u_vlastniho_v_otevrenem` | Lead, vlastní OPEN/A | ne | ne |
  | `Pm_vidi_ikony_u_ciziho_v_otevrenem` | PM, vyjádření Gestora OPEN | ano | ano |
  | `Uzavrene_jednani_bez_ikon_i_pro_pm` | PM, vlastní CLOSED | ne | ne |

- [ ] **Krok 2: Unit testy služby a delegace.**
  - `RecordServiceDelegationTests`: nový test `GetCommentActionsAsync_DelegatesToCommentService` podle vzoru delegace `GetMeetingIdsAllowingNewCommentAsync` v témže souboru.
  - Nový `PmTracker.Tests.Unit/Services/CommentActionsTests.cs`:
    - EF InMemory, vzor `CreateDb` z `ProjectRoleCacheTests`. InMemory nehlídá cizí klíče, takže jde vložit orphan.
    - Instance: `new CommentService(db, Mock.Of<IRichTextContentService>(), new CommentAuthorizationPolicy(), Mock.Of<IAuditWriteService>(), new ProjectRoleCache(db), TimeProvider.System)`.
    - Seed: projekt, záznam, `CiselnikStavuJednani` DRAFT a CLOSED, jednání, vyjádření.
    - `Priznaky_respektuji_oddelene_klice` — Gestor s přímým `comments.add` + `comments.edit.own`, vlastní v DRAFT: `(true, false)`.
    - `Orphan_vyjadreni_smaze_jen_superadmin` — vyjádření s `JednaniId`, které neexistuje: superadmin `(false, true)`, PM `(false, false)`.
    - `Uzavrene_jednani_nic` — PM, vlastní v CLOSED: `(false, false)`.
    - `Cizi_id_ve_vysledku_chybi` — id vyjádření jiného záznamu: klíč ve výsledku není.

- [ ] **Krok 3: Spustit testy.**
  - Spustit: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~CommentIconsByRoleRenderTests"`, `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~CommentActionsTests|FullyQualifiedName~RecordServiceDelegationTests"`
  - Očekávám:
    - Api: FAIL jen `Gestor_vidi_ikony_u_vlastniho_v_priprave_i_otevrenem` (u vlastního v OPEN dnes ikony chybí). Ostatní projdou už dnes a hlídají, že se nic nerozbije. U metodika se dnes ikony v Přípravě ukazují, jen je server odmítal; to po Tasku 2 sedí.
    - Unit: build FAIL, `GetCommentActionsAsync` ani `CommentActions` neexistují.

- [ ] **Krok 4: Implementace.**
  - `CommentActions`, `GetCommentActionsAsync` v `CommentService`, průchod v `RecordService`, falešné služby vrací prázdný slovník.
  - `PrepareRecordCommentsPresentationAsync` (karta i stránka záznamu, `ZaznamyController.cs:131`) zavolá `GetCommentActionsAsync(summary.Id, model.Vyjadreni.Select(x => x.Id).ToList(), CurrentUserContext, ct)` a nastaví `SmiUpravit` a `SmiSmazat` každého vyjádření.
  - Šablona:
    - `canEditThisComment` nahradit `vyjadreni.SmiUpravit` (tužka i editační formulář) a `vyjadreni.SmiSmazat` (formulář koše);
    - obal `comment-row-actions` vykreslit, když `SmiUpravit || SmiSmazat`;
    - komentář nad řádkem přepsat: „ikony podle příznaků ze serveru — CommentService.GetCommentActionsAsync, spec 2026-11-10 R6“.
  - `Model.CanEditRecord` zůstává pro poznámku u uzavřeného záznamu (`_ZaznamCommentsPartial.cshtml:172`).
  - Smazat `canCommentAsSubsystemLead` a nepoužívané vlastnosti podle seznamu souborů výše.

- [ ] **Krok 5: Spustit testy.**
  - Spustit:
    - `dotnet build PmTracker.sln 2>&1 | grep -E "error|Warn.*CS" | head`
    - `dotnet test PmTracker.Tests.Unit`
    - `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~Comment|FullyQualifiedName~Vyjadreni|FullyQualifiedName~RecordEditorControllerTests"`
    - `dotnet test PmTracker.Tests.Integration --filter "FullyQualifiedName~MeetingAndCommentDataStoreTests"`
  - Očekávám: 0 chyb buildu, vše PASS.

- [ ] **Krok 6: Commit.**
  - `git commit -m "feat(vyjadreni): ikony Upravit a Smazat podle serveru (spec 2026-11-10 R6)"`

---

### Task 4: Smazání mrtvé cesty detailu jednání (R8)

**Soubory:**
- Smazat:
  - `PmTracker.Web/Views/Jednani/_TaskItemPartial.cshtml`
  - `PmTracker.Tests.Integration/DataStore/MeetingTasksDataStoreTests.cs` (jediný test, hlídá `JednaniDetailViewModel.Ukoly`, který se nevykresluje; viditelnou záložku záznamů jednání má vlastní dotaz, ten se nemění)
- Upravit:
  - `PmTracker.Web/Controllers/JednaniController.cs`: smazat akci `TaskItemPartial` (~158–208).
  - `PmTracker.Web/Models/ViewModels/JednaniViewModels.cs`: smazat `JednaniDetailViewModel.Ukoly`, `JednaniUkolViewModel`, `JednaniVyjadreniViewModel`, `JednaniTaskItemPartialViewModel`.
  - `PmTracker.Web/Services/IMeetingService.cs`, `PmTracker.Web/Services/MeetingService.DetailQueries.cs`: smazat `GetSingleTaskAsync`, `BuildMeetingTasksAsync` a volání v `BuildJednaniDetailAsync` (řádky 54 a 102).
  - `PmTracker.Web/Services/Records/RecordUiFlowResolver.cs`: smazat větve `UiContextMeeting` v obou metodách a konstantu. Hodnotu `"meeting"` posílaly na endpointy vyjádření jen formuláře `_TaskItemPartial`. Editor a mazání záznamu mají vlastní zpracování `uiContext` a nemění se.
  - `PmTracker.Web/wwwroot/js/modules/recordRefresh.js`: smazat `refreshMeetingTaskItem` a větev `scope === "meeting-task-item"`.
  - `PmTracker.Web/wwwroot/css/site.css`: smazat pravidla `.task-item` (~4876–4890). Komentáře na ~1756 a ~4166 upravit (bez zmínky o `_TaskItemPartial`).
  - `PmTracker.Web/wwwroot/js/modules/searchHighlight.js:119`, `PmTracker.Web/wwwroot/js/modules/comments.js:91`: komentáře bez `_TaskItemPartial`.
  - Testy:
    - `PmTracker.Tests.Api/Controllers/JednaniControllerTests.cs` — smazat `TaskItemPartial_*` (7 testů); když tím soubor zůstane prázdný, smazat ho;
    - `PmTracker.Tests.Unit/Meetings/JednaniControllerBehaviorTests.cs` — smazat `TaskItemPartial_ShouldBuildPartialFromSingleTaskAndMeetingListAsync`, z falešné služby `SingleTask` a `GetSingleTaskAsync`;
    - falešné `IMeetingService` v `Projects/ProjektyControllerBehaviorTests.cs`, `Meetings/JednaniControllerExceptionHandlingTests.cs`, `Controllers/ProjektyControllerTeamAuthzTests.cs` — smazat `GetSingleTaskAsync`;
    - `PmTracker.Tests.Unit/Records/RecordUiFlowResolverTests.cs` — test meeting větve nahradit níže uvedeným;
    - `PmTracker.Tests.Unit/Layout/CompactCommentsBlockCssTests.cs:42` — text důvodu bez `_TaskItemPartial`.
  - `docs/known-issues/meetings-endpoints-split-between-controllers.md`: řádky `TaskItemPartial` označit „odstraněno 2026-11-11 (spec vyjádření R8)“.

- [ ] **Krok 1: Ověřit, že nic jiného na cestu nesahá.**
  - Spustit: `grep -rnE "TaskItemPartial|GetSingleTaskAsync|BuildMeetingTasksAsync|JednaniUkolViewModel|JednaniVyjadreniViewModel|\.Ukoly\b|refreshMeetingTaskItem|meeting-task-item|task-item" PmTracker.Web PmTracker.Tests.Unit PmTracker.Tests.Api PmTracker.Tests.Integration PmTracker.Tests.E2E | grep -v "/bin/\|/obj/\|/lib/\|/dist/\|HarmonogramUkoly\|PmTracker_Analyza"`
  - Očekávám: jen výskyty vyjmenované v seznamu souborů. Cokoli jiného = STOP a zapsat Ruling.

- [ ] **Krok 2: Test resolveru (RED).** V `RecordUiFlowResolverTests` nahradit `ResolveCommentAjaxFlow_ShouldReturnMeetingRefresh_WhenMeetingContextProvided`:

```csharp
[Fact]
public void ResolveCommentAjaxFlow_MeetingContext_ObnoviPanelVyjadreni()
{
    var result = _sut.ResolveCommentAjaxFlow("meeting", 17, 31, 44, kontextJednaniId: 44);

    result.RefreshScope.Should().Be("record-comments", "obnovení bloku úkolu jednání (_TaskItemPartial) zaniklo");
    result.ActionName.Should().Be("RecordCommentsPartial");
    result.RouteValues["kontextJednaniId"].Should().Be(44);
}
```

  Test `ResolveCommentRedirectFlow_ShouldReturnMeetingDetail_WhenMeetingContextProvided` přepsat na očekávání `Projekty/Detail`, tab `zaznamy`.
  - Spustit: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~RecordUiFlowResolverTests"`
  - Očekávám: FAIL u obou přepsaných testů.

- [ ] **Krok 3: Smazat podle seznamu souborů.**

- [ ] **Krok 4: Spustit testy.**
  - Spustit:
    - `dotnet build PmTracker.sln 2>&1 | grep -E " error " | head`
    - `dotnet test PmTracker.Tests.Unit`
    - `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~Jednani|FullyQualifiedName~Meeting"`
    - `npm test`
    - grep z Kroku 1
  - Očekávám:
    - build 0 chyb;
    - Unit vše PASS (počet = 2329 + nové − smazané, rozdíl zapsat do ledgeru);
    - Api PASS;
    - JS 78/78;
    - grep: jen `PmTracker_Analyza_Refaktorizace.md` (historický dokument, nemění se) a docs.

- [ ] **Krok 5: Commit.**
  - `git commit -m "refactor(jednani): smazat nevykreslovaný blok úkolu jednání a jeho kopii pravidla vyjádření (spec 2026-11-10 R8)"`

---

### Task 5: Dokumentace, celé sady, průchod v prohlížeči, merge

**Soubory:**
- Upravit: `docs/specs/record-comments.md`, `docs/k-otestovani.md`, `docs/superpowers/specs/2026-11-10-vyjadreni-uprava-podle-role-design.md` (stav „schváleno, implementováno“)
- Memory: `project_subsystem_lead_comment_authz.md` (stav: implementováno, merge hash), `project_pre_existing_test_failures.md` (nové počty)

- [ ] **Krok 1: Dokumentace.**
  - `record-comments.md`: nová sekce „## Kdo smí přidat, upravit a smazat (spec 2026-11-10)“ obsahuje:
    - matici R1 jako tabulku;
    - jedno pravidlo R2;
    - kde se počítá (`CommentAuthorizationPolicy` + `CommentService.ResolveCommentScopeAsync` / `GetCommentActionsAsync`);
    - testy (`CommentRoleMatrixServerTests`, `CommentIconsByRoleRenderTests`, `CommentAuthorizationPolicyTests.Matice_R1`).
  - Do „Pokrytí testy“ přidat tyto třídy.
  - `k-otestovani.md`:
    - v §5 smazat odrážku „Nesoulad UI × server…“ a nahradit ji odkazem na novou §10;
    - pokud se v Tasku 2, Kroku 2 ukázalo, že smazání Gestora v Přípravě dnes neprojde, opravit i větu „Smazání projde“ v historickém popisu na skutečný stav.

- [ ] **Krok 2: Celé sady.**
  - Spustit:
    - `dotnet test PmTracker.Tests.Unit`
    - `dotnet test PmTracker.Tests.Api`
    - `dotnet test PmTracker.Tests.Integration`
    - `npm test`
    - E2E na větvi a na worktree `main`; porovnat jména selhání z TRX Pythonem (postup v memory `project_e2e_baseline_failures.md`).
  - Očekávám:
    - Unit, Api a JS vše PASS;
    - Integration vše PASS (od 2026-11-11, `d91cde9`, je zelený celý);
    - E2E: na větvi žádné nové jméno selhání proti `main` (baseline od `d91cde9`: 118 testů, 22 známých selhání).

- [ ] **Krok 3: Průchod v prohlížeči** (memory `reference_all_roles_browser_walkthrough.md`, `feedback_verify_ui_in_real_browser.md`).
  - Lokální app s env connection stringem, záloha dev DB, dočasné osoby „Průchod GEST / PROJ_MAN / VEDOUCI / METODIK / HOST“.
  - Projekt s jednáním v Přípravě, Otevřeno a Uzavřeno. Mezi rolemi uvolnit zámek editace.
  - Za každou roli Playwrightem (bundled driver z E2E bin):
    - ikony u vlastního a cizího vyjádření ve všech třech stavech;
    - úprava a smazání tam, kde ikony jsou;
    - nabídka jednání ve formuláři přidání;
    - podvržený POST úpravy tam, kde ikony nejsou → hláška „Nemáte oprávnění…“.
  - Stejně v detailu jednání (karta záznamu) a na stránce záznamu.
  - Snímky do scratchpadu, úklid osob a obnova DB ze zálohy.
  - Očekávám: shoda s maticí R1 ve všech buňkách.

- [ ] **Krok 4: `k-otestovani.md` §10 „Vyjádření — kdo smí přidat, upravit a smazat“.** Co se změnilo (tabulka kap. 4 spec), ruční checklist pro Gestora, metodika, vedoucího a PM v Přípravě, Otevřeno a Uzavřeno (asi 20 min), známá omezení podle spec kap. 7.

- [ ] **Krok 5: Commit a merge do `main`.**
  - `git commit -m "docs(vyjadreni): pravidla úprav podle role, k otestování §10"`
  - `git checkout main && git merge --no-ff feat/vyjadreni-uprava-podle-role`
  - Hash doplnit do §10 samostatným commitem. Push jen na pokyn.

## Odhad

| Task | Opus inline |
|---|---|
| 1 ProjectRoleCache | 0,3 h |
| 2 Pravidla a uložení + Api matice | 1,5 h |
| 3 Příznaky ikon + Api render | 1,2 h |
| 4 Mrtvá cesta | 0,5 h |
| 5 Sady, E2E baseline, průchod 5 rolí × 3 stavy, docs | 1,5 h |
| **Celkem** | **~5 h** |

Tvůj ruční test asi 20 min (§10).
