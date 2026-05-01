---
title: Kontakty
description: Telefony, ServiceDesk, support pro PM Tracker a propojené systémy.
---

# Kontakty

Pokud sis ve [FAQ](faq.md) ani v [Řešení problémů](reseni-problemu.md) nenašel
odpověď, kontaktuj podporu.

## Hotline / podpora propojených systémů

Pro problémy s **konkrétním informačním systémem** (ne s PM Trackerem):

| Systém | Telefon | Účel |
|---|---|---|
| **FIS** | 973 200 840 | Finanční informační systém |
| **ISSP** | 973 225 500 | Informační systém služebních poměrů |
| **ŠIS** | 973 211 111 | Štábní informační systém |

## ServiceDesk

Pro **vznášení tiketů** přímo do SD (PMP / PNF / NES):

- **URL**: [https://servicedesk.fis.acr](https://servicedesk.fis.acr)
- **Detail ticketu**: `https://servicedesk.fis.acr/Hotline/Ticket/Details/{id}`

> **Pozor:** PM Tracker do SD **nezapisuje**. Nový ticket vytvoříš přímo
> v ServiceDesku, PM Tracker ho pak read-only zobrazí.

## PM Tracker — aplikační podpora

Pro problémy s **PM Trackerem samotným** (přihlášení, oprávnění, bug, feature
request):

- **Aplikační admin** — kontakt z interní organizační struktury
- **Hlášení incidentů** — typicky přes ServiceDesk ticket nebo email
  aplikačního admina
- **Office hours** — pracovní doba dle org. zvyklostí

> Tato sekce je úmyslně **bez konkrétních jmen / mailů** — kontakty jsou
> per-instalace (organizace si plní podle svých interních pravidel) a v
> průběhu času se mění. Aktualizovat při každé personální změně by bylo
> nespolehlivé. Pro aktuální kontakt se obrať na svého IT správce nebo na
> intranetovou stránku org. struktury.

## Eskalační matice

```
Úroveň 1: Aplikační admin (běžné dotazy, oprávnění, návody)
   │
   ▼ pokud nevyřeší
Úroveň 2: Aplikační podpora — vyšší úroveň (bugs, integrace)
   │
   ▼ pokud nevyřeší / je to vývojářská záležitost
Úroveň 3: Vývojový tým (změny v kódu, deploy, infrastruktura)
```

## Co poslat při hlášení

Bez těchto údajů hlášení nemůžeme efektivně řešit:

1. **Co jsi dělal** — URL, akce, vstupní data
2. **Co se stalo** — chyba, zpráva, screenshot
3. **Co jsi očekával** — co mělo proběhnout
4. **Trace-Id** — z chybové stránky / z toast notifikace
5. **Tvůj login** — abychom mohli ověřit oprávnění a sledovat audit log
6. **Browser + verze** — pokud je problém na klientu (Edge 121, Chrome 122, …)
7. **Čas události** — abychom mohli najít event v logu

## Šablona pro mail / tiket

```
Předmět: PM Tracker — [krátký popis]

Co jsem dělal:
1. Otevřel jsem URL https://...
2. Klikl jsem na *Upravit projekt*
3. Vyplnil jsem pole X = ...
4. Klikl jsem *Uložit*

Co se stalo:
[chyba / nečekané chování / screenshot]

Co jsem očekával:
[normální průběh akce]

Trace-Id: [ze stránky / toastu]
Login: [doménový account]
Browser: [Chrome 122 / Edge / Firefox]
Čas: [timestamp s timezone]
```

## Kdy NE mailovat

- **Akutní výpadek** — telefon hotline je rychlejší
- **Žádost o role / oprávnění** — typicky stačí krátký mail / tiket bez šablony
- **Otázka kterou pokrývá [FAQ](faq.md)** — projdi nejdřív FAQ
