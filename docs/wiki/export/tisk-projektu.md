---
title: Tisk projektu
description: Souhrn projektu jako PDF, HTML nebo Word.
---

# Tisk projektu

Vytvoří **souhrnný export projektu** — jeden dokument se všemi relevantními
informacemi.

## Kde najít tlačítko

- **Detail projektu** → tlačítko *Tisk* v hlavičce
- Případně ikona tiskárny v hlavním menu detailu

## Co je v exportu

Default obsah:

### Hlavička

- Název projektu, zkratka
- Stav, datum založení, vlastník
- Informační systém (pokud je propojení FIS / ISSP)

### Tým

- Přehled členů týmu
- Projektové role
- Subsystémy

### Záznamy

Tabulka záznamů projektu se základními sloupci (Typ, Název, Stav, Termín,
Vlastník). Volitelně lze rozšířit o popis.

### Jednání

Seznam jednání s daty a účastníky. Volitelně i body programu.

### Harmonogram

Tabulka kroků s plánem a skutečností.

## Volby exportu

V dialogu *Tisk* lze volit:

- **Formát** — PDF / HTML / Word
- **Co zahrnout / vyloučit**:
  - Tým (ano / ne)
  - Záznamy (všechny / jen aktivní / žádné)
  - Jednání (všechny / jen aktivní / žádná)
  - Harmonogram (ano / ne)
  - Komentáře u záznamů (ano / ne)

## Doba generování

- **Malé projekty** (< 50 záznamů) → ihned (do 1 sekundy)
- **Střední projekty** (50–500 záznamů) → několik sekund
- **Velké projekty** (> 500 záznamů) → desítky sekund. Aplikace zobrazí spinner.

Pokud generování trvá příliš dlouho a aplikace ho ukončí (timeout), zkus
omezit obsah ve volbách (např. vyloučit komentáře).

## Velikost souboru

Orientačně:

- PDF — 100 KB až několik MB (záleží na obsahu)
- Word — podobně, někdy větší (vložené styly)
- HTML — nejmenší

## Výchozí formát

V profilu lze nastavit výchozí formát — pak ti aplikace nezobrazí dialog,
rovnou stáhne soubor v preferred formátu. Detail:
[Profil → Uživatelské nastavení](../profil/nastaveni-uzivatele.md).

## Permission

Tisk projektu **nemá vlastní permission key**. Pokud máš `projects.view`,
můžeš si tisknout.

## Související

- [PDF versus Word](pdf-vs-word.md)
- [Tisk jednání](tisk-jednani.md)
- [Tisk úkolu](tisk-ukolu.md)
