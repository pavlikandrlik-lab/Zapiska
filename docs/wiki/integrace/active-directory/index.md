---
title: Active Directory
description: Propojení PM Trackeru s firemním AD — autentifikace, picker osob, synchronizace atributů.
---

# Active Directory (AD)

PM Tracker využívá firemní Active Directory na **třech úrovních**. Každá má
vlastní stránku v této sekci.

## 1. Přihlášení (autentifikace)

IIS Windows authentication — aplikace nemá vlastní heslo, identitu přebírá
ze single sign-on. Detail: [Přihlášení přes AD](prihlaseni-ad.md).

## 2. AD picker (vyhledávání osob)

Při přiřazování osoby do projektového týmu nebo jako collaboratora záznamu
aplikace dělá **přímý dotaz na AD**. User vyhledá podle jména / loginu /
emailu, vybere a uloží. Detail: [AD picker](ad-picker.md).

## 3. Synchronizace atributů osob

Periodický job čte z AD aktualizace **displayName**, **organizační celek**,
**email** a aktualizuje záznamy v `dbo.osoby`. Detail:
[Synchronizace osob](synchronizace-osob.md).

## Co AD **nedělá automaticky**

PM Tracker je **konzervativní** ve vztahu k AD:

- **Nezakládá osoby** v `dbo.osoby` — pro přihlášení musí mít user záznam
  v aplikaci. Sync osob je read-only doplnění atributů, ne create.
- **Nemaže** osoby co v AD už nejsou — jen je logicky deaktivuje (`is_active=0`)
- **Nemění role / oprávnění** — to spravuje admin v Nastavení, ne automatika

Důvod: business pravidla autorizace nejsou v AD. AD ví "jaký člověk", ale ne
"co může dělat v PM Trackeru".

## Stránky v této sekci

- [Přihlášení přes AD](prihlaseni-ad.md) — autentifikace + diagnostika
- [AD picker](ad-picker.md) — UI pro výběr osob z AD
- [Synchronizace osob](synchronizace-osob.md) — periodický sync atributů

## Pro koho je sekce

- **Uživatel** — *Přihlášení* a *AD picker* (kdy a jak ho vidím)
- **Admin** — všechny tři podstránky včetně synchronizace

## Konfigurace

Nastavení `PmTracker.ActiveDirectory` v `appsettings.json`:

| Klíč | Účel | Typický default |
|---|---|---|
| `Domain` | Doména AD | `acr` |
| `MaxResults` | Limit pro AD picker výsledky | `15` |
| `QueryTimeoutSeconds` | Timeout AD dotazu | `8` |

Detaily v [docs/technical/](../../). Tato wiki sekci konfigurace nepokrývá.

## Související

- [Osoby](../../osoby/) — `dbo.osoby` z pohledu aplikace
- [Profil](../../profil/) — co user vidí o sobě po přihlášení
- [Nastavení → Synchronizace](../../nastaveni-administrace/synchronizace/) — admin diagnostika
