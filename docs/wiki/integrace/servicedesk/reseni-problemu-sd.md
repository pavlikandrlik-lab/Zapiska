---
title: Řešení problémů — ServiceDesk integrace
description: Co dělat když SD integrace nefunguje (nezobrazují se data, NES panel je prázdný, atd.).
---

# Řešení problémů — ServiceDesk

Strukturovaný troubleshooting podle příznaku problému. Pokud nenajdeš svoji
situaci, viz [Pomoc → Kontakty](../../pomoc/kontakty.md).

## NES panel projektu je prázdný

### Příznak

Otevřeš projektový dashboard → panel **NES v prodlení** je prázdný (0 řádků)
nebo zobrazuje *graceful state*.

### Možné příčiny

#### 1. Projekt nemá propojení na IS

**Diagnostika:** detail projektu → tlačítko *Upravit* → ověř dropdown
**Informační systém**.

Pokud je *— bez napojení —*, panel ukáže graceful state. Vyřešení: vyber
FIS / ISSP a ulož.

Detail: [Propojení projektu na IS](propojeni-projekt-is.md).

#### 2. V daném IS opravdu nejsou žádné NES v prodlení

**Diagnostika:** otevři `/SDConnector/Inspect` se zkušebním NES ticketem,
ověř že existuje a má překročený termín.

Vyřešení: pokud reálně žádné NES neexistuje, panel je prázdný správně.
Není to bug.

#### 3. Filtry vyřadily všechny tickety

NES panel filtruje **stav** (typicky aktivní stavy, ne uzavřené) a **termín**
(překročený). Pokud má daný IS jen uzavřené nebo dosud neexpirované NES,
panel je prázdný.

#### 4. `Ticketing.Enabled = false`

**Diagnostika:** `/SDConnector` → KPI ukazuje "Vypnutá".

Vyřešení: admin musí v `appsettings.json` nastavit `Ticketing.Enabled = true`,
zajistit valid connection string a restartovat app pool.

## Externí vazba říká *"Ticket nenalezen"*

### Příznak

V editoru záznamu vyplníš číslo ticketu, aplikace odpoví "Ticket #X v
HOT_ZAZNAMY neexistuje."

### Možné příčiny

#### 1. Číslo neexistuje v `HOT_ZAZNAMY`

**Diagnostika:** otevři `/SDConnector/Inspect?cislo=XXXXXX`. Pokud taky řekne
"neexistuje", ticket reálně v aktivním HOT_ZAZNAMY není.

Vyřešení: ověř číslo v ServiceDesku přímo. Možná překlep, nebo ticket je
v archivované DB (mimo aktivní set).

#### 2. SD connection nefunguje

**Diagnostika:** otevři `/SDConnector/Diag?cislo=363139` (libovolné existující
číslo). Pokud Diag nehlásí *ALL STAGES PASSED*, máš connection problém.

Typické chyby:

- `Login failed for user '...'` — špatné heslo
- `Cannot open database "intranetNEW"` — chybí `CONNECT` permission
- `SELECT permission was denied` — chybí `db_datareader`
- `Network-related error` — firewall mezi PM Tracker a SD SQL serverem
- `InvalidCastException` — schema mismatch (entity vs reálná DB)

Vyřešení: záleží na typu chyby — viz konkrétní oprava níže.

## Aplikace má `Ticketing.Enabled=true` ale nic z SD se nezobrazuje

### Plain-text diagnostika přes /Diag

Otevři `/SDConnector/Diag?cislo=XXXXXX` (libovolné existující číslo). Endpoint
projde celou pipelinu a stage-by-stage řekne kde to selhalo.

### Login failed for user 'X'

**Příčina:** špatné heslo v connection stringu (typicky placeholder zůstal,
heslo se změnilo, nebo chybí escape speciálních znaků).

**Vyřešení:**

```json
"ConnectionStrings": {
  "TicketingReadOnly": "Server=...;Database=intranetNEW;User Id=...;Password=<heslo>;..."
}
```

Po opravě recyklovat app pool.

### Cannot open database "intranetNEW"

**Příčina:** read-only login nemá CONNECT permission na DB.

**Vyřešení:**

```sql
USE intranetNEW;
CREATE USER [pm_tracker_ro] FOR LOGIN [pm_tracker_ro];
ALTER ROLE db_datareader ADD MEMBER [pm_tracker_ro];
```

### SELECT permission was denied

**Příčina:** user existuje a může se připojit, ale nemá `db_datareader`.

**Vyřešení:**

