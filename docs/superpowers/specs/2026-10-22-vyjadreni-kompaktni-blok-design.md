# Vyjádření v jednom kompaktním bloku — specifikace

**Datum:** 2026-10-22
**Stav:** odsouhlaseno s úpravami 2026-10-22 (R8, šedé podbarvení, průchod za všechny role; R10 a R11 doplněny před implementací)
**Komponenta:** panel vyjádření záznamu (`Views/Projekty/_ZaznamCommentsPartial.cshtml`)

## 1. Zadání

> „V záznamech boxy pro vyjádření zmenšit a konsolidovat, dát všechny zobrazené vyjádření do jednoho boxu a rozdělit jen podbarvením, pokud na daný box najedu myší. Odstranit trvale viditelné, kdo kdy zapsal, nechat kdo kdy napsal jen při najetí myší na jednání. Graficky tedy zůstane hranice kolem celého bloku vyjádření, tak aby byl blok vyjádření viditelně ohraničený a odlišitelný, a na pravé straně v bloku budou pouze stejně jako nyní vykreslené prvky, ve kterých je napsáno, k jakému jednání se vyjádření vztahuje.“ (uživatel, 2026-10-21)

Důvod: blok je vertikálně příliš roztahaný. Pět vyjádření zabere půl stránky i víc.

## 2. Současný stav (změřeno 2026-10-22, 1400 px, světlý motiv)

Pět jednořádkových vyjádření zabere **600 px**, tedy asi 120 px na jedno. Výšku nedělá text. Dělají ji tři věci:

| Prvek | Výška |
|---|---|
| řádek „Autor · datum“ nad každou dlaždicí (`.comment-meta-outer`) | ~20 px |
| vlastní rámeček a odsazení dlaždice (`.comment`, padding 12 px) | ~26 px |
| pod štítkem jednání naskládaná tlačítka Upravit a Smazat (34 px) | ~40 px |
| mezera mezi dlaždicemi | 10 px |

Snímek současného stavu: dlaždice s vlastním rámečkem, nad každou jméno s e-mailem a datem, vpravo štítek „Jednání č. 901 (09.07.2026)“ a pod ním tužka a koš.

## 3. Cíl a měřítko úspěchu

- Všechna zobrazená vyjádření jsou v **jednom ohraničeném bloku**.
- **Jednořádkové vyjádření má výšku jednoho řádku textu** včetně štítku jednání, cca 32 px.
- **Pět jednořádkových vyjádření ≤ 200 px** (dnes 600 px). Měří se výška seznamu vyjádření v E2E testu a v průchodu prohlížečem.
- Nic, co dnes jde udělat (upravit, smazat, zjistit autora a datum, řadit, načíst další), nesmí zmizet. Jen se přesune z trvalého zobrazení na najetí myší nebo fokus.

## 4. Rozhodnutí

| # | Rozhodnutí | Zdroj |
|---|---|---|
| R1 | Všechna zobrazená vyjádření jsou v **jednom bloku** s rámečkem. Vyjádření uvnitř nemají vlastní rámeček, čáru ani mezeru navíc. | uživatel |
| R2 | Jednotlivá vyjádření odliší jen **podbarvení řádku, na kterém je myš** (nebo v něm fokus klávesnice). | uživatel |
| R3 | **„Kdo a kdy napsal“ trvale zmizí.** Ukáže se jako bublina při najetí myší na štítek jednání. | uživatel |
| R4 | Vpravo v řádku je trvale **jen štítek jednání**, vykreslený stejně jako dnes. | uživatel |
| R5 | **Upravit a Smazat** se objeví jako malé ikony na řádku, na kterém je myš nebo fokus, vlevo od štítku jednání. Text se kvůli nim neposouvá. | uživatel (volba „Při najetí na řádek“) |
| R6 | Platí **všude, kde se panel vyjádření zobrazuje**: karta v záložce Záznamy, detail jednání a stránka záznamu. Je to jedna sdílená komponenta. | doplněno (k potvrzení) |
| R7 | Štítek jde vybrat **klávesnicí** (Tab) a na dotykovém displeji klepnutím. Bublina s autorem a datem se pak ukáže i bez myši. | doplněno (přístupnost) |
| R8 | U vyjádření k **uzavřenému jednání se ikony Upravit a Smazat neobjeví** ani při najetí myší a server úpravu i smazání odmítne. Je to dnešní chování a zůstává (viz 5.4). | uživatel 2026-10-22 |
| R9 | Podbarvení řádku pod myší je **jemně šedé**: token `--background-neutral-subtle`. | uživatel 2026-10-22 |
| R10 | Panel ukáže **posledních 10 vyjádření** (dnes 5). Tlačítko „Další“ dál přidává **po 5** a „Zobrazit vše“ zůstává. | uživatel 2026-10-22 |
| R11 | Tlačítka **„Další (5)“ / „Předchozí (5)“ a „Zobrazit vše“ jsou v hlavičce vlevo vedle přepínače řazení**, v obou směrech řazení na stejném místě. Server je vykreslí rovnou tam; JS je už nepřesouvá. | uživatel 2026-10-22 |

