# Podbarvení ukončených úkolů v tisku — design

**Datum:** 2026-09-05
**Stav:** návrh schválen uživatelem, připraven k implementaci
**Podnět:** hlášení testera — „ukončené úkoly v tisku nejsou podbarvené modře"
**Souvisí:** `2026-09-04-serverove-pdf-tisk-design.md` (serverové PDF — tisk už prochází generátorem, podbarvení se tedy propíše i do PDF)

## 1. Cíl

Ukončený úkol má být v tisku **jemně modře podbarvený**, aby šel odlišit od ostatních. Platí pro **PDF i Word**.

## 2. Východisko — co dnes platí

Hlášení testera není o rozbité funkci, ale o **chybějící**:

| Zjištění | Místo |
|---|---|
| Modrá `#2563EB` se uplatňuje **jen jako barva písma** celého vyjádření (hlavička + text), nikde jako podbarvení | `Views/Export/_PdfRecordRow.cshtml:63-64` |
| Modrá znamená „nová informace od minule" — týká se **vyjádření**, ne úkolu | `ExportProjectionBuilders.cs:914` (`ResolveHighlightColor`) |
| Na úrovni úkolu existuje jediné podbarvení — pozastavené záznamy, krémová | `pdf-export.css:189`, `OpenXmlWordExportService.cs:26` (`FDF4E8`) |
| Model záznamu pro tisk **nenese informaci o ukončenosti** — má jen textový `Stav` a `IsPaused` | `PdfExportViewModels.cs:64` |
| Pozastavení se pozná podle toho, že **název stavu obsahuje „pozastav"** — žádný kód ani příznak | `ExportProjectionBuilders.cs:896` |
| **Stavy úkolů nejsou v číselnících aplikace** — nabídka má 7 položek a tato mezi nimi není; udržují se přímo v databázi | `DictionaryService.Queries.cs:284` |

Poslední bod je podstatný: obsluha uložení stavů úkolů v kódu **zůstala** (`DictionaryService.Commands.cs:338`) a endpoint `/Ciselniky/SaveRow` by šel oslovit ručně sestaveným požadavkem, ale z rozhraní se na ni uživatel nedostane. **Hlídání kolize proto nelze postavit na ukládacím formuláři.**

## 3. Rozhodnutí uživatele (diskuse 2026-09-05)

| # | Rozhodnutí |
|---|---|
| U1 | Pozastavený úkol **nesmí nikdy** být koncový (`is_final`). Kolizi hlídá program. |
| U2 | Ukončený úkol = **jemné modré podbarvení**, jen aby byl odlišitelný. |
| U3 | Vyjádření zůstávají **sytě modrým písmem** beze změny — sytější odstín je na světlém podbarvení čitelný. |
| U4 | Platí pro **PDF i Word**. |
| U5 | V tisku jednání se ukončenost bere **ke dni toho jednání**, ne z dneška. |
| U6 | Nesrovnalost sloupce „Stav" (ukazuje dnešní stav, ne stav k datu jednání) se v této změně **neřeší**. |

## 4. Pravidla

### 4.1 Kdy je úkol „ukončený"

```
ukončený  =  stav je koncový (is_final)  A ZÁROVEŇ  stav není pozastavení
```

Druhá podmínka je hlídání z U1: i kdyby v databázi uvázl stav označený zároveň jako pozastavení a jako koncový, ve výstupu se nikdy neprojeví jako ukončený.

### 4.2 Který stav se bere

| Tisk | Použitý stav | Proč |
|---|---|---|
| **Jednání** | stav **k datu jednání** | Ukončený úkol se v tisku jednání objeví právě jednou — na zápise, kde se poprvé hlásí jako hotový. To je ta chvíle, kdy má být zvýrazněný. Dnešní stav by podbarvil i úkol, který v době jednání běžel a skončil o půl roku později. |
| **Projekt** | dnešní stav | Žádné kotevní jednání neexistuje, jiný stav není k dispozici. |
| **Záznam** | dnešní stav | Totéž. |

