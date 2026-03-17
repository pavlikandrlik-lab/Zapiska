# PM Tracker - Refactor & Optimization Checklist

Datum: 2026-03-16

## Cíl
- Zachovat 100 % funkčního chování aplikace.
- Snižovat redundanci (DRY), zlepšovat strukturu (OOP/SoC) a postupně omezovat technický dluh.

## Aktuální hotspoty (objem/struktura)
- `PmTracker.Web/Services/Data/SqlServerDataStore.cs`: interně stále obsahuje více nezávislých use-case oblastí v jedné třídě.
- `PmTracker.Web/Services/Export/OpenXmlWordRecordsSectionWriter.cs`: orchestrace je oddělena, ale exportní vrstva má stále prostor pro další jemné oddělení projekcí.
- `PmTracker.Web/Modules/Export/ExportTemplateQueries.cs` (~425 řádků): orchestruje více projection pravidel.
- `PmTracker.Web/Controllers/ProjektyController.cs` (~634 řádků): vysoká orchestrace.
- `PmTracker.Web/Controllers/ZaznamyController.cs` (~549 řádků): UI flow větvení.

## Stav refaktor backlogu
- [x] R1: Team-management command flow v `ProjektyController` sjednocen.
- [x] R2: Settings modal konstrukce přesunuta do `ISettingsModalModelFactory`.
- [x] R3: Comment/redirect UI flow v `ZaznamyController` přesunut do `IRecordUiFlowResolver`.
- [~] R4: DataStore boundary split po modulech.
- Hotovo: `IRecordsDataStore`, `IMeetingsDataStore`, `IProjectsDataStore`, `IExportDataStore`, `ISettingsDataStore` + adaptéry; modulové služby/handlery těchto modulů už nevolají přímo `IPmTrackerDataStore`.
- Hotovo navíc: `IRecordCommentCommandsUseCase` + `RecordCommentCommandsUseCase` (comment command slice) a `IProfilePageQueriesUseCase` + `ProfilePageQueriesUseCase` (profilový query slice).
- Hotovo navíc: `IMeetingListQueriesUseCase` + `MeetingListQueriesUseCase` (meeting overview/list query slice).
- Hotovo navíc: `IMeetingDetailQueriesUseCase` + `MeetingDetailQueriesUseCase` (meeting detail query slice) a `IMeetingWriteCommandsUseCase` + `MeetingWriteCommandsUseCase` (SaveMeeting/DeleteMeeting/SaveAttendance/AddMeetingParticipant).
- Hotovo navíc: `IProjectListQueriesUseCase` + `ProjectListQueriesUseCase` a `IProjectCommandsUseCase` + `ProjectCommandsUseCase` (BuildProjektyList + SaveProject/SoftDeleteProject).
- Hotovo navíc: `IProjectDetailQueriesUseCase` + `ProjectDetailQueriesUseCase` (orchestrace `BuildProjektDetail` přesunuta mimo `SqlServerDataStore` přes `IProjectDetailComposition`).
- Hotovo navíc: meeting command boundary doplněna o `SaveMeetingStatus` + `SaveMeetingNote` do `IMeetingWriteCommandsUseCase`.
- Hotovo navíc: person read-slice `BuildOsoby` přesunut do `IPeoplePageQueriesUseCase` / `PeoplePageQueriesUseCase`.
- Zbývá: pokračovat ve stejném patternu pro další velké oblasti uvnitř `SqlServerDataStore` (hlavně vnitřní projekční/record helpery použité v `IProjectDetailComposition`, records a dictionary/person command/query bloky).
- [~] R5: Rozpad export logiky.
- Hotovo v `ExportTemplateQueries`: vytaženy komponenty `IExportCommentProjectionBuilder`, `IExportRoleProjectionBuilder`, `IExportAttendanceProjectionBuilder`, `IExportRecordVisibilityEvaluator`.
- Hotovo navíc v query vrstvě: `IExportRecordProjectionBuilder` + `ExportRecordProjectionBuilder` (core projekce záznamů přesunuta mimo orchestrátor).
- Hotovo navíc: `IExportTemplateSummaryBuilder` + `ExportTemplateSummaryBuilder` (summary/rules metadata mimo `ExportTemplateQueries`).
- Hotovo v Word exportu: `OpenXmlWordExportService` je orchestrace; sekce jsou ve `OpenXmlWordHeaderSectionWriter` a `OpenXmlWordRecordsSectionWriter`.
- Hotovo navíc v records sekci: samostatné writery `OpenXmlWordRecordHeaderWriter`, `OpenXmlWordRecordCommentsCellWriter`, `OpenXmlWordRecordPeopleCellWriter`, `OpenXmlWordRecordDeadlinesCellWriter`, `OpenXmlWordRichHtmlParagraphWriter`.
- Zbývá: případná další granularizace/export pravidel tam, kde ještě zůstávají složitější query orchestrace.
- [x] R6: Sjednocené fallback validační texty přes `BaseController.InvalidFormFallbackMessage`.
- [~] Legacy controller dependency přes `BaseController`.
- Hotovo: `BaseController` už není závislý na `IPmTrackerDataStore`; controllery už datastore nedědí přes base.
- Hotovo navíc: `ProfilController` přepojen na `IProfileService`/`IProfileDataStore` místo přímé závislosti na datastore.
- Hotovo navíc: `PeopleService` a `DictionariesService` delegují přes `IPeopleDataStore` a `IDictionariesDataStore`.
- Zbývá: sjednotit stejný přístup v dalších službách, kde ještě existuje přímý bridge na monolit.
- [x] Hygiena repa: odstraněny dřívější dočasné architektonické artefakty mimo produkční dokumentaci.

