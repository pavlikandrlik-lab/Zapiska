# Specifikace — harmonogram: plán vs. skutečnost

**Stav:** rozpracováno (fáze 4 implementace)
**Závisí na:** [automat-vytezovani-vyjadreni.md](automat-vytezovani-vyjadreni.md),
[ticketing-integration.md](ticketing-integration.md)

## Kontext

Dnes se v editoru záznamu ruční edituje **oboje**: plán i skutečnost harmonogramu.
**Nová logika:**

- **Plán** — vyplňuje a schvaluje projektový manažer (stav dnes). Editovatelný.
- **Skutečnost** — **automaticky odvozená** ze stavu ticketů přes tagy na vyjádřeních ticketů.
  **Read-only** v editoru záznamu.

## Chování

### Plán

Beze změny oproti dnešku:

- PM / PMP vyplní plánované termíny kroků harmonogramu
- Prochází schválením (existující flow)
- Editovatelný do schválení, poté uzamčen (audit)

### Skutečnost — výpočet

Pro každý krok harmonogramu záznamu:

1. Najdi všechny `TicketVyjadreniTag` s `TagKod = <krok>` pro vazby záznamu → ticket
2. Najdi **MIN(Datum)** vyjádření s tímto tagem
3. To je **skutečné datum** kroku

Pokud žádný tag neexistuje → skutečnost je **nezjištěna** (UI zobrazí „–").

### UI změny

V editoru záznamu (`_EditZaznamForm.cshtml`) karta **Harmonogram**:

- **Tabulka kroků** — 3 sloupce: *Krok / Plán (editovatelný) / Skutečnost (read-only)*
- Sloupec **Skutečnost**:
  - Hodnota nebo „–"
  - Ikona zdroje: automat 🤖 / manuál 👤 (po najetí tooltip: pravidlo/osoba + datum záznamu tagu)
  - Klik → otevírá modal „Externí vazby → vyjádření ticketu" (viz `automat-vytezovani-vyjadreni.md`)
- Sloupec **Plán** zůstává editovatelný dle stávajících permission pravidel

### Co se stane se stávajícími daty

Při migraci (fáze 4):

1. Existující **skutečnost** v DB zůstává viditelná jako **historická**
   (fallback, pokud neexistuje žádný tag)
2. Nová logika má **přednost**: tag → MIN(Datum) překryje historickou skutečnost
3. Po měsíci běhu na produkci + ověření se historická skutečnost smaže migraci

### Dopad na close-guard modal

Dnes close-guard dirty tracking sleduje i pole skutečnosti harmonogramu
(viz [modal-close-guard.md](modal-close-guard.md)). Po změně:

- Pole skutečnosti **nebudou v DOM** jako input → automaticky zmizí z tracking
- `UiHarmonogramDatumy` (vypočítaná pole) již ignorována v close-guardu
- **Žádná změna** v close-guard logice potřebná

## Validace

- Plán musí být **<= skutečnost** (pokud skutečnost existuje). Jinak warning, ne error
  (může jít o zpoždění, které je záměrné)
- Pokud skutečnost je před plánem → UI vykreslí **zelený** badge "v předstihu"
- Pokud skutečnost >= plán → **červený** "zpožděno"
- Pokud skutečnost chybí a plán je v minulosti → **žlutý** "čeká"

## API a datový model

### Existující tabulka `ProjektovyZaznamHarmonogram`

Sloupce *PlanDatum* zůstávají. Sloupce *SkutecnostDatum* se budou **dopočítávat**,
uloží se však stále pro rychlé čtení (denormalizace).

**Nové pole:**

| Sloupec | Typ | Popis |
|---|---|---|
| `SkutecnostZdrojEnum` | tinyint | 0=Neznámo, 1=Automat, 2=Manual, 3=Historicka (pre-migration) |
| `SkutecnostPosledniPrepocet` | datetime2 | Kdy byl MIN(Datum) naposled přepočítán |

### Recurring job — `HarmonogramSkutecnostSyncJob`

Hangfire job (viz `ticketing-integration.md`):

- Běží každých **15 min**
- Pro každý aktivní ticket (netriviální počet) přepočítá MIN(Datum) dle tagů
- Zapíše do `ProjektovyZaznamHarmonogram.SkutecnostDatum` + `SkutecnostPosledniPrepocet`
- Interval konfigurovatelný v UI `/Nastaveni/Integrace`

**Alternativa (lepší, ale složitější):** event-driven — pokud přidán/smazán tag,
vyvolá se `IHarmonogramRecomputeService.RequestRecompute(ticketId)`, který
invalidátne cached skutečnost. Job pak jen řeší fallback (přehlédnuté invalidace).

Doporučení: **začít s polling** (jednodušší), přejít na event-driven v pozdější iteraci,
pokud dojde k výkonnostním problémům.

## Testy (fáze 4)

- **Unit** `HarmonogramSkutecnostServiceTests` — MIN(Datum) výpočet, edge cases
  (žádný tag, víc tagů, manuál před automat)
- **Integration** — DB test s fixture: záznam + ticket + tagy + ověření přepsaných polí
- **Regression** — migrace ze stavu „historická skutečnost" na nový systém

## Otevřené otázky

| # | Otázka | Kdo rozhodne | Deadline |
|---|---|---|---|
| H1 | **Redukce kroků harmonogramu** — vedení zvažuje škrtnutí některých kroků, které nelze zjistit automaticky | Vedení | Před fází 4 |
| H2 | Jak naložit se záznamy bez vazby na ticket (organizační, jiné typy)? | Claude doporučení: ponechat ruční edit skutečnosti + badge „off-ticket" | Před fází 4 |
| H3 | Víc ticketů na jeden záznam — jak kombinovat skutečnost? | Claude doporučení: MIN(Datum) přes všechny tagy všech vazebních ticketů | Při implementaci |
| H4 | Zamčení plánu při schválení — zmrazit i schvalovací datum? (historie) | Claude doporučení: ano, snapshot do audit tabulky | Při implementaci |
| H5 | Termíny vypořádání připomínek — zvažovaný nový krok | Vedení | Před fází 4 |

## Zodpovědnost

- **Redukce kroků:** vedení
- **Mapování kroků na tagy:** projektový manažer (konfigurace pravidel, viz automat-vytezovani-vyjadreni.md)
- **Migrace historické skutečnosti:** dodavatel po ověření 1 měsíce produkčního běhu
- **UI změny editoru:** Claude (dle fáze 2 komponent)
