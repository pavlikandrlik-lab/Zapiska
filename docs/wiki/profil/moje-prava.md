---
title: Moje práva
description: Co konkrétně mohu — výpis efektivních permission keys mé osoby.
---

# Moje práva

Stránka **Profil → Moje práva** ukazuje výpis tvých **efektivních permission
keys** — co konkrétně můžeš v aplikaci dělat.

## Co panel ukazuje

### Tvoje role

Seznam tvých přiřazených rolí, rozdělený na:

- **Globální** — platí napříč všemi projekty
- **Per-projekt** — uvedeno který projekt + jaká role

Příklad:

```
Globální role:
  - HOST

Per-projekt role:
  - FIS-EIS Modernizace      → VLASTNIK_PROJEKTU
  - ISSP Refresh             → PROJ_MAN
```

### Permission keys (efektivní)

Sloučený seznam klíčů ze všech rolí. Pro per-projekt role je u klíče poznámka
o scope:

```
Permission keys:
  records.view          (globální, z role HOST)
  projects.view         (globální, z role HOST)
  records.edit          (FIS-EIS, z role VLASTNIK_PROJEKTU)
  team.member.add       (FIS-EIS, z role VLASTNIK_PROJEKTU)
  records.create        (FIS-EIS, z role VLASTNIK_PROJEKTU)
  records.edit          (ISSP, z role PROJ_MAN)
  meetings.create       (FIS-EIS, z role VLASTNIK_PROJEKTU)
  ... (další klíče)
```

### Organizační scope

Pokud má některá z tvých rolí omezení na organizační celek (org unit), zobrazí
se i toto. Pro většinu uživatelů je scope = celá organizace.

## Jak to číst

Pravidlo: pokud **klíč existuje v seznamu**, akci můžeš provést. Pokud **chybí**,
aplikace ti tlačítko / sekci ne­zobrazí.

Pokud má klíč scope = konkrétní projekt, mimo ten projekt klíč **neplatí**.
Příklad:

- Máš `records.edit` jen na FIS-EIS
- Otevřeš záznam na ISSP → tlačítko *Upravit* nevidíš (klíč chybí pro daný
  scope)
- Otevřeš záznam na FIS-EIS → tlačítko *Upravit* vidíš

## Typické dotazy

### Mám tady `projects.edit` ale nevidím tlačítko *Upravit projekt* na projektu X

Možnosti:

1. Tvoje `projects.edit` je s scope na **jiný projekt**
2. Projekt X je ve stavu, kde je editace zakázaná (např. archivovaný)
3. Bug v aplikaci — nahlas adminovi

### Měl jsem víc klíčů, teď jich mám méně

Admin pravděpodobně změnil tvoje role. Profil ukazuje **aktuální** stav,
nikoli historii.

### Co znamená klíč `dashboard.nes.view`?

Klíč pro NES panel projektového dashboardu. Pokud ho nemáš, panel se nezobrazí.

Detailní popis klíčů: [Akce / permissions](../nastaveni-administrace/akce-permissions/).

## Read-only

**Z této stránky NEzměníš svoje role**. Stránka je informativní. Pokud chceš
jiné oprávnění, kontaktuj admina (viz [Pomoc → Kontakty](../pomoc/kontakty.md)).

## Související

- [Role a oprávnění — přehled](../zacatek/role-a-prava-prehled.md) — vysvětlení konceptu
- [Efektivní práva (admin)](../nastaveni-administrace/efektivni-prava/) — admin
  diagnostika "co může jiný user"
- [Nastavení → Uživatelé × role](../nastaveni-administrace/uzivatele-role/)
