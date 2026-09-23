# Sjednocení řazení záznamů (Záznamy ⇄ tisk) — design spec

**Datum:** 2026-07-06
**Fáze:** návrh (schválen uživatelem) → tato spec

## Cíl

Sjednotit pořadí záznamů tak, aby **záložka Záznamy** a **tisk jednání / tisk celého projektu** zobrazovaly záznamy ve **stejném pořadí** — aby tištěná příprava seděla na to, co je vidět v Záznamech (pro časově aktuální jednání; u uzavřených/starších jednání to z principu sedět nemůže, protože viditelná čísla jsou snapshot daného jednání).

## Problém (zjištěno průzkumem)

Oba pohledy seskupují záznamy dle subsystému (vnější úroveň) a subsystémy řadí dle pořadí projektu — to **sedí**. Základní helper `OrderRecordsByVisibleNumber` (viditelné číslo: `PartA → PartB → CisloZaznamu`) existuje dokonce **třikrát identicky** (RecordComposition, MeetingService.DetailQueries, ExportProjectionBuilders).

Jediný reálný rozdíl je pořadí záznamů **uvnitř subsystému**:
- **Záznamy:** čistě dle viditelného čísla (bez kategorie).
- **Tisk** ([ExportTemplateUseCase.BuildSubsystemGroups](../../PmTracker.Web/Services/Export/ExportTemplateUseCase.cs)): **primárně dle kategorie** (`CategoryOrder`: Informace → Rozhodnutí → Úkol → ostatní), pak dle viditelného čísla.

## Požadované chování

Pořadí záznamů uvnitř každého subsystému = **jeden složený sort, oba klíče aplikované současně** (`OrderBy(k1).ThenBy(k2)`):

1. **Klíč 1 (primární): kategorie** — pořadí `Informace → Rozhodnutí → Úkol → ostatní` (funkce `CategoryOrder`).
2. **Klíč 2 (sekundární): číslo záznamu** — viditelné číslo dle jednání:
   `ResolveVisibleNumberPartA` (číslo jednania, např. 873 před 881; fallback `CisloZaznamu`) →
   `ResolveVisibleNumberPartB` (pozice v jednání, ‑1 před ‑2) →
   `CisloZaznamu`.
   Tj. 873‑1 je před 881‑2 **bez ohledu na interní id** záznamu.

**Příklad (subsystém GESTOR):**

| Záznam | Kategorie | Číslo | Výsledné pořadí |
|---|---|---|---|
| B | Informace | 873‑1 | 1 |
| E | Informace | 881‑1 | 2 |
| C | Rozhodnutí | 873‑2 | 3 |
| A | Úkol | 875‑1 | 4 |
| D | Úkol | 881‑2 | 5 |

## Rozsah

**Platí pro:**
- **Záznamy tab** (server-side pořadí karet) — hlavní změna: přidá se primární klíč kategorie (dnes řadí jen dle čísla).
- **Tisk jednání** (`BuildMeetingTemplateAsync`) a **tisk celého projektu** (`BuildProjectTemplateAsync`) — už tak řadí (kategorie → číslo); sjednotí se na sdílený algoritmus (fakticky beze změny výstupu).

**Mimo rozsah:**
- **Tisk úkolu** (`BuildTaskTemplateAsync`, endpointy `Ukol/{id}/Tisk|Word`) — netýká se, neměnit.
- **Subsystémové seskupení** a jeho pořadí (dle pořadí projektu) — beze změny, zůstává vnější úroveň v obou pohledech.

## Architektura řešení

### 1. Jeden sdílený řadicí algoritmus (single source of truth)

Vytvořit sdílenou utilitu (např. `RecordDisplayOrdering` ve `Services/Common`), která je jediným zdrojem pravdy pro:
- `int CategoryOrder(string? categoryName)` — přesunuto z `ExportTemplateUseCase` (Informace=1, Rozhodnutí=2, Úkol=3, ostatní=4; match dle názvu kategorie, ponechat stávající logiku).
- Řadicí klíč viditelného čísla — `ResolveVisibleNumberPartA/PartB` (dnes triplikované).

Utilita musí umět řadit jak entity (`ProjektovyZaznamEntity` v Záznamech / meeting detailu), tak export VM (`PdfExportRecordViewModel` v tisku). Řešení: metoda přijímající selektory (category name + partA + partB + cisloZaznamu), nebo dvojice tenkých overloadů. Konkrétní podoba je detail plánu.

Tři existující kopie `OrderRecordsByVisibleNumber` (RecordComposition, MeetingService.DetailQueries, ExportProjectionBuilders) se sjednotí na tuto utilitu.

**Přesné pořadí klíčů** (aby tisk zůstal 1:1): `CategoryOrder → název kategorie (tie-break uvnitř stejného CategoryOrder) → PartA → PartB → CisloZaznamu`. Tie-break dle názvu se v produkci fakticky neuplatní (existují jen 3 kategorie Info/Rozhodnutí/Úkol s odlišným CategoryOrder), ale zachováním klíče je zaručeno, že se výstup tisku nezmění.