Stav k datu jednání se už dnes počítá kvůli rozhodnutí, zda se řádek vůbec vytiskne (`ExportRecordVisibilityEvaluator`), ale zahazuje se. Nová metoda ho zpřístupní; nejde tedy o nový výpočet, jen o využití existujícího.

### 4.3 Přednost při souběhu

Kdyby záznam vyšel jako pozastavený i ukončený zároveň, **vyhrává pozastavení**. Po zavedení pravidla 4.1 by nastat nemělo; přednost se určuje, aby výsledek nezáležel na pořadí pravidel.

## 5. Vzhled

| Situace | PDF / HTML | Word |
|---|---|---|
| Ukončený úkol | pozadí řádku `#EFF6FF` | výplň všech tří buněk `EFF6FF` |
| Pozastavený úkol | pozadí řádku `#fdf4e8` (beze změny) | výplň `FDF4E8` (beze změny) |
| Vyjádření „nová informace" | písmo `#2563EB` (beze změny) | písmo `#2563EB` (beze změny) |

Odstín `#EFF6FF` je záměrně stejně světlý jako stávající krémová u pozastavených, aby tisk nekřičel. Syté modré písmo vyjádření na něm zůstává čitelné.

**V CSS musí pravidlo pro ukončené stát před pravidlem pro pozastavené.** Obě mají stejnou specificitu, takže rozhoduje pořadí v souboru a pozastavení musí vyhrát (bod 4.3).

## 6. Hlídání kolize

Protože stavy úkolů nejdou měnit z aplikace, hlídání se dělí na dvě části:

**Při čtení** — odvození ukončenosti podle 4.1. Kolize se ve výstupu nemůže projevit, ať je v databázi cokoli. Toto je vlastní ochrana.

**Při startu aplikace** — když v číselníku stavů úkolů existuje řádek, který je zároveň pozastavení a koncový stav, zapíše se **varování do logu** se jmény takových stavů. Aplikace normálně poběží.

Varování má smysl proto, že se v tomto nasazení do databáze zasahuje ručně skripty — bez něj by se o rozporném řádku nikdo nedozvěděl. Použije se stávající vzor nefatálního varování ve `SqlStartupValidatorHostedService` (řádky 190-204 řeší orphaned role stejným způsobem).

Číselník stavů úkolů je malý, proto se načte celý a filtruje se v paměti **stejnou funkcí**, jakou používá export. Tím je vyloučeno, že by se obě definice pozastavení časem rozešly.

## 7. Architektura a soubory

| Akce | Soubor | Odpovědnost |
|---|---|---|
| Vytvořit | `Services/Common/TaskStatusRules.cs` | jediná definice „co je pozastavení" a „co je ukončený stav" |
| Změnit | `Models/ViewModels/PdfExportViewModels.cs` | `bool IsCompleted` na `PdfExportRecordViewModel` |
| Změnit | `Services/Export/ExportProjectionBuilders.cs` | nová metoda evaluátoru, naplnění `IsCompleted`, `IsPaused` přes sdílenou funkci |
| Změnit | `Views/Export/_PdfRecordRow.cshtml` | třída `completed` na řádku |
| Změnit | `wwwroot/css/pdf-export.css` | pravidlo podbarvení, **před** pravidlem pozastavených |
| Změnit | `Services/Export/OpenXmlWordExportService.cs` | konstanta `CompletedRecordFillHex = "EFF6FF"` |
| Změnit | `Services/Export/OpenXmlWordExportService.Records.cs` | volba výplně s předností pozastavení |
| Změnit | `Services/Data/SqlStartupValidatorHostedService.cs` | varování při rozporném stavu |

### 7.1 Sdílená pravidla

