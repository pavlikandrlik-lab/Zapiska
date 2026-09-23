# Průchod aplikací

Jak se uživatel aplikací pohybuje. Doplňuje [11-wireframy.md](11-wireframy.md) (co je kde)
o to, **co po čem následuje**.

---

## Navigační mapa

```
                          ┌──────────────────────┐
                          │  O1 Seznam číselníků │ ◄── vstup, vidí každý
                          └──────────┬───────────┘
                                     │
                          ┌──────────▼───────────┐
             ┌────────────┤  O2 Detail číselníku ├────────────┐
             │            └──────────┬───────────┘            │
             │                       │                        │
   ┌─────────▼────────┐   ┌──────────▼─────────┐   ┌──────────▼─────────┐
   │ O5 Editace       │   │ záložka Verze      │   │ O7 Struktura       │
   │    hodnot        │   │ záložka Rozdíl     │   │    (správce)       │
   └─────────┬────────┘   └────────────────────┘   └────────────────────┘
             │
   ┌─────────▼────────┐
   │ O6 Import JSON   │  ── promítne se zpět do O5, nikdy přímo do dat
   └──────────────────┘

   Nezávisle na kontextu číselníku:
   O3 Vyhledávání · O8 Role · O9 Efektivní práva · O10 Audit · O11 Profil · O12 Wiki
```

**Pravidlo, které z mapy plyne:** ze všech editačních cest vede návrat do O2, ne do O1.
Kdo právě upravoval Cíle, chce vidět Cíle — ne seznam všech číselníků.

---

## Slovník akcí

Jedna akce se **jmenuje stejně po celou dobu**. Kdo klikl na *Publikovat verzi*, dozví se,
že bylo *publikováno*. Nikdy „operace proběhla úspěšně".

| Tlačítko | Potvrzení | Co se skutečně stalo |
|---|---|---|
| Uložit | Změny uloženy | Rozpracované změny zapsány, konzumenti nic nepoznali |
| Publikovat verzi | Verze 3.15 publikována | Vydána verze, uvolněn zámek, konzumenti vidí nová data |
| Promítnout do tabulky | 12 hodnot promítnuto | Soubor se dostal do editační tabulky, ne do databáze |
| Přidat hodnotu | — | Nový řádek v tabulce, zatím neuložený |
| Vyřadit hodnotu | Platnost ukončena k 7. 9. 2026 | Nic se nesmazalo |
| Odebrat zámek | Zámek odebrán | Zapsáno do auditu |
| Založit číselník | Číselník Cíle založen | Existuje, zatím bez hodnot |

Slovo **„smazat" se v aplikaci nevyskytuje.** Nic se nemaže, a název tlačítka nesmí
tvrdit opak.

---

## W0 — První spuštění

Čerstvě nasazená aplikace nemá číselníky ani osoby. Kdokoli se přihlásí, je čtenář
bez práv — a nikdo tedy nemůže přidělit první roli.

**Řešení: řádek v databázi, ne instalátor.** Nasazení aplikace je kopírování souborů do
složky na IIS; žádný instalační program neexistuje a nemá vznikat. Databáze se ale **stejně
zakládá ručně spuštěním SQL skriptů** (rozhodnutí A5) — první správce je proto jen
další skript v téže posloupnosti, ne nový mechanismus.

```
1. zkopíruj publikované soubory do složky na IIS
2. založ databázi a spusť  db/db_baseline_0_1.sql
3. uprav a spusť           db/db_seed_prvni_superadmin.sql   ← doplň doménový login
4. spusť aplikaci
```

Skript založí osobu a vloží ji do tabulky `superadmini`. Ten člověk se přihlásí,
přidělí role ostatním a od té chvíle se superadmin k běžné práci nepoužívá.

> **Superadmin není role.** Je to nouzový klíč, který zkratuje kontrolu oprávnění —
> viz [02-role-a-opravneni.md](02-role-a-opravneni.md).

---

## W1 — Návštěvník poprvé

Doménový uživatel bez záznamu v aplikaci. Nezakládá si nic, nikam se nehlásí.

1. Otevře adresu aplikace. Windows ho přihlásí samo.
2. Vidí **rail se všemi číselníky** a seznam s počty hodnot a verzemi.
3. Klikne na číselník → vidí hodnoty, strukturu, verze i historii. **Všechno.**
4. Nikde nevidí `Upravit hodnoty` ani `+ Nový číselník`.

**Co v tomhle průchodu nesmí nastat:** žádná výzva k přihlášení, žádná žádost o přístup,
žádná prázdná stránka s hláškou o chybějícím oprávnění. Přesně tyhle bariéry aplikace
odstraňuje.

---

## W2 — Vývojář hledá, jak číselník načíst strojově

1. Najde číselník v seznamu, otevře **záložku Struktura**.
2. Tam je odkaz na **popis struktury pro konzumenty** (JSON Schema) a příklad volání.
3. Zjistí stálý identifikátor číselníku a adresu, na které se hodnoty berou.
4. Ve vlastní aplikaci se přihlásí doménovým servisním účtem a číselník si načte.

Aby to fungovalo, musí být na záložce Struktura **adresa rozhraní vidět** — ne schovaná
v dokumentaci, kterou nikdo nenajde.

---

