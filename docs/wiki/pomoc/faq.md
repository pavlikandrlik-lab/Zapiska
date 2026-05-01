---
title: FAQ — nejčastější otázky
description: Rychlé odpovědi na typické situace.
---

# FAQ

Otázky seskupené podle oblasti. Pokud nenajdeš svoji situaci, podívej se na
[Řešení problémů](reseni-problemu.md) nebo [Kontakty](kontakty.md).

## Přihlášení a přístup

### 1. Proč se nemůžu přihlásit přes AD?

Aplikace tě poznává podle `Guid_AD`. Pokud ti aplikace vrací 401/403:

- **Záznam neexistuje v `dbo.osoby`** — admin musí založit
- **`Guid_AD` neodpovídá** tvému AD účtu — admin musí opravit

Detail: [Přihlášení](../zacatek/prihlaseni.md).

### 2. Synchronizují se osoby z AD automaticky do aplikace?

**Ne.** Sync atributů (jméno, OU, email) jede pravidelně, ale **nezakládá nové
osoby**. Pro přihlášení musí mít user záznam v `dbo.osoby` s vyplněným
`Guid_AD`.

### 3. Proč některé osoby vidím a jiné ne?

V seznamu osob zobrazuje aplikace všechny aktivní záznamy v `dbo.osoby`. Pokud
hledáš někoho v AD picker (přiřazení do týmu), to je **přímý dotaz na AD** —
vrátí se i osoby, které ještě v `osoby` nejsou.

## Oprávnění

### 4. Proč nevidím tlačítko *Upravit*?

Nejčastěji **chybí permission key** (`projects.edit`, `records.edit`, …) nebo
je permission omezený scope mimo aktuální projekt.

Diagnostika: Profil → [Moje práva](../profil/moje-prava.md). Pokud klíč chybí,
požádej admina.

### 5. Proč nevidím sekci *Nastavení*?

Sekce je dostupná jen pro role s permission `settings.view` nebo SuperAdmin.

### 6. Proč nelze upravit některý řádek v číselníku?

Možnosti:

- **Nemáš `ciselniky.edit`** → tlačítko *Upravit* je skryté
- **Položka je systémově uzamčená** (`is_locked=1`) → tlačítko vidíš, ale po
  kliku zobrazí informativní dialog

Detail: [Číselníky → Editace](../ciselniky/editace.md).

### 7. Jak ověřím, co konkrétní uživatel reálně může?

Admin v [Nastavení → Efektivní práva](../nastaveni-administrace/efektivni-prava/)
vybere **uživatele × projekt** a uvidí finální seznam permission keys, které
ten user na projektu má.

## Záznamy a jednání

### 8. Proč je jednání read-only?

Jednání ve stavu `CLOSED` je standardně **uzavřené pro běžné úpravy**.
Otevřít zpět ho může user s permission `meetings.reopen`.

Detail: [Uzavřené jednání](../projekty/jednani/uzavrene-jednani.md).

### 9. Proč při založení jednání dostávám chybu duplicitního čísla?

Číslo jednání musí být **unikátní v rámci jednoho projektu**. Pokud existuje
jednání se stejným číslem na stejném projektu (i archivované), aplikace
zamítne uložení.

### 10. Záznam mám propojený s SD ticketem, ale chat modal je prázdný

Možnosti:

- **Ticket existuje, ale ještě nemá vyjádření** v ServiceDesku
- **Harvest neprošel** — zkus *Re-harvest* z chat modalu nebo z `/SDConnector`
- **Connection na SD selhalo** — admin diagnostika přes `/SDConnector/Diag`

## ServiceDesk integrace

### 11. Kam vede klik na externí vazbu PMP/PNF/NES?

Na detail tiketu v ServiceDesku:

```
https://servicedesk.fis.acr/Hotline/Ticket/Details/{ticketId}
```

### 12. Proč některé externí vazby nejsou klikací?

Pokud hodnota **neobsahuje 6-ciferné číselné `id`**, odkaz zůstane jen
informativní text. Memory rule: ticket bez `id` = mimo scope, nikdy nemapujeme
na fallback `id=0`.

### 13. NES panel mého projektu je prázdný — proč?

- **Projekt nemá propojení na IS** (FIS / ISSP) → graceful state s odkazem
  do edit projektu
- **V daném IS aktuálně nejsou žádné NES tickety v prodlení** — opravdu prázdný
  výsledek
- **Ticketing.Enabled je false** v aplikaci (admin / deploy)

Detail: [Řešení problémů SD](../integrace/servicedesk/reseni-problemu-sd.md).

## Tisk a export

### 14. Lze exportovat víc projektů najednou?

V aktuální verzi UI **ne**. Tisk je per-projekt, per-jednání, per-záznam.

### 15. Proč generování PDF u velkého projektu trvá dlouho?

Aplikace generuje PDF synchronně. Velký projekt (> 500 záznamů, hodně
komentářů) může trvat desítky sekund. Pokud narazíš na timeout, zkus omezit
volby exportu (vyloučit komentáře, jen aktivní jednání, …).

## Synchronizace a admin

### 16. Jak často probíhá AD sync?

Periodicita je v konfiguraci (typicky každých několik hodin). Manuální trigger
je v [Nastavení → Synchronizace](../nastaveni-administrace/synchronizace/).

### 17. Jak často SD harvest stahuje vyjádření?

Dva joby:

- **Aktivní záznamy** — častější (typicky desítky minut)
- **Archivní záznamy** — řidší (typicky hodiny)

Plus **reaktivní sync** — okamžitý po změně vazby v UI.

## Obecné

### 18. Kde najdu detailní instalační a provozní dokumentaci?

- **Tato wiki** (jsi tady) — uživatelská a administrátorská
- **In-app technická dokumentace** — `/Dokumentace/Technicka/Strom-dokumentace`
- **Vývojářská dokumentace v repu** — `docs/technical/`, `docs/architecture/`

### 19. Kde hlásit incident / bug?

Viz [Kontakty](kontakty.md). Při hlášení uveď:

- Co jsi dělal (URL, akce)
- Co se stalo (chyba, špatný výsledek)
- Co jsi očekával
- **Trace-Id** (z chybové stránky / toast)
- Browser + verze (pokud je relevantní)

### 20. Jak nahlásit nápad / feature request?

Stejnou cestou jako bug — admin podpora. U nápadu zdůrazni use case ("chtěl
bych X protože Y").

## Související

- [Řešení problémů](reseni-problemu.md) — strukturovaný troubleshooting
- [Kontakty](kontakty.md)
- [Slovníček](../slovnicek.md) — pojmy