## 5. Návrh

### 5.1 Struktura

```
Vyjádření              [Předchozí (5)] [Zobrazit vše] [Řazení: jednání vzestupně]
┌──────────────────────────────────────────────────────────────────────┐
│ Dodavatel potvrdil termín dodání podkladů.     [Jednání č. 901 (09.07.2026)] │
│░Podklady neúplné, chybí harmonogram etap 3 a 4.░[✎][🗑][Jednání č. 903 (14.07.2026)]│ ← myš
│ OK.                                             [Jednání č. 904 (17.07.2026)] │
│ Řídící výbor požaduje variantu B s posunem      [Jednání č. 905 (18.07.2026)] │
│ termínu o dva týdny; zástupce vedoucího připraví dopad…                       │
│ Varianta B schválena.                           [Jednání č. 906 (19.07.2026)] │
└──────────────────────────────────────────────────────────────────────┘
                         ┌───────────────────────────────┐
  bublina při najetí  →  │ Ing. Jana Testerová <jana…>   │
  na štítek jednání      │ Zapsáno 14.07.2026            │
                         └───────────────────────────────┘
[editor nového vyjádření]  [Vazba na jednání ▾] [Uložit]
```

Hlavička (R11) — stránkování vlevo od řazení, v obou směrech stejně; text tlačítka podle směru jako dnes (sestupně „Další (5)“, vzestupně „Předchozí (5)“):

```cshtml
<div class="record-comments-header">
    <h4>Vyjádření</h4>
    <div class="record-comments-tools">                          @* jen když je co ukázat *@
        <div class="comment-pagination-actions">[Další (5) | Předchozí (5)] [Zobrazit vše]</div>  @* jen když jsou další *@
        <pm-button class="comment-sort-toggle" data-comment-sort-toggle>…</pm-button>              @* jen při 2+ vyjádřeních *@
    </div>
</div>
```

- Třídy `record-comments-header--stacked-right` a `comment-pagination-actions--in-header` a přesun bloku v `applyCommentSort` (`comments.js`) odpadnou, protože tlačítka už mají jedno stálé místo.
- Na úzkém okně se skupina nástrojů zalomí pod nadpis a zůstane zarovnaná vpravo.

Markup jednoho vyjádření (řádku):

```cshtml
<div class="comment-list" data-comment-list>                     @* rámeček celého bloku *@
    <div class="comment" data-comment-item data-comment-id="…" data-comment-meeting="…" data-comment-date="…">
        <div class="comment-side">                               @* float vpravo, text ho obtéká *@
            @if (canEditThisComment)
            {
                <div class="comment-row-actions">…tužka, koš…</div>  @* viditelné jen při :hover / :focus-within řádku *@
            }
            <span class="chip-wrap">
                <span class="comment-meeting-chip" tabindex="0" aria-describedby="vyjadreni-meta-{Id}">Jednání č. 903 (14.07.2026)</span>
                <span id="vyjadreni-meta-{Id}" class="chip-tooltip chip-tooltip--end" role="tooltip">
                    <span class="chip-tooltip-title">{Autor}</span>
                    <span>Zapsáno {Datum:dd.MM.yyyy}</span>
                </span>
            </span>
        </div>
        <div class="comment-text richtext-render" data-comment-text>…</div>
        <form class="comment-edit-form" data-comment-edit-form hidden>…beze změny…</form>
    </div>
</div>
```

