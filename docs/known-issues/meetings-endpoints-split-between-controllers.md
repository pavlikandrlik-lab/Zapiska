# Chyba: Endpointy „Jednání" rozdělené mezi dva kontroléry bez systematického důvodu

**Status:** Open, vznik 2026-04-23. Ready for implementation po byznysovém rozhodnutí o pořadí priorit.
**Kategorie:** Architecture / Code organization (není to bezpečnostní chyba).
**Impact:** Nízký běhově, **střední** pro údržbu a audit.

## Popis chyby

Doména „Jednání" (meetings) má své HTTP endpointy **rozdělené mezi dva kontroléry**, bez toho, aby rozdělení odpovídalo nějakému systematickému principu. Rozdělení je historické — vzniklo podle toho, z jakého UI kontextu se která akce otevírala:

### `ProjektyController` (mountnuté na `/Projekty/...`)

CRUD nad entitou Jednání + tab v detailu projektu:

| Akce | Endpoint | Policy |
|---|---|---|
| NewMeetingModal | `GET /Projekty/NewMeetingModal` | `meetings.create` |
| EditMeetingModal | `GET /Projekty/EditMeetingModal` | `meetings.edit` |
| SaveMeeting | `POST /Projekty/SaveMeeting` | `meetings.create` / `meetings.edit` (imperativní) |
| DeleteMeeting | `POST /Projekty/DeleteMeeting` | `meetings.edit` |
| JednaniTabPartial | `GET /Projekty/JednaniTabPartial/{id}` | `auth` |

### `JednaniController` (mountnutý na `/Jednani/...`)

Seznam, detail a vnitřní akce jednání:

| Akce | Endpoint | Policy |
|---|---|---|
| Index | `GET /Jednani?projektId=…` | `auth` + `CanAccessProject` filtr |
| Detail | `GET /Jednani/Detail/{id}` | `auth` + `CanAccessProject` |
| TaskItemPartial | `GET /Jednani/TaskItemPartial` | `records.edit` / `records.comment.subsystemlead` |
| AddMeetingParticipantModal | `GET /Jednani/AddMeetingParticipantModal` | `meetings.edit` |
| AddMeetingParticipant | `POST /Jednani/AddMeetingParticipant` | `meetings.edit` (imperativní) |
| SaveStatus | `POST /Jednani/SaveStatus` | `meetings.edit` (imperativní) |
| SaveAttendance | `POST /Jednani/SaveAttendance` | `meetings.edit` (imperativní) |
| SaveNotes | `POST /Jednani/SaveNotes` | `records.edit` (imperativní) |

### Společné body

- **Stejný doménový service** — `IMeetingService` obsluhuje akce obou kontrolérů.
- **Stejné permission keys** — `meetings.create`, `meetings.edit`, `records.edit`, `records.comment.subsystemlead`.
- **Žádný bezpečnostní rozdíl** — autorizace je identická bez ohledu na to, z jakého endpointu se akce spustí.

## Proč je to chyba (i když ne bezpečnostní)

1. **Mentální model vývojáře** — při hledání „kde se ukládá stav jednání" programátor musí zvážit oba kontroléry. Fakt, že `SaveMeeting` je pod `ProjektyController` a `SaveStatus` pod `JednaniController`, není odvoditelný z domény; je potřeba to vědět.
2. **Audit autorizace** — při průchodu kontroléry za účelem ověření pokrytí permission keys (viz [authz-redesign-per-action-keys.md](authz-redesign-per-action-keys.md)) vzniká dojem duplicitního modelu. Ve skutečnosti není, ale audit musí explicitně říct „Jednání = oba kontroléry".
3. **Sémantický mismatch URL** — `POST /Projekty/DeleteMeeting` maže jednání, nikoliv projekt. URL namespace neodpovídá tomu, co endpoint dělá. Stejně tak `GET /Projekty/NewMeetingModal` neotevírá modal projektu, ale modal **jednání**.
4. **Riziko rozvahy při rozšiřování** — když vývojář přidá nový endpoint nad jednáním (např. `Reopen`), musí se rozhodnout, do kterého kontroléru ho dát. Bez jasného pravidla vznikne další ad-hoc rozdělení.
5. **UI routing** — Razor `Url.Action("SaveMeeting", "Projekty")` versus `Url.Action("SaveStatus", "Jednani")` — inkonzistentní z pohledu, kde volat akce nad jednou entitou.

