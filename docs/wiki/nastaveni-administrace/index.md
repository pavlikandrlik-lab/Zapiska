---
title: Nastavení a administrace
description: Admin sekce — role, oprávnění, přiřazení uživatelů, synchronizace.
---

# Nastavení a administrace

Sekce **Nastavení** je dostupná uživatelům s permission `settings.view`
(typicky SuperAdmin, App Admin). Slouží k diagnostice a omezené správě:

- Přehled rolí
- Přehled akcí (permission keys)
- Matice role × akce — kdo může co
- Přiřazení uživatelů k rolím (per-projekt scope)
- Efektivní práva — diagnostika "co tento uživatel reálně může"
- Synchronizace — stav AD a SD harvest jobů

## Read-only versus editovatelné

**Většina sekcí je read-only.** Toto rozhodnutí je záměrné:

- Role a permission keys jsou **definovány v kódu aplikace**
  (`PermissionSeedConfiguration.cs`)
- Při deploy se aplikuje seed do DB
- Změna v rolích / klíčích = pull request + deploy

Důvod: business pravidla autorizace jsou součást aplikační logiky, ne
runtime konfigurace. Drift mezi kódem a DB by způsoboval špatně debuggovatelné
chyby ("v dev funguje, v produkci ne").

**Editovatelné z UI:**

- **Synchronizace** — manuální spuštění AD / SD sync jobů
- **Uživatelé × role** — přiřazení (per-projekt scope) — záleží na konkrétní akci

## Sub-sekce

| Sekce | Obsah | Read-only? |
|---|---|---|
| [Role](role/) | Přehled rolí v PM Trackeru | Ano |
| [Akce / permissions](akce-permissions/) | Permission keys per-action | Ano |
| [Role × akce matice](role-akce-matice/) | Která role co umí | Ano |
| [Uživatelé × role](uzivatele-role/) | Přiřazení rolí uživatelům | Záleží |
| [Efektivní práva](efektivni-prava/) | Diagnostika konkrétního uživatele | Ano (diagnostika) |
| [Synchronizace](synchronizace/) | Stav AD/SD jobů | Editovatelné (trigger) |

## Pro koho

- **SuperAdmin** — full přístup
- **App Admin** — diagnostika, přiřazení rolí (per-projekt)
- **Vedoucí projektu** (`VLASTNIK_PROJEKTU` / `ADM_PROJ`) — typicky nemá. Pokud má `settings.view`, vidí
  diagnostiku ale nic nemění.
- **Member / běžný user** — sekci nevidí

## Co tady **nenajdeš**

- **Změna rolí v kódu** — to je vývojářská práce, viz
  `docs/architecture/backend-layering.md` a `PermissionSeedConfiguration.cs`
  v repu
- **Konfigurace IIS / deploy** — `docs/technical/04-installation-deployment-iis.md`
- **Audit log prohlížeč** — aktuálně není UI; admin se musí dívat do DB

## Související

- [Zacatek → Role a oprávnění — přehled](../zacatek/role-a-prava-prehled.md) — pohled běžného uživatele
- [Profil → Moje práva](../profil/moje-prava.md) — co konkrétně user vidí o sobě
- [Integrace](../integrace/) — AD a SD jsou cílem mnoha sync jobů
