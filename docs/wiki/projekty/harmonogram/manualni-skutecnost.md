---
title: Manuální skutečnost a režim Auto/Ručně
description: Jak ručně zadat skutečnost kroku a kdy aplikace přestane přepisovat.
---

# Manuální skutečnost — režim Auto vs. Ručně

Auto-fill ze SD je výborný default, ale někdy ho user chce **přepsat ručně**:

- Auto-fill vybral jiné vyjádření než user chce
- User chce zafixovat aktuální hodnotu (žádné další přepisování)
- SD vyjádření existuje ale klasifikace je *None* (auto-fill ho ignoruje)

Pro tyto situace má každý krok harmonogramu **přepínač Auto / Ručně**.

## Default režim — Auto

Pokud nic nezměníš, krok je v režimu **Auto**:

- Sync služba periodicky aktualizuje skutečnost ze SD vyjádření
- Pokud dorazí novější vyjádření, **přepíše dřívější**
- Pohyb harvestu = pohyb hodnoty

V buňce skutečnosti je badge **🤖 Automat** (nebo *— Neznámo* pokud žádné
vyjádření zatím není).

## Přepnutí na režim Ručně

Tlačítko **[Auto | Ručně]** v buňce skutečnosti přepíná režim.

Klikneš → confirm dialog ("Přepnout na Ručně? Sync služba pak skutečnost
nepřepíše."):

- **Potvrdíš** → režim se změní, hodnota zůstává
- **Zrušíš** → nic se nemění

Po přepnutí na Ručně:

- Sync služba krok **nepřepíše**, i kdyby dorazila nová vyjádření
- Hodnota zůstane na tom co tam je teď
- Badge se změní na **✍️ Manual**

## Tlačítko viditelné jen pro relevantní kroky

Tlačítko Auto / Ručně je k dispozici **pouze pokud**:

1. Krok existuje v `zaznam_harmonogram_hodnoty` (`DelayHodnotaId` je vyplněné)
2. User má permission `harmonogram.toggle-rezim`
3. Krok není uzamčený `pendingScheduleProposalLock` (není rozjetý
   [návrh](../navrhy/) na změnu plánu)

Pokud podmínky nesplněné, tlačítko není v UI.

## Manuální zadání hodnoty

V režimu Ručně můžeš **přímo zadat číslo dní** do buňky skutečnosti:

1. Klikni do buňky → otevře se inline editor
2. Zadej číslo (celé, kladné nebo záporné)
3. Enter → uloží

### Validace

- Musí být **celé číslo** (žádné desetinné)
- Rozsah dle business pravidel (typicky -90 až +180 dní)
- Mimo rozsah → inline chyba

## Audit zdroje

Po každé změně skutečnosti se aktualizuje **`SkutecnostZdroj`**:

| Akce | Nový zdroj |
|---|---|
| Auto-fill (sync) | 🤖 Automat |
| User zadal ručně | ✍️ Manual |
| Migrace dat (před auto-fill) | 📜 Historicka |
| Reset / žádná hodnota | — Neznámo |

Badge je **vidět vedle hodnoty**. Tooltip vysvětluje detaily ("Skutečnost
vyplněná automatem ze SD vyjádření #1234").

## Dropdown alternativních kandidátů

Pokud má krok **více vyjádření klasifikovaných stejným predikátem**, je
k dispozici **dropdown s kandidáty**:

```
Skutečnost: +2 dny 🤖

[ Vybrat kandidáta ▾ ]
  ✓ Vyjádření #1234 (24.4.2026): "Dodáno..."  ← aktuálně zvolené
    Vyjádření #1235 (25.4.2026): "Oprava: dodávka..."
    Vyjádření #1236 (26.4.2026): "Finální verze..."
```

Klik na jiného kandidáta:

- Aplikace **automaticky přepne na režim Ručně**
- Hodnota se přepočítá z data zvoleného vyjádření
- Badge se změní na ✍️ Manual

Důvod: výběr kandidáta = explicitní user volba, sync nemá přepisovat.

## Vrátit zpět na Auto

Pokud po čase chceš **vrátit krok na Auto**:

1. Klikni *Auto* na přepínači
2. Aplikace se zeptá: "Spustit sync teď?"
   - **Ano** → re-harvest, hodnota se přepočítá z aktuálních vyjádření
   - **Ne** → režim se přepne ale hodnota zůstává; další periodický sync
     ji eventuálně přepíše

Permission `harmonogram.toggle-rezim`.

## Vztah k návrhům

Pokud existuje [návrh](../navrhy/) na změnu plánu pro tento krok:

- Krok je **uzamčený** (`pendingScheduleProposalLock`)
- Tlačítko Auto/Ručně je **disabled**
- Manuální editace není možná
- Auto-fill běží dál na pozadí, ale výsledek se nezobrazuje na buňce

Po vyřešení návrhu (apply / zamítnutí / stažení) se krok odemkne.

## Hromadné akce

V aktuální verzi UI nejsou hromadné akce typu "přepnout všechny kroky
záznamu na Ručně". Akce se dělá per-krok.

## Permissions

| Akce | Klíč |
|---|---|
| Vidět skutečnost | `harmonogram.view` (implicitní) |
| Editovat hodnotu (Ručně) | `harmonogram.edit` |
| Přepínat Auto/Ručně | `harmonogram.toggle-rezim` |
| Vybrat kandidáta z dropdownu | `harmonogram.select-candidate` |

## Související

- [Auto-fill ze SD](auto-fill-ze-sd.md)
- [Plán versus skutečnost](plan-vs-skutecnost.md)
- [Návrhy](../navrhy/)
- [Chat vyjádření](../../integrace/servicedesk/chat-vyjadreni.md) — odkud vyjádření přicházejí