## Provedeno v této iteraci
- [x] R4 records write slice: `SaveRecord` + `DeleteRecord` + `AssignMeetingIdentifier` přesunuty do `IRecordWriteCommandsUseCase` / `RecordWriteCommandsUseCase` (včetně validační/schedule/write helper logiky).
- [x] R4 projects assignment command slice: `AssignProjectRole` / `DeactivateProjectRole` / `AssignProjectSubsystem` / `DeactivateProjectSubsystem` / `AssignProjectSubsystemRole` / `DeactivateProjectSubsystemRole` přesunuty do `IProjectAssignmentCommandsUseCase` / `ProjectAssignmentCommandsUseCase`.
- [x] R4 dictionary command slice: `SaveCiselnikRow` + `DeleteCiselnikRow` + lock pravidla + handler mapy přesunuty do `IDictionariesCommandsUseCase` / `DictionariesCommandsUseCase` (harmonogram step write/delete zůstává přes composition boundary).
- [x] `SqlServerDataStore` cleanup: odstraněny původní record validation/schedule/save helpery a dictionary row upsert/delete helpery; soubor zmenšen přibližně z ~4611 na ~2606 řádků.
- [x] DI rozšířena o nové use-case komponenty (`IRecordWriteCommandsUseCase`, `IProjectAssignmentCommandsUseCase`, `IDictionariesCommandsUseCase`).
- [x] `IntegrationTestHelper` aktualizován pro nový konstruktor `SqlServerDataStore`.
- [x] R4 projects detail slice: `BuildProjektDetail` přesunut do `IProjectDetailQueriesUseCase` / `ProjectDetailQueriesUseCase`; `SqlServerDataStore` deleguje.
- [x] R4 meetings commands slice dokončen: `SaveMeetingStatus` + `SaveMeetingNote` přesunuty do `IMeetingWriteCommandsUseCase` / `MeetingWriteCommandsUseCase`.
- [x] R4 person read-slice: `BuildOsoby` přesunut do `IPeoplePageQueriesUseCase` / `PeoplePageQueriesUseCase`.
- [x] R4 dictionaries query slice: `BuildCiselnikyDashboard` + `BuildCiselnikDetail` přesunuty do `IDictionariesQueriesUseCase` / `DictionariesQueriesUseCase`; `SqlServerDataStore` deleguje přes `IDictionariesQueriesComposition`.
- [x] R4 people command slice: `SaveManualPerson` + `SaveAdPerson` + `DeletePerson` přesunuty do `IPersonCommandsUseCase` / `PersonCommandsUseCase`.
- [x] R4 records query/edit slice: `BuildZaznamEdit` + `BuildZaznamCreate` + `BuildDeleteRecordModal` přesunuty do `IRecordEditorQueriesUseCase` / `RecordEditorQueriesUseCase`; `SqlServerDataStore` deleguje přes `IRecordEditorQueriesComposition`.
- [x] Monolit cleanup: ze `SqlServerDataStore` odstraněny původní dictionary/person implementace a jejich pomocné metody; soubor zmenšen na ~4696 řádků.
- [x] Monolit cleanup pokračování: `SqlServerDataStore` dále zmenšen na ~4611 řádků po přesunu records query/edit orchestrace.
- [x] DI/kompozice doplněna pro nové use-case komponenty (`IProjectDetailQueriesUseCase`, `IPeoplePageQueriesUseCase`), včetně `IntegrationTestHelper`.
- [x] DI/kompozice rozšířena o `IDictionariesQueriesUseCase` + `IPersonCommandsUseCase` (včetně `IntegrationTestHelper` a `PmTrackerModuleServiceCollectionExtensionsTests`).
- [x] DI/kompozice rozšířena o `IRecordEditorQueriesUseCase` (včetně `IntegrationTestHelper` a `PmTrackerModuleServiceCollectionExtensionsTests`).
- [x] Meetings unit delegace rozšířena o testy `SaveMeetingStatus` + `SaveMeetingNote`.
- [x] R4 meeting detail/query slice: `BuildJednaniDetail` přesunut do `IMeetingDetailQueriesUseCase` / `MeetingDetailQueriesUseCase`; `SqlServerDataStore` teď deleguje.
- [x] R4 meeting write-flow slice: `SaveMeeting`, `DeleteMeeting`, `SaveAttendance`, `AddMeetingParticipant` přesunuty do `IMeetingWriteCommandsUseCase` / `MeetingWriteCommandsUseCase`.
- [x] R4 projects slice (část): `BuildProjektyList` přesunut do `IProjectListQueriesUseCase` / `ProjectListQueriesUseCase`, `SaveProject` + `SoftDeleteProject` do `IProjectCommandsUseCase` / `ProjectCommandsUseCase`.
- [x] R5 export summary/rules slice: `ExportTemplateQueries` už nesestavuje summary/rules inline, používá `IExportTemplateSummaryBuilder`.
- [x] Monolit cleanup: z `SqlServerDataStore` odstraněny přesunuté meeting/project metody + interní helpery, které už nebyly potřeba.
- [x] DI/kompozice doplněna pro nové use-case komponenty včetně `IntegrationTestHelper`.
- [x] Nové cílené unit testy:
  - `ExportTemplateSummaryBuilderTests`
