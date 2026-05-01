---
title: PDF versus Word versus HTML
description: Kdy použít který formát exportu.
---

# PDF versus Word versus HTML

Aplikace umí exportovat ve třech formátech. Každý má svoje pro a proti.

## Srovnávací tabulka

| Vlastnost | PDF | Word (.docx) | HTML |
|---|---|---|---|
| **Stabilní vzhled** | ✅ napříč zařízeními | ⚠️ závisí na verzi MS Office / LibreOffice | ⚠️ závisí na browseru |
| **Editovatelnost** | ❌ jen čtení | ✅ ano | ⚠️ jen přes vývojářské tools |
| **Velikost souboru** | Střední | Větší (vložené styly) | Malá |
| **Otevírání** | Adobe Reader / browser | MS Word / LibreOffice / Pages | Jakýkoli browser |
| **Tisk na papír** | ✅ přímý | ✅ přes Word print | ⚠️ záleží na print CSS |
| **Distribuce mailem** | ✅ ideální | ⚠️ velikost, kompatibilita | ⚠️ otevře se v browseru |
| **Archivace** | ✅ doporučený formát | ⚠️ formát se mění verzemi | ❌ rozsypaný layout po čase |
| **Sdílení odkazem** | Stáhne se | Stáhne se | Otevře přímo |

## Kdy použít PDF

**Default volba pro většinu případů.** Použij když:

- Distribuuješ dokument více lidem (mail, sdílený disk)
- Archivuješ pro audit / kontrolu
- Tiskneš na papír
- Chceš stabilní vzhled napříč zařízeními

## Kdy použít Word

Použij když:

- Potřebuješ **dále upravit** obsah (dopsat poznámky, změnit hlavičku, doplnit
  podpisové bloky)
- Cílový příjemce **standardně pracuje ve Wordu**
- Vytváříš šablonu pro opakované použití

⚠️ **Pozor na verze:** docx z PM Trackeru otevře MS Word (Office 2016+) i
LibreOffice. Hodně staré klienty (Office 2007 a starší) můžou mít problémy
s formátováním.

## Kdy použít HTML

Použij když:

- **Rychlý náhled v browseru** bez stahování
- Chceš **kopírovat tabulky / text** do jiných aplikací (Excel, Confluence)
- Chceš **embed** do interní wiki / SharePointu

⚠️ **HTML není ideální pro archivaci** — chybí self-contained styly, browser
rendering se mění verzemi.

## Ekvivalence obsahu

**Obsah je stejný napříč formáty.** PDF a Word a HTML mají všechny ty samé
sekce, ten samý text, ty samé tabulky. Liší se jen vizuální stránkou a
možnostmi další úpravy.

## Výchozí formát

V profilu si nastav výchozí formát — pak ti aplikace přestane zobrazovat dialog
*Vyber formát* a rovnou stáhne soubor v preferred formátu. Detail:
[Profil → Uživatelské nastavení](../profil/nastaveni-uzivatele.md).

## Speciální případy

### NES panel — pouze Excel

NES panel projektového dashboardu lze exportovat **jen do Excelu (.xlsx)**.
Důvod: tabulkový formát s číselnými sloupci je primárně tabulkový pohled.
Detail: [Export NES panelu](../projekty/projektovy-dashboard/nes-v-prodleni/export-excel.md).

### Tisk přímo (Ctrl+P)

V aplikaci jsou některé stránky uzpůsobené pro **prohlížečový print** (Ctrl+P)
přes print CSS. Tato cesta nepoužívá generování PDF na serveru, jen tiskne
aktuálně zobrazenou stránku. Vhodné pro rychlý ad-hoc tisk.

## Související

- [Tisk projektu](tisk-projektu.md)
- [Tisk jednání](tisk-jednani.md)
- [Tisk úkolu](tisk-ukolu.md)