- `.comment-wrapper` a `.comment-meta-outer` (autor a datum nad dlaždicí) se z markupu odstraní.
- **Zachované háčky pro JS a testy:** `data-comment-list`, `data-comment-item`, `data-comment-id`, `data-comment-meeting`, `data-comment-date`, `data-comment-text`, `data-comment-edit-toggle`, `data-comment-edit-form`, `data-comment-edit-cancel` a stav `data-editing`. Formuláře úpravy a smazání (pole včetně `kontextJednaniId` z 2026-10-21) se nemění.
- **Bublina** používá existující vzor štítků externích vazeb (`.chip-wrap` + `.chip-tooltip`, `_ZaznamDetailPartial.cshtml`), který se ukáže při `:hover` i `:focus-within`. Nový modifikátor `.chip-tooltip--end` ji zarovná k pravému okraji štítku, aby u štítku vpravo nevyjela z bloku. Šipka je vpravo a `min-width` je automatická (obsah je krátký).
- **Štítek jednání se vykreslí vždy.** Bez data jednání má text „Jednání č. N“. Dnes se bez data nevykreslí vůbec, a pak by autor a datum nebyly k dispozici.

### 5.2 Vzhled (jen tokeny gov / `--app-*`)

| Prvek | Pravidlo |
|---|---|
| Blok `.record-comments .comment-list` | `border: 1px solid var(--border-subtle)`, `border-radius: var(--gov-radius)`, `background: var(--background-block-primary)`, `padding: 4px 0`. Bez `overflow: hidden`, aby se bublina neořízla. |
| Řádek `.record-comments .comment` | bez rámečku a stínu, `padding: 6px 12px`, `margin: 0`, `display: flow-root` (obsáhne float štítku bez ořezu), `position: relative` |
| Řádek při najetí nebo fokusu | `.comment:hover, .comment:focus-within { background: var(--background-neutral-subtle); }`, tedy **jemně šedá**: světlý motiv `neutral-100` = rgb(231, 231, 231) na bílém bloku, tmavý `neutral-900` na `neutral-950`. Ne `--background-neutral-subtlest`: ten je ve světlém motivu téměř bílý (246) a v tmavém **stejný jako pozadí bloku**, takže by podbarvení nebylo vidět. |
| Text | 14 px jako dnes, `.richtext-render` (řádkování 1,4). První a poslední odstavec bez vnějšího okraje, ať jednořádkový text nepřidává výšku. |
| Štítek jednání | beze změny (`.comment-meeting-chip`) + `:focus-visible { outline: 2px solid var(--border-primary); outline-offset: 2px; }` |
| Ikony Upravit a Smazat | `.comment-row-actions`: absolutně umístěné vlevo od štítku, svisle na řádku štítku, pozadí `var(--background-neutral-subtle)` jako podbarvený řádek (zakryje konec textu pod sebou). Klidový stav `opacity: 0; pointer-events: none`. Při `.comment:hover` a `.comment:focus-within` `opacity: 1; pointer-events: auto`. Tlačítka zůstávají v pořadí pro Tab: fokus na nich řádek „otevře“. Ikony jsou pm-button Ghost, malé, tužka a koš (koš v barvě chyby jako dnes). |
| Řádek v režimu úpravy (`data-editing="true"`) | pozadí jako při najetí trvale, štítek, bublina i ikony skryté, editor přes celou šířku jako dnes |

Bez `:has`, `max-content` a přechodů navíc. `:hover` a `:focus-within` fungují v Edge na i15 (memory „Edge na i15“).

### 5.3 Chování