- [x] R4 meeting read-slice: `BuildJednaniOverview` + `BuildJednaniList` přesunuty do `MeetingListQueriesUseCase`; `SqlServerDataStore` teď jen deleguje.
- [x] R5 query-slice: `BuildExportRecords` přesunut z `ExportTemplateQueries` do `ExportRecordProjectionBuilder`; `ExportTemplateQueries` je orchestrace variant.
- [x] Controller/service decoupling: zavedeny boundary `IPeopleDataStore`/`PeopleDataStore` a `IDictionariesDataStore`/`DictionariesDataStore`; služby už nepoužívají přímo `IPmTrackerDataStore`.
- [x] Export Word records slice rozdělen na menší komponenty:
  - `IWordExportRecordCommentsCellWriter` / `OpenXmlWordRecordCommentsCellWriter`
  - `IWordExportRecordHeaderWriter` / `OpenXmlWordRecordHeaderWriter`
  - `IWordExportRecordPeopleCellWriter` / `OpenXmlWordRecordPeopleCellWriter`
  - `IWordExportRecordDeadlinesCellWriter` / `OpenXmlWordRecordDeadlinesCellWriter`
  - `IWordExportRichHtmlParagraphWriter` / `OpenXmlWordRichHtmlParagraphWriter`
- [x] `OpenXmlWordRecordsSectionWriter` je nyní orchestrace tabulky (bez detailního render kódu).
- [x] `BaseController` odpojen od `IPmTrackerDataStore`; konstruktor controllerů zjednodušen.
- [x] Přidána profilová služba a boundary:
  - `IProfileService` / `ProfileService`
  - `IProfileDataStore` / `ProfileDataStore`
- [x] `SqlServerDataStore` dále zmenšen přes `IProfilePageQueriesUseCase` / `ProfilePageQueriesUseCase` (profilová query logika přesunutá mimo monolit).
- [x] DI registrace doplněny pro nové komponenty.
- [x] Cílené testy doplněny/aktualizovány:
  - unit: `ProfileServiceTests`, `OpenXmlWordExportServiceTests`, `PmTrackerModuleServiceCollectionExtensionsTests`
  - unit: aktualizován `ProjektyControllerBehaviorTests` kvůli novému `BaseController` konstruktoru
- [x] Nové cílené unit testy pro service boundary:
  - `PeopleServiceTests`
  - `DictionariesServiceTests`
