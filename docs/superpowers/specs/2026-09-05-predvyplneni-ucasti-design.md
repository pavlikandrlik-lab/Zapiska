# Předvyplnění účasti u nového jednání — design

**Datum:** 2026-09-05
**Stav:** návrh schválen uživatelem, připraven k implementaci
**Podnět:** hlášení testera — „Tisk osob na jednání: nutno u všech vyplnit stav, i u těch, co běžně nechodí"

## 1. Cíl

Dvě věci, které spolu souvisejí přes číselník stavů účasti:

1. **Předvyplnit účast u nově založeného jednání** podle toho, jak se každá osoba účastnila v minulosti — místo dnešního „všem Přítomen". Obsluha pak dovyplňuje jen výjimky.
2. **Doplnit stav Videokonference do seedu** a sjednotit jeho název napříč seedy.

## 2. Východisko

### 2.1 Předvyplnění

Docházku nového jednání zakládá `CreateMeetingAttendanceSnapshotAsync` (`MeetingService.WriteCommands.cs:345`), volaná jednou při vytvoření jednání (`:69`). Všem účastníkům nastaví **jeden společný stav** — `PRESENT`, případně první dostupný, když `PRESENT` v číselníku není (`ResolveDefaultAttendanceStatusIdAsync`, `:320`).

Účastníci = aktivní projektové role kromě hosta + aktivní role subsystémů projektu (`BuildDefaultAttendanceParticipantIdsAsync`, `:389`).

Proto musí obsluha ručně přepsat stav i u lidí, kteří na jednání nikdy nechodí.

### 2.2 Stav Videokonference

| Seed | Co obsahuje |
|---|---|
| `PMTracker_insert_sql` (produkční baseline, ř. 771-774) | `PRESENT`/Přítomen, **`ONLINE`/Online**, `EXCUSED`/Omluven, `ABSENT`/Nepřítomen |
| `db_seed_dev_admin.sql` (ř. 112-117) | jen `PRESENT`, `EXCUSED`, `ABSENT` — **`ONLINE` chybí** |

Překlep „VIdeokonference" **není nikde v repozitáři** (ověřeno hledáním bez ohledu na velikost písmen napříč všemi soubory). Vznikl přejmenováním přímo v databázi. Opravou v databázi je vyřízený; ve zdrojích není co opravovat.

Skutečná vada je jinde: **vývojový seed ten stav vůbec nezakládá**, takže se chování s videokonferencí lokálně ani v testech nikdy neprojde. Právě proto si toho nikdo nevšiml dřív.

Kód `ONLINE` aplikace už zná — `AttendancePrintOrder` ho v tisku řadí hned za Přítomen (`ExportProjectionBuilders.cs:557`).

## 3. Rozhodnutí uživatele (diskuse 2026-09-05)

| # | Rozhodnutí |
|---|---|
| U1 | Stav `ONLINE` se **všude** jmenuje **Videokonference** — v produkčním baseline i ve vývojovém seedu. Pin v testu se srovná. |
| U2 | Odhad se počítá z **maximálně 15 posledních jednání**; je-li jich méně, počítá se z toho, co je. |
| U3 | Do odhadu se počítají **jen uzavřená jednání** (`CLOSED`). |
| U4 | Vybere se **nejčetnější** stav účasti dané osoby. |
| U5 | Odhad platí **jen pro nově zakládané jednání**; existující jednání se nemění. |

K U3: jednání v přípravě i otevřené k zápisu mohou mít účast jen předvyplněnou tímto odhadem nebo rozepsanou. Počítat je by znamenalo, že si odhad potvrzuje sám sebe.

## 4. Pravidla odhadu

Pro každého účastníka nového jednání zvlášť:

```
1. Vezmi posledních max 15 UZAVŘENÝCH jednání téhož projektu.
2. Z nich vyber záznamy účasti této osoby.
3. Žádné záznamy → PRESENT (dnešní chování).
4. Jinak → stav s nejvyšším počtem výskytů.
5. Shoda počtů → vyhrává stav z nejnovějšího jednání mezi shodnými.
```

### 4.1 Pořadí jednání

„Posledních 15" se určuje podle **čísla jednání sestupně**, ne podle data. Je to stejné pravidlo, jakým už aplikace určuje předchozí jednání v tisku (`ExportProjectionBuilders.cs:798`); dvě různá pojetí „předchozího" by mátla.

### 4.2 Rozhodnutí shody

Pravidlo 5 je nutné, aby byl výsledek určitý. Bez něj by při poměru 5:5 rozhodovalo pořadí řádků z databáze a stejné zadání by mohlo dát pokaždé jiný výsledek. „Co dělal naposledy" je zároveň nejbližší tomu, co by člověk čekal.

### 4.3 Osoby bez historie

