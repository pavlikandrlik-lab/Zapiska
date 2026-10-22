# Vyjádření v jednom kompaktním bloku — specifikace

**Datum:** 2026-10-22
**Stav:** návrh k odsouhlasení
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

## 5. Návrh

### 5.1 Struktura

```
Vyjádření                                   [Řazení: jednání vzestupně]
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
[Další (5)] [Zobrazit vše]
[editor nového vyjádření]  [Vazba na jednání ▾] [Uložit]
```

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
| Řádek při najetí nebo fokusu | `.comment:hover, .comment:focus-within { background: var(--background-neutral-subtlest); }` |
| Text | 14 px jako dnes, `.richtext-render` (řádkování 1,4). První a poslední odstavec bez vnějšího okraje, ať jednořádkový text nepřidává výšku. |
| Štítek jednání | beze změny (`.comment-meeting-chip`) + `:focus-visible { outline: 2px solid var(--border-primary); outline-offset: 2px; }` |
| Ikony Upravit a Smazat | `.comment-row-actions`: absolutně umístěné vlevo od štítku, svisle na řádku štítku, pozadí `var(--background-neutral-subtlest)` (zakryje konec textu pod sebou). Klidový stav `opacity: 0; pointer-events: none`. Při `.comment:hover` a `.comment:focus-within` `opacity: 1; pointer-events: auto`. Tlačítka zůstávají v pořadí pro Tab: fokus na nich řádek „otevře“. Ikony jsou pm-button Ghost, malé, tužka a koš (koš v barvě chyby jako dnes). |
| Řádek v režimu úpravy (`data-editing="true"`) | pozadí jako při najetí trvale, štítek, bublina i ikony skryté, editor přes celou šířku jako dnes |

Bez `:has`, `max-content` a přechodů navíc. `:hover` a `:focus-within` fungují v Edge na i15 (memory „Edge na i15“).

### 5.3 Chování

- **Najetí myší na řádek:** podbarví se a ukážou se ikony Upravit a Smazat (jen u vyjádření, které uživatel smí upravit, pravidla beze změny). Výška ani text se nehnou.
- **Najetí na štítek jednání:** ukáže se bublina „Autor“ + „Zapsáno dd.MM.yyyy“. Text autora je stejný jako dnes (jméno, u koho je i e-mail).
- **Klávesnice:** Tab projde štítkem (ukáže bublinu, autora čte i čtečka přes `aria-describedby`), pak tužkou a košem (řádek se podbarví a ikony ukážou).
- **Dotykový displej:** klepnutí na štítek ukáže bublinu (fokus). Klepnutí do řádku ho podbarví a ukáže ikony (prohlížeč emuluje `:hover`).
- **Upravit:** jako dnes se editor otevře v řádku přes celou šířku, Zrušit a Uložit beze změny. Po uložení se panel obnoví ze serveru a kontext jednání zůstane (2026-10-21).
- **Smazat:** potvrzovací dotaz jako dnes.
- **Řazení** (vzestupně a sestupně podle jednání) přesouvá přímo řádky `[data-comment-item]`. Obal `[data-comment-wrapper]` v `comments.js` odpadne.
- **Další / Zobrazit vše:** beze změny, nově načtená vyjádření se přidají do stejného bloku.
- **Cíl z vyhledávání** (rozbalení karty, posun na vyjádření, zvýraznění hledaného textu): beze změny.

## 6. Rozsah

**Mění se:**
- `Views/Projekty/_ZaznamCommentsPartial.cshtml`: markup seznamu a řádku.
- `wwwroot/css/site.css`:
  - nová pravidla bloku, řádku, ikon a `.chip-tooltip--end`, vše omezené na `.record-comments`;
  - smazat pravidla, která po změně nic nepoužívá (`.comment-wrapper`, `.comment-meta-outer`, `.comment-author`, `.comment-sep`, `.comment-date`, `.comment-tile-side`), ale jen pokud je opravdu nic jiného nepoužívá (grep);
  - žádná duplicitní pravidla (memory „Duplicate CSS“).
- `wwwroot/js/modules/comments.js`: řazení bez obalu `[data-comment-wrapper]`.

**Projeví se na:** kartě záznamu v záložce Záznamy, v detailu jednání a na stránce záznamu.

**Nemění se:**
- Hlavička „Vyjádření“ s řazením, tlačítka Další a Zobrazit vše, formulář nového vyjádření včetně předvolby jednání.
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
| Žádné vyjádření | „Zatím bez vyjádření.“ bez rámečku bloku (beze změny). |
| Tmavý motiv | Jen tokeny, podbarvení i bublina čitelné (kontrola v průchodu). |

## 8. Testy

- **Api (render panelu):**
  - jeden blok `data-comment-list` s počtem `data-comment-item` jako dnes; žádné `comment-meta-outer` ani `comment-wrapper`;
  - každý řádek má štítek s `tabindex="0"` a `aria-describedby`, které ukazuje na `role="tooltip"` s autorem a datem;
  - vyjádření bez data jednání má štítek;
  - ikony Upravit a Smazat jen u vyjádření, která uživatel smí upravit (PM ano, host ne). Kotvy na atributy, ne na český text (memory).
- **Unit (zdroj CSS):**
  - ikony řádku jsou v klidu skryté a ukáže je `:hover` nebo `:focus-within` řádku;
  - blok ani řádek nemají `overflow: hidden`;
  - pravidla jsou omezená na `.record-comments`;
  - `AppCssColorTokenTests` zelené.
- **JS:** řazení přesouvá `[data-comment-item]` bez obalu (test v `tests/js/dom`).
- **E2E:**
  - 5 jednořádkových vyjádření má seznam ≤ 200 px;
  - najetí na štítek ukáže bublinu s autorem, odjetí ji schová;
  - najetí na řádek ukáže tužku, klik otevře editor a uložení projde;
  - Tab na štítek ukáže bublinu;
  - stávající E2E vyjádření (`RecordRichTextScenariosTests`, `RichTextUnicodeScenariosTests`, `SearchResultTargetScenariosTests`) beze změny výsledku.
- **Průchod v prohlížeči** (stálé pravidlo):
  - za role, které upravují (PM, admin projektu), za vedoucího subsystému (jen svoje v přípravě) a za hosta (jen čtení);
  - na kartě v záložce Záznamy, v detailu jednání a na stránce záznamu;
  - světlý a tmavý motiv, 1400 px a 375 px, konzole bez chyb;
  - snímek před a po se změřenou výškou.

## 9. Rizika

- **Autor a datum nejsou vidět na první pohled.** Je to záměr zadání (R3). Kdo je potřebuje, najede na štítek. Tisk a PDF je dál obsahují.
- **Ikony při najetí zakryjí konec prvního řádku textu.** Jen po dobu najetí a jen u řádku s myší. Text pod nimi zůstává v DOM i pro vyhledávání.
- **Bez čar mezi vyjádřeními** můžou dvě dlouhá vyjádření pod sebou splývat. Začátek každého ukazuje štítek jednání vpravo nahoře. Kdyby to v praxi vadilo, jde doplnit jemnou dělicí čáru (mimo toto zadání).
