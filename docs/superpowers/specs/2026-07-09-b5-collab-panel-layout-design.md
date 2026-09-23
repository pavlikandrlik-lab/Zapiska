# B5 — Spolupráce: kompaktní řádky (jméno + org. celek, bez emailu)

**Datum:** 2026-07-09 · **Stav:** schváleno uživatelem (analýza 2026-07-09)

## Problém a kontext okolí
`_EditZaznamCollaborationPanel.cshtml` renderuje v `.collab-option` tři spany POD sebou: jméno / email / `Organizace / OrgCelek` → řádek je ~3× vyšší než nutno, na stránku se vejde málo osob.

## Řešení
Jeden řádek na osobu: checkbox + **jméno** (tučně) + **org. celek** (tlumeně, za jménem). Email z výpisu odstranit; `data-collab-label` (searchable string, ř. 14) **zachovat včetně emailu** — vyhledávání podle emailu funguje dál. Organizaci (nadřazenou) z výpisu vypustit (user: „jméno a org celek"); zůstává v searchable.

Markup `.collab-option`:
```razor
<span class="collab-option-name">@osoba.Osoba</span>
<span class="collab-option-org">@(osoba.OrganizacniCelek ?? "-")</span>
```
CSS: `.collab-option { display:flex; align-items:center; gap:8px; }`, name `flex:0 1 auto`, org `color: var(--pm-text-muted); margin-left:auto;` (org zarovnaný vpravo — čitelný sloupcový dojem). Smazat `.collab-option-email` + staré stacking pravidlo (grep duplicit vč. dark-mode).

## Dotčené soubory
- `PmTracker.Web/Views/Projekty/_EditZaznamCollaborationPanel.cshtml`
- `PmTracker.Web/wwwroot/css/site.css` (collab-option blok)

## Akceptační kritéria
- Řádek osoby je jednořádkový (výška ≤ ~36 px), obsahuje jméno + org celek, žádný email.
- Hledání „@" / část emailu dál filtruje správné osoby.
- Checkbox chování beze změny (výběr, persist při save).
- V decision módu (B4) disabled vzhled funguje na novém layoutu.

## Testy
- Api render: panel obsahuje `collab-option-org`, NEobsahuje `collab-option-email`.
- E2E/skript: filtrace podle emailu najde osobu; měření výšky řádku.

## Mimo scope
Změna datového zdroje kandidátů; stránkování seznamu.