- [x] Meetings modul: zaveden `IMeetingsDataStore` + `MeetingsDataStore`, všechny query/command handlery přepojeny na modulový boundary.
- [x] Projects modul: zaveden `IProjectsDataStore` + `ProjectsDataStore`, všechny query/command handlery přepojeny na modulový boundary.
- [x] Export modul: zaveden `IExportDataStore` + `ExportDataStore`, handlery přepojeny.
- [x] `ExportTemplateQueries`: role/attendance/record-visibility pravidla přesunuta do samostatných komponent.
- [x] `OpenXmlWordExportService`: rozpad na orchestration + writer komponenty:
  - `IWordExportHeaderSectionWriter` / `OpenXmlWordHeaderSectionWriter`
  - `IWordExportRecordsSectionWriter` / `OpenXmlWordRecordsSectionWriter`
  - sdílené primitives v `OpenXmlWordElements`
- [x] Settings modul: zaveden `ISettingsDataStore` + `SettingsDataStore`; `SettingsService` deleguje přes modulový boundary místo přímých `ISettingsAuthz*` závislostí.
- [x] DI registrace doplněny v `ExportModuleServiceCollectionExtensions`.
- [x] Testy upraveny na nové boundary:
  - integration: `MeetingsModuleTests`, `ProjectsModuleTests`
  - unit: `OpenXmlWordExportServiceTests`, `PmTrackerModuleServiceCollectionExtensionsTests`
- [x] Nové cílené ověření settings slice:
  - integration: `SettingsAuthzModuleTests` (včetně průchodu přes `SettingsService` + `SettingsDataStore`)
  - api: `SettingsAuthzAdminControllerTests`
  - unit: `DeleteRolePermissionCommandTests` + DI registrace v `PmTrackerModuleServiceCollectionExtensionsTests`

