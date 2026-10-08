---
title: Globální vyhledávání
description: Vyhledávání záznamů napříč projekty podle názvu, popisu, vyjádření a čísel externích záznamů.
---

# Globální vyhledávání

PM Tracker hledá **záznamy** napříč projekty. Jednotkou výsledku je vždy záznam —
i když se hledaný výraz našel v navázaném vyjádření nebo v čísle externího záznamu,
ve výsledku uvidíš ten záznam, ke kterému patří.

Hledá se v:

- **názvu** a **čísle** záznamu (např. `RU 123`),
- **popisu** a **cíli** záznamu,
- **textu navázaných vyjádření** (u výsledku se ukáže i číslo jednání),
- **čísle navázaného externího záznamu** (ServiceDesk ticket apod.).

## Jak otevřít

- **Globální pole v hlavičce** aplikace (vždy dostupné). Po napsání dotazu se pod
  polem rozbalí nabídka (dropdown) s nejlepšími výsledky.
- **Stránka výsledků** `/Search/Index` — kompletní seznam, otevře se odesláním
  vyhledávacího pole (Enter).

## Po kliknutí na výsledek

Otevře se detail projektu na záložce Záznamy, rozbalí se nalezený záznam a stránka
na něj sjede. Hledaná slova se na kartě záznamu **na 15 sekund podsvítí žlutě**,
pak podsvícení samo zmizí.

Pokud se shoda našla ve **vyjádření**, stránka sjede rovnou na to vyjádření. Platí to
i tehdy, když je starší a mezi prvními načtenými by nebylo: aplikace v takovém
případě načte všechna vyjádření záznamu.

Podsvítí se jen doslovná shoda, velikost písmen nehraje roli. Na diakritice zde
záleží: dotaz „reseni“ záznam se slovem „řešení“ najde, ale slovo nepodsvítí.

## Filtrace přístupovými právy

Výsledky se **filtrují podle tvých přístupových práv**. Uvidíš záznam právě tehdy,
když máš přístup k jeho projektu — buď jsi v projektu obsazený, nebo ti přístup
dává role. Záznam z projektu, kam nemáš přístup, se ti nezobrazí, i kdyby textově
odpovídal — a to ani tehdy, když shoda padla v jeho vyjádření.

To znamená dvě věci:

- Bezpečnost — hledání „neunikne“ data mezi projekty.
- Diagnostika — když vyhledávání nenajde, co čekáš, ověř, že máš přístup k danému
  projektu.

## Jak hledání funguje

Hledá se přes `LIKE` přímo nad ostrými tabulkami (žádný index, výsledky jsou vždy
čerstvé). Produkční SQL Server nemá komponentu Full-Text Search a mít nebude
(rozhodnutí 2026-09-17), takže hledání **není fulltext** — nemá toleranci překlepů,
stop-words ani řazení podle relevance. Prakticky:

- **Hledá se od tří znaků.** Kratší dotaz nevrací nic.
- **Na diakritice ani velikosti písmen nezáleží** — „zalohovani“ najde „Zálohování“,
  „rizeni“ najde „Řízení“. Platí i pro písmena s háčkem (`č ř š ž`).
- **Hledá se na podřetězce, ne na slovní tvary** — „záznam“ najde „záznamu“
  i „záznamech“, ale „záznamu“ **nenajde** „záznam“. Zkus kratší tvar nebo kořen slova.
- **Víceslovný dotaz vyžaduje všechna slova**, ne nutně vedle sebe. Bere se prvních
  6 slov.
- **Slova kratší než tři znaky se vedle delších přeskakují** — spojky a předložky jako
  „a“, „v“, „na“. „stav migrace a dat“ hledá „stav“, „migrace“ a „dat“ a jen ta se po
  otevření výsledku podsvítí. Dotaz jen z krátkých slov („50 %“) se hledá celý.
- **Text v uvozovkách se hledá jako celek** (jako na Googlu) — `"stav migrace a dat"`
  najde jen záznamy, kde ta slova stojí přesně v tomto pořadí, a podsvítí je celá, včetně
  „a“. Jde kombinovat se slovy: `"stav migrace" dat`. Platí rovné i české uvozovky
  (`"…"`, `„…“`); fráze se počítá jako jedno slovo do limitu šesti.
- **`%` a `_` se berou doslova** — hledání „50 %“ hledá opravdu „50 %“.
- **Řazení je vzestupné** podle názvu (texty i čísla), bez skórování.

## Tipy pro efektivní hledání

- **Přesné znění** — dej text do uvozovek: `"oprava importu faktur"`.
- **Krátká zkratka vedle dalších slov** — dej ji do uvozovek, jinak se přeskočí:
  `"IS" migrace`.
- **Číslo externího záznamu** — zadáním 6-ciferného `id` ticketu (např. `363139`)
  najdeš záznam(y) s touto externí vazbou.
- **Část názvu** — „FIS“ najde „FIS-EIS Modernizace“.
- **Číslo záznamu** — např. `RU 123`.

## Nabídka pod polem (dropdown)

- Zobrazuje **7 nejlepších výsledků**.
- Každá položka má dva řádky: nahoře číslo a název záznamu (vpravo zkratka
  subsystému), dole úryvek okolo nalezené shody se **žlutě zvýrazněným** místem
  (u vyjádření je vpravo číslo jednání).
- Vyhledává se s krátkým zpožděním po dopsání (debounce), aby každý úhoz neposílal
  dotaz.

## Limity

- **Kratší dotaz než 3 znaky** se nevyhledává.
- **Speciální znaky** (`%`, `_`) se berou doslova, regulární výrazy nejsou podporované.
- **Fráze přes formátování** — Fráze v uvozovkách se najde i přes formátování (tučné
  slovo, odkaz) a zalomení řádku. Hledá se v textu bez HTML, takže hledání slova jako
  „strong“ nenajde formátovací značky.
- **Bez scope na jeden projekt** — globální hledání neumí omezit na projekt; pro to
  použij filtr v záznamech projektu.

## Performance

Vyhledávací endpoint má **rate limiter** (pevné okno, řádově desítky dotazů za
10 sekund; okno je zatím sdílené, ne per-uživatel). Při hodně rychlých dotazech
můžeš narazit na 429 Too Many Requests — proto ten debounce.

## Ověření chování na konkrétní databázi

Že hledání opravdu skládá i písmena s háčkem, ukáže `db_check_search_collation.sql` —
spustí se proti produkci, nic nemění a ve sloupci *Verdikt* má mít samé `OK`. Kvůli
těmto písmenům se porovnává přes `COLLATE Latin1_General_CI_AI`, ne přes českou
collation (`Czech_CI_AI` totiž `č ř š ž` neskládá).

## Související

- [Moje priority](moje-priority.md) — aktivní pracovní pohled (ne hledání)
- [Projekty](../projekty/) — listing s filtry, alternativa k vyhledávání