## Návrh opravy

### Cílový stav

**Všechny endpointy týkající se entity Jednání** přesunout do `JednaniController`. `ProjektyController` bude obsluhovat jen projekty, tým a záložkové partialy.

### Cílové rozdělení

`JednaniController` — finální sada:

| Akce (nová) | Endpoint (cíl) | Policy |
|---|---|---|
| Index | `GET /Jednani` | `auth` + `CanAccessProject` filtr |
| Detail | `GET /Jednani/Detail/{id}` | `auth` + `CanAccessProject` |
| **NewMeetingModal** (přesunuto z `Projekty`) | `GET /Jednani/NewMeetingModal?projektId=...` | `meetings.create` |
| **EditMeetingModal** (přesunuto) | `GET /Jednani/EditMeetingModal?projektId=...&meetingId=...` | `meetings.edit` |
| **SaveMeeting** (přesunuto) | `POST /Jednani/Save` | `meetings.create` / `meetings.edit` |
| **DeleteMeeting** (přesunuto) | `POST /Jednani/Delete` | `meetings.edit` |
| TaskItemPartial | `GET /Jednani/TaskItemPartial` | `records.edit` / `records.comment.subsystemlead` |
| AddMeetingParticipantModal | `GET /Jednani/AddMeetingParticipantModal` | `meetings.edit` |
| AddMeetingParticipant | `POST /Jednani/AddMeetingParticipant` | `meetings.edit` |
| SaveStatus | `POST /Jednani/SaveStatus` | `meetings.edit` |
| SaveAttendance | `POST /Jednani/SaveAttendance` | `meetings.edit` |
| SaveNotes | `POST /Jednani/SaveNotes` | `records.edit` |

`ProjektyController` — zůstává:

- `JednaniTabPartial` (jen render — list tab, ne akce) — může zůstat v `ProjektyController`, protože je to tab renderovaný **v kontextu detailu projektu**. Alternativa: přesunout i ho jako `JednaniController.ProjectTabPartial` pro úplnou čistotu; v PR rozhodnout.

### Postup

1. **Přesunout akce** z `ProjektyController.MeetingModals.cs` + `ProjektyController.Commands.cs` (části týkající se meetings) do `JednaniController` / `JednaniController.Commands.cs` / `JednaniController.Modals.cs` (nová partial soubory pro udržení velikosti kontroléru).
2. **Přejmenovat** `SaveMeeting` → `Save`, `DeleteMeeting` → `Delete` (konvence: jméno kontroléru už říká entitu, takže v názvu akce už ji nepotřebujeme duplikovat — konzistentní s `ZaznamyController.Save` / `Delete`).
3. **Aktualizovat URL konstanty / Url.Action volání ve Views** — přetáhnout všechny reference `Url.Action("SaveMeeting", "Projekty")` → `Url.Action("Save", "Jednani")` apod.
4. **Zpětná kompatibilita URL** — zachovat staré URL přes `Route` atributy nebo 301 redirect, **pokud** aplikace běží na offline intranetu a uživatelé mají bookmarky / externí odkazy na `/Projekty/DeleteMeeting` apod. Jinak stačí prostá změna.

### Seznam souborů k úpravě

**Kontrolér — přesun:**

- [PmTracker.Web/Controllers/ProjektyController.MeetingModals.cs](../../PmTracker.Web/Controllers/ProjektyController.MeetingModals.cs) — **smazat** obsah, nahradit v [PmTracker.Web/Controllers/JednaniController.Modals.cs](../../PmTracker.Web/Controllers/JednaniController.Modals.cs) (nový soubor). Akce: `NewMeetingModal`, `EditMeetingModal`.
- [PmTracker.Web/Controllers/ProjektyController.Commands.cs](../../PmTracker.Web/Controllers/ProjektyController.Commands.cs) — z něj **odebrat** meeting-related akce (`SaveMeeting`, `DeleteMeeting`). Přesunout do [PmTracker.Web/Controllers/JednaniController.Commands.cs](../../PmTracker.Web/Controllers/JednaniController.Commands.cs) (nový soubor) a přejmenovat na `Save`, `Delete`.
- [PmTracker.Web/Controllers/JednaniController.cs](../../PmTracker.Web/Controllers/JednaniController.cs) — ponechat, případně rozdělit na partial classes.

