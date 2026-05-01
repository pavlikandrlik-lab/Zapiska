---
title: Efektivní práva
description: Diagnostika — co konkrétní uživatel reálně může na konkrétním projektu.
---

# Efektivní práva

**Klíčový admin diagnostický nástroj.** Odpovídá na otázku:

> "Proč Jan Novák nevidí tlačítko *Upravit projekt* na projektu FIS-EIS?"

Aplikace pro vybranou kombinaci **uživatel × projekt** spočítá **finální seznam
permission keys**, které tento uživatel reálně na tom projektu má.

## Workflow

### 1. Vyber uživatele

Z dropdownu / vyhledávání. Můžeš filtrovat podle login, displayName,
organizace.

### 2. Vyber projekt

Z dropdownu projektů. Pokud user nemá přístup k žádnému projektu, dropdown
ukáže "(žádné projekty s přístupem)".

### 3. Aplikace spočítá

Algoritmus:

1. Načte **globální role** uživatele
2. Načte **per-projekt role** pro daný projekt
3. Sjednotí permission keys ze všech rolí
4. Aplikuje **per-projekt scope** (per-roli)
5. Aplikuje případný **subsystém scope** (pro subsystém-omezené role)
6. Vrátí finální seznam klíčů

### 4. Zobrazí výsledek

```
Uživatel:  Jan Novák (jan.novak)
Projekt:   FIS-EIS Modernizace

Role aplikované:
  ▸ HOST (globální)
  ▸ VLASTNIK_PROJEKTU (per-projekt: FIS-EIS)
  ▸ METODIK_SUBSYSTEMU (per-subsystém: R_DAN)

Efektivní permission keys (47):
  ✓ projects.view
  ✓ projects.edit
  ✓ records.view
  ✓ records.edit
  ✓ records.create
  ✓ team.member.add
  ✓ team.role.assign
  ✓ harmonogram.view
  ✓ harmonogram.toggle-rezim
  ... (další klíče)

Klíče které user nemá (relevantní pro tento projekt):
  ✗ projects.delete  (HOST to nedává; VLASTNIK_PROJEKTU to nemá v této instalaci)
  ✗ proposals.approve (jen ADM_PROJ role)
  ✗ settings.view
  ...
```

## Use case scenarios

### "User si stěžuje že nevidí tlačítko"

1. Uživatel: "Nevidím tlačítko *Schválit návrh* na projektu FIS-EIS"
2. Admin: otevře Efektivní práva → user: Jan Novák, projekt: FIS-EIS
3. Hledá `proposals.approve` → vidí ✗ (klíč chybí)
4. Vyhodnotí: "Jan má roli VLASTNIK_PROJEKTU, ale ta v této instalaci nemá
   `proposals.approve`. Pro schvalování návrhů potřebuje roli
   *ADM_PROJ*."
5. Akce: přiřadit roli ADM_PROJ v Uživatelé × role

### "Audit — co konkrétní user může"

Security review. Admin vybere usera, vybere každý projekt, exportuje seznam
klíčů. Porovná s expected list.

### "Test po deploy nové role"

Po deploy s novou rolí admin chce ověřit, že seed proběhl správně. Otevře
Efektivní práva, vybere testovacího usera s tou rolí, ověří klíče.

## Read-only

Stránka **nic nemění**. Je to čistá diagnostika.

Pro změnu přiřazení role jdi do [Uživatelé × role](../uzivatele-role/).
Pro změnu definice role jdi do kódu (deploy).

## Permission keys

Tato stránka vyžaduje `settings.view` (nebo specifičtější `settings.efektivni-prava.view`).
Nahlížení do detailu konkrétního usera **neznamená moc nic** — jen vidění
oprávnění, ne škoda.

## Vztah k profilu

[Profil → Moje práva](../../profil/moje-prava.md) je **stejná diagnostika
ale pro mě samotného**. Uživatel vidí svoje vlastní permission keys.

Admin Efektivní práva ukazuje **pro libovolného usera** — se všemi projekty.

## Limity

### Nezohledňuje *runtime* podmínky

Některé kontroly jsou navíc kontextuální — např. *uzavřené jednání* je
read-only bez ohledu na klíče. Efektivní práva to nereflektují, ukazují jen
authz vrstvu.

### Neukazuje *proč* klíč chybí

Pokud user klíč nemá, stránka jen řekne "✗". Pro analýzu *která role by ho
měla dát* musíš jít do [Role × akce matice](../role-akce-matice/) a hledat,
která role obsahuje daný klíč.

## Pro koho

- **App Admin / SuperAdmin** — denní support
- **Security Auditor** — review

## Související

- [Profil → Moje práva](../../profil/moje-prava.md)
- [Uživatelé × role](../uzivatele-role/)
- [Role × akce matice](../role-akce-matice/)
- [Role](../role/)
