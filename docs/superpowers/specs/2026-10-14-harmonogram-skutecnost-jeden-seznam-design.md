# Skutečnost harmonogramu v jednom seznamu — návrh

**Datum:** 2026-10-14
**Stav:** schváleno uživatelem (varianta V3, R2 potvrzeno 2026-10-14)
**Navazuje na:** `docs/superpowers/specs/2026-09-17-record-edit-concurrency-design.md` §6.2 (oprava R6, která část problému způsobila)

## 1. Problém

Skutečnost kroků harmonogramu se z editoru posílala **dvěma seznamy**: `HarmonogramHodnoty[i]`
(pevný index 0–9, nese plán) a `ManualActualKroky[n]` (vlastní čítač). Čítač přiděluje tabulka
(`_ScheduleTable.cshtml`), o vykreslení vstupu ale rozhoduje šablona buňky
(`_ScheduleBlockManualCell.cshtml`) podle dalších podmínek. Z toho vzniklo šest vad — všechny
ověřené v prohlížeči nebo na odeslaných datech 2026-10-13/14:

| | Vada | Projev | Původ |
|---|---|---|---|
| **D1** | Díra v číslování `ManualActualKroky` | ASP.NET naváže seznam jen po první díru → ruční data **všech dalších kroků** (i 2/5/8/9) se tiše zahodí | přepínač režimu (FIX 2026-05-04); běžná od opravy R6 |
| **D2** | Krok se zdrojem Ruční/Historická dostane špatné pole | změna ani smazání uloženého ručního data se neuloží (uložení hlásí úspěch); návrh změnu uplatní, smazání ne | výběr buňky zná jen zdroje FromVyjadreni a None |
| **D3** | Vypnutí automatiky + Uložit smaže data automatu | ruční vstup je u kroku od automatu prázdný a server ho v ručním režimu zapíše jako prázdný; krok je pak ruční, automat ho už nedoplní | **oprava R6, verze 1.0** |
| **D4** | Zapnutí automatiky nechá ruční data viset | server v Auto režimu ruční vstupy nečte; mazání dělal `ApplyHarmonogramRezimAsync`, který zmizel v `9525ebd` | refaktor na datový model |
| **D5** | Volba „Ručně / Z vyjádření“ u kroků 2/5/8/9 | **mrtvý kód** — nikdy se nevykreslí, protože `HarmonogramKrokDatumMapping` kroky 2/5/8/9 nemapuje; `PreferredZdroj` server od `9525ebd` nečte | FIX 2026-05-04 |
| **D6** | Mrtvé komentáře | popisují `ApplyHarmonogramRezimAsync`, `clearKeys`, `PreferredZdroj` | refaktor na datový model |

Testy to nechytily, protože posílaly příkaz rovnou službě — nikdy neprošly cestou
**vykreslení → vazba formuláře → uložení**.

## 2. Rozhodnutí

| | Rozhodnutí | Kdo |
|---|---|---|
| **R1** | Skutečnost jde **jen** v `HarmonogramHodnoty[i].SkutecnostDatum`; `ManualActualKroky` zaniká. Index `i` je řádek kroku, pevný 0–9 → díra z principu nevznikne. | uživatel 2026-10-14 (V3) |
| **R2** | Uložení s přepnutím na automatiku **smaže data kroků 1/3/4/6/7/10, která nedodal automat** — ruční (✍️) i historická (📜); zůstanou jen data automatu (🤖). Platí i pro krok, který už Auto byl (smíšený stav ze starých návrhů). Po uložení se naplánuje stažení dat ze ServiceDesku. Záznam, který už automatický je, se uložením nemění. | uživatel 2026-10-14: „buď to dělám ručně, nebo to dělá automat, nic mezi“ |
| **R3** | Kroky 2/5/8/9 jsou **vždy ruční**: automat ani přepínač je nemění, jejich řádek se zakládá jako ruční, `SelectCandidate` je odmítne. | uživatel 2026-10-14 |
| **R4** | Rozpracované návrhy se **nemažou**: starý formát JSON (`ManualActualKroky`) se při čtení převede na `ActualHarmonogramHodnoty`. Totéž zajistí čitelnou historii. | uživatel mazání dovolil; převod je bezpečnější a nepotřebuje krok při nasazení |
| **R5** | D5 se odstraní jako mrtvý kód. `manual-zdroj-toggle.js` se **vyprázdní, ne smaže** — `bootstrap.js` z mezipaměti prohlížeče by importoval neexistující modul a shodil celé JS. | návrh |
| **R6** | Změnu pozná server porovnáním `SkutecnostDatum` s `SkutecnostDatumPuvodni` (hodnota, se kterou se editor vykreslil). Nedotčený krok se nepřepíše — zůstane hodnota **i zdroj** (oprava D3) a novější hodnota automatu z doby editace se nepřepíše starou. | návrh |
| **R7** | Chronologie skutečnosti se kontroluje jen mezi hodnotami, které po uložení patří uživateli (2/5/8/9 vždy, 1/3/4/6/7/10 v ručním režimu), a jen u dvojic, kde se aspoň jedna hodnota v tomto uložení změnila. Uložení nesmí blokovat stará data, na která uživatel nesáhl. | návrh |

