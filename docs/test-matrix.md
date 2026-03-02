# Zápiska Test Matrix

## Obsah
1. [Security a oprávnění](#security-a-opravneni)
2. [Nastavení (authz)](#nastaveni-authz)
3. [Jednání a vyjádření](#jednani-a-vyjadreni)
4. [Filtry projektových záznamů](#filtry-projektovych-zaznamu)
5. [UI/E2E smoke](#uie2e-smoke)

## Security a oprávnění
| Oblast | Scénář | Vrstva | Soubor |
|---|---|---|---|
| Permission key registry | `IsSupported` známé/neznámé klíče | Unit | `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Tests.Unit/Security/PermissionKeysTests.cs` |
| User grants | `HasPermission` pro `GLOBAL/ALL/INCLUDE` a project null | Unit | `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Tests.Unit/Security/CurrentUserContextPermissionTests.cs` |

## Nastavení (authz)
| Oblast | Scénář | Vrstva | Soubor |
|---|---|---|---|
| SaveAuthzPermission | valid save/update | Integration | `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Tests.Integration/DataStore/AuthzPermissionDataStoreTests.cs` |
| SaveAuthzPermission | unsupported key | Integration | `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Tests.Integration/DataStore/AuthzPermissionDataStoreTests.cs` |
| SaveAuthzPermission | duplicate key | Integration | `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Tests.Integration/DataStore/AuthzPermissionDataStoreTests.cs` |
| SaveAuthzPermission | unknown category | Integration | `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Tests.Integration/DataStore/AuthzPermissionDataStoreTests.cs` |
| SaveRolePermission | INCLUDE s neexistujícím projektem | Integration | `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Tests.Integration/DataStore/RolePermissionDataStoreTests.cs` |
| SaveRolePermission | INCLUDE s validními projekty | Integration | `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Tests.Integration/DataStore/RolePermissionDataStoreTests.cs` |
| NastaveniController | AJAX error pro unsupported key | API | `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Tests.Api/Controllers/AjaxControllersTests.cs` |

## Jednání a vyjádření
| Oblast | Scénář | Vrstva | Soubor |
|---|---|---|---|
| DeleteMeeting | cascade delete `ucast + vyjadreni` + audit | Integration | `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Tests.Integration/DataStore/MeetingAndCommentDataStoreTests.cs` |
| Comment permissions | `records.comment.subsystemlead` pravidla | Integration | `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Tests.Integration/DataStore/MeetingAndCommentDataStoreTests.cs` |
| Locked meeting | comment delete blocked in readonly meeting | Integration | `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Tests.Integration/DataStore/MeetingAndCommentDataStoreTests.cs` |
| ProjektyController.SaveMeeting | AJAX field errors for invalid `CasZacatek` | API | `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Tests.Api/Controllers/AjaxControllersTests.cs` |
| ProjektyController.DeleteMeeting | AJAX success + DB delete | API | `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Tests.Api/Controllers/AjaxControllersTests.cs` |
| ZaznamyController.AddComment | AJAX success scope `record-card` | API | `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Tests.Api/Controllers/AjaxControllersTests.cs` |

## Filtry projektových záznamů
| Oblast | Scénář | Vrstva | Soubor |
|---|---|---|---|
| Kategorie strict | `INFO` vs `ROZHODNUTI` data odděleně | Integration | `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Tests.Integration/DataStore/RecordFilterDataStoreTests.cs` |
| Jednání-vyjádření | stavy vyjádření navázány na konkrétní `stav_jednani_id` | Integration | `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Tests.Integration/DataStore/RecordFilterDataStoreTests.cs` |

## UI/E2E smoke
| Oblast | Scénář | Vrstva | Soubor |
|---|---|---|---|
| Nastavení | otevření modalu `Nová akce` | E2E | `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Tests.E2E/Scenarios/SmokeScenariosTests.cs` |
| Projekty/Jednání | otevření modalu `Nová porada` | E2E | `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Tests.E2E/Scenarios/SmokeScenariosTests.cs` |