- **Najetí myší na řádek:** podbarví se a ukážou se ikony Upravit a Smazat (jen u vyjádření, které uživatel smí upravit, pravidla beze změny). Výška ani text se nehnou.
- **Najetí na štítek jednání:** ukáže se bublina „Autor“ + „Zapsáno dd.MM.yyyy“. Text autora je stejný jako dnes (jméno, u koho je i e-mail).
- **Klávesnice:** Tab projde štítkem (ukáže bublinu, autora čte i čtečka přes `aria-describedby`), pak tužkou a košem (řádek se podbarví a ikony ukážou).
- **Dotykový displej:** klepnutí na štítek ukáže bublinu (fokus). Klepnutí do řádku ho podbarví a ukáže ikony (prohlížeč emuluje `:hover`).
- **Upravit:** jako dnes se editor otevře v řádku přes celou šířku, Zrušit a Uložit beze změny. Po uložení se panel obnoví ze serveru a kontext jednání zůstane (2026-10-21).
- **Smazat:** potvrzovací dotaz jako dnes.
- **Řazení** (vzestupně a sestupně podle jednání) přesouvá přímo řádky `[data-comment-item]`. Obal `[data-comment-wrapper]` odpadne. `comments.js` to už dnes umí, takže se logika nemění.
- **Počet a stránkování (R10, R11):** panel otevře posledních 10 vyjádření. „Další (5)“ (vzestupně „Předchozí (5)“) přidá 5 starších, „Zobrazit vše“ načte všechna; nově načtená se přidají do stejného bloku. Obě tlačítka jsou v hlavičce vlevo od řazení a po přepnutí řazení zůstanou na místě. Obnovení panelu po uložení dál drží počet načtených (`recordRefresh.js`), výchozí limit bez parametru je 10 i na stránce záznamu a v detailu jednání.
- **Cíl z vyhledávání** (rozbalení karty, posun na vyjádření, zvýraznění hledaného textu): beze změny.

### 5.4 Kdo uvidí ikony Upravit a Smazat (pravidla beze změny)

Ikony se vykreslí jen u vyjádření, kde dnes platí `LzeUpravit && (CanEditRecord || (CanEditOwnAsSubsystemLeader && autor = já))`:
- `LzeUpravit` = jednání **není uzavřené** (`MeetingStatePolicy.IsReadOnly`: kód CLOSED, název „uzav…“ nebo zamčené). Server totéž hlídá v `CommentService.EnsureMeetingAllowsCommentChangesAsync`.
- `CanEditRecord` = `records.edit` v projektu (Superadmin, Administrátor aplikace, Vlastník, Admin projektu, PM).
- `CanEditOwnAsSubsystemLeader` = jednání je **v přípravě**. Spolu s podmínkou „autor = já“ dává ikony u **vlastního** vyjádření v přípravě **komukoli**, nejen vedoucímu (název vlastnosti je zavádějící; ověřeno průchodem 2026-10-22).

**Ověřeno 2026-10-22 v prohlížeči nad dnešním kódem** (osoba Admin aplikace + PM a osoba PM + vedoucí; karta v záložce Záznamy, detail jednání, stránka záznamu):
- vyjádření k uzavřenému jednání: 0 ikon;
- vyjádření k otevřenému jednání: tužka i koš;
- podvržená úprava i smazání vyjádření k uzavřenému jednání: server odmítl (HTTP 400, „Vyjádření u uzavřeného jednání nelze upravovat ani mazat. Nejprve jednání otevřete.“).

Hlídá to dnes Api test `ClosedRecordCommentsRenderTests.CommentInClosedMeeting_HasNoEditOrDeleteControls` (vykreslení). Odmítnutí na serveru dnes žádný test nehlídá, plán ho doplní.

**Známý nesoulad UI × server, tato změna ho neřeší** (potvrzeno průchodem 2026-10-22 podvrženými požadavky; `CommentAuthorizationPolicy.CanModifyComment`):

| Vlastní vyjádření | UI (ikony) | Server (úprava) |
|---|---|---|
| Gestor / Metodik, jednání v přípravě | ukáže | **odmítne** („Nemáte oprávnění upravit toto vyjádření.“) |
| Gestor / Metodik / Vedoucí, otevřené jednání | neukáže | **povolí** |
| Vedoucí / Zástupce, jednání v přípravě | ukáže | povolí |

Je to stav před touto změnou a zůstává. Rozhodne se zvlášť (sjednotit UI na `CanModifyComment`, nebo zúžit server).

## 6. Rozsah

**Mění se:**
- `Views/Projekty/_ZaznamCommentsPartial.cshtml`: markup seznamu a řádku.
- `wwwroot/css/site.css`:
  - nová pravidla bloku, řádku, ikon a `.chip-tooltip--end`, vše omezené na `.record-comments`;
  - smazat pravidla, která po změně nic nepoužívá (`.comment-wrapper`, `.comment-meta-outer`, `.comment-author`, `.comment-sep`, `.comment-date`, `.comment-tile-side`), ale jen pokud je opravdu nic jiného nepoužívá (grep);
  - žádná duplicitní pravidla (memory „Duplicate CSS“).