```csharp
public static class TaskStatusRules
{
    /// <summary>Pozastavení se pozná podle názvu stavu — číselník nemá vlastní příznak.</summary>
    public static bool IsPausedName(string? nazev);

    /// <summary>Ukončený = koncový a zároveň ne pozastavený (rozhodnutí U1).</summary>
    public static bool IsCompleted(CiselnikStavuUkoluEntity? state);
}
```

Test na název zůstává doslova takový, jaký je dnes v exportu (`Contains("pozastav", StringComparison.CurrentCultureIgnoreCase)`), aby se chování nezměnilo. Kdyby číselník někdy dostal vlastní příznak pozastavení, mění se jen tato jedna funkce.

### 7.2 Rozšíření evaluátoru

```csharp
public interface IExportRecordVisibilityEvaluator
{
    // stávající
    bool IsVisibleForMeetingPrint(...);

    /// <summary>Byl úkol ukončený ke dni jednání? (rozhodnutí U5)</summary>
    bool IsCompletedForMeetingPrint(
        ProjektovyZaznamEntity record,
        IReadOnlyDictionary<int, CiselnikStavuUkoluEntity> taskStates,
        IReadOnlyList<ZaznamHistorieStavuZaznamuEntity> statusHistory,
        DateTime anchorMeetingDate);
}
```

Metoda využívá stejné soukromé pomocníky jako `IsVisibleForMeetingPrint` (zpětný přepočet stavu k datu). V bloku snímkových pravidel se z ní sestaví množina id ukončených záznamů, kterou pak čte skládání modelu.

Mimo tisk jednání se `IsCompleted` odvodí z **dnešního** stavu záznamu přes `TaskStatusRules.IsCompleted`.

## 8. Testy

| Vrstva | Test |
|---|---|
| Unit | `TaskStatusRules` — rozpozná pozastavení podle názvu včetně velikosti písmen; koncový stav je ukončený; **koncový a zároveň pozastavený není ukončený** |
| Unit | evaluátor — úkol ukončený až po jednání **není** označen jako ukončený k datu jednání; úkol ukončený před jednáním ano |
| Unit | Word — řádek ukončeného úkolu má výplň `EFF6FF` ve všech třech buňkách; při souběhu s pozastavením má `FDF4E8` |
| Unit | tiskové CSS obsahuje pravidlo pro ukončené a stojí **před** pravidlem pro pozastavené |
| Api | tisk jednání vykreslí u ukončeného úkolu třídu `completed`; u běžícího ne |

Test evaluátoru je čistě paměťový — pracuje s entitami, databázi nepotřebuje. Datová logika z bodu 4.2 se tedy ověřuje tam, ne přes HTTP.

U Wordu se kontroluje **vygenerovaný dokument**, ne model; navazuje na stávající `BuildDocument_ShouldShadeWholePausedRecordRow`.

## 9. Mimo rozsah

**Sloupec „Stav" ukazuje v tisku jednání dnešní stav**, ne stav k datu jednání (`ExportProjectionBuilders.cs:895`). Na starším zápise tak může být podbarvený řádek, u kterého je napsáno „Rozpracováno" — protože k datu jednání hotový byl, ale mezitím se znovu otevřel. Rozhodnutí U6: neřeší se zde.

Ze stejného kořene plyne důsledek, se kterým je třeba počítat: **`IsPaused` se také počítá z dnešního stavu.** Úkol ukončený k datu jednání, který je dnes pozastavený, dostane podle pravidla 4.3 krémové podbarvení místo modrého. Je to okrajový případ a zmizí sám, až se sloupec „Stav" a pozastavení převedou na stav k datu jednání.

Dále se neřeší **osiřelá obsluha uložení stavů úkolů** v `DictionaryService` — z rozhraní nedosažitelná, ale stále živá.

## 10. Co se nemění

Pravidla viditelnosti záznamů v tisku jednání, barva a chování zvýraznění vyjádření, podbarvení pozastavených, tisk projektu a záznamu v ostatních ohledech, model dat v databázi.
