# Návrh změny harmonogramu — opravy k udělání (zadáno 2026-10-08)

**Stav:** opraveno 2026-10-12 (commity 691d6b0, 48d2d9f, c524ea6, 59df659, 7ebf87a, eb27f12,
4ad21b7, 33dbd55).

Tři připomínky uživatele k návrhům změny harmonogramu. Stav byl ověřený v kódu, oprava je
popsaná u každého bodu a v sekcích níže.

## Rozhodnutí uživatele (2026-10-08)

1. **Důvod změny:** v návrhu změny termínu nebo harmonogramu dát autorovi pole pro text, proč
   změnu chce. Schvalovateli ho ukázat jako sdělení autora. Využít k tomu prvek „důvod“
   (`zaznam_historie_terminu.duvod`), který dnes nese jen pevné „Úprava záznamu“ — viz
   Související vyjasnění na konci.
   **Vyřešeno (59df659, 7ebf87a):** editor návrhu má nepovinné pole „Důvod změny (volitelné)“,
   schvalovatel ho vidí v boxu „Sdělení autora návrhu“ a při schválení se text zapíše do
   `zaznam_historie_terminu.duvod`, pokud návrh termín opravdu měnil.
2. **Opakovaný návrh při aktivním návrhu:** ověřit po kliknutí, ještě před přechodem na
   editor návrhu. Když návrh nejde založit, zobrazit dialog (`gov-dialog` se stavem error),
   ne chybovou stránku. Dialog má přednost před infobarem. Zašedlá položka se nedělá.
   **Vyřešeno (691d6b0, 48d2d9f, c524ea6):** klik nejdřív ověří dostupnost jedním pravidlem a
   při čekajícím návrhu nebo chybějícím oprávnění zobrazí dialog „Návrh nelze založit“ místo
   přechodu na editor; přímé otevření URL editoru při blokaci přesměruje zpět s červenou
   hláškou nahoře stránky.
3. **Režim vyplňování v návrhu:** opravit, aby se přepínač do návrhu ukládal a po schválení se
   přepnutí opravdu provedlo. Schvalovatel vidí stav z návrhu, ne ze záznamu. Když se stav
   v návrhu liší od záznamu, zobrazit nad záznamem `gov-infobar` (error, bold, ikona
   `components/exclamation-triangle-fill`, `closable` false). Podbarvení přepínače v editoru
   návrhu (požadavek z prvního zadání) zůstává.
   **Vyřešeno (eb27f12, 4ad21b7, 33dbd55):** přepínač se ukládá do payloadu návrhu a při
   schválení se uplatní stejně jako přímá úprava záznamu, jen když návrh režim opravdu mění;
   schvalovatel vidí stav podle návrhu a červený infobar s vysvětlením důsledku.

## 1. Opakovaný návrh při aktivním návrhu končí chybovou stránkou

**Vyřešeno.** Jedno pravidlo `GetScheduleProposalAvailabilityAsync` (a JSON endpoint
`/Navrhy/ScheduleProposalAvailability`) teď rozhoduje před otevřením editoru. Klik na
„Navrhnout změnu harmonogramu“ v menu karty i na stránce záznamu ho zavolá nejdřív; při
čekajícím návrhu se zobrazí dialog „Návrh nelze založit“ (text: „K záznamu už existuje aktivní
návrh na změnu termínu a harmonogramu. Nový návrh půjde založit, až bude čekající návrh
schválen nebo zamítnut.“) s tlačítky „Rozumím“ a „Zobrazit čekající návrh“ (druhé jen když
uživatel smí vidět tab Návrhy). Přímé otevření URL editoru při blokaci (čekající návrh nebo bez
oprávnění) přesměruje zpět na záznam s červenou hláškou nahoře stránky místo chybové stránky.
Ctrl/Cmd/Shift-klik nebo prostřední tlačítko (nová karta) se nezachytává — rozhoduje server.

**Původní stav.** Položka „Navrhnout změnu harmonogramu“ v menu karty záznamu
(`_ZaznamPartial.cshtml`) a na stránce záznamu (`ZaznamDetailPage.cshtml`) se zobrazuje jen
podle oprávnění (`CanCreateScheduleProposal`). Na čekající návrh se nedívá. Kliknutí otevře
`GET /Navrhy/CreateScheduleProposal` a až `RecordProposalService.BuildScheduleProposalEditorAsync`
zjistí čekající návrh (`PendingScheduleProposalLockEvaluator`) a vyhodí
`InvalidOperationException` s hláškou „Pro tento záznam už čeká návrh…“. Uživatel skončí na
chybové stránce, i když hláška sama je správná.

**Co chce uživatel.** Nedovolit klik, který skončí chybou:
- nejlépe položka zašedlá (neaktivní) a po najetí myší se ukáže důvod;
- pokud by to zatěžovalo aplikaci, ověřit možnost návrhu před přechodem na stránku editoru a
  při čekajícím návrhu jen zobrazit okno s hláškou podle DS gov (`gov-dialog` / `gov-message`).
  Uživatelův text: „Záznam nelze založit, již existuje aktivní návrh na změnu harmonogramu.“
  K zvážení přesnější znění: „Návrh nelze založit, k záznamu už existuje aktivní návrh na změnu
  harmonogramu.“

**Poznámky k řešení.**
- Karty záznamů se načítají dávkově (`ProjectService.RecordCards`), takže stav „čeká návrh“
  jde načíst jedním dotazem pro všechny karty na stránce. Zašedlá položka by aplikaci
  nezatížila; je to preferovaná varianta.
