---
title: Záznamy
description: Záznamy projektu — úkoly, požadavky, evidence (NES/PMP/PNF a vlastní typy).
---

# Záznamy

Záznamy jsou **základní pracovní jednotky projektu**. Každá akce, požadavek,
úkol, ticket — to je záznam. Aplikace eviduje pět hlavních typů, čtyři z nich
mají vazbu na ServiceDesk:

| Typ | Vazba na SD | Použití |
|---|---|---|
| **NES** | `HOT_ZAZNAMY` typu NES | Nesrovnalost — incident / vada IS |
| **PMP** | `HOT_ZAZNAMY` typu PMP | Požadavek metodické podpory (s kalkulací) |
| **PNF** | `HOT_ZAZNAMY` typu PNF | Požadavek nové funkcionality (s kalkulací) |
| **Úkol** | žádná | Interní úkol týmu PM Trackeru |

## Layout záložky Záznamy

Záložka *Záznamy* v detailu projektu obsahuje:

### Filtry

- **Typ** (NES / PMP / PNF / úkol / všechny)
- **Stav** (otevřené / dokončené / všechny)
- **Vlastník** (já / kdokoliv)
- **Subsystém**
- **Skrýt dokončené** (toggle)
- **Skrýt smazané** (toggle)

Filtry se ukládají v `localStorage`, persist mezi přihlášeními.

### Tabulka záznamů

Sloupce:

- **ID** (vlastní PM Tracker ID, nebo identifikátor jednání pokud je projekt
  s `PouzivatIdentJednani=true`)
- **Typ** (badge NES / PMP / PNF / úkol)
- **Název**
- **Stav**
- **Vlastník**
- **Termín** (plánovaný / dodaný)
- **Externí ticket** (pokud je propojený, klikací odkaz do SD)
- **Akce** — *Upravit*, *Smazat*

### Lazy-scroll

Tabulka používá lazy-scroll — postupně dotahuje další položky. Žádná
stránkování.

### Tlačítka v hlavičce

- **Nový záznam** (pokud máš `records.create`)
- **Export tabulky** (CSV / Excel — pokud existuje)

## Co je v této sekci wiki

- [Typy záznamů](typy-zaznamu.md) — NES vs PMP vs PNF vs úkol
- [Nový záznam](novy-zaznam.md) — jak založit
- [Upravit záznam](upravit-zaznam.md) — full editor s taby
- [Komentáře](komentare.md) — interní vyjádření, vazby na jednání
- [Externí vazba na SD](externi-vazba-sd.md) — propojení s ticketem
- [Smazat záznam](smazat-zaznam.md) — soft-delete
- [Filtry a režimy zobrazení](filtry-a-rezimy-zobrazeni.md)
- [Barvy karet záznamů](barvy-karet.md) — význam barevného proužku (kategorie + stav úkolu)

## Pro koho

| Persona | Co může |
|---|---|
| **HOST** | Číst záznamy (`records.view`) |
| **Member** | + Vytvářet a editovat (`records.create`, `records.edit`) |
| **Leader** | + Mazat (`records.delete`) |
| **Admin** | Vše + překračování zámků |

## Vztah k jiným oblastem

- **[Jednání](../jednani/)** — záznam může mít vazbu na jednání (zapsán
  během porady)
- **[Harmonogram](../harmonogram/)** — záznam má svůj harmonogram (kroky a
  termíny)
- **[Tým](../tym/)** — vlastník a collaboratoři ze záznamu jsou členové týmu
- **[ServiceDesk](../../integrace/servicedesk/)** — externí vazba propojuje
  záznam s SD ticketem
- **[Profil → Moje priority](../../uzivatelsky-dashboard/moje-priority.md)** —
  záznamy ke kterým jsi vlastník / collaborator se objeví v dashboardu

## Hlavní akce

| Akce | Detail |
|---|---|
| Vytvořit | [Nový záznam](novy-zaznam.md) |
| Upravit | [Upravit záznam](upravit-zaznam.md) |
| Komentovat | [Komentáře](komentare.md) |
| Propojit s SD ticketem | [Externí vazba](externi-vazba-sd.md) |
| Smazat | [Smazat záznam](smazat-zaznam.md) |
