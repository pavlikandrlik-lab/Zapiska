# Vyjádření: kdo smí přidat, upravit a smazat — sjednocení UI a serveru — specifikace

**Datum:** 2026-11-10
**Stav:** schváleno 2026-11-11, implementováno (plán `docs/superpowers/plans/2026-11-11-vyjadreni-uprava-podle-role.md`)
**Navazuje na:** rozhodnutí 2026-06-16 (vícevrstvá autorizace vyjádření vedoucího subsystému, `CommentAuthorizationPolicy`), spec `2026-10-22-vyjadreni-kompaktni-blok-design.md` (kap. 5.4 — známý nesoulad, který tato spec řeší)
**Komponenty:** pravidla (`Services/Common/CommentAuthorizationPolicy.cs`), uložení (`Services/CommentService.cs`), osoby subsystémových rolí (`Services/ProjectRoleCache.cs`), vykreslení ikon (`Views/Projekty/_ZaznamCommentsPartial.cshtml`), příprava panelu vyjádření (`Controllers/ZaznamyController.Partials.cs`)

## 1. Zadání (uživatel, 2026-11-10)

> Gestor, projektový manažer, administrátor projektu mají všude ANO. Metodik, vedoucí subsystému a zástupce mohou v přípravě měnit CRUD, v otevřeno pro zápis už nic.

Doplňující otázka (2026-11-10): má mít metodik stejné omezení na vlastní subsystém jako vedoucí? Odpověď: **ano, jen svůj subsystém.**

Stavy jednání: **Příprava** (`DRAFT`), **Otevřeno pro zápis** (`OPEN`), **Uzavřeno** (`CLOSED`).

## 2. Současný stav (ověřeno 2026-11-10 v kódu; UI × server průchodem 2026-10-22)

### Přidání vyjádření (`CommentAuthorizationPolicy.CanAddComment`)
1. Přímá projektová nebo globální role s `comments.add` (Superadmin, Admin aplikace, Vlastník, Admin projektu, PM, Gestor) → v Přípravě i Otevřeno, u všech subsystémů.
2. Vedoucí nebo zástupce subsystému → jen v Přípravě a jen u záznamů svého subsystému (+ `meetings.notes.subsystemlead`).
3. Ostatní s `comments.add` zděděným ze subsystémové role, tj. **metodik** → v Přípravě i Otevřeno, **u všech subsystémů projektu** (technický kompromis z června, ne záměr).

Nabídka jednání ve formuláři „Přidat vyjádření“ se počítá touž metodou (`GetMeetingIdsAllowingNewCommentAsync`), takže je se serverem v souladu.

### Úprava a smazání (`CanModifyComment`, volá ho `UpdateCommentAsync` i `DeleteCommentAsync`)
- `comments.edit.any` (Superadmin, Admin aplikace, Vlastník, Admin projektu, PM) → jakékoli vyjádření.
- Vlastní vyjádření s `comments.edit.own` → **mimo Přípravu** povoleno komukoli; **v Přípravě** jen vedoucímu nebo zástupci subsystému záznamu.
- Vedoucí a zástupce smí v Přípravě upravit a smazat vlastní vyjádření i bez `comments.edit.own` (rozhodnutí 2026-06-16, „implikovaný CRUD“).
- Smazání se kontroluje klíči pro **úpravu**, ne `comments.delete.*` (v seedu je mají tytéž role, takže to dnes nevadí, ale je to náhoda).

### Ikony Upravit a Smazat (`_ZaznamCommentsPartial.cshtml:67`)
`LzeUpravit && (CanEditRecord || (CanEditOwnAsSubsystemLeader && autor = já))`, kde `CanEditOwnAsSubsystemLeader` znamená jen „jednání je v Přípravě“ (roli nekontroluje, název mate). Kdo nemá `records.edit`, vidí ikony u svého vyjádření jen v Přípravě.

### Výsledek — vlastní vyjádření rolí bez `records.edit`
| Role | Stav jednání | Ikony | Server |
|---|---|---|---|
| Gestor, Metodik | Příprava | ukáže | **odmítne** („Nemáte oprávnění upravit toto vyjádření.“) |
| Gestor, Metodik | Otevřeno pro zápis | **neukáže** | povolí |
| Vedoucí, Zástupce | Příprava | ukáže | povolí |
| Vedoucí, Zástupce | Otevřeno pro zápis | **neukáže** | povolí |

`docs/k-otestovani.md` §5 u Gestora a Metodika v Přípravě uvádí „smazání projde“. Podle kódu by mělo být odmítnuto stejně jako úprava. Ověří se testem před změnou; výsledek se zapíše do plánu.

