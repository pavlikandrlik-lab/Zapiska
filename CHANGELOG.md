# Changelog

Tento soubor je generován skriptem `scripts/generate-changelog.sh` z verzovaných podkladů v `docs/changelog/releases/`.

## 0.5 - 2026-03-04

### Přidáno
- Přehled projektů obsahuje malý filtr `Skrýt hotové` a `Skrýt smazané` s uložením volby do tohoto prohlížeče.

### Změněno
- Filtry v sekci `Záznamy` používají pro `Mé záznamy` a `Aktivní úkoly` sjednocený GOV switch styl.
- Sekce `Účast` v detailu jednání je výchozí sbalená do kompaktního souhrnu a lze ji rozbalit jen při potřebě úprav.
- Ovládací prvky `Uložit stav` a `Uzavřít jednání` jsou v detailu jednání srovnané do jednoho akčního bloku.

### Opraveno
- Uložení nového záznamu v modalu i na samostatné stránce už nepadá na `400 Bad Request`, pokud v databázi chyběla aktivní harmonogramová šablona.
- První uložení záznamu na čerstvé databázi automaticky vytvoří chybějící aktivní harmonogramovou šablonu a její výchozí kroky.


## 0.4 - 2026-03-03

### Přidáno
- Profil obsahuje samostatnou volbu pro zrušení uloženého výchozího otevření editoru záznamu.
- Harmonogram zobrazuje rozpad kroků přímo v projektovém detailu bez nutnosti přechodu do samostatné GANTT záložky.

### Změněno
- Otevření editoru záznamu používá jednu výchozí volbu uloženou v tomto prohlížeči; chooser se ukáže jen pokud volba ještě není nastavená.
- Dialog volby otevření editoru používá negativní checkbox `Neukládat pro tentokrát jako výchozí volbu`.
- Samostatná záložka `GANTT` byla sloučena do záložky `Harmonogram`.
- Harmonogram byl zjednodušen na filtrování podle subsystému a zvýrazňuje plán jako základní vrstvu a skutečnost jako překryv.
- Indikátor aktuálního subsystému v přehledu záznamů je nově boční rail s pohyblivou bublinou místo plovoucího boxu.

### Opraveno
- Tlačítka pro otevření editoru záznamu už nepoužívají split-button se šipkou.
- Uložené výchozí otevření editoru záznamu lze vyčistit z profilu bez zásahu do ostatních preferencí.


## 0.3 - 2026-03-02

### Přidáno
- Aplikace zobrazuje svou verzi ve footeru.
- Detail projektu má nové klientské filtry `Jen mé záznamy` a `Jen mé úkoly` pro panely Záznamy, Harmonogram a GANTT.
- Aktivní projektové filtry se zobrazují jako odebíratelné čipy.
- Uživatel si může uložit výchozí projektové filtry do local storage a smazat je v profilu.

### Změněno
- Přepínač `Seskupit dle subsystému` byl přesunut z hlavičky panelu do sekce filtrů a sjednocen do gov switch stylu.
- Viditelnost hlavních navigačních záložek `Osoby`, `Číselníky` a `Nastavení` se nově řídí prefixy oprávnění `people.*`, `ciselniky.*` a `settings.*`.