- `wwwroot/js/modules/comments.js`: logika řazení řádků beze změny. Řazení už dnes přesouvá samotné `[data-comment-item]`, když obal `[data-comment-wrapper]` chybí (fallback pro `_TaskItemPartial`). Aktualizuje se komentář v kódu. **Odpadne přesun bloku stránkování** mezi hlavičkou a seznamem podle směru (R11).
- `Services/ProjectService.LazyQueries.cs`: výchozí počet vyjádření 10 (`RecordCommentsInitialCount`), krok „Další“ zůstává 5 (`RecordCommentsLoadStep`) (R10).
- `docs/specs/record-comments.md`: počet a poloha tlačítek podle R10 a R11.

**Projeví se na:** kartě záznamu v záložce Záznamy, v detailu jednání a na stránce záznamu.

**Nemění se:**
- Přepínač řazení a texty tlačítek Další / Předchozí / Zobrazit vše (mění se jen jejich místo, R11), formulář nového vyjádření včetně předvolby jednání.
- Pravidla, kdo smí vyjádření přidat, upravit a smazat.
- Tisk, PDF, Word a zápis jednání.
- Okno vyjádření tiketu ze ServiceDesku (bubliny kroků).
- Starý `Views/Jednani/_TaskItemPartial.cshtml` (sekce „Zápis úkolů“ v UI od 2026-07-08 není). Nová pravidla jsou omezená na `.record-comments`, takže ho nezasáhnou.
- Značky „má / nemá vyjádření“ (rozhodnutí R7 z 2026-10-21).

## 7. Okrajové případy

| Případ | Očekávané chování |
|---|---|
| Dlouhé vyjádření (několik řádků) | Text obtéká štítek vlevo a pod ním pokračuje přes celou šířku jako dnes. Řádek je vysoký jako text. |
| Velmi dlouhé slovo nebo URL | Zalomí se (`overflow-wrap: anywhere`), blok nepřeteče. |
| Rich text (tučné, odkaz, seznamy, odsazení) | Vykreslí se jako dnes. Okraje odstavců uvnitř zůstávají, jen první a poslední bez vnějšího okraje. |
| Poslední vyjádření v bloku a bublina | Bublina vyjede pod blok přes formulář nebo okraj karty. Karta má `overflow: visible`, blok ani řádek nesmí ořezávat. |
| Štítek u pravého okraje úzkého okna (375 px) | Bublina zarovnaná vpravo (`--end`) s `max-width` do šířky okna, bez vodorovného posuvníku stránky. |
| Uživatel bez práva upravit | Žádné ikony, řádek se při najetí jen podbarví. |
| Vyjádření bez data jednání | Štítek „Jednání č. N“, bublina funguje. |
| Uzavřený záznam | Vyjádření se zobrazí stejně, nové nejde přidat (beze změny). |
| Jedno vyjádření | Blok s jedním řádkem, řazení se neukazuje (beze změny). |
| 10 a méně vyjádření | Žádná tlačítka stránkování, v hlavičce jen řazení. |
| 11 a více vyjádření | 10 posledních, v hlavičce vlevo od řazení „Další (5)“ / „Předchozí (5)“ a „Zobrazit vše“. Po načtení všech tlačítka zmizí. |
| Žádné vyjádření | „Zatím bez vyjádření.“ bez rámečku bloku (beze změny). |
| Tmavý motiv | Jen tokeny, podbarvení i bublina čitelné (kontrola v průchodu). |

## 8. Testy

- **Api (render panelu):**
  - jeden blok `data-comment-list` s počtem `data-comment-item` jako dnes; žádné `comment-meta-outer` ani `comment-wrapper`;
  - každý řádek má štítek s `tabindex="0"` a `aria-describedby`, které ukazuje na `role="tooltip"` s autorem a datem;
  - vyjádření bez data jednání má štítek;
  - ikony Upravit a Smazat jen u vyjádření, která uživatel smí upravit (PM ano, host ne). Kotvy na atributy, ne na český text (memory);
  - bez parametru 10 nejnovějších vyjádření z 12 a `data-record-comments-load-step="5"` (R10);
  - tlačítka stránkování jsou uvnitř `.record-comments-header` před přepínačem řazení, žádné pod seznamem (R11).
- **Unit (zdroj CSS):**
  - ikony řádku jsou v klidu skryté a ukáže je `:hover` nebo `:focus-within` řádku;
  - blok ani řádek nemají `overflow: hidden`;
  - pravidla jsou omezená na `.record-comments`;
  - `AppCssColorTokenTests` zelené.