Nový člen týmu nemá v uzavřených jednáních žádný záznam a dostane `PRESENT` — přesně jako dnes. Odhad tedy nikdy nezhorší dnešní stav, jen ho zpřesní tam, kde je z čeho.

### 4.4 Hranice

Osoba mohla mít v minulosti účast u jednání, kde dnes v týmu není, i naopak. Počítá se **výhradně z jejích záznamů účasti**, ne z členství — kdo se historicky neúčastnil, prostě má méně datových bodů.

## 5. Architektura

```
JednaniController → MeetingService.CreateMeetingAsync
                        │
                        └─ CreateMeetingAttendanceSnapshotAsync
                               │  účastníci (beze změny)
                               │
                               ├─ IAttendancePredictor.PredictAsync(projectId, osobaIds, ct)
                               │      └─ posledních 15 uzavřených jednání → nejčetnější stav
                               │
                               └─ UcastEntity se stavem z odhadu,
                                  nebo výchozím, když odhad nic nevrátí
```

Odhad je **samostatná služba**, ne kód uvnitř zápisové metody. Důvod: jde o čistě rozhodovací logiku nad daty, kterou je potřeba otestovat na hraničních případech (shoda počtů, prázdná historie, méně než 15 jednání). Uvnitř `CreateMeetingAttendanceSnapshotAsync` by šla testovat jen přes zakládání jednání.

### 5.1 Soubory

| Akce | Soubor | Odpovědnost |
|---|---|---|
| Vytvořit | `Services/Meetings/IAttendancePredictor.cs` | rozhraní + výsledný typ |
| Vytvořit | `Services/Meetings/AttendancePredictor.cs` | dotaz do historie + volba nejčetnějšího stavu |
| Změnit | `Services/MeetingService.WriteCommands.cs:345-387` | použití odhadu při zakládání docházky |
| Změnit | `Services/Data/DataStoreServiceCollectionExtensions.cs` | registrace služby |
| Změnit | `db_seed_dev_admin.sql:112-117` | doplnit `ONLINE` / Videokonference |
| Změnit | `PMTracker_insert_sql:772` | přejmenovat `Online` → `Videokonference` |
| Změnit | `PmTracker.Tests.Unit/Common/SeedBaselineDocumentationTests.cs:71` | srovnat pin s novým názvem |

### 5.2 Kontrakt

```csharp
public interface IAttendancePredictor
{
    /// <summary>
    /// Pro každou osobu vrátí id nejčetnějšího stavu účasti z posledních uzavřených
    /// jednání projektu. Osoby bez historie ve výsledku nejsou.
    /// </summary>
    Task<IReadOnlyDictionary<int, int>> PredictAsync(
        int projectId,
        IReadOnlyCollection<int> osobaIds,
        CancellationToken ct = default);
}
```

Vrací **slovník osoba → id stavu**, ne stav pro jednu osobu. Historie se tak načte jedním dotazem pro celý tým místo dotazu na každého člověka.

Osoba bez historie ve slovníku chybí — volající pro ni použije výchozí stav. Kontrakt tím nemusí řešit „žádný odhad" zvláštní hodnotou.

### 5.3 Konstanta

Počet jednání je konstanta `MaxConsideredMeetings = 15` v `AttendancePredictor`. Není v konfiguraci — nikdo ji nebude ladit za provozu a další nastavení v `appsettings` by bylo na obtíž.

## 6. Testy

| Vrstva | Test |
|---|---|
| Unit | z 10 uzavřených jednání má osoba 7× Videokonference a 3× Přítomen → Videokonference |
| Unit | méně než 15 jednání se počítá z toho, co je (U2) |
| Unit | při více než 15 uzavřených jednáních se starší **nezapočítají** — jinak by pravidlo 15 nebylo k ničemu |
| Unit | jednání v přípravě a otevřená se **nepočítají** (U3) |
| Unit | shoda počtů → vyhrává stav z nejnovějšího jednání (§4.2) |
| Unit | osoba bez historie ve výsledku není |
| Unit | historie z **jiného projektu** se nepočítá |
| Integration | založení jednání předvyplní účast podle historie; osoba bez historie dostane Přítomen |
| Unit | seed obsahuje `N'ONLINE', N'Videokonference'` v obou souborech |

Předposlední test je ten, který ověřuje celý průchod až do databáze; ostatní hlídají rozhodovací pravidla bez databáze.

## 7. Co se nemění

Seznam účastníků nového jednání, ruční editace účasti, zobrazení a tisk docházky, existující jednání, číselník stavů účasti jako takový (mění se jen název jednoho řádku v seedu).

## 8. Mimo rozsah

**Nastavení výchozí účasti u osoby v týmu** — druhá varianta z diskuse. Zamítnuta ve prospěch odhadu z historie: bylo by to další políčko, které musí někdo vyplnit a hlavně udržovat, když se návyky změní.

**Zpětné přepočítání** účasti u jednání, která už existují. Odhad se uplatní až na nově zakládaná.