### 2. Záznamy tab (server)

[ProjectService.RecordCards.cs:17](../../PmTracker.Web/Services/ProjectService.RecordCards.cs) — dnes `OrderRecordsByVisibleNumber(records)`. Změna: nejdřív načíst `categories` (už se načítají na ř. 21), pak řadit sdílenou utilitou s primárním klíčem kategorie:
`OrderBy(CategoryOrder(názevKategorie)).ThenBy(PartA).ThenBy(PartB).ThenBy(CisloZaznamu)`.

Klient ([recordDisplay.js](../../PmTracker.Web/wwwroot/js/modules/filters/recordDisplay.js)) **zachovává** pořadí karet uvnitř subsystémové skupiny (řadí jen skupiny) → změna server-side pořadí stačí, klient netřeba měnit kvůli řazení.

### 3. Tisk (meeting + projekt)

[ExportTemplateUseCase.BuildSubsystemGroups](../../PmTracker.Web/Services/Export/ExportTemplateUseCase.cs) — within-group řazení (`CategoryOrder → Kategorie → CisloViditelneA → CisloViditelneB → CisloZaznamu`) nahradit voláním sdílené utility. Export VM už nese **rozřešené** `CisloViditelneA/B` ([ExportProjectionBuilders.cs:886-887](../../PmTracker.Web/Services/Export/ExportProjectionBuilders.cs)), takže výstup se nezmění — jen se odstraní duplicitní logika.

### 4. Přejmenování labelu filtru

[_ProjectFilterShell.cshtml:34](../../PmTracker.Web/Views/Projekty/_ProjectFilterShell.cshtml) — label **„Řadit podle" → „Řazení subsystémů"**. Filtr `sortBy` totiž řadí **jen subsystémy** (skupiny), ne záznamy — původní název je zavádějící. Options (`project-asc/desc`, `alpha-asc/desc`) i jejich chování zůstávají. `sortBy` má `skipChip: true`, žádný chip label k úpravě. Label je sdílený pro Záznamy i Harmonogram tab (oba seskupují dle subsystému) — konzistentní.

### 5. Žádné jiné řazení záznamů

Pořadí záznamů je **pevné** (kategorie → číslo). Nezavádí se žádná uživatelská volba řazení záznamů. `sortBy` ovládá výhradně řazení subsystémů.

## Data flow

Projekty/Detail → záložka Záznamy: `BuildRecordCardsForProjectAsync` seřadí karty (subsystém-agnosticky, sdílenou utilitou) → server render karet → klient je seskupí dle subsystému a **uvnitř zachová** server pořadí (kategorie → číslo).

Export/Tisk: `BuildMeetingTemplateAsync`/`BuildProjectTemplateAsync` → `BuildSubsystemGroups` → within-group sdílenou utilitou (kategorie → číslo). Subsystémové skupiny řazené dle pořadí projektu.

## Okrajové případy

- **Uzavřená / starší jednání:** viditelná čísla jsou snapshot daného jednání → pořadí odpovídá tehdejšímu stavu, ne aktuálním Záznamům. Očekávané, uživatel to bere.
- **Záznam bez viditelného čísla** (`CisloViditelneA==0`): fallback na `CisloZaznamu` (stávající logika `ResolveVisibleNumberPartA`) — konzistentně v Záznamech i tisku díky sdílené utilitě.
- **Neznámá kategorie:** `CategoryOrder` → 4 (za Info/Rozhodnutí/Úkol).

## Testy

- **Unit:** sdílená utilita `RecordDisplayOrdering` — složený sort (kategorie primár, číslo sekundár) na sadě s promíchanými kategoriemi a čísly jednání (ověří „873‑1 před 881‑2 bez ohledu na id" i pořadí Info→Rozhodnutí→Úkol).
- **Unit (source):** label `_ProjectFilterShell.cshtml` = „Řazení subsystémů" (ne „Řadit podle").
- **Api render:** Záznamy tab — karty v pořadí kategorie → číslo uvnitř subsystému (seed záznamů s promíchanými kategoriemi/čísly).
- **Api/Integration:** meeting + project tisk template — stejné pořadí jako Záznamy (regrese: výstup tisku se nemění).
- **E2E/Playwright:** vizuální ověření pořadí v Záznamech + že tisk sedí (dle možností dev dat).

## Soubory (předpoklad)

- Nový: `PmTracker.Web/Services/Common/RecordDisplayOrdering.cs` (sdílená utilita).
- Změna: `ProjectService.RecordCards.cs`, `ProjectService.RecordComposition.cs` (odstranit lokální kopii), `MeetingService.DetailQueries.cs` (odstranit lokální kopii), `Export/ExportProjectionBuilders.cs` (odstranit lokální kopii), `Export/ExportTemplateUseCase.cs` (BuildSubsystemGroups + přesun CategoryOrder), `Views/Projekty/_ProjectFilterShell.cshtml` (label).
- Testy: Unit + Api + E2E dle výše.
