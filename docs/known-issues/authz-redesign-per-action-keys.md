# Zadání k opravě: Kompletní redesign permission modelu na per-action klíče

**Status:** Ready for implementation, vznik 2026-04-23.
**Rozsah:** celá aplikace — každý controller, každá mutující akce, seed, service vrstva, ViewModels, Views, testy.
**Autor rozhodnutí:** Pavel Andrlík.
**Nahrazuje:** [fixes-batch-20260423-authz-cleanup.md](fixes-batch-20260423-authz-cleanup.md) (čtyři dílčí úlohy A–D — jejich obsah je plně pokrytý tímto redesignem).
**Souvisí:**
- [authz-ui-serverside-mismatch.md](authz-ui-serverside-mismatch.md) — všechny nálezy 1–12 budou tímto redesignem vyřešeny.
- [meetings-endpoints-split-between-controllers.md](meetings-endpoints-split-between-controllers.md) — **zohledněno** v cílových endpointech. Meeting akce budou v `JednaniController` (`Jednani.NewMeetingModal`, `Jednani.EditMeetingModal`, `Jednani.Save`, `Jednani.Delete`) — ne v `ProjektyController`. Permission keys (`meetings.create`, `meetings.edit`, …) tím nejsou dotčeny — je to URL reorganizace. Doporučení: oba redesigny udělat **v jednom PR**, aby se odkazy ve Views nepřepisovaly dvakrát.

## Filozofie

**Každá mutující akce (POST / DELETE / PUT) má vlastní permission klíč.** Rolím se klíče skládají podle toho, jaké akce smí role dělat. Žádný klíč nefunguje v „compound" režimu (dnes např. `records.edit` pokrývá edit + delete + částečně schedule + přiřazení jednání + backdoor pro cizí komentáře — po redesignu už ne).

### Proč per-action