## 3. Pravidla zápisu skutečnosti

Jedna komponenta `ScheduleActualApplier` je použitá všude, kde se skutečnost zapisuje:
uložení editoru, uložení jen záložky Harmonogram a schválení návrhu změny harmonogramu.

1. **Přepnutí na ruční režim:** kroky 1/3/4/6/7/10 dostanou režim Ruční (chybějící řádek se založí). Hodnoty ani zdroje se nemění.
2. **Přepnutí na automatiku (R2):** jen když záznam byl v ručním režimu. Všechny kroky 1/3/4/6/7/10 přejdou na Auto; každý s datem, které nedodal automat (Ruční, Historická), se vyprázdní. Výsledek hlásí `PrepnutoNaAuto` → volající naplánuje stažení ze SD.
3. **Změna kroku 2/5/8/9:** zapíše se vždy (datum + zdroj Ruční; prázdné = smazáno, zdroj Neznámý).
4. **Změna kroku 1/3/4/6/7/10:** zapíše se jen v ručním režimu (cílovém, jinak stávajícím stavu záznamu). V automatickém se ignoruje.
5. **Bez změny:** krok se nedotkne.
6. Přepínač nikdy nemění kroky 2/5/8/9 (R3).

## 4. Kontrakt formuláře

| Pole | Kdy se vykreslí | Význam |
|---|---|---|
| `HarmonogramHodnoty[i].Poradi` | vždy, `i` = řádek 0–9 | pořadí kroku |
| `HarmonogramHodnoty[i].PlanDatum` | vždy | plánované datum (beze změny) |
| `HarmonogramHodnoty[i].SkutecnostDatum` | u editovatelné skutečnosti; u zakládání záznamu jako zamčené pole | skutečnost, jak ji uživatel nechal |
| `HarmonogramHodnoty[i].SkutecnostDatumPuvodni` | spolu s editovatelnou skutečností | hodnota při vykreslení |
| `HarmonogramRezim` | u úkolu | `Auto` / `Manual` |

Editovatelná skutečnost je **předvyplněná aktuální hodnotou bez ohledu na zdroj**. V automatickém
režimu je vstup kroků 1/3/4/6/7/10 skrytý (zobrazí se bublina z vyjádření, uložené
ruční/historické datum — to po opravě zůstává jen z migrace starých delay hodnot `1_3_10` —
nebo varování „—“).

**Payload návrhu:** `ActualHarmonogramHodnoty` je **seznam změn** (`SkutecnostDatum` null = smazat).
`ChangesScheduleActual = ActualHarmonogramHodnoty.Count > 0`. Když je cílový režim návrhu
automatický, změny kroků 1/3/4/6/7/10 se do payloadu nedávají.

## 5. Mimo rozsah

- Pravidlo „skutečnost nesmí být v budoucnosti“ — dnes jen upozornění v prohlížeči u všech ručních vstupů stejně; beze změny.
- Sémantika plánovaných dat — beze změny.
- Přepis celé wiki harmonogramu — upraví se jen stránky o ruční skutečnosti a návrzích.

## 6. Rizika

- **Historická data z migrace `1_3_10` na záznamu v automatickém režimu** (Historická + Auto) jsou smíšený stav, který tato oprava nevytváří, ale ani hromadně neuklízí: zmizí, až automat dodá vlastní hodnotu, nebo po vypnutí a zapnutí automatiky. Hromadný úklid na serveru je samostatné rozhodnutí uživatele (na lokální DB takové řádky nejsou).

- **Souběžná práce na návrzích** (druhá session, commity 2026-10-12/13) → práce ve worktree, před sloučením rebase na aktuální `main`.
- **Starý JS v mezipaměti** → `manual-zdroj-toggle.js` zůstane jako prázdný modul (R5).
- **Staré čekající návrhy s ručními daty na záznamu v automatickém režimu** se po schválení nově neuplatní (dřív tiše přepnuly krok na ruční). Infobar detailu návrhu to řekne pravdivě.
