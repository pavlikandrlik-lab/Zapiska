---
title: ServiceDesk
description: Propojení PM Trackeru s firemním ServiceDeskem (FIS, ISSP) — tickety, vyjádření, harmonogram.
---

# ServiceDesk (SD)

ServiceDesk je firemní hotline systém pro evidenci ticketů (PMP, PNF, NES,
další). Existuje **mimo PM Tracker** — má vlastní UI, vlastní DB, vlastní
workflow. PM Tracker se k němu připojuje jako **read-only** klient.

## Proč existuje propojení

Bez SD by uživatel musel manuálně:

- Kopírovat tickety z SD do PM Trackeru (duplikace evidence)
- Sledovat dvě prostředí (SD pro tickety, PM Tracker pro úkoly)
- Ručně přepisovat termíny dodání z SD vyjádření do projektového harmonogramu

Propojení **automatizuje** většinu této práce:

- Záznam PM Trackeru může mít externí vazbu na SD ticket — z 6-ciferného `id`
- Vyjádření z SD se automaticky načítají do chat modalu na záznamu
- Klasifikační pravidla rozeznávají, které vyjádření odpovídá kterému kroku
  harmonogramu, a navrhnou skutečnost
- Projektový dashboard ukazuje NES tickety v prodlení daného IS (FIS / ISSP)

## Hlavní integrační body

| Co | Pro koho | Detail |
|---|---|---|
| Propojení projekt ↔ informační systém (FIS / ISSP) | Project leader | [Propojení projektu na IS](propojeni-projekt-is.md) |
| Propojení záznam ↔ SD ticket (PMP / PNF / NES) | Uživatel | [Tickety PMP/PNF/NES](tickety-pmp-pnf-nes.md) |
| Chat modal s vyjádřeními z ticketu | Uživatel | [Chat vyjádření](chat-vyjadreni.md) |
| Auto-fill skutečnosti harmonogramu | Uživatel | [Auto-fill skutečnosti](auto-fill-skutecnosti.md) |
| SD konektor — diagnostika harvestu | Admin | [SD konektor — diagnostika](sd-konektor-diagnostika.md) |
| Řešení problémů s SD integrací | Admin | [Řešení problémů SD](reseni-problemu-sd.md) |

## Read-only filozofie

PM Tracker do SD **nikdy nezapisuje**. Veškerá data ze SD jsou čtená
prostřednictvím dedikovaného read-only DB connectionu (login má jen
`db_datareader` na `intranetNEW`).

Důsledky:

- Chyba v PM Trackeru **nemůže poškodit** ServiceDesk
- Nový ticket / vyjádření zadáš **přímo v ServiceDesku** — PM Tracker ho
  pak při dalším syncu nasaje
- Ručně psaný PM Tracker ticket bez vazby na SD existuje (typ "Úkol"), ale
  není vidět v SD

## Co ServiceDesk **nedělá**

- Nezakládá projekty ani uživatele v PM Trackeru (je to opačně — PM Tracker
  čte ze SD)
- Nemění žádný stav v PM Tracker DB
- Neposkytuje data o uživatelích PM Trackeru (autentifikace jde přes AD, ne SD)

## Klíčové entity v SD (které čteme)

Tabulky v `intranetNEW.dbo`:

| Tabulka | Obsah | Co PM Tracker čte |
|---|---|---|
| `HOT_ZAZNAMY` | Tickety (NES, PMP, PNF, …) | Hlavička, pid, stav, termín |
| `HOT_VYJADRENI` | Vyjádření / komentáře k ticketům | Text, autor, datum |
| `HOT_KALKULACE` | Finanční rozpis PMP | Hodiny × sazba |
| `HOT_SUBSYSTEM` | Subsystémy IS | Pro filtraci subsystémů |
| `HOT_MODULY` | Moduly subsystémů | Doplňková data |
| `HOT_IS` | Katalog informačních systémů | Pro propojení projekt → IS |

PM Tracker tyto tabulky čte, **nikdy do nich nezapisuje**. Detail schématu:
`docs/technical/12-servicedesk-schema-reference.md`.

## Pro koho je sekce

- **Uživatel** — propojení projektů na IS, chat s vyjádřeními, auto-fill
  harmonogramu
- **Admin** — všechno + diagnostika a troubleshooting

## Konfigurace

Nastavení `Ticketing` v `appsettings.json`:

```json
"Ticketing": {
  "Enabled": true,
  "ConnectionStringName": "TicketingReadOnly",
  "CommandTimeoutSeconds": 30
}
```

Plus `ConnectionStrings.TicketingReadOnly` s connection na `intranetNEW`
(read-only login s `db_datareader`).

Detail setup: `docs/technical/13-servicedesk-connection-setup.md`.

Pokud `Enabled = false`, aplikace registruje "Disabled" stub services — žádné
SD volání nelze provést, ale aplikace dál funguje (pro projekty bez SD vazby).

## Pro vývojáře — read-only context

Aplikace má v kódu `TicketingReadOnlyDbContext` (oddělený od
`PmTrackerDbContext`). Hard guard:

```csharp
public override int SaveChanges()
    => throw new InvalidOperationException("TicketingReadOnlyDbContext is strictly read-only.");
```

I když by někdo omylem zavolal SaveChanges, aplikace exception. Plus měkký
guard `ApplicationIntent=ReadOnly` v connection stringu.

## Související

- [Active Directory](../active-directory/) — druhá hlavní integrace
- [Nastavení → Synchronizace → SD harvest](../../nastaveni-administrace/synchronizace/sd-harvest.md)
