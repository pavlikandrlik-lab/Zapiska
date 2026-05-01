---
title: Auto-fill skutečnosti harmonogramu
description: Jak SD vyjádření automaticky doplňuje skutečné termíny v harmonogramu projektu.
---

# Auto-fill skutečnosti harmonogramu

PM Tracker dokáže **automaticky doplnit skutečnost harmonogramu** ze SD
vyjádření. To je hlavní přínos integrace se ServiceDeskem — uživatel nemusí
ručně přepisovat termíny dodání z SD do PM Trackeru.

Tato stránka popisuje **jak auto-fill funguje z pohledu SD integrace**. Pro
user-friendly variantu z pohledu projektu viz
[Projekty → Harmonogram → Auto-fill ze SD](../../projekty/harmonogram/auto-fill-ze-sd.md).

## Princip — od vyjádření k skutečnosti

```
SD vyjádření           Klasifikace            Vazba na krok          Skutečnost
─────────────         ─────────────          ─────────────          ─────────────
HOT_VYJADRENI    →    Harvest predikát   →   vyjadreni_vazby   →   zaznam_harmonogram
"Dodáno...      "      K4_K7                  krok HS04             _hodnoty.HodnotaInt
"K1.5.2026..."         (Dodání řešení)        (přes ZpozdeniTypId)   (delay v dnech)
```

## Předpoklady

Pro auto-fill musí být splněno:

1. **Záznam má externí vazbu** na SD ticket (6-ciferné `id`)
2. **Krok harmonogramu má nastavený typ delay** (HS0X_DELAY) — řídí na který
   predikát se mapuje
3. **Vyjádření v SD je klasifikované** harvest predikátem (ne *None*)
4. **Krok je v režimu Auto** (default) nebo má fixní preferred kandidáta

## Harvest predikáty — texty které aplikace hledá