- **Api (server, zafixovat dnešní chování):** `UpdateComment` i `DeleteComment` vyjádření k uzavřenému jednání vrátí `ok=false` za PM. Projde už dnes a hlídá, že to platí i po změně.
- **Řazení:** E2E klik na „Řazení“ otočí pořadí řádků v bloku (JS testy v `tests/js` nemají DOM).
- **E2E:**
  - 5 jednořádkových vyjádření má seznam ≤ 200 px;
  - najetí na štítek ukáže bublinu s autorem, odjetí ji schová;
  - najetí na řádek ukáže tužku, klik otevře editor a uložení projde;
  - Tab na štítek ukáže bublinu;
  - 12 vyjádření: 10 řádků, tlačítka stránkování v hlavičce vlevo od řazení v obou směrech; „Další“ / „Předchozí“ → 12 a tlačítka zmizí (R10, R11); `RecordRichTextScenariosTests` stránkování 10 → 15 → vše;
  - stávající E2E vyjádření (`RecordRichTextScenariosTests`, `RichTextUnicodeScenariosTests`, `SearchResultTargetScenariosTests`) beze změny výsledku.
- **Průchod v prohlížeči nad spuštěnou aplikací za všechny role** (stálé pravidlo, uživatel 2026-10-22: „všichni budou mít co mají mít“):
  - **Role (12 person):** 11 rolí (Superadmin, Administrátor aplikace, Read-all, Vlastník, Admin projektu, PM, Host, Gestor, Vedoucí, Zástupce vedoucího, Metodik), dále PM + vedoucí v jedné osobě a uživatel bez role v projektu. Každá role má vlastní dočasnou osobu (memory „Průchod za všechny role“).
  - **Data:** jeden záznam vlastního subsystému vedoucích. Vyjádření od různých autorů (včetně vedoucího a zástupce) k jednání v přípravě, k otevřenému a k uzavřenému jednání.
  - **Místa:** karta v záložce Záznamy, detail otevřeného jednání a stránka záznamu.
  - **Za každou personu a místo se ověří, co se má vidět:**

    | Role | Blok vyjádření | Bublina na štítku | Ikony Upravit a Smazat při najetí |
    |---|---|---|---|
    | Superadmin, Admin aplikace, Vlastník, Admin projektu, PM, PM + vedoucí | ano | u všech vyjádření | u všech vyjádření k jednání v přípravě a otevřenému; **u uzavřeného ne** |
    | Vedoucí, Zástupce vedoucího, Gestor, Metodik | ano | u všech | jen u **vlastního** vyjádření k jednání **v přípravě**; jinde ne (dnešní pravidlo UI, nesoulad se serverem viz 5.4) |
    | Host, Read-all | ano | u všech | nikde (vyjádření nepíšou) |
    | Bez role v projektu | stránka neexistuje (404) | – | – |

  - **Akce:** kdo ikony má, jedno vyjádření upraví (najetí → tužka → uložit) a jedno smaže. U uzavřeného jednání ikony ani při najetí nejsou a podvržený požadavek server odmítne.
  - **Stránkování (jednou, nezávisí na roli):** záznam s 12 vyjádřeními ukáže 10, tlačítka v hlavičce vlevo od řazení v obou směrech, „Další“ přidá zbylá 2.
  - **Vzhled:** šedé podbarvení při najetí ve světlém i tmavém motivu, Tab na štítek ukáže bublinu, 1400 px a 375 px bez vodorovného posuvníku, konzole bez chyb. Snímek před a po se změřenou výškou.
  - **Úklid:** dočasné osoby a data se po průchodu smažou, před průchodem záloha DB.

## 9. Rizika

- **Autor a datum nejsou vidět na první pohled.** Je to záměr zadání (R3). Kdo je potřebuje, najede na štítek. Tisk a PDF je dál obsahují.
- **Ikony při najetí zakryjí konec prvního řádku textu.** Jen po dobu najetí a jen u řádku s myší. Text pod nimi zůstává v DOM i pro vyhledávání.
- **Bez čar mezi vyjádřeními** můžou dvě dlouhá vyjádření pod sebou splývat. Začátek každého ukazuje štítek jednání vpravo nahoře. Kdyby to v praxi vadilo, jde doplnit jemnou dělicí čáru (mimo toto zadání).