## Ověření
- `dotnet build PmTracker.sln --nologo` -> OK
- `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~PmTrackerModuleServiceCollectionExtensionsTests|FullyQualifiedName~ProjectsCommandsDelegationTests|FullyQualifiedName~RecordsServiceDelegationTests" --nologo` -> 7/7 OK
- `dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj --filter "FullyQualifiedName~RecordSaveDataStoreTests|FullyQualifiedName~RecordDeleteDataStoreTests|FullyQualifiedName~ScheduleAddAuthorizationDataStoreTests|FullyQualifiedName~CiselnikDataStoreSecurityTests|FullyQualifiedName~ProjectsModuleTests" --nologo` -> 28/28 OK
- `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~RecordEditorControllerTests|FullyQualifiedName~CiselnikyControllerTests|FullyQualifiedName~ProjectsCommandsControllerTests" --nologo` -> 54/54 OK
- `dotnet build PmTracker.sln --nologo` -> OK
- `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~MeetingsCommandsDelegationTests|FullyQualifiedName~MeetingsQueriesDelegationTests|FullyQualifiedName~PmTrackerModuleServiceCollectionExtensionsTests|FullyQualifiedName~ProjectsQueriesDelegationTests|FullyQualifiedName~ProjectsCommandsDelegationTests" --nologo` -> 15/15 OK
- `dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj --filter "FullyQualifiedName~MeetingsModuleTests|FullyQualifiedName~ProjectsModuleTests|FullyQualifiedName~ExportTemplateUseCaseTests|FullyQualifiedName~SettingsAuthzModuleTests" --nologo` -> 8/8 OK
- `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~JednaniControllerTests|FullyQualifiedName~ProjectsCommandsControllerTests|FullyQualifiedName~ProjectsModalsControllerTests|FullyQualifiedName~ExportControllerTests" --nologo` -> 79/79 OK
- `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --nologo` -> 126/126 OK
- `dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj --nologo` -> 67/67 OK
- `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --nologo` -> 252/252 OK
- `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~MeetingsQueriesDelegationTests|FullyQualifiedName~MeetingsCommandsDelegationTests|FullyQualifiedName~PmTrackerModuleServiceCollectionExtensionsTests|FullyQualifiedName~ExportQueriesDelegationTests|FullyQualifiedName~ExportCommentProjectionBuilderTests|FullyQualifiedName~ExportTemplateSummaryBuilderTests" --nologo` -> 13/13 OK
- `dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj --filter "FullyQualifiedName~MeetingsModuleTests|FullyQualifiedName~ProjectsModuleTests|FullyQualifiedName~ExportTemplateUseCaseTests" --nologo` -> 5/5 OK
- `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~JednaniControllerTests|FullyQualifiedName~ProjectsCommandsControllerTests|FullyQualifiedName~ExportControllerTests" --nologo` -> 59/59 OK
- `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~MeetingsQueriesDelegationTests|FullyQualifiedName~MeetingsCommandsDelegationTests|FullyQualifiedName~ExportQueriesDelegationTests|FullyQualifiedName~ExportCommentProjectionBuilderTests|FullyQualifiedName~PmTrackerModuleServiceCollectionExtensionsTests|FullyQualifiedName~PeopleServiceTests|FullyQualifiedName~DictionariesServiceTests" --nologo` -> 12/12 OK
- `dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj --filter "FullyQualifiedName~MeetingsModuleTests|FullyQualifiedName~ProjectsModuleTests|FullyQualifiedName~ExportTemplateUseCaseTests|FullyQualifiedName~SettingsAuthzModuleTests" --nologo` -> 8/8 OK
- `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~ExportControllerTests|FullyQualifiedName~SettingsAuthzAdminControllerTests|FullyQualifiedName~AjaxControllersTests" --nologo` -> 65/65 OK
- `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~OpenXmlWordExportServiceTests|FullyQualifiedName~PmTrackerModuleServiceCollectionExtensionsTests|FullyQualifiedName~ProjektyControllerBehaviorTests|FullyQualifiedName~ProfileServiceTests" --nologo` -> 7/7 OK
- `dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj --filter "FullyQualifiedName~MeetingsModuleTests|FullyQualifiedName~SettingsAuthzModuleTests" --nologo` -> 5/5 OK
- `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~ExportControllerTests|FullyQualifiedName~SettingsAuthzAdminControllerTests" --nologo` -> 56/56 OK
- `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --nologo` -> 120/120 OK
- `dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj --filter "FullyQualifiedName~MeetingsModuleTests|FullyQualifiedName~ProjectsModuleTests|FullyQualifiedName~ExportTemplateUseCaseTests" --nologo` -> 5/5 OK
- `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~ExportControllerTests" --nologo` -> 12/12 OK
- `dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj --filter "FullyQualifiedName~SettingsAuthzModuleTests" --nologo` -> 3/3 OK
- `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~SettingsAuthzAdminControllerTests" --nologo` -> 44/44 OK
- `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~PmTrackerModuleServiceCollectionExtensionsTests|FullyQualifiedName~PeopleServiceTests|FullyQualifiedName~DictionariesServiceTests" --nologo` -> 3/3 OK
- `dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj --filter "FullyQualifiedName~CiselnikDataStoreSecurityTests|FullyQualifiedName~ProjectsModuleTests" --nologo` -> 12/12 OK
- `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~PeopleControllerTests|FullyQualifiedName~CiselnikyControllerTests" --nologo` -> 42/42 OK
- `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~PmTrackerModuleServiceCollectionExtensionsTests|FullyQualifiedName~RecordsServiceDelegationTests" --nologo` -> 3/3 OK
- `dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj --filter "FullyQualifiedName~RecordSaveDataStoreTests|FullyQualifiedName~RecordDeleteDataStoreTests|FullyQualifiedName~ScheduleAddAuthorizationDataStoreTests" --nologo` -> 16/16 OK
- `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~RecordEditorControllerTests" --nologo` -> 16/16 OK

## Další kroky (doporučené pořadí)
1. Dokončit rozpad vnitřku `SqlServerDataStore` pro harmonogram schema/clone část (`CloneActiveHarmonogramSchema`, `NormalizeHarmonogramSchemaRows`, `EnsurePersistedActiveHarmonogramSchemaVersion`) do samostatné komponenty, aby command/query vrstva sdílela jedno boundary místo přímých interních helperů.
2. Pokračovat v redukci zbývajících monolitických query helperů kolem project/record kompozice (`BuildRecordCardsForProject`, role-history/projection bloky) do menších query use-case tříd.
3. Dovést decoupling service vrstvy do konce: zkontrolovat zbývající služby/controllers, že interně už nesahají na monolitické helpery mimo modulové boundary.
4. Dokončit jemný rozpad `ExportTemplateQueries` tam, kde ještě zůstává vyšší koncentrace projekční logiky.
5. Po každém dalším slice držet stejný režim ověření: targeted unit + integration + API + průběžná aktualizace checklistu a repo hygiena.
