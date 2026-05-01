---
title: Osoby
description: Přehled osob v aplikaci — uživatelé, kontakty, organizační příslušnost.
---

# Osoby

Sekce **Osoby** ukazuje seznam uživatelů aplikace (záznamy v `dbo.osoby`).
Slouží jako:

- **Adresář** — kdo v organizaci pracuje s aplikací
- **Vstup pro přiřazení** — odkud admin / leader vybírá osobu pro [tým projektu](../projekty/tym/)
- **Diagnostika** — pro admina, kdo má účet a v jakém stavu

## Vztah k Active Directory

Osoba v PM Trackeru je **vázaná na záznam v AD** přes `Guid_AD`. Atributy jako
displayName, organizační celek a email se [synchronizují](../integrace/active-directory/synchronizace-osob.md)
periodicky z AD.

PM Tracker **nezakládá osoby automaticky** — i když má uživatel účet v AD,
pro přihlášení musí mít odpovídající záznam v `dbo.osoby`. Založení dělá
admin nebo se osoba vytvoří automaticky při prvním přiřazení do týmu (přes
AD picker).

## Co najdeš v této sekci

- [Přiřazení osob k právům](prirazeni-prav.md) — jak je osoba propojena s rolemi a projekty

## Hlavní akce

| Akce | Kdo | Detail |
|---|---|---|
| Procházet seznam | Každý (filtruje permission) | (na hlavní stránce sekce) |
| Vidět detail | Každý (vlastní data vždy) | — |
| Měnit role osoby | Admin | [Nastavení → Uživatelé × role](../nastaveni-administrace/uzivatele-role/) |
| Deaktivovat osobu | Admin | (přes DB / migraci) |

## Pro koho

- **Member** — vidí adresář, vlastní detail
- **Leader** — používá pro přiřazení do projektového týmu
- **Admin** — full management

## Související

- [Active Directory](../integrace/active-directory/)
- [Profil](../profil/) — moje vlastní osoba
- [Projekty → Tým](../projekty/tym/) — přiřazení do týmu projektu
