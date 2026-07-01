# Vizuální oddělení návrhů — sub-karty

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this spec.

**Goal:** Jednotlivé návrhy (proposal-card) uvnitř sekcí "Návrhy založení záznamu" a "Návrhy změny termínu a harmonogramu" vizuálně oddělit jako sub-karty se samostatným orámováním.

**Problem:** Třídy `.proposal-card`, `.proposal-list`, `.proposal-card-header`, `.proposal-card-actions` a `.proposal-section-grid` existují v HTML (`_ProjectProposalsTab.cshtml`), ale nemají žádné CSS definice — návrhy se vizuálně slévají dohromady.

## Rozsah

Čistě CSS změna v `site.css`. Žádné úpravy Razor views, JS, ani ViewModelů.

## Nová CSS pravidla

### `.proposal-section-grid`

Kontejner dvou section.card (záznamy + harmonogram). Flex column layout s mezerou mezi sekcemi.

- `display: flex; flex-direction: column; gap: 16px;`

### `.proposal-list`

Kontejner `.proposal-card` článků uvnitř jedné sekce.

- `display: flex; flex-direction: column; gap: 12px;`

### `.proposal-card`

Sub-karta — každý návrh má vlastní vizuální hranici.

- `border: 1px solid var(--gov-color-border)`
- `border-radius: var(--gov-radius)` (konzistence s `.card`)
- `padding: 12px`

### `.proposal-card-header`

Flex row: název vlevo, badge stavu vpravo.

- `display: flex; justify-content: space-between; align-items: flex-start; gap: 12px;`

### `.proposal-card-actions`

Řádek akčních tlačítek.

- `display: flex; gap: 8px; margin-top: 8px;`

## Dark mode

Žádné explicitní dark-mode varianty nejsou potřeba — `var(--gov-color-border)`, `var(--gov-color-surface)` a `var(--gov-radius)` se přepínají automaticky přes `:root[data-theme="dark"]` gov tokeny.

## Soubory

- Modifikace: `PmTracker.Web/wwwroot/css/site.css` — přidat 5 nových pravidel
- Beze změny: `PmTracker.Web/Views/Projekty/_ProjectProposalsTab.cshtml`

## Kritéria přijetí

1. Každý návrh má viditelný border a padding — jasně oddělený od ostatních
2. Header (název + badge) je flex row, badge zarovnaný vpravo
3. Tlačítka v akční řadě mají konzistentní mezery
4. Dark mode funguje bez dalších pravidel (gov tokeny)
5. Layout dvou sekcí (záznamy + harmonogram) má mezeru mezi sebou
