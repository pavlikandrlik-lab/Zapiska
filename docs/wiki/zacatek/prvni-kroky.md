---
title: První kroky
description: Co uvidíš po prvním přihlášení a kam jít dál.
---

# První kroky v PM Trackeru

Po úspěšném přihlášení tě aplikace přesměruje na **uživatelský dashboard**.
Tato stránka je tvoje denní rozcestník.

## Co uvidíš na dashboardu

- **Moje priority** — záznamy které tě čekají, seřazené podle urgentnosti
- **Novinky** — co se v projektech dělo
- **Globální vyhledávání** — pole pro fulltext napříč entitami

Pokud máš málo přiřazení (nový uživatel), priority a novinky můžou být prázdné.
To je v pořádku.

## Hlavní navigace

V horním menu / sidebar najdeš odkazy na:

- **Projekty** — centrální oblast (kde se odehrává většina práce)
- **Jednání** — porady (lze se k nim dostat i z projektu)
- **Osoby** — adresář
- **Číselníky** — referenční data
- **Nastavení** — jen pokud máš `settings.view`
- **Profil** (vpravo nahoře pod tvým jménem) — moje práva, preference

## Co zkusit jako první

### 1. Otevři projekt

Klikni *Projekty* → uvidíš seznam projektů ke kterým máš přístup → klikni na
některý → otevře se detail s taby. Projdi taby (Záznamy, Jednání, Tým, Harmonogram).

### 2. Prozkoumej záznam

V tabu **Záznamy** klikni na nějaký záznam → otevře se editor (modal nebo
celá stránka, záleží na tvojí preferenci v profilu).

### 3. Prohlédni si svoje práva

Profil (pravý horní roh) → **Moje práva** → vidíš výpis permission keys,
které ti přiřazené role dávají.

### 4. Zkus globální vyhledávání

Dashboard → vyhledávací pole → napiš část jména projektu nebo záznamu →
uvidíš rychlé výsledky napříč entitami.

## Pokud něco nefunguje

- **Nevidím tlačítko *Upravit* na záznamu** → typicky chybí permission
  `records.edit`. Detail: [Role a oprávnění](role-a-prava-prehled.md).
- **Po kliknutí na projekt vidím 404** → projekt buď neexistuje, nebo k němu
  nemáš přístup. Admin tě musí přiřadit.
- **Prázdný dashboard / žádné projekty** → nemáš zatím přiřazení k žádnému
  projektu. Kontaktuj leadera nebo admina.

## Související

- [Základní ovládání](ovladani-zakladni.md) — UI vzorce, modaly, taby
- [Role a oprávnění](role-a-prava-prehled.md) — co můžeš a co ne
- [Uživatelský dashboard](../uzivatelsky-dashboard/) — detail
- [Projekty](../projekty/) — hlavní pracovní oblast
