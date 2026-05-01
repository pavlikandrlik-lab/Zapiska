---
title: Moje priority
description: Co tě čeká — záznamy seřazené podle urgentnosti napříč projekty.
---

# Moje priority

Panel **Moje priority** je hlavní pracovní pohled na uživatelském dashboardu.
Cíl: jediný pohled odpovídající otázku "co mám dnes dělat" napříč všemi
projekty.

## Co panel ukazuje

Záznamy ke kterým jsi přiřazen jako:

- **Vlastník** (primary owner)
- **Collaborator** (spolupracující osoba)

Z těchto záznamů aplikace vybere **prioritní** — typicky:

- S blížícím se nebo překročeným termínem
- V aktivních stavech (otevřené, čekající, …)
- S vyšší urgentností (priority field)

Záznamy z různých projektů jsou v jednom seznamu — agregovaný pohled.

## Logika priority

Aplikace počítá prioritu podle:

- **Termín dodání** — čím blíž, tím vyšší
- **Překročený deadline** — kladná penalizace
- **Urgentnost záznamu** (z číselníku)
- **Stav** — aktivní stavy mají vyšší prioritu než čekající

Konkrétní vzorec a parametry jsou v konfiguraci aplikace (`DashboardPriority`):

- `PriorityHorizonDays` — kolik dní dopředu počítáme priority (default 30)
- `PriorityOverdueCapDays` — strop překročení (záznamy déle překročené už
  další body nedostávají, default 30)

## Persistence

Aplikace přepočítává priority pro všechny aktivní záznamy:

- **Periodicky** — nightly rebuild (default `02:00` lokálního času)
- **Reaktivně** — po změně záznamu (úprava termínu, stavu, …)

Cache priority je v dedikované tabulce, takže load dashboardu je rychlý.

## Akce z panelu

Klik na záznam → otevře se **editor záznamu**. Záleží na tvojí preferenci
(modal nebo full-page).

Po uložení v editoru → vrátíš se na dashboard, **přepočet priority** může
trvat sekundu (reaktivní rebuild).

## Co panel nezobrazuje

- **Záznamy ze smazaných projektů**
- **Smazané / archivované záznamy**
- **Záznamy projektů ke kterým nemáš přístup** (zarobeneuje per-project authz)
- **Záznamy mimo time horizon** (`PriorityHorizonDays`)

## Pro koho

Pro každého přihlášeného uživatele. Pokud nemáš žádné přiřazení záznamu
(nový user), panel je prázdný — to je v pořádku.

## Související

- [Novinky](novinky.md) — pasivní pohled "co se dělo"
- [Záznamy v projektu](../projekty/zaznamy/) — aktivní práce na záznamu
- [Profil → Moje práva](../profil/moje-prava.md) — co můžeš dělat