1. **Flexibilita mapování rolí.** Business později přiřadí roli jen některé akce (např. „vedoucí subsystému smí navrhnout záznam, ne ale smazat") bez nutnosti přidávat nové klíče nebo refaktorovat kód.
2. **Čitelnost.** Atribut `[Authorize(Policy = "permission:proposals.accept")]` přímo říká, co akce dělá. Bez nutnosti skákat do service a číst, co vše spadá pod hrubší klíč.
3. **Architecture enforcement.** Reflection test „každý mutující endpoint má vlastní policy klíč, který není sdílen s jinými endpointy" je triviálně vynutitelný.
4. **Seed jako katalog akcí.** Čtenář ze seedu vidí kompletní povolené chování aplikace — slouží jako specifikace, audit trail, dokumentace.
5. **Konec skrytých backdoorů.** Dnes se „admin smí upravit cizí komentář" řeší implicitně přes `records.edit`. Po redesignu: buď má `comments.edit.any`, nebo ne. Jednoznačně.
6. **Konec hardkódovaných rolí.** Nálezy 9 a 10 z audit dokumentu pramení z toho, že permission model nebyl dostatečně granulární → vývojář sáhl po „role code contains X" jako zkratce. S per-action klíči tahle zkratka nikdy nevznikne.

### Trade-off

- Větší volume seedu: ~22 klíčů → ~60 klíčů, ~100 řádků mappingů → ~400 řádků.
- Menší overhead při přidání nové akce (5 řádků v seedu místo rozhodování „do kterého existujícího klíče to spadne").

Pro aplikaci této velikosti (≈30 mutating endpointů dnes, odhadovaný růst) akceptovatelné.

### Konvence klíčů

`<doména>.<entita>?.<akce>[.<scope>]`

Příklady: `projects.create`, `team.member.add`, `comments.edit.own`, `vyzvy.pnf.reassign`.

Read-only GET endpointy, které jen zobrazí partial / detail, mohou být **groupovány** pod společný klíč (např. `dashboard.view` pro všechny dashboard panely). Mutace nikdy ne.

---

## Katalog cílových klíčů (kompletní)

Sekce následuje pořadí domén. Každý klíč má:
- **Akce** = co pokrývá (endpoint / UI element)
- **Scope level** = Global / Project / Subsystem (pro `AuthorizationHandler`)

### 1. Projekty

| Klíč | Akce | Scope |
|---|---|---|
| `projects.read.all` | Číst všechny projekty (management visibility) | Global |
| `projects.create` | Založit nový projekt | Global |
| `projects.edit` | Upravit metadata projektu | Project |
| `projects.delete` | Smazat projekt (soft-delete) | Project |

### 2. Záznamy (projektové)

| Klíč | Akce | Scope |
|---|---|---|
| `records.create` | Založit nový záznam | Project |
| `records.edit` | Upravit metadata záznamu (nezahrnuje komentáře ani schedule ani přiřazení jednání) | Project |
| `records.delete` | Smazat záznam | Project |
| `records.schedule.edit` | Upravit harmonogram úkolu (všechny sloty) | Project |
| `records.assign.meeting` | Přiřadit identifikátor jednání k záznamu | Project |

> **Zrušené oproti dnešku:** `records.schedule.add` (nahrazeno schvalovacím workflow proposals). `records.comment.subsystemlead` (přesunut do proposals pro návrhy, zachován jen pro `JednaniController` jako `meetings.notes.subsystemlead` — viz §4).

### 3. Komentáře

| Klíč | Akce | Scope |
|---|---|---|
| `comments.add` | Přidat komentář k záznamu | Project |
| `comments.edit.own` | Upravit vlastní komentář | Project |
| `comments.edit.any` | Upravit cizí komentář (admin) | Project |
| `comments.delete.own` | Smazat vlastní komentář | Project |
| `comments.delete.any` | Smazat cizí komentář (admin) | Project |

### 4. Jednání

| Klíč | Akce | Scope |
|---|---|---|
| `meetings.create` | Založit jednání | Project |
| `meetings.edit` | Upravit metadata jednání | Project |
| `meetings.delete` | Smazat jednání | Project |
| `meetings.status.change` | Změnit stav jednání | Project |
| `meetings.notes.edit` | Upravit zápis jednání | Project |
| `meetings.notes.subsystemlead` | Přidat zápis/vyjádření za vedoucího subsystému | Project |
| `meetings.attendance.edit` | Upravit docházku / účastníky | Project |
| `meetings.participant.add` | Přidat účastníka | Project |

> **Poznámka k `meetings.notes.subsystemlead`:** Nahrazuje dnešní dvojroli klíče `records.comment.subsystemlead`, který byl používán pro jednání i návrhy. Teď má každá doména vlastní klíč. Proposals má `proposals.create` (viz §5).

### 5. Návrhy (proposals)

| Klíč | Akce | Scope |
|---|---|---|
| `proposals.create` | Založit nový návrh (záznamu, harmonogramu) | Project |
| `proposals.edit.own` | Upravit vlastní návrh před rozhodnutím | Project |
| `proposals.edit.any` | Upravit cizí návrh před rozhodnutím (admin) | Project |
| `proposals.accept` | Schválit návrh | Project |
| `proposals.reject` | Zamítnout návrh | Project |
| `proposals.takeover` | Zamítnout a převzít vytvoření | Project |

> Akce `RejectAndEditProposal` = kombinace `proposals.reject` + následné `records.edit`; řeší se jako dvě policies (controller: `reject`, service check `records.edit`). `EditFromProposal` = admin bere návrh a edituje záznam → policy `records.edit` (záznam vlastní operace).

### 6. Vyjádření a externí odkazy

| Klíč | Akce | Scope |
|---|---|---|
| `externiodkazy.sync` | Ručně synchronizovat externí odkaz s SD | Project |
| `vyjadreni.modal.open` | Otevřít chat modal s vyjádřením | Project |
| `vyjadreni.refresh` | Obnovit data vyjádření z externího zdroje | Project |
| `vyjadreni.vazba.create` | Vytvořit vazbu harmonogramu | Project |
| `vyjadreni.vazba.delete` | Smazat vazbu harmonogramu | Project |
| `vyjadreni.reharvest` | ReHarvest (admin akce, per ticket) | Global / Project |

> **Poznámka:** Dnes všechny akce pod `records.edit`, ReHarvest výjimečně pod `settings.manage`. Nový model odděluje UI akce (vazby, refresh) od admin akce (reharvest).

### 7. Tým projektu

| Klíč | Akce | Scope |
|---|---|---|
| `team.member.add` | Přidat člena týmu | Project |
| `team.member.remove` | Odebrat člena týmu | Project |
| `team.role.assign` | Přiřadit projektovou roli | Project |
| `team.role.deactivate` | Deaktivovat projektovou roli | Project |
| `team.subsystem.create` | Vytvořit subsystém projektu (assign) | Project |
| `team.subsystem.reorder` | Přeřadit subsystémy | Project |
| `team.subsystem.deactivate` | Deaktivovat subsystém | Project |
| `team.subsystem.role.assign` | Přiřadit roli subsystému | Project |
| `team.subsystem.role.deactivate` | Deaktivovat roli subsystému | Project |
| `team.candidates.search` | Hledat kandidáta do týmu | Project |

> Dnes vše pod `team.manage` — nové klíče rozdělují jednotlivé akce.

### 8. Osoby

| Klíč | Akce | Scope |
|---|---|---|
| `people.create` | Přidat novou osobu | Global |
| `people.edit` | Upravit osobu | Global |
| `people.delete` | Smazat osobu | Global |
| `people.ad.search` | Hledat v Active Directory | Global |
| `people.ad.sync` | Synchronizovat osobu z AD | Global |

> Dnes vše pod `people.manage`.

### 9. Číselníky

| Klíč | Akce | Scope |
|---|---|---|
| `ciselniky.row.edit` | Upravit / uložit řádek číselníku | Global |
| `ciselniky.row.delete` | Smazat řádek | Global |

> Dnes vše pod `ciselniky.edit`.

### 10. Výzvy

| Klíč | Akce | Scope |
|---|---|---|
| `vyzvy.create` | Založit výzvu z bufferu | Project |
| `vyzvy.state.change` | Změnit stav výzvy | Project |
| `vyzvy.pnf.assign` | Zařadit / vyřadit PNF do bufferu | Project |
| `vyzvy.pnf.reassign` | Přeřadit PNF mezi výzvami | Project |
| `vyzvy.word.export` | Stáhnout Word export výzvy (připraveno pro budoucí feature) | Project |

> Dnes bez klíčů, jen `CanAccessProject` (Nálezy 2, 3, 6).

### 11. Dashboard projektu

| Klíč | Akce | Scope |
|---|---|---|
| `dashboard.view` | Otevřít dashboard projektu | Project |
| `dashboard.records.view` | Záložka Záznamy panel | Project |
| `dashboard.nes.view` | Záložka NES v prodlení | Project |
| `dashboard.statistics.view` | Záložka Statistiky | Project |
| `dashboard.vyzvy.view` | Záložka Výzvy | Project |

> Dnes všechny záložky pod `dashboard.view`. Rozdělení umožní business skrýt některou záložku pro určité role (např. HOST vidí jen Records, ne Statistiky).

### 12. Export

| Klíč | Akce | Scope |
|---|---|---|
| `export.pdf.projekt` | Tisk projektu do PDF | Project |
| `export.pdf.jednani` | Tisk jednání do PDF | Project |
| `export.pdf.ukol` | Tisk úkolu do PDF | Project |
| `export.word.projekt` | Word export projektu | Project |
| `export.word.jednani` | Word export jednání | Project |
| `export.word.ukol` | Word export úkolu | Project |

> Alternativa: jen `export.pdf` + `export.word` (hrubší). Rozhodnutí: rozdělit per entita — dává flexibilitu pro scenario „HOST může exportovat jednání, ne celý projekt".

### 13. Nastavení

| Klíč | Akce | Scope |
|---|---|---|
| `settings.view` | Otevřít sekci Nastavení | Global |
| `settings.roles.assign` | Přiřadit globální roli uživateli | Global |
| `settings.sync.configure` | Uložit nastavení synchronizačního jobu | Global |
| `settings.sync.run` | Ručně spustit synchronizační job | Global |
| `settings.sd.view` | Otevřít SD konektor | Global |
| `settings.sd.reharvest` | SD ReHarvest (per-ticket admin) | Global |

> Dnes vše pod `settings.view` + `settings.manage`. Nově: `.view` zůstává jako tab gate, admin akce dostávají vlastní klíče.

### 14. Hledání

| Klíč | Akce | Scope |
|---|---|---|
| `search.index` | Fulltext hledání (globální) | Global |
| `search.reindex` | Spustit reindex (admin) | Global |

### 15. Harmonogram preview

| Klíč | Akce | Scope |
|---|---|---|
| `schedule.preview` | Náhledový přepočet harmonogramu (stateless kalkulace) | Project |

> Navazuje na Nález 5. Klíč `schedule.preview` získají všechny role, které smí editovat harmonogram nebo záznam (`records.edit`, `records.schedule.edit`).

---

## Celkový počet klíčů

Přibližně **60 klíčů** napříč 15 doménami.

---

## Matice role × klíč (cílový stav)

Role v aplikaci (11 rolí):
- **SUPERADMIN** — plná kontrola
- **APP_ADMIN** — globální správa, bez projektové manipulace
- **READ_ALL** — read-only management visibility
- **VLASTNIK_PROJEKTU** — plný vlastník projektu (project scope)
- **ADM_PROJ** — silný admin projektu
- **PROJ_MAN** — projektový manažer
- **GEST** — gestor projektu (read + komentáře + exports)
- **HOST** — host projektu (read-only + vlastní komentáře)
- **VEDOUCI_SUBSYSTEMU** — vedoucí subsystému
- **ZASTUPCE_VEDOUCIHO_SUBSYSTEMU** — zástupce (stejná práva jako vedoucí)
- **METODIK_SUBSYSTEMU** — metodik subsystému

Matice 60 × 11 se nevejde do markdown tabulky čitelně, bude udržovaná v Excelu [authz-matrix.xlsx](authz-matrix.xlsx) jako nový sheet **Target state (per-action)**. V rámci implementace tento sheet vyplnit, odsouhlasit s uživatelem a přenést do [PermissionSeedConfiguration.cs](../../PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs).

### Předběžné skupinové rozhodnutí (draft)

Pro rychlou orientaci — níže jsou klíčové business rozhodnutí, která se v matici projeví. **Finální matici uživatel odsouhlasí v průběhu Fáze 2** (viz Plán implementace níže).

**ADMIN balíček** (SUPERADMIN, APP_ADMIN):
- Všech 60 klíčů = full access (modulo některé projektové akce, kde APP_ADMIN není „vlastník konkrétního projektu").

**PROJECT_EXECUTIVE balíček** (VLASTNIK_PROJEKTU, ADM_PROJ, PROJ_MAN):
- `projects.*`, `records.*`, `comments.*`, `meetings.*`, `proposals.*`, `team.*`, `vyzvy.*`, `dashboard.*`, `export.*.projekt`, `schedule.preview`, `externiodkazy.sync`, `vyjadreni.*` (bez reharvest)
- Rozdíly mezi VP / ADM_PROJ / PROJ_MAN: dnes minimální, finální matice potvrdí, zda zachovat.

**GEST balíček**:
- Read: `dashboard.view`, `dashboard.*.view` (všechny panely), `export.*`, `schedule.preview`
- Write: `comments.add`, `comments.edit.own`, `comments.delete.own`
- Čistě komentátor + read-only viewer.

**HOST balíček**:
- Read: `dashboard.view` + omezený subset panelů (např. bez `dashboard.statistics.view`)
- Write: nic (ani `comments.add` — host nepřispívá?)
- **Business rozhodnutí potřeba:** mají hosté přispívat komentáře? Dnes nemají.

**READ_ALL balíček**:
- `projects.read.all`, `dashboard.*.view`, `export.*`
- Žádný write.

**SUBSYSTEM_LEAD balíček** (VEDOUCI_SUBSYSTEMU, ZASTUPCE_VEDOUCIHO_SUBSYSTEMU):
- Write: `comments.add`, `comments.edit.own`, `comments.delete.own`, `proposals.create`, `proposals.edit.own`, `meetings.notes.subsystemlead`
- Read: bez projektového dashboardu (dnes nemají `dashboard.view`)
- Subsystémový filtr v service: proposals jen pro svůj subsystém.

**METODIK balíček** (METODIK_SUBSYSTEMU):
- Write: `comments.add`, `comments.edit.own`, `comments.delete.own`
- Nic dalšího — dnes stejná sada, budoucí business rozhodnutí může přidat `proposals.create`.

---

## Mapování akcí na klíče (per controller)

Pro každou controller akci je v následujících tabulkách uveden **cílový** `[Authorize(Policy = "permission:...")]` atribut. Pokud akce potřebuje dodatečný service-level check (např. autor comment → `comments.edit.own`, admin → `comments.edit.any`), uvedeno v poznámce.

### `ProjektyController.cs` + partials

| Akce | Dnes | Nový policy klíč |
|---|---|---|
| `Index` | auth | auth + `VisibleProjectIds` filter (service) |
| `Detail` | `CanAccessProject` | auth + `VisibleProjectIds` check |
| `NewProjectModal` | `projects.create` | `projects.create` (beze změny) |
| `SaveProject` | imperativní check | `projects.create` (POST nový) nebo `projects.edit` (POST update) — rozdělit na dvě akce? |
| `EditProjectModal` | `projects.edit` | `projects.edit` |
| `DeleteProjectModal` | `projects.delete` | `projects.delete` |
| `DeleteProject` | `projects.delete` | `projects.delete` |
| ~~`NewMeetingModal`~~ | `meetings.create` | **přesunuto do `JednaniController.NewMeetingModal`** |
| ~~`EditMeetingModal`~~ | `meetings.edit` | **přesunuto do `JednaniController.EditMeetingModal`** |
| ~~`SaveMeeting`~~ | imperativní | **přesunuto do `JednaniController.Save`** — policy `meetings.create` nebo `meetings.edit` |
| ~~`DeleteMeeting`~~ | `meetings.edit` | **přesunuto do `JednaniController.Delete`** — policy `meetings.delete` (**nový**) |
| `AddTeamMemberModal` | `team.manage` | `team.member.add` |
| `SaveTeamMember` | imperativní | `team.member.add` |
| `RemoveTeamMember` | imperativní | `team.member.remove` |
| `AssignProjectRoleModal` | `team.manage` | `team.role.assign` |
| `AssignProjectRole` | — (bez check) | `team.role.assign` |
| `DeactivateProjectRole` | — (bez check) | `team.role.deactivate` |
| `AssignProjectSubsystemModal` | `team.manage` | `team.subsystem.create` |
| `AssignProjectSubsystem` | — (bez check) | `team.subsystem.create` |
| `ReorderProjectSubsystem` | — (bez check) | `team.subsystem.reorder` |
| `DeactivateProjectSubsystem` | — (bez check) | `team.subsystem.deactivate` |
| `AssignProjectSubsystemRoleModal` | `team.manage` | `team.subsystem.role.assign` |
| `AssignProjectSubsystemRole` | — (bez check) | `team.subsystem.role.assign` |
| `DeactivateProjectSubsystemRole` | — (bez check) | `team.subsystem.role.deactivate` |
| `SearchProjectMemberCandidates` | `team.manage` | `team.candidates.search` |
| TabPartials (Records/Harmonogram/Jednani/Tym/Navrhy) | auth | auth + VisibleProjectIds |

### `ZaznamyController.cs` + partials + commands

| Akce | Dnes | Nový policy klíč |
|---|---|---|
| `Edit` (GET) | auth + imperativní check | `records.edit` (pro editor otevření) |
| `Create` (GET) | `records.edit` | `records.create` **(nový, oddělené)** |
| `Save` (POST) | imperativní `records.edit` | `records.create` nebo `records.edit` (podle isUpdate) |
| `DeleteRecordModal` | `records.edit` | `records.delete` **(nový)** |
| `DeleteRecord` | imperativní | `records.delete` |
| `AssignMeetingIdentifierModal` | `records.edit` | `records.assign.meeting` **(nový)** |
| `AssignMeetingIdentifier` | imperativní | `records.assign.meeting` |
| `AddComment` | `comments.add` | `comments.add` |
| `UpdateComment` | `comments.edit.own` (policy) + `records.edit` (service) | policy `comments.edit.own`; service zkontroluje `comments.edit.any` pro cizí autora |
| `DeleteComment` | `comments.delete.own` (policy) + `records.edit` (service) | policy `comments.delete.own`; service zkontroluje `comments.delete.any` pro cizí autora |

### `JednaniController.cs`

Po sloučení (viz [meetings-endpoints-split](meetings-endpoints-split-between-controllers.md)) obsahuje **všechny** endpointy nad entitou jednání:

| Akce | Dnes | Nový policy klíč |
|---|---|---|
| `Index` | auth | auth + project filter |
| `Detail` | auth | auth + project filter |
| `TaskItemPartial` | auth | auth + project filter |
| `NewMeetingModal` (přesunuto) | `meetings.create` | `meetings.create` |
| `EditMeetingModal` (přesunuto) | `meetings.edit` | `meetings.edit` |
| `Save` (přesunuto ze `SaveMeeting`) | imperativní | `meetings.create` (new) nebo `meetings.edit` (update) |
| `Delete` (přesunuto z `DeleteMeeting`) | `meetings.edit` | `meetings.delete` **(nový)** |
| `AddMeetingParticipantModal` | `meetings.edit` | `meetings.participant.add` |
| `AddMeetingParticipant` | imperativní | `meetings.participant.add` |
| `SaveStatus` | imperativní | `meetings.status.change` **(nový)** |
| `SaveAttendance` | imperativní | `meetings.attendance.edit` **(nový)** |
| `SaveNotes` | imperativní `records.edit` | `meetings.notes.edit` **(nový)** + autor check v service pro subsystem lead (přes `meetings.notes.subsystemlead`) |

### `NavrhyController.cs`

| Akce | Dnes | Nový policy klíč |
|---|---|---|
| `CreateRecordProposal` | `records.comment.subsystemlead` | `proposals.create` |
| `CreateScheduleProposal` | `records.comment.subsystemlead` | `proposals.create` |
| `SubmitCreateProposal` | `records.comment.subsystemlead` | `proposals.create` |
| `SubmitScheduleProposal` | `records.comment.subsystemlead` | `proposals.create` |
| `ProposalDetail` | auth | auth + project filter |
| `EditFromProposal` | — | `records.edit` (sáhá na záznam) |
| `PrefillCreateProposal` | — | `proposals.edit.own` |
| `ApproveProposal` | — | `proposals.accept` |
| `RejectProposal` | — | `proposals.reject` |
| `RejectAndTakeOverCreateProposal` | — | `proposals.takeover` |
| `RejectAndEditProposal` | — | `proposals.reject` + service check `records.edit` |

### `VyjadreniModalController.cs`

| Akce | Dnes | Nový policy klíč |
|---|---|---|
| `Modal` (GET) | `records.edit` | `vyjadreni.modal.open` **(nový)** |
| `Refresh` | `records.edit` | `vyjadreni.refresh` **(nový)** |
| `CreateVazba` | `records.edit` | `vyjadreni.vazba.create` **(nový)** |
| `DeleteVazba` | `records.edit` | `vyjadreni.vazba.delete` **(nový)** |
| `ReHarvest` | `settings.manage` | `vyjadreni.reharvest` **(nový — sjednoceno se SD)** |

### `ExterniOdkazController.cs`

| Akce | Dnes | Nový policy klíč |
|---|---|---|
| `Sync` | imperativní `records.edit` | `externiodkazy.sync` **(nový)** |

### `VyzvyController.cs`

| Akce | Dnes | Nový policy klíč |
|---|---|---|
| `Zalozit` | jen CanAccessProject | `vyzvy.create` **(nový)** |
| `ZmenitStav` | jen CanAccessProject | `vyzvy.state.change` **(nový)** |
| `SetZaradid` | — (žádný check) | `vyzvy.pnf.assign` **(nový)** |
| `Prerdit` | — (žádný check) | `vyzvy.pnf.reassign` **(nový)** |
| `ReassignModal` | CanAccessProject | `vyzvy.pnf.reassign` (GET modal) |

### `ProjectDashboardController.cs`

| Akce | Dnes | Nový policy klíč |
|---|---|---|
| `Index` | `dashboard.view` | `dashboard.view` |
| `RecordsPanel` | `dashboard.view` | `dashboard.records.view` |
| `NesPanel` | `dashboard.view` | `dashboard.nes.view` |
| `StatisticsPanel` | `dashboard.view` | `dashboard.statistics.view` |
| `VyzvyPanel` | `dashboard.view` | `dashboard.vyzvy.view` |

> **Odstranit hardkódovaný whitelist** v `ProjectDashboardService.CanAccessDashboardAsync` + `CanUserEditProjectVyzvyAsync` (Nálezy 1, 3). Nahradit za `HasPermission(dashboard.view, projektId)` a `HasPermission(vyzvy.create, projektId)` respektive.

### `ExportController.cs`

| Akce | Dnes | Nový policy klíč |
|---|---|---|
| `ProjektTisk` | `export.pdf` | `export.pdf.projekt` |
| `ProjektWord` | `export.word` | `export.word.projekt` |
| `JednaniTisk` | `export.pdf` | `export.pdf.jednani` |
| `JednaniWord` | `export.word` | `export.word.jednani` |
| `UkolTisk` | `export.pdf` | `export.pdf.ukol` |
| `UkolWord` | `export.word` | `export.word.ukol` |
| `Pdf` | `export.pdf` | `export.pdf.projekt` (dialog wrapper) |
| `Dialog` | auth | auth (jen konfigurace) |

### `ScheduleController.cs`

| Akce | Dnes | Nový policy klíč |
|---|---|---|
| `Recalc` | jen auth (Nález 5) | `schedule.preview` **(nový)** — vyžaduje i `projektId` v requestu (viz implementační poznámka) |

### `SearchController.cs`

| Akce | Dnes | Nový policy klíč |
|---|---|---|
| `Index` | auth | `search.index` |
| `Suggest` | auth | `search.index` |
| `Reindex` | `search.reindex` | `search.reindex` |
| `Status` | `search.reindex` | `search.reindex` |

### `NastaveniController.cs`

| Akce | Dnes | Nový policy klíč |
|---|---|---|
| `Index` / `Panel` | `settings.view` | `settings.view` |
| `UserRolesModal` | `settings.manage` | `settings.roles.assign` |
| `SaveUserRole` | `settings.manage` | `settings.roles.assign` |
| `SaveUserRolesForUser` | `settings.manage` | `settings.roles.assign` |

### `NastaveniSyncController.cs`

| Akce | Dnes | Nový policy klíč |
|---|---|---|
| `Save` | `settings.manage` | `settings.sync.configure` |
| `RunNow` | `settings.manage` | `settings.sync.run` |

### `SDConnectorController.cs`

| Akce | Dnes | Nový policy klíč |
|---|---|---|
| `Index` | `settings.manage` | `settings.sd.view` |
| `ReHarvest` | `settings.manage` | `settings.sd.reharvest` |

### `OsobyController.cs`

| Akce | Dnes | Nový policy klíč |
|---|---|---|
| `Index` | auth | `people.create` OR `people.edit` (= „vidím správu osob") — alternativa: samostatný `people.view` |
| `AdPersonModal` | `people.manage` | `people.ad.search` |
| `SearchAd` | `people.manage` | `people.ad.search` |
| `SaveAd` | `people.manage` | `people.create` |
| `ManualPersonModal` | `people.manage` | `people.create` (pokud id=null) / `people.edit` (pokud id>0) |
| `SaveManual` | `people.manage` | `people.create` / `people.edit` |
| `SyncFromAd` | `people.manage` | `people.ad.sync` |
| `Delete` | `people.manage` | `people.delete` |

### `CiselnikyController.cs`

| Akce | Dnes | Nový policy klíč |
|---|---|---|
| `Index` / `Detail` / `Panel` | auth | auth + `ciselniky.row.edit` OR `ciselniky.row.delete` pro UI gating |
| `EditRow` | `ciselniky.edit` | `ciselniky.row.edit` |
| `SaveRow` | `ciselniky.edit` | `ciselniky.row.edit` |
| `DeleteRow` | `ciselniky.edit` | `ciselniky.row.delete` |

### `ObsazeniController.cs` / `DashboardController.cs` / ostatní read-only

Beze změny v autorizaci (auth + scope filter). Dashboard (hlavní) je viditelný všem přihlášeným.

---

## Seznam souborů k úpravě (velký — celý authz stack)

### Seed + konstanty
- [PermissionSeedConfiguration.cs](../../PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs) — kompletní přepis `Actions`, `Roles`, `RoleMappings`. Očekávaná velikost: ~60 Actions + ~400 RoleMappings.
- [SecurityViewModels.cs](../../PmTracker.Web/Models/ViewModels/SecurityViewModels.cs) — kompletní přepis `PermissionKeys` konstant, `PermissionMetadata`, `PermissionKeys.GrantsProjectRead`, `PermissionKeys.IsBlockedForDeletedProject`.

### Controllers
- **Všechny** soubory v `PmTracker.Web/Controllers/` s mutating endpointy — každý endpoint dostane nový / přepsaný `[Authorize(Policy = "permission:…")]` atribut podle mapovací tabulky výše.

### Service vrstva
- [CommentAuthorizationPolicy.cs](../../PmTracker.Web/Services/Common/CommentAuthorizationPolicy.cs) — nahradit `records.edit` za `comments.edit.any` / `comments.delete.any`.
- [RecordProposalAuthorizationPolicy.cs](../../PmTracker.Web/Services/Records/RecordProposalAuthorizationPolicy.cs) — **odstranit** hardkódovaný whitelist role codes, nahradit za `HasPermission(proposals.accept, projektId)` pro rozhodování; subsystémový filtr pro navrhovatele zachovat.
- [ProjectDashboardService.cs](../../PmTracker.Web/Services/ProjectDashboard/ProjectDashboardService.cs) — **odstranit** `CanUserEditProjectVyzvyAsync` (hardkódované role), nahradit za `HasPermission(vyzvy.create, projektId)`. **Odstranit** `CanAccessDashboardAsync`, nahradit za `HasPermission(dashboard.view, projektId)`.
- [ProjectDashboardAuthorizationPolicy.cs](../../PmTracker.Web/Services/ProjectDashboard/ProjectDashboardAuthorizationPolicy.cs) — **SMAZAT** celý soubor (hardkódovaný whitelist, Nález 1).
- [RecordService.SaveRecord.cs](../../PmTracker.Web/Services/RecordService.SaveRecord.cs) — odstranit větev pro `records.schedule.add`, zachovat jen `records.schedule.edit`.
- [VyzvaService.Assignment.cs](../../PmTracker.Web/Services/Vyzvy/VyzvaService.Assignment.cs) — nahradit `records.edit` za `vyzvy.pnf.reassign` / `vyzvy.pnf.assign` podle kontextu.

### ViewModels
- [SecurityViewModels.cs](../../PmTracker.Web/Models/ViewModels/SecurityViewModels.cs) — nové `Can…` computed properties podle nových klíčů.
- [ProjektZaznamyTabViewModels.cs](../../PmTracker.Web/Models/ViewModels/Projekty/ProjektZaznamyTabViewModels.cs) — odstranit `CanAddSchedule`, zachovat `CanEditSchedule`, `CanManageSchedule`.
- [ZaznamEditViewModels.cs](../../PmTracker.Web/Models/ViewModels/Projekty/ZaznamEditViewModels.cs) — analogicky, ověřit všechny `Can…` properties.

### Views (Razor)
- Grep `@if (Model.Can…)` napříč `PmTracker.Web/Views/**/*.cshtml` — každý odkaz musí odpovídat novému klíči. Kde se dnes používá hrubý flag (např. `CanEditRecord` pro delete button), rozdělit na jemnější (`CanDeleteRecord`).

### JavaScript
- [block.js](../../PmTracker.Web/wwwroot/js/modules/schedule/block.js) — `fetchSchedulePreview` přidat `projektId` do payloadu (viz Úloha D).
- Obecný grep `data-permission` / `data-can-*` v `PmTracker.Web/wwwroot/js/` — aktualizovat client-side gating.

### Tests

- **Rozšířit** `AuthorizationPolicyEnforcementTests`:
  - Každý mutující endpoint **musí** mít `[Authorize(Policy = "permission:…")]`. Odstranit allowlist (nálezy 4, 5, 6).
  - Nový test: **každý policy klíč je použit právě jednou** v atributech (detekce kopírování klíčů mezi nesouvisejícími akcemi).
- **Rozšířit** `SeedSourceOfTruthTests`:
  - Všechny klíče z `PermissionKeys` konstant odpovídají seed Actions.
  - Žádný klíč v kódu, který není v seedu, a naopak.
- **Nový** `NoHardcodedRolesTest`:
  - Reflection scan — v `PmTracker.Web/Services/**` a `PmTracker.Web/Controllers/**` žádné přímé porovnání s `ProjectRoleCodes.*` / `"proj_man"` / `"adm_proj"` atd. Výjimky: seeder, linker, unit testy.
- **Přidat** integrační testy pro každou roli × každý endpoint (samplové kombinace):
  - PROJ_MAN: smí skoro všechno v projektu, kde je přiřazený.
  - HOST: read-only, žádný write.
  - VEDOUCI_SUBSYSTEMU: proposals + comments pro svůj subsystém.
  - APP_ADMIN: globální správa, ale ne mutace na projektech.
  - GEST: jen komentáře + view.

### DB migrace

`db_upgrade_*_full_permission_redesign.sql`:
```sql
-- Odstranit staré klíče, které byly sjednoceny pod granulárnější nové
DELETE FROM authz.role_permissions WHERE permission_id IN (
  SELECT id FROM authz.permissions WHERE klic IN (
    'records.schedule.add',
    'records.comment.subsystemlead',
    'team.manage',
    'people.manage',
    'ciselniky.edit',
    'settings.manage',
    'export.pdf',
    'export.word'
  )
);
DELETE FROM authz.permissions WHERE klic IN (
  'records.schedule.add',
  'records.comment.subsystemlead',
  'team.manage',
  'people.manage',
  'ciselniky.edit',
  'settings.manage',
  'export.pdf',
  'export.word'
);
```

Nové klíče přidá `PermissionSeeder` při startu aplikace. **Poznámka k offline deploymentu**: skript připravit, při release poslat DBA s explicitním upozorněním na backward-incompatible změnu.

**Příprava seeder logiky**: pokud dnes seeder umí přidávat nové klíče, ověřit, že umí i odebírat staré — jinak kompletní DB restore / re-seed z čistého stavu.

---

## Plán implementace (fáze)

### Fáze 0 — katalog + matice (vstupní rozhodnutí)

**Bez kódu.** Uživatel + implementátor:
1. Projdou kompletní katalog klíčů z tohoto dokumentu.
2. Vyplní sheet „Target state (per-action)" v [authz-matrix.xlsx](authz-matrix.xlsx) — 60 klíčů × 11 rolí = ~660 buněk.
3. Odsouhlasí finální matici.
4. Identifikují edge cases (HOST × komentáře, GEST × exports, METODIK × proposals).

**Výstup:** schválený sheet matice role × klíč.

### Fáze 1 — Seed + konstanty

1. Přepsat `PermissionSeedConfiguration.cs` podle schválené matice.
2. Přepsat `SecurityViewModels.cs` (konstanty `PermissionKeys`, metadata, seznamy).
3. Spustit `SeedSourceOfTruthTests` — ověřit konzistenci.
4. Spustit aplikaci → `PermissionSeeder` nahraje do DB (dev prostředí).

### Fáze 2 — Controller atributy

1. Pro každý controller projít mapovací tabulku výše.
2. Přepsat `[Authorize(Policy = "…")]` atributy.
3. Odstranit imperativní `HasPermission` checky tam, kde je policy atribut dostačující.
4. Spustit `AuthorizationPolicyEnforcementTests` — musí projít bez allowlistu pro staré nálezy.

### Fáze 3 — Service vrstva

1. `CommentAuthorizationPolicy` — `.any` keys místo `records.edit`.
2. `RecordProposalAuthorizationPolicy` — odstranit DB query na role codes, použít `HasPermission(proposals.accept)`.
3. `ProjectDashboardService` — smazat hardkódované metody, použít `HasPermission`.
4. **Smazat** `ProjectDashboardAuthorizationPolicy.cs`.
5. `VyzvaService.Assignment` — použít nové vyzvy keys místo records.edit.
6. `RecordService.SaveRecord` — smazat schedule.add větev.

### Fáze 4 — ViewModels + Views

1. Přidat nové `Can…` properties do ViewModelů — vypočítané z nových klíčů.
2. Aktualizovat `@if` guardy v Views.
3. Smazat `CanAddSchedule` z VM + Views.

### Fáze 5 — JavaScript

1. `block.js` — `fetchSchedulePreview` s `projektId`.
2. Client-side gating (`data-permission` atributy) — aktualizovat.

### Fáze 6 — Testy

1. Aktualizovat existující testy na nové klíče.
2. Přidat `NoHardcodedRolesTest`.
3. Rozšířit `AuthorizationPolicyEnforcementTests` o „každý klíč právě jednou" test.
4. Přidat integrační testy per-role × per-endpoint.

### Fáze 7 — Verifikace

1. Manuální průchod UI pod 6 rolemi (SUPERADMIN, APP_ADMIN, PROJ_MAN, GEST, HOST, VEDOUCI_SUBSYSTEMU).
2. Check, že žádná akce nezůstala přístupná bez policy.
3. Data scoping check: list endpointy vrací jen povolená data.

### Fáze 8 — Migrace a release

1. Sestavit `db_upgrade_*.sql` skript.
2. Sepsat changelog + rollback plán.
3. Předat DBA + release koordinátorovi.
4. Po release: manuální smoke test všech klíčových workflow.

---

## Acceptance kritéria (souhrn)

1. **Seed obsahuje ~60 klíčů**, každý odpovídá přesně jedné sémantické akci.
2. **Každá mutující controller akce má `[Authorize(Policy)]`** — `AuthorizationPolicyEnforcementTests` projde bez allowlistu.
3. **Reflection test**: žádný policy klíč se neopakuje na dvou nesouvisejících endpointech.
4. **`NoHardcodedRolesTest`**: žádný výskyt `ProjectRoleCodes.*` nebo role code string literalů v `Services/**` a `Controllers/**` mimo seeder/linker.
5. **`SeedSourceOfTruthTests`**: bijekce mezi `PermissionKeys` konstantami a seed Actions.
6. **Smazány**:
   - `ProjectDashboardAuthorizationPolicy.cs` (hardkódovaný whitelist).
   - Metoda `CanUserEditProjectVyzvyAsync` v `ProjectDashboardService`.
   - Metoda `CanAccessDashboardAsync` (nahrazena `HasPermission`).
   - DB query na role codes v `RecordProposalAuthorizationPolicy.CanDecideProjectProposalAsync`.
7. **Nálezy 1–12** z [authz-ui-serverside-mismatch.md](authz-ui-serverside-mismatch.md) jsou pokryté:
   - Nálezy 1, 3, 9 — hardkódované whitelists pryč.
   - Nálezy 2, 4, 5, 6, 7, 8 — chybějící policies doplněny.
   - Nález 12 — `.any` klíče pro komentáře.
8. **Matice autorizace v Excelu** je finální a odpovídá kódu (jeden soubor pravdy).
9. **Integrační testy per-role** pokrývají alespoň happy + forbidden path pro každý major scénář.
10. **UI parita**: každé tlačítko v UI je skryté pro uživatele bez odpovídajícího klíče; server vrací 403 i při přímém volání.

---

## Rizika

- **Velký rozsah PR.** Doporučuju rozdělit na fáze a udělat každou jako samostatný commit v PR (seed → controllers → service → VM/Views → tests). Ne samostatné PR, aby autorizace nebyla nikdy v inconsistentním stavu.
- **Backward incompatibility.** Role, která dnes měla `records.edit`, dnes mohla dělat i věci, které po redesignu vyžadují jiný klíč (schedule, assign meeting, comments admin). Po migraci musí mít role **všechny** odpovídající nové klíče, jinak přestane akce fungovat. Kritická část matice.
- **Offline deployment.** DB skript musí být přesný a testovaný v dev prostředí před produkcí. Žádný online rollback.
- **Subsystémový filtr**. Proposals a comments mají business constraints (subsystem membership), které permission model nepokrývá — ty dál zůstávají v service. Test musí ověřit, že service filter funguje spolu s novými klíči.
- **Změna psychologie vývojářů.** Přidání nového endpointu nyní vyžaduje přidat **nový klíč** + namapovat na role. Doporučuju do CONTRIBUTING.md (nebo CLAUDE.md) přidat checklist „když přidáváš mutující endpoint, přidej permission klíč". Architecture testy tohle vynutí automaticky, ale explicitní dokumentace pomůže.

---

## Závislosti

- Žádné blokující závislosti — může se začít ihned po odsouhlasení matice role × klíč (Fáze 0).
- **Souběžně s tím nepracovat** na `PmTracker.Web/Services/Security/**` ani `PmTracker.Web/Controllers/**` v jiných větvích — merge konflikty by byly krutí.
- **Nedělat současně** s úlohou `records.schedule.add` cleanup ani s dílčími fixes — ty jsou součástí tohoto redesignu.