### Mrtvá druhá kopie pravidla (detail jednání)
`Views/Jednani/_TaskItemPartial.cshtml` má vlastní kopii podmínky pro ikony i pro formulář přidání. Vykresluje ho jen `JednaniController.TaskItemPartial`, a ten volá jen obnovení `refreshMeetingTaskItem` po uložení s `uiContext = "meeting"` — a tu hodnotu posílají jen formuláře uvnitř `_TaskItemPartial`. Detail jednání dnes vykresluje záznamy přes `_ProjectRecordsTab` → karta → `_ZaznamCommentsPartial`, takže se `_TaskItemPartial` do stránky nikdy nedostane. Navíc `MeetingService.BuildMeetingTasksAsync` plní `JednaniDetailViewModel.Ukoly` při každém otevření detailu jednání, ale žádná šablona `Ukoly` nečte (zbytečný dotaz do DB). Příznak `CanCommentAsSubsystemLeader` na kartě a panelu vyjádření čte jen `_TaskItemPartial`.

## 3. Rozhodnutí

| # | Rozhodnutí | Zdroj |
|---|---|---|
| R1 | **Cílová matice** (Uzavřené jednání: nikdo nic — beze změny): <br>• **Superadmin, Admin aplikace, Vlastník projektu, Admin projektu, PM:** přidat v Přípravě i Otevřeno u všech subsystémů; upravit a smazat **jakékoli** vyjádření v Přípravě i Otevřeno (beze změny). <br>• **Gestor:** přidat v Přípravě i Otevřeno u všech subsystémů (beze změny); upravit a smazat **vlastní** vyjádření v Přípravě i Otevřeno. Cizí ne. <br>• **Vedoucí, Zástupce vedoucího, Metodik subsystému:** přidat, upravit a smazat **vlastní** vyjádření **jen v Přípravě a jen u záznamů svého subsystému**. V Otevřeno nic. Cizí ne. <br>• **Host, Read-all:** nic. | uživatel |
| R2 | **Jedno pravidlo pro vlastní vyjádření:** smí ho upravit a smazat, **kdo by ho teď na tomtéž místě smel přidat** (role, stav jednání, subsystém záznamu). Úprava navíc vyžaduje `comments.edit.own`, smazání `comments.delete.own`. Výjimka 2026-06-16 (vedoucí a zástupce v Přípravě bez klíče) zůstává. | odvozeno z R1 |
| R3 | **Metodik jen ve svém subsystému:** subsystémové omezení platí pro všechny tři subsystémové role stejně. Osoby se pro subsystém záznamu (globální `SubsystemId`) zjišťují stejně jako dnes u vedoucího (`ProjectRoleCache`, mapování `ProjektSubsystemId → SubsystemId`), nově včetně metodika. | uživatel |
| R4 | **Kombinace rolí — „vyšší bere“ (beze změny):** kdo má přímou projektovou roli s `comments.add` (Gestor, PM, …) i subsystémovou roli, řídí se přímou rolí. Kdo má subsystémové role ve více subsystémech, smí v každém z nich. | rozhodnutí 2026-06-16 |
| R5 | **Smazání podle klíčů pro mazání:** úprava kontroluje `comments.edit.own` / `comments.edit.any`, smazání `comments.delete.own` / `comments.delete.any`. Seed se nemění, takže se chování podle seedu nezmění. | odvozeno |
| R6 | **Ikony počítá server touž metodou jako uložení:** pro každé vyjádření dva příznaky — **smí upravit** (tužka) a **smí smazat** (koš). Šablona je jen vykreslí, vlastní podmínku už neskládá. Zavádějící `CanEditOwnAsSubsystemLeader` zmizí. Co UI ukáže, server přijme; co neukáže, server odmítne. | uživatel (sjednotit UI se serverem) |
| R7 | **Nabídka jednání pro nové vyjádření** se počítá dál touž metodou jako uložení (beze změny mechanismu). Metodikovi se tím zúží na jednání v Přípravě u záznamů jeho subsystému; u ostatních záznamů formulář „Přidat vyjádření“ neuvidí. | odvozeno z R1 |
| R8 | **Mrtvá cesta detailu jednání se smaže**, aby pravidlo nezůstalo ve druhé kopii, kterou nic nevykresluje a nic v prohlížeči neověří: `_TaskItemPartial.cshtml`, `JednaniController.TaskItemPartial`, `JednaniTaskItemPartialViewModel`, `IMeetingService.GetSingleTaskAsync`, `BuildMeetingTasksAsync` a `JednaniDetailViewModel.Ukoly`, větev `uiContext = "meeting"` v `RecordUiFlowResolver` a `refreshMeetingTaskItem`, příznaky `CanCommentAsSubsystemLeader`, Api testy `TaskItemPartial_*`. Před smazáním se každá část ověří grepem (Views, JS, testy) a smazání se ověří E2E sadou a průchodem detailem jednání. | odvozeno (pravidlo „oprav všechny sourozence“) |
| R9 | **Stav záznamu se do pravidla nepromítá (beze změny):** k uzavřenému záznamu nelze vyjádření přidat (`AddCommentAsync`), úprava a smazání existujícího se řídí jen rolí a jednáním jako dnes. | beze změny |