**Views — update odkazů:**

Grep `asp-controller="Projekty"` kombinovaný s `asp-action` obsahujícím „Meeting" / „SaveMeeting" / „DeleteMeeting" / „NewMeetingModal" / „EditMeetingModal":

- Kandidáti: `Views/Projekty/_JednaniTab.cshtml`, `Views/Projekty/_ProjectMeetingsTab.cshtml`, modaly pod `Views/Jednani/` i `Views/Projekty/`.
- Stejně pro JS `fetch` / `Url.Action` v `wwwroot/js/modules/meetings/*`.

**Route konstanty (pokud existují):**

- Grep na `"SaveMeeting"`, `"DeleteMeeting"`, `"NewMeetingModal"`, `"EditMeetingModal"` v celém stromě — ideálně nahradit stringové reference za `nameof(JednaniController.Save)` apod. (pokud tyto stringové konstanty nejsou už z MVC generátoru).

**Zpětná kompatibilita URL (pokud požadovaná):**

- V `JednaniController` přidat druhý `[Route("Projekty/SaveMeeting")]` + `[HttpPost]` → jen delegát na nový handler. Nebo `[ActionName("SaveMeeting")]` na druhé akci. Plán rozhodnout při implementaci.
- Alternativa: middleware / `app.MapGet("/Projekty/SaveMeeting", ctx => ctx.Response.Redirect("/Jednani/Save"))` — ale u POSTů se redirect chová jinak, proto raději druhý route attribute.

**Testy:**

- Integrační testy volající `POST /Projekty/SaveMeeting` → aktualizovat na `POST /Jednani/Save`, případně otestovat oba URL vzory.
- `AuthorizationPolicyEnforcementTests` — projet, zda nemá hardcoded endpointy.

## Acceptance kritéria

1. `ProjektyController.*` **neobsahuje** akce `NewMeetingModal`, `EditMeetingModal`, `SaveMeeting`, `DeleteMeeting`. `JednaniController.*` obsahuje jejich ekvivalenty (`NewMeetingModal`, `EditMeetingModal`, `Save`, `Delete`).
2. Všechny Views volají meeting akce přes `JednaniController` — grep `asp-controller="Projekty"` + `asp-action` obsahující „Meeting" nevrací hit.
3. Permission keys zůstávají nezměněné (`meetings.create`, `meetings.edit`, `records.edit`) — toto je jen reorganizace URL namespace, ne authz změna.
4. Existující bookmarky / externí odkazy stále fungují (301 redirect nebo duplicitní route atribut), **pokud** se rozhodne o zpětné kompatibilitě.
5. UI chování beze změny — manuální smoke test: vytvoření, úprava, smazání, změna stavu, úprava účasti, úprava poznámek.
6. Integrační testy volající meeting endpointy projdou.

## Navazující benefity

- **Audit autorizace (authz-redesign)** — sekce „Jednání" v matici se sjednotí do jedné místo aktuálního rozdělení „Jednání – v projektu" vs „Jednání – globální".
- **Rozšiřitelnost** — další meeting akce (Reopen, Close, Archive, Export, ...) mají jasný domov bez rozhodování „kam to dát".
- **Konzistentní URL namespace** — každá entita má svůj kontrolér a URL prefix (`/Projekty`, `/Zaznamy`, `/Jednani`, `/Osoby`, ...).

## Poznámka k prioritě

Tato úloha **není blokér** pro žádnou jinou opravu ani bezpečnostní problém. Je to **cleanup** a dává smysl ji udělat:

- Buď před velkým redesignem autorizace ([authz-redesign-per-action-keys.md](authz-redesign-per-action-keys.md)), aby authz matice byla jednoduchá.
- Nebo po něm, až bude authz stabilizované.

Pokud se `authz-redesign` dělá, je užitečné tuto úlohu **zařadit dovnitř** (společný PR se přesunutím endpointů a přepisem permission modelu) — revize proběhne najednou, méně přepracovávání odkazů ve Views.

---

# Úloha: Smazat legacy ObsazeniController a vyčistit reference