- Položka menu je `<a class="record-actions-menu-item">`, ne `gov-button`. Neaktivní stav
  potřebuje `aria-disabled="true"`, odstranit `href` a důvod v `gov-tooltip` (jen tokeny DS).
- Server musí kontrolu ponechat (souběh dvou uživatelů). Místo chybové stránky ale vrátit
  zpět na záznam s hláškou.

## 2. Přepínač „Automatické vyplňování harmonogramu“ v návrhu

**Vyřešeno.** `SchedulePlanProposalPayload` teď nese `HarmonogramRezim` (`null` u starých
návrhů = beze změny). Při schválení se uplatní stejně jako
`RecordService.SaveRecord.ApplyHarmonogramRezimAsync` u přímé úpravy záznamu, a jen když se
režim z návrhu od záznamu skutečně liší. V editoru návrhu se přepínač podbarví, když je
přepnutý opačně než na záznamu.
Na stránce schvalování ukazuje přepínač stav z návrhu (ne ze záznamu) a nad záznamem se
zobrazí `gov-infobar` (error, bold, ikona `components/exclamation-triangle-fill`, `closable`
false) s textem podle směru přepnutí.

**Kdo ho může přepnout.** Přepínač nemá vlastní oprávnění. Ukáže se každému, kdo otevře
editor záznamu nebo editor návrhu změny harmonogramu u úkolu (`_EditZaznamForm.cshtml`,
podmínka `JeUkolKategorie && !HideActual`). Editor návrhu otevře uživatel s
`proposals.schedule.create`, který smí navrhovat pro subsystém záznamu
(`RecordProposalAuthorizationPolicy`): s `proposals.edit.any` pro všechny subsystémy projektu
(administrátorské role), jinak vedoucí nebo zástupce vedoucího daného subsystému. Na stránce
schvalování je přepínač jen ke čtení (`IsProposalDecisionDetail`).

**Co se stane po přepnutí v návrhu a odeslání.** Návrh hodnotu přepínače neukládá:
`RecordProposalPayloadMapper.BuildSchedulePayload` přenáší jen plán, skutečnost a ruční data
(`ManualActualKroky`), pole `HarmonogramRezim` v payloadu není.
- **Auto → Ručně a vyplněná data:** po schválení se každý krok s vyplněným ručním datem trvale
  přepne na ruční režim (`RecordProposalService.DecisionCommands`, `SkutecnostRezim = Manual`)
  a přestane se plnit ze ServiceDesku. Kroky bez data zůstanou automatické.
- **Ručně → Auto:** JS vymaže ruční data ve formuláři, ale do návrhu se přepnutí nedostane.
  Po schválení se nezmění nic a kroky zůstanou ruční. Uživatel přitom v návrhu viděl „Auto“.
- **Schvalovatel změnu nevidí.** Na stránce schvalování ukazuje přepínač stav záznamu
  (`HarmonogramAutoFillSwitchOn` z krok řádků záznamu), ne stav z návrhu. Ruční data jsou
  vidět jen jako hodnoty v buňkách.

**Co chce uživatel.**
- V návrhu přepínač podbarvit, když je přepnutý opačně než na záznamu.
- Na stránce schvalování nad záznamem zobrazit `gov-infobar` s `color="error"`,
  `type="bold"`, ikonou `components/exclamation-triangle-fill` a `closable="false"`. Má
  upozornit, že návrh mění režim vyplňování harmonogramu. Ostatní hodnoty jsou na mně.

**Poznámky k řešení.**
- Předpoklad obojího: návrh musí režim nést. Doplnit `HarmonogramRezim` do
  `SchedulePlanProposalPayload` (u starých návrhů `null` = beze změny) a při schválení ho
  uplatnit stejně jako `RecordService.SaveRecord.ApplyHarmonogramRezimAsync`. Jinak by
  zvýraznění ukazovalo změnu, která se po schválení neprovede.
- Podbarvení v editoru návrhu: porovnat stav přepínače s výchozím stavem ze záznamu
  (`data-` atribut s původním stavem) a přidat třídu. Barva jen z tokenů DS.
- Infobar na schvalování: zobrazit, jen když `payload.HarmonogramRezim` existuje a liší se od
  stavu záznamu, nebo když návrh nese ruční data u kroků, které jsou na záznamu automatické.
  Přepínač na schvalování ukázat ve stavu podle návrhu.
- Ověřit `gov-infobar` v DS gov 4.7.0 (`assets/gov/components`): atributy `color`, `type`,
  `closable` a slot pro ikonu.

## Související vyjasnění

Uživatel se ptal, k čemu slouží „důvod změny termínu“. Ve sloupci
`zaznam_historie_terminu.duvod` je vždy pevný text „Úprava záznamu“. Zapíše ho
`RecordService.SaveRecord`, když se při uložení změní termín. Uživatel ho nikde nezadává a
aplikace ho nikde nečte; historie termínu v UI a v PDF ukazuje jen původní data. Sloupec je
přežitek a jde ho zrušit, v hledání se nepoužívá.

**Update 2026-10-12.** Sloupec teď přestal být přežitek. Od schválení návrhu s vyplněným
polem „Důvod změny“ se jeho text zapíše do `zaznam_historie_terminu.duvod` — ale jen tehdy,
když schválený návrh termín skutečně měnil; bez vyplněného důvodu se zapíše „Schválený návrh
#<id>“. Dřív schválení návrhu nezapisovalo do historie termínu vůbec žádný řádek — psala ji
jen přímá úprava záznamu (`RecordService.SaveRecord`, pevně „Úprava záznamu“).