## 4. Co se pro uživatele mění

| Role | Dnes | Nově |
|---|---|---|
| Gestor | vlastní v Přípravě: tužka je, uložení odmítne; v Otevřeno: tužka chybí | vlastní v Přípravě i Otevřeno: tužka i koš, uložení projde |
| Metodik | přidá v Přípravě i Otevřeno u všech subsystémů; úprava jako Gestor | přidá, upraví, smaže jen v Přípravě u svého subsystému; jinde nic |
| Vedoucí, Zástupce | vlastní v Otevřeno: tužka chybí, server by povolil | tužka chybí a server odmítne (pro uživatele beze změny) |
| Superadmin, Admin aplikace, Vlastník, Admin projektu, PM, Host, Read-all | — | beze změny |

**Starší vyjádření**, která metodik napsal u cizího subsystému nebo v Otevřeno, zůstávají. Metodik je upravit nemůže, PM a admini ano.

## 5. Rozsah

**Mění se:**
- `CommentAuthorizationPolicy` — `CanAddComment` (metodik pod subsystémové omezení, R1/R3), úprava a smazání vlastního podle R2/R5 (samostatná metoda pro úpravu a pro smazání), kontrakt `ICommentAuthorizationPolicy`.
- `ProjectRoleCache` / `IProjectRoleCache` — osoby subsystémových rolí pro subsystém včetně metodika (R3).
- `CommentService` — úprava a smazání podle nových metod; nová metoda, která pro panel vyjádření spočítá příznaky „smí upravit / smí smazat“ pro každé vyjádření (R6).
- `ZaznamyController.Partials.PrepareRecordCommentsPresentationAsync` (karta i stránka záznamu) — doplní příznaky; `VyjadreniViewModel` — dva nové příznaky místo `CanEditOwnAsSubsystemLeader`; `ProjectService.LazyQueries` / `RecordCards` — přestanou příznak počítat.
- `_ZaznamCommentsPartial.cshtml` — ikony podle příznaků.
- Smazání mrtvé cesty podle R8.
- Dokumentace: `docs/specs/record-comments.md` (pravidla), `docs/k-otestovani.md` (§5 známé omezení pryč, nová sekce).

**Nemění se:** klíče oprávnění a seed rolí, zápis z jednání (`CanSaveMeetingNote`), zamčení u uzavřeného jednání (`EnsureMeetingAllowsCommentChangesAsync`), vzhled ikon a panelu.

## 6. Testy

- **Unit, pravidla:** matice role × stav jednání (Příprava, Otevřeno) × vlastní/cizí vyjádření × vlastní/cizí subsystém pro přidání, úpravu a smazání; kombinace rolí (Gestor + metodik, PM + vedoucí, metodik ve dvou subsystémech); osoba, která roli ztratila.
- **Unit, `ProjectRoleCache`:** metodik je v seznamu osob svého subsystému, ne cizího; odebraná role se nepočítá.
- **Api, uložení:** podvržená úprava i smazání — Gestor v Přípravě projde; metodik v Otevřeno a u cizího subsystému odmítnuto; vedoucí v Otevřeno odmítnuto; metodik nedostane jednání v Otevřeno v nabídce.
- **Api, vykreslení:** ikony u vlastního vyjádření podle R1 pro Gestora, metodika a vedoucího v Přípravě i Otevřeno.
- **Před změnou** se testem ověří dnešní chování smazání Gestora v Přípravě (kap. 2).
- **E2E:** celá sada, porovnání jmen selhání s baseline.
- **Průchod v prohlížeči** za všechny role (dočasné osoby) v Přípravě, Otevřeno i Uzavřeno: ikony, uložení úpravy, smazání, nabídka jednání; podvržené požadavky tam, kde ikony nejsou.

## 7. Mimo rozsah

- Okamžité zmizení ikon, když se jednání přepne do jiného stavu v jiném okně (projeví se po obnovení panelu, jako dnes).
- Redesign klíčů oprávnění (`docs/known-issues/authz-ui-serverside-mismatch.md`).

## 8. Odhad

~5 h inline (pravidla a matice testů ~1,5 h, příznaky pro ikony a Api testy ~1,5 h, smazání mrtvé cesty ~0,5 h, průchod v prohlížeči ~1,5 h). Ruční test uživatele ~20 min.
