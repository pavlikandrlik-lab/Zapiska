---
title: Uživatelské nastavení
description: Téma, preference editoru, tisk — lokální volby uložené v prohlížeči.
---

# Uživatelské nastavení

V profilu si můžeš nastavit **uživatelské preference**. Tyto volby ovlivňují
jen tvůj uživatelský zážitek a **ukládají se v browseru** (sessionStorage /
localStorage), nikoli na server.

To znamená:

- Volby **nejsou sdílené napříč zařízeními**
- Pokud vymažeš data prohlížeče, volby se resetují
- Při přihlášení z jiného PC začínáš s defaulty

## Co lze nastavit

### Téma

| Volba | Vzhled |
|---|---|
| **Světlé** | Default; bílé pozadí, tmavý text |
| **Tmavé** | Tmavé pozadí, světlý text — vhodné pro večerní práci |

Přepínač je **gov-theme-switch** komponenta, vidíš ji v hlavičce nebo v profilu.

### Výchozí editor záznamu

Když otevíráš záznam, aplikace ti nabídne **modal** nebo **full-page** editor:

- **Modal** — dialogové okno překryje stránku. Rychlejší pro krátké úpravy.
  Nezachová URL.
- **Full-page** — celá stránka. Lepší pro dlouhé záznamy, zachová URL pro
  sdílení.

Pokud nezvolíš výchozí, aplikace tě pokaždé zeptá. Volba *Pamatovat moji volbu*
v dialogu uloží tu jednu jako default.

### Výchozí formát tisku

PDF / HTML / Word. Detail: [PDF versus Word](../export/pdf-vs-word.md).

Default je **PDF**. Pokud nastavíš jiný, tlačítka *Tisk* tě už nebudou ptát.

## Reset preferencí

Každá preference má v profilu tlačítko *Vymazat uloženou volbu*:

- *Vymazat výchozí formát tisku* — smaže preferenci, příště ti aplikace
  nabídne dialog
- *Vymazat výchozí editor záznamu* — stejně
- *Vymazat uložené filtry* — smaže filtry projektů (Skrýt smazané, …)

Reset je instant a non-destructive.

## Kde se to ukládá

| Preference | Storage | Klíč |
|---|---|---|
| Téma | localStorage | `pmtracker.theme` |
| Editor záznamu | localStorage | `pmtracker.recordEditor.preference` |
| Formát tisku | localStorage | `pmtracker.print.format` |
| Filtry projektů | localStorage | `pmtracker.projects.*` |
| Filtry harmonogramu | sessionStorage | `pmtracker.schedule.*` |

(Klíče jsou orientační, můžou se měnit verzemi.)

## Co tady **nenastavíš**

- **Heslo** — heslo PM Tracker neukládá. Heslo měň v Active Directory.
- **Email / jméno** — atributy se synchronizují z AD. Změna v AD se propíše.
- **Role / oprávnění** — to dělá admin v
  [Nastavení → Uživatelé × role](../nastaveni-administrace/uzivatele-role/).
- **Notifikace** — aplikace zatím nemá notification system.

## Přístupnost (accessibility)

Aplikace dodržuje WCAG 2.1 AA. Tmavé téma má dostatečný kontrast pro AA. Pokud
máš specifické accessibility požadavky, použij OS-level tools (zoom, kontrast,
screen reader).

## Související

- [Profil](index.md) — celá oblast
- [Export → PDF vs Word](../export/pdf-vs-word.md)
