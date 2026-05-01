---
title: AD picker
description: Vyhledávání osob v AD při přiřazování do projektů a záznamů.
---

# AD picker

**AD picker** je UI komponenta pro **výběr osoby z firemního Active Directory**.
Aplikace ji používá vždy, když potřebuješ "vybrat člověka" — typicky při
přiřazování do projektu nebo jako collaboratora záznamu.

## Kde se AD picker používá

| Místo | Účel |
|---|---|
| **Projekt → Tým → Přidat člena** | Přiřazení nové osoby do projektového týmu |
| **Projekt → Tým → Přidat projektovou roli** | Role pro stávajícího nebo nového člena |
| **Projekt → Tým → Přiřadit roli v subsystému** | Specialist pro konkrétní subsystém |
| **Editor záznamu → Spolupracující osoby** | Collaborator s přístupem k záznamu |
| **Editor záznamu → Vlastník** | (single-person picker varianta) |

## Jak vyhledávání funguje

1. Začneš psát do textového pole AD pickeru
2. Po **debounce** (typicky 300ms po posledním stisku klávesy) aplikace zavolá
   AD search endpoint
3. Aplikace pošle query (jméno / login / email) na firemní AD server
4. AD vrátí seznam matchů (max `MaxResults`, default 15)
5. Picker zobrazí dropdown s výsledky
6. Klikneš na osobu → vybere se a vyplní do formuláře

## Co AD picker vrátí

Pro každou osobu:

- **Display name** (`cn`)
- **Login** (`sAMAccountName`, např. `jan.novak`)
- **Email** (`mail`)
- **Organizační celek** (typicky `department` nebo `OU`)
- **Titul** (`title`, pokud je v AD)

## Vztah k `dbo.osoby`

AD picker je **přímý dotaz na AD**, ne na `dbo.osoby`. To znamená:

- Vrátí i osoby **které ještě nejsou v aplikaci**
- Po výběru a uložení (Add team member) aplikace **založí záznam** v `dbo.osoby`
  podle `Guid_AD` z AD

To je hlavní cesta jak se osoby do PM Trackeru "dostanou" — typicky když je
admin přiřadí do nějakého projektu.

## Konfigurace

V `appsettings.json` sekce `PmTracker.ActiveDirectory`:

| Klíč | Default | Účel |
|---|---|---|
| `Domain` | `acr` | Doména AD (LDAP server) |
| `MaxResults` | `15` | Max výsledků na dotaz |
| `QueryTimeoutSeconds` | `8` | Timeout AD dotazu |

## Limity a edge case

### Pomalé AD

Pokud AD odpovídá pomalu, `QueryTimeoutSeconds` (default 8s) způsobí prázdný
výsledek. User uvidí "žádné výsledky" místo timeout chyby — UX kompromis,
aby UI nelagovalo.

### Příliš mnoho matchů

Při zadání jen 1–2 znaků může AD vrátit stovky matchů. Aplikace zobrazí
prvních `MaxResults` a varuje "Zužte dotaz".

### Hledání podle emailu

Funguje, ale je pomalejší než hledání podle loginu (AD index je primárně na
samAccountName).

### Speciální znaky v dotazu

LDAP injection je ošetřen — speciální znaky (`*`, `(`, `)`, `\`) se escapují
před odesláním do AD.

## Performance optimalizace — cache

Aplikace má **`AdLoginCache`** — in-memory cache pro překlad login → display
name. TTL 6 hodin. Cíl: v chat modalu (kde se zobrazuje displayName autora
vyjádření) nedotazovat AD při každém renderu.

Detail (vývojářský): `PmTracker.Web/Services/ServiceDesk/AdLoginCache.cs`.

## Permissions

AD picker má vlastní permission key:

- `team.member.add` — přidání člena
- `team.role.assign` — přiřazení projektové role
- `team.subsystem.create` — přiřazení subsystému
- `team.subsystem.role.assign` — přiřazení role v subsystému
- `records.collaborator.add` — collaborator na záznamu

Pokud klíč chybí, AD picker se vůbec nezobrazí.

## Související

- [Synchronizace osob](synchronizace-osob.md)
- [Osoby → Přiřazení k právům](../../osoby/prirazeni-prav.md)
- [Projekty → Tým → Přidat člena](../../projekty/tym/pridat-clena.md)