## W3 — Editor mění hodnotu a publikuje

Nejčastější průchod v celé aplikaci.

1. V railu klikne na **Rozpočtové cíle**.
2. Klikne `Upravit hodnoty`. Aplikace si vyžádá **zámek**.
   - Drží-li ho někdo jiný → viz W6, dál se nejde.
3. V tabulce přepíše stav cíle `C-2025-014` z `A` na `U`.
   Řádek se označí `~` a zvýrazní.
   Spodní lišta ukáže **1 neuložená změna**.
4. Rozhodne se, co dál:
   - `Uložit` → změna je zapsaná jako **rozpracovaná**. Konzumenti nic nepoznali.
     Může zavřít prohlížeč a vrátit se zítra.
   - `Publikovat verzi` → vznikne **3.15**, konzumenti dostanou nová data, zámek se uvolní.
5. Chce-li si před publikováním změny projít, klikne `Zobrazit změny` — otevře se seznam.
   **Není to povinný krok**, jen nabídnutý.

**Co dělá tenhle průchod bezpečným:** mezi „přepsal jsem hodnotu" a „všechny konzumující
aplikace to vidí" stojí vědomé kliknutí. Kdyby se verze vydávala sama při každém uložení,
byl by referenční zdroj závislý na tom, jak přesně editor píše.

---

## W4 — Editor naimportuje číselník z vyhlášky

Typicky poté, co nechal jazykový model přepsat PDF do dohodnutého tvaru JSON.

1. V editaci klikne `Nahrát soubor JSON`.
2. **Krok 1** — přetáhne soubor.
3. **Krok 2** — aplikace ho celý ověří.
   - Chyby → vypíše **všechny najednou** s uvedením položky a toho, co je špatně.
     Uživatel opraví soubor a nahraje znovu. Nic se nezměnilo.
   - Bez chyb → ukáže, co se stane: kolik přibude, změní se a vyřadí.
4. **Krok 3** — `Promítnout do tabulky`. Soubor **nejde do databáze**, jde do editační
   tabulky ze W3 se zvýrazněnými změnami.
5. Odsud pokračuje jako W3: může ještě ručně sáhnout do dat, pak `Uložit`
   nebo `Publikovat verzi`.

**Proč to nekončí uložením:** u tisícovky hodnot je jediná obrana proti tichému rozbití dat
překlepem v přepisu to, že se výsledek nejdřív ukáže.

---

## W5 — Správce zakládá nový číselník

1. `+ Nový číselník` v railu. Zadá kód, název a popis.
2. Zvolí **režim správy** — ručně, nebo externě. Volba je zásadní: u externího nebude
   nikdy možná ruční editace.
3. Na záložce **Struktura** přidá atributy (kód, název, typ, povinnost)
   a vazby (kód, název, cílový číselník).
4. Přejde na hodnoty a naplní je ručně, nebo importem podle W4.
5. `Publikovat verzi` → vznikne **1.0** a číselník je od té chvíle dostupný konzumentům.

Co se smí se strukturou dělat později a co ne:
[13-zivotni-cyklus-struktury.md](13-zivotni-cyklus-struktury.md).

**Do publikování první verze číselník přes rozhraní neexistuje.** Nedokončený číselník
nemá co nabízet.

---

## W6 — Editor narazí na zamčený číselník

1. Klikne `Upravit hodnoty`.
2. Editace se **neotevře**. Nahoře je pruh:
   *„Číselník upravuje Eva Dvořáková od 14:32. Otevřít pro úpravy zatím nejde.
   Zámek se sám uvolní po pěti hodinách bez její činnosti."*
3. Obsah zůstává v režimu čtení — může si data prohlédnout, jen do nich nesáhne.
4. Správce vidí navíc `Odebrat zámek`; odebrání se zapisuje do auditu.

**Proč hláška mluví i o uvolnění:** bez té věty uživatel neví, jestli má čekat minutu,
nebo psát správci. Nejlevnější odpověď na otázku je tu napsat ji rovnou.

---

## W7 — „Co se změnilo od minule"

Ptá se na to jak uživatel, tak vývojář konzumující aplikace.

1. Detail číselníku → záložka **Rozdíl verzí**.
2. Vybere dvě verze a porovná.
3. Vidí souhrn (kolik přidaných, změněných, vyřazených) a pod ním **změny seskupené
   po položkách** — u každé konkrétní atribut a hodnotu před a po.

Vodorovná čára v seznamu verzí odděluje změny hlavního čísla. Kdo hledá „změnilo se něco,
co se mě může dotknout", najde odpověď dřív, než začne porovnávat.

---

## Průřezová pravidla průchodu

| Pravidlo | Důvod |
|---|---|
| **Návrat vede do detailu číselníku**, ne na seznam | Uživatel drží kontext jedné věci |
| **Modály se zavírají jen křížkem** | Tažení myší z pole ven jinak zavře rozdělanou práci |
| **Ovládací prvek, na který uživatel nemá právo, chybí** — není zašedlý | Zašedlé tlačítko slibuje, že to jednou půjde |
| **Rozpracované změny přežijí odhlášení i zavření prohlížeče** | Patří číselníku, ne osobě ani sezení |
| **Žádná obrazovka nekončí slepě** | Z každé vede zpět do detailu nebo na seznam |

---
