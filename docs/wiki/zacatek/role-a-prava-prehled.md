---
title: Role a oprávnění — přehled pro uživatele
description: Co jsou role, jak zjistím co můžu, co dělat když nevidím tlačítko.
---

# Role a oprávnění (pohled uživatele)

PM Tracker používá **per-action authorization model**. To znamená:

- Každá akce v aplikaci (vytvořit, upravit, smazat, schválit, …) má vlastní
  **permission key** (např. `projects.edit`)
- Role je sada těchto klíčů
- Tvoje uživatelské oprávnění = sjednocení klíčů ze všech tvých rolí

## Co je role

Role je **logická skupina oprávnění**. PM Tracker má 11 rolí:

| Kód | Účel |
|---|---|
| `SUPERADMIN` | Plný přístup napříč celou aplikací |
| `APP_ADMIN` | Admin diagnostika + management |
| `READ_ALL` | Read-only přístup ke všem projektům (management visibility) |
| `VLASTNIK_PROJEKTU` | Vlastník projektu — full management |
| `ADM_PROJ` | Projektový admin (silný) bez práva měnit metadata |
| `PROJ_MAN` | Projektový manažer |
| `HOST` | Read-only host projektu |
| `GEST` | Gestor s komentovacími právy |
| `VEDOUCI_SUBSYSTEMU` | Vedoucí subsystému |
| `ZASTUPCE_VEDOUCIHO_SUBSYSTEMU` | Zástupce vedoucího |
| `METODIK_SUBSYSTEMU` | Metodik (komentovací práva) |

Konkrétní seznam rolí v PM Trackeru je definovaný v kódu (`PermissionSeedConfiguration.cs`)
a aplikuje se při deploy. Podrobně: [Role v Nastavení](../nastaveni-administrace/role/).

## Co je permission key

Stringový identifikátor konkrétní akce. Konvence: `objekt.akce`, případně
`objekt.kontext.akce`. Příklady:

- `projects.read.all`, `projects.create`, `projects.edit`, `projects.delete`
- `records.create`, `records.edit`, `records.delete`, `records.schedule.edit`
- `meetings.create`, `meetings.edit`, `meetings.delete`, `meetings.status.change`
- `team.member.add`, `team.role.assign`, `team.subsystem.create`,
  `team.subsystem.role.assign`
- `dashboard.view`, `dashboard.records.view`, `dashboard.statistics.view`,
  `dashboard.nes.view`, `dashboard.vyzvy.view`
- `vyjadreni.reharvest`, `vyjadreni.modal.open`, `vyjadreni.vazba.create`
- `proposals.record.create`, `proposals.schedule.create`, `proposals.accept`,
  `proposals.reject`
- `settings.view`, `settings.sd.view`, `settings.roles.assign`
- `ciselniky.row.edit`, `ciselniky.row.delete`

## Per-projekt scope

Role může být **omezená na konkrétní projekt**:

- "Honza je VLASTNIK_PROJEKTU **pouze na projektu FIS-EIS**"
- "Marie je READ_ALL **na všech projektech**" (globální)

Aplikace při kontrole oprávnění bere v úvahu i scope. Detail diagnostiky:
[Efektivní práva](../nastaveni-administrace/efektivni-prava/).

## Jak zjistím co můžu

### Profil → Moje práva

Otevři Profil (pravý horní roh) → **Moje práva**. Uvidíš:

- Tvoje role (globální + per-projekt)
- Z nich rozbalené permission keys
- Případný organizační scope

### Vizuálně v aplikaci

Aplikace **skrývá tlačítka, na která nemáš oprávnění**. Pokud nevidíš tlačítko
*Upravit*, *Vytvořit*, *Smazat*, … — typicky chybí klíč.

## Časté otázky

### Proč nevidím tlačítko *Upravit projekt*?

Chybí ti `projects.edit`. Buď nemáš roli která ten klíč obsahuje, nebo máš
roli ale s scope na jiný projekt. Detail: [FAQ](../pomoc/faq.md).

### Proč nevidím sekci *Nastavení*?

Chybí ti `settings.view`. Tato sekce je dostupná jen adminům.

### Proč jednání je read-only?

Buď ti chybí `meetings.edit` / `meetings.notes.edit`, nebo je jednání ve stavu
**CLOSED**. Uzavřené jednání je business-pravidlo read-only pro většinu rolí.

### Mám roli ale nemůžu nic dělat

Možnosti:

1. Tvoje role je **per-projekt** ale nejsi v daném projektu (přiřazen)
2. Role je **globální read-only** (READ_ALL)
3. Role je definovaná ale bez klíčů (chyba v seedu) — kontaktovat admina

## Jak změnit svoje role

**Z UI nelze.** Role nastavuje admin v
[Nastavení → Uživatelé × role](../nastaveni-administrace/uzivatele-role/).
Definice rolí (které klíče obsahují) se mění **přes deploy** — pull request +
nasazení.

Pokud potřebuješ jiné oprávnění:

1. Identifikuj **co konkrétně** chceš dělat
2. Najdi **kterou rolí** to umožňuje (matice [Role × akce](../nastaveni-administrace/role-akce-matice/))
3. Požádej admina o přiřazení té role

## Související

- [Profil → Moje práva](../profil/moje-prava.md)
- [Nastavení → Efektivní práva](../nastaveni-administrace/efektivni-prava/) — admin
  diagnostika "co může konkrétní user"
- [FAQ](../pomoc/faq.md) — typické otázky o oprávněních
