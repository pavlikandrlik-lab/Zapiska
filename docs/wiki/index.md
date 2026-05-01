---
title: PM Tracker — uživatelská wiki
description: Hlavní rozcestník dokumentace pro uživatele a administrátory aplikace PM Tracker.
---

# PM Tracker — wiki

PM Tracker je vnitřní aplikace pro **správu projektů a evidenci úkolů**. Eviduje
projekty, jejich záznamy (úkoly, požadavky, externí tickety), jednání, týmy
a harmonogramy. Integruje se s firemním **Active Directory** (přihlášení,
adresář osob) a **ServiceDeskem** (tickety PMP/PNF/NES, vyjádření, harmonogram).

Tato wiki popisuje **co aplikace umí z pohledu uživatele a administrátora**.
Vývojářská reference (UI komponenty, deploy, schema DB) je zvlášť — viz
[Co tady **nenajdeš**](#co-tady-nenajd%C3%AD%C5%A1) níže.

## Pro koho je wiki

| Persona | Co tě zajímá nejvíc |
|---|---|
| **Nový uživatel** | [Začátek](zacatek/) → [Uživatelský dashboard](uzivatelsky-dashboard/) |
| **Project leader** | [Projekty](projekty/) — celá sekce, hlavně harmonogram, jednání, výzvy |
| **Member projektu** | [Projekty → Záznamy](projekty/zaznamy/), [Komentáře](projekty/zaznamy/komentare.md) |
| **Admin / SuperAdmin** | [Nastavení a administrace](nastaveni-administrace/), [Integrace](integrace/) |
| **Power user** | [Pomoc → FAQ](pomoc/faq.md), [Slovníček](slovnicek.md) |

## Hlavní oblasti

- [Začátek](zacatek/) — přihlášení, první kroky, ovládání, role a oprávnění
- [Uživatelský dashboard](uzivatelsky-dashboard/) — moje priority, novinky, vyhledávání
- [Projekty](projekty/) — projektový management (záznamy, jednání, tým, harmonogram, návrhy, projektový dashboard)
- [Číselníky](ciselniky/) — referenční data
- [Osoby](osoby/) — přehled osob a přiřazení
- [Profil](profil/) — můj účet, moje práva
- [Export](export/) — tisk a exporty (PDF, Word, Excel)
- [Integrace](integrace/) — Active Directory, ServiceDesk
- [Nastavení a administrace](nastaveni-administrace/) — role, oprávnění, synchronizace
- [Pomoc](pomoc/) — FAQ, řešení problémů, kontakty

## Klíčové koncepty

- **Projekt** je centrální entita. Vše ostatní (záznamy, jednání, harmonogram,
  výzvy) existuje **v kontextu konkrétního projektu**.
- **Permission keys** jsou granulární klíče oprávnění (např. `projects.edit`,
  `dashboard.nes.view`). Aplikace má per-action authz model: každá mutující
  akce má vlastní klíč. Tlačítko které nemáš oprávnění použít je v UI skryté.
- **Read-only ServiceDesk** — PM Tracker do SD **nikdy nezapisuje**, jen čte.
  Vyjádření zadáš přímo v ServiceDesku, PM Tracker je pak nasaje.
- **Soft-delete** — projekty a záznamy se fyzicky nemažou. Označí se jako
  smazané a skryjí v default přehledech.

## Co tady **nenajdeš**

- Vývojářská reference UI komponent (`docs/architecture/`) — buttons, cards,
  fields, dialogs, atd. To je interní design system reference.
- Instalace, deploy, IIS konfigurace, DB migrace (`docs/technical/`) — pro
  ops / deploy operátora.
- Specifikace jednotlivých feature (`docs/specs/`) — vývojářský archiv.
- Plánování a brainstorming (`docs/superpowers/`) — interní team artifacts.

## Slovníček

Rychlý přehled klíčových pojmů (NES, PMP, PNF, vyjádření, harmonogram,
externí vazba, …) najdeš v [slovníčku](slovnicek.md).

## Verze a aktualizace

Aktuální verze aplikace je vidět v patičce každé stránky aplikace. Verze této
wiki sleduje verze aplikace — pokud přibyde feature, doplní se do wiki s
poznámkou *od verze X.Y*.