## Kontext
`PmTracker.Web/Controllers/ObsazeniController.cs` je legacy redirect — jediná akce
`Index()` přesměruje na `Projekty.Index`. Žádná View složka neexistuje, žádný
asp-controller="Obsazeni" odkaz v aktivních Views nenalezen. V UI už se na něj
nikde neodkazuje — je to dead code, zachování kvůli backward-compat URL
(staré bookmarky). Business potřebu zachovat redirect jsme ověřili — **neexistuje**.

Pozor: tabulky `obsazeni_projektu` a `obsazeni_subsystemu_projektu` jsou úplně
jiná věc (doménový koncept "obsazení týmu projektu"), NEMAZAT je, NESAHAT na
ně v kódu, NESAHAT v docs/authorization.md.

## Co smazat

1. **Controller** — celý soubor:
   - `PmTracker.Web/Controllers/ObsazeniController.cs`

2. **Integrační testy** — celý soubor (testuje jen existenci redirect routes):
   - `PmTracker.Tests.Api/Controllers/HomeObsazeniControllerTests.cs`
   
   Důvod: test pokrývá routy `/Obsazeni`, `/Obsazeni/Index`, `/Obsazeni?projektId=…`
   které po smazání vrátí 404. HomeController testy si tam ponech, pokud jsou
   oddělené — ale v tomhle souboru jsou spojené, takže ověř, jestli neobsahuje
   testy pro HomeController, které bychom si měli zachovat. Pokud ano, zachovej je
   a smaž jen sekci `ObsazeniRoutes_*`.

3. **LeafControllerAuthorizeTests** — odebrat jeden řádek:
   - `PmTracker.Tests.Unit/Authorization/LeafControllerAuthorizeTests.cs`
   - řádek: `[InlineData("ObsazeniController.cs")]`

## Co aktualizovat (nikoli smazat)

1. **Dokumentační strom:**
   - `docs/technical/00-documentation-tree.md` — odstranit řádek 
     `| ObsazeniController | N2.9.1 |` (nebo celou jeho sekci, podle formátu).

2. **Redesign dokument:**
   - `docs/known-issues/authz-redesign-per-action-keys.md` — na řádku 479 v sekci
     "ObsazeniController.cs / DashboardController.cs / ostatní read-only"
     odebrat zmínku `ObsazeniController.cs`. Zbytek věty nech.

## Co NESAHAT

- `docs/authorization.md` — zmínka `obsazeni_projektu` je o DB tabulce, ne o controlleru.
- `docs/superpowers/plans/**` — historické plány, nech be
ze změny (referenční záznam).
- `CODEX_REFACTOR_WORKLOG.md` — historický záznam.
- Tabulky `obsazeni_projektu` / `obsazeni_subsystemu_projektu` v DB i kódu — NEMAZAT.
  Jsou součástí doménového modelu (přiřazení osob do projektových rolí).

## Ověření před commitem

Spusť tyto příkazy a ujisti se, že nevrací nic kritického:

```bash
# Nesmí vracet žádný hit v aktivním kódu (jen historické docs):
grep -rn --include='*.cs' --include='*.cshtml' 'ObsazeniController' PmTracker.Web PmTracker.Tests.Api PmTracker.Tests.Unit

# Build + testy musí projít:
dotnet build
dotnet test
```

## Commit

Jeden commit, zpráva:

```
chore(cleanup): smazat legacy ObsazeniController redirect

ObsazeniController.Index() byl backward-compat redirect na Projekty.Index.
V UI už se na něj nikde neodkazuje, žádná View složka neexistuje.
Smazán controller + dedikovaný integrační test + reference v authz
redesign dokumentu a tech doc stromu.

DB tabulky obsazeni_projektu a obsazeni_subsystemu_projektu zůstávají
beze změny — jsou součástí doménového modelu (přiřazení týmu projektu),
nemají s tímto controllerem nic společného.
```

## Rizika

Pokud má někdo externí bookmark `/Obsazeni`, po nasazení dostane 404. Pokud je to
blokující, zachovat redirect přes middleware v Program.cs (5 řádků) místo
controlleru — ale to je dodatečná práce, ne mazání. Rozhodnutí business:
zatím nikdo o bookmark nehlásil, jdeme na čisté smazání.