```sql
USE intranetNEW;
ALTER ROLE db_datareader ADD MEMBER [pm_tracker_ro];
```

### Network-related error

**Příčina:** PM Tracker server nemá síťovou cestu k SD SQL serveru.

**Diagnostika z PM Tracker serveru:**

```powershell
Test-NetConnection -ComputerName <SD_HOST> -Port 1433
```

**Vyřešení:** infra fix (firewall, NSG, route). Mimo scope aplikace.

### InvalidCastException při materializaci

**Příčina:** schema mismatch mezi entity v PM Trackeru a reálnou SD DB
(např. PM Tracker očekává `int`, DB má `bigint`).

**Diagnostika:** plný stack trace v `/SDConnector/Diag` výstupu.

**Vyřešení:** typicky úprava entity / mapping v `TicketingReadOnlyDbContext`.
Vývojářská práce + deploy.

## Chat modal nezobrazuje žádná vyjádření

### Příznak

Otevřeš chat modal pro ticket → bubliny jsou prázdné, žádná chyba.

### Možné příčiny

1. **Ticket existuje ale ještě nemá vyjádření** — zcela validní stav
2. **Harvest ještě neproběhl** pro tento ticket
3. **Text vyjádření je v cizí HTML struktuře** kterou aplikace neumí parsovat
   (vzácné)

**Diagnostika:** `/SDConnector/Inspect?cislo=XXXXXX` ukáže raw seznam vyjádření
přímo z `HOT_VYJADRENI`. Pokud Inspect ukáže bubliny ale chat modal ne, je
to bug v UI komponentě.

**Vyřešení:** zkus *Re-harvest* z `/SDConnector` nebo z chat modalu samotného.

## Auto-fill harmonogramu nedoplňuje skutečnost

### Příznak

Krok harmonogramu má prázdnou skutečnost (— Neznámo badge) i když v SD
existují odpovídající vyjádření.

### Možné příčiny

#### 1. Klasifikace = None

Vyjádření je tam, ale text neodpovídá žádnému harvest predikátu (K3 / K4_K7 /
K6 / K10 / Plán dodání).

**Diagnostika:** `/SDConnector/Inspect` → bubliny → ověř badge klasifikace.

**Vyřešení:** nelze obejít z UI. Buď přidat predikát (vývojářská změna), nebo
zadat skutečnost ručně (přepnout krok na Ručně).

#### 2. Krok nemá nastavený typ delay

Krok harmonogramu musí mít vyplněný `ZpozdeniTypId` (HS0X_DELAY) v
`zaznam_harmonogram_hodnoty`. Bez něj sync neví, na který predikát mapovat.

**Diagnostika:** otevři SQL dotaz na DB (admin):

```sql
SELECT * FROM zaznam_harmonogram_hodnoty
WHERE zaznam_id = ? AND typ_id LIKE '%DELAY%';
```

**Vyřešení:** doplnit typ delay přes editor záznamu nebo migraci.

#### 3. Režim je Ručně

User cíleně přepnul krok na Ručně — sync nepřepíše hodnotu.

**Diagnostika:** badge u skutečnosti zobrazuje ✍️ Manual.

**Vyřešení:** přepnout zpět na Auto (pokud chce auto-fill).

## SD harvest běží pomalu / má fail-rate

### Diagnostika

`/SDConnector` → KPI "Vytěženo za 24 h" vs. "Celkem externích vazeb". Pokud
poměr je nízký, harvest má problémy.

V Nastavení → Synchronizace → SD harvest jsou detaily posledního běhu.

### Možné příčiny

- **SD SQL je pomalý** (denní backup window, vysoká zátěž jiných systémů)
- **Velký počet vazeb** — harvest stagger neumí zpracovat všechno za interval
- **Network latency** mezi PM Tracker a SD

### Vyřešení

- Zvýšit `CommandTimeoutSeconds` v `Ticketing` config (default 30s)
- Zkrátit harvest interval pro aktivní vazby (`SdActivePeriodicSyncHostedService`)
- Vyloučit nepoužívané vazby (manuálně označit `is_active=0`)

## Eskalace

Pokud problém nezvládneš sám:

- [Pomoc → Kontakty](../../pomoc/kontakty.md)
- Vždycky pošli **Trace-Id** + screenshot + co jsi zkusil

## Související

- [SD konektor — diagnostika](sd-konektor-diagnostika.md) — admin tooling
- [Synchronizace → SD harvest](../../nastaveni-administrace/synchronizace/sd-harvest.md)
- [Auto-fill skutečnosti](auto-fill-skutecnosti.md)