Aplikace klasifikuje vyjádření **podle textu** v `HOT_VYJADRENI.popis`.
Detekce je `Contains` match (case-insensitive). Pořadí kontroly je dáno —
specifičtější predikáty první (např. `PlanDodani` obsahuje fragment "předal
záznam dodavateli", takže se kontroluje **před** K4_K7).

5 typů predikátů (zdroj: `HarvestPredicates.cs`):

### K3 — Odeslání zadání PMP

```
"Záznam byl založen a předán dodavateli k řešení pod značkou:"
```

Vzniká když dodavatel přijme PMP zadání a založí si ho s identifikátorem
("pod značkou: XYZ"). Pro PMP záznam → krok 3.

### K4 / K7 — Dodání řešení

```
"Dodavatel přidal řešení"
```

Vzniká když dodavatel dodá řešení / aktualizaci. Pro:
- **PMP** → krok 4 (dodání řešení)
- **PNF** → krok 7 (dodání funkcionality)

V harvest enum je tento predikát sdílený (`K4_K7_DodaniReseni`), liší se jen
mapování na krok podle typu tiketu.

### K6 — Odeslání požadavku

```
"Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována."
```

Vzniká když je akceptována kalkulace PNF a požadavek se předá dodavateli.
Pro PNF záznam → krok 6.

### K10 — Nasazení / archivace

```
"Záznam byl převeden do archivu."
```

Vzniká při archivaci ticketu (terminal stav). Pro PMP a PNF → krok 10.

### Plán dodání

```
Vyjádření obsahuje OBĚ fráze:
  - "předal záznam dodavateli :"
  - "s termínem plnění dodavatele"
```

Vzniká když dodavatel akceptuje termín plnění. Tento predikát neodpovídá
konkrétnímu kroku v auto-fill matici — slouží jako informativní pohled
v chat modalu (badge na bublině).

### None — žádný match

Pokud text vyjádření neodpovídá žádnému predikátu, klasifikace = `None`.
Vyjádření se v chat modalu **zobrazí**, ale **nepoužije se pro auto-fill**.

## SQL LIKE patterns (pro DB-side filter)

Aplikace má pro každý predikát SQL LIKE pattern (z `HarvestPredicates.GetSqlLikePattern`).
Slouží pro efektivní harvest jobs — místo memory scan se filtruje přímo v SQL:

```sql
-- K3
WHERE popis LIKE '%Záznam byl založen a předán dodavateli k řešení pod značkou:%'

-- K4_K7
WHERE popis LIKE '%Dodavatel přidal řešení%'

-- K6
WHERE popis LIKE '%Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována.%'

-- K10
WHERE popis LIKE '%Záznam byl převeden do archivu.%'

-- PlanDodani
WHERE popis LIKE '%předal záznam dodavateli :%s termínem plnění dodavatele%'
```

## Vazba krok ↔ predikát (mapování)

Každý krok harmonogramu má v `zaznam_harmonogram_hodnoty` typ delay
(HS0X_DELAY) přes `ZpozdeniTypId`. Tento typ se mapuje na konkrétní predikát
podle **typu tiketu × pořadí kroku** (z `HarmonogramKrokDatumMapping.cs`):

| Typ tiketu | Krok 3 | Krok 4 | Krok 6 | Krok 7 | Krok 10 |
|---|---|---|---|---|---|
| **NES** | — | — | — | — | — |
| **PMP** | K3 | K4_K7 | — | — | — |
| **PNF** | — | — | K6 | K4_K7 | K10 |

**NES** nemá žádné auto-fill kroky (jen K1 = datum založení, manuální).

**Kroky 1, 2, 5, 8, 9** nejsou v matici — buď manuální, nebo mimo aktivní
sadu typu. Aplikace pro ně nedělá auto-fill.

Vývojářská reference: `HarmonogramKrokDatumMapping.cs`,
`HarmonogramSkutecnostResolver.cs`.

## Sync služba — workflow

`HarmonogramSkutecnostSyncService` je jádro auto-fillu. Spouští se:

- **Reaktivně** — po vytvoření / smazání vazby v chat modalu
- **Periodicky** — `SdActivePeriodicSyncHostedService` /
  `SdArchivePeriodicSyncHostedService`
- **Manuálně** — z `/SDConnector` re-harvest

### Krok po kroku

1. Pro každý záznam s externí vazbou → načti vyjádření z `HOT_VYJADRENI`
2. Klasifikuj každé vyjádření predikátem
3. Pro každý krok harmonogramu (s typem delay):
   - Najdi vyjádření matching daný predikát
   - Pokud je víc kandidátů, vyber **nejnovější** podle data
   - Pokud má krok **preferred kandidáta** (manuální fixace), použij ho
4. Spočítej **delay v dnech** (rozdíl skutečnosti × baseline plánu)
5. Zapiš do `zaznam_harmonogram_hodnoty.HodnotaInt`
6. Aktualizuj `SkutecnostZdroj` na `Automat`

### Idempotence

Sync je idempotentní — opakované spuštění se stejnými daty produkuje stejný
výsledek. Žádné data race conditions, žádné duplicitní zápisy.

### Conflict handling — Auto vs Manual režim

| Stav před sync | Vyjádření změnilo se | Akce sync |
|---|---|---|
| Auto, žádný delay | Nový kandidát | Nastaví delay |
| Auto, existující delay | Novější kandidát | Přepíše delay |
| Auto, existující delay | Žádný novější | Nechá hodnotu |
| Manual, fixní preferred | Změnilo se preferred | Přepíše delay (sleduje user volbu) |
| Manual, vlastní hodnota | Cokoli v SD | Nechá hodnotu (user fixoval) |

## Audit zdroje (badge)

V buňce skutečnosti je vždy **badge** říkající odkud hodnota přišla:

| Ikona | Zdroj | Význam |
|---|---|---|
| 🤖 | Automat | Auto-fill ze SD vyjádření |
| ✍️ | Manual | Uživatel zadal ručně |
| 📜 | Historicka | Migrovaná před zavedením auto-fill (před deploy 2026-04-25) |
| — | Neznámo | Žádná skutečnost zatím |

`SkutecnostZdroj` je v DB sloupci `dbo.zaznam_harmonogram_hodnoty.skutecnost_zdroj`
jako TINYINT (enum `SkutecnostZdrojEnum`).

## Limity

### Více kandidátů, žádný preferred

Pokud má krok více vyjádření matching predikát, default volba = **nejnovější**.
Někdy to není správně (např. dodavatel pošle "dodávka 15.4." a později
"oprava: dodávka 18.4." — chceme tu druhou). User pak musí ručně přepnout
na **Ručně** a vybrat preferred kandidáta z dropdownu.

### None klasifikace

Pokud SD obsahuje vyjádření s relevantním textem, ale formulace neodpovídá
predikátu (např. "termín se posouvá na 30.4." místo "Plán dodání: 30.4."),
auto-fill ho **přeskočí**. User to musí zachytit ručním zápisem nebo přidáním
nového predikátu (vývojářská změna).

### Migrace historických dat

Záznamy existující před zavedením auto-fillu mají `SkutecnostZdroj = Historicka`
(typicky migrované přes SQL skript). Sync je **nezasahuje** — historická data
respektuje jako manuální.

## Související

- [Projekty → Harmonogram → Auto-fill ze SD](../../projekty/harmonogram/auto-fill-ze-sd.md) — user pohled
- [Manuální skutečnost a režim Auto/Ručně](../../projekty/harmonogram/manualni-skutecnost.md)
- [Chat vyjádření](chat-vyjadreni.md)
- [SD harvest sync](../../nastaveni-administrace/synchronizace/sd-harvest.md)
