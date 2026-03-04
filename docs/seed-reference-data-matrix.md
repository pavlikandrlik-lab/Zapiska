# Seed Cleanup a Vstupní Matice Referenčních Dat

Tento dokument odděluje:

1. systémové minimum, které zůstává pevně v `PMTracker_insert_sql`,
2. minimální provozní business baseline, která je nově také součástí `PMTracker_insert_sql`,
3. provozní data, která nejsou součástí baseline seedu.

## Systémové minimum v baseline seedu

Tyto tabulky nebo jejich fixní kódy musí zůstat v `PMTracker_insert_sql`:

- `dbo.ciselnik_stavu_projektu`
  - povinné kódy: `PLAN`, `RUN`, `DONE`, `DELETED`
- `dbo.ciselnik_roli_projektu`
  - povinné kódy: `VLASTNIK_PROJEKTU`, `HOST`, `ADM_PROJ`
- `dbo.ciselnik_roli_subsystemu`
  - povinné kódy: `VEDOUCI_SUBSYSTEMU`, `ZASTUPCE_VEDOUCIHO_SUBSYSTEMU`, `METODIK_SUBSYSTEMU`
- `dbo.ciselnik_organizace`
  - bootstrap kód: `MO`
- `authz.permission_categories`
  - povinné kódy: `PROJECTS`, `RECORDS`, `MEETINGS`, `MASTER`, `SETTINGS`
- `authz.permissions`
  - povinné systémové klíče používané aplikací
- `authz.roles`
  - povinné systémové role: `SUPERADMIN`, `APP_ADMIN`
- `authz.role_permissions`
  - povinné seed mapování minimálně pro `SUPERADMIN` a `APP_ADMIN`

Poznámka:
- baseline seed záměrně nevytváří žádnou osobu, projekt ani superadmin účet,
- první superadmin se zakládá ručně do DB,
- `dbo.osoby.organizacni_celek_id` může být při bootstrapu `NULL`, ale `organizace_id` musí ukazovat na `MO`.

## Vlna 1: business kritické číselníky

Tyto tabulky nejsou nutné pro samotný start webu, ale bez dat rozbíjejí reálné workflow. Níže je stav po zavedení minimální provozní baseline:

| Pořadí | Tabulka | Co dodat | Povinné sloupce | Stav |
| --- | --- | --- | --- | --- |
| 1 | `dbo.ciselnik_kategorii_zaznamu` | minimálně `U / Úkol` | `kod`, `nazev` | seeded v baseline |
| 2 | `dbo.ciselnik_stavu_ukolu` | minimálně `OPEN`, `DONE` | `kod`, `nazev` | seeded v baseline |
| 3 | `dbo.ciselnik_typu_ukolu` | `mp`, `A`, `P`, `RU` | `kod`, `nazev` | seeded v baseline |
| 4 | `dbo.subsystemy` | globální katalog subsystémů | `kod`, `nazev` | čeká na vstup |
| 5 | `dbo.ciselnik_stavu_jednani` | minimálně `DRAFT`, `OPEN`, `CLOSED` | `kod`, `nazev` | seeded v baseline |
| 6 | `dbo.ciselnik_stavu_ucasti` | minimálně `PRESENT`, `ONLINE`, `EXCUSED`, `ABSENT` | `kod`, `nazev` | seeded v baseline |
| 7 | `dbo.harmonogram_sablony` | aspoň jedna aktivní šablona | `delay_barva_hex`, `is_aktivni` | seeded v baseline |
| 8 | `dbo.ciselnik_harmonogram_typu` | výchozí kroky harmonogramu | `kod`, `nazev`, `krok_poradi`, `je_zpozdeni` | seeded v baseline |

### Poznámky k vlně 1

- `dbo.ciselnik_kategorii_zaznamu`
  - ovlivňuje založení záznamu, filtrování i logiku typu záznamu,
  - baseline používá `U / Úkol`,
  - kód `U` je zároveň požadovaný kvůli tisku do PDF.
- `dbo.ciselnik_stavu_ukolu`
  - ovlivňuje default stav při založení záznamu a historii změn stavu.
- `dbo.subsystemy`
  - ovlivňují záznamy, projektové subsystémy i subsystemové role.
- `dbo.ciselnik_stavu_ucasti`
  - bez dat může create jednání skončit chybou `Není nadefinován žádný stav účasti.`.
- `dbo.harmonogram_sablony` + `dbo.ciselnik_harmonogram_typu`
  - jsou business kritické pro harmonogram a GANTT.

## Vlna 2: rozšiřující funkční číselníky

Tyto tabulky je vhodné doplnit po první vlně:

| Pořadí | Tabulka | Co dodat | Povinné sloupce | Stav |
| --- | --- | --- | --- | --- |
| 9 | `dbo.ciselnik_typu_externich_odkazu` | `PMP`, `PNF`, `NES` | `kod`, `nazev` | seeded v baseline |
| 10 | `dbo.ciselnik_vyzvy` | katalog výzev | `kod`, `nazev`, `rok` | čeká na vstup |
| 11 | `dbo.ciselnik_organizace` | seznam organizací | `kod`, `nazev` | čeká na vstup |
| 12 | `dbo.ciselnik_organizacni_celky` | seznam organizačních celků | `kod`, `nazev` | čeká na vstup |

### Poznámky k vlně 2

- `dbo.ciselnik_typu_externich_odkazu`
  - ovlivňuje externí odkazy u záznamů.
- `dbo.ciselnik_vyzvy`
  - ovlivňuje metadata a návaznosti externích odkazů.
- `dbo.ciselnik_organizace` a `dbo.ciselnik_organizacni_celky`
  - ovlivňují osoby, výběry v modalech a budoucí provozní data.

## Vstupní formát pro business tabulky

### Jednoduché číselníky

Použij pro tabulky:

- `dbo.ciselnik_kategorii_zaznamu`
- `dbo.ciselnik_stavu_ukolu`
- `dbo.ciselnik_typu_ukolu`
- `dbo.ciselnik_typu_externich_odkazu`
- `dbo.ciselnik_stavu_jednani`
- `dbo.ciselnik_stavu_ucasti`
- `dbo.ciselnik_organizace`
- `dbo.ciselnik_organizacni_celky`
- `dbo.subsystemy`

Požadovaný vstup:

| Sloupec | Povinnost | Poznámka |
| --- | --- | --- |
| `kod` | povinný | stabilní technický identifikátor |
| `nazev` | povinný | zobrazovaný název |
| `poznamka` | volitelná | jen pokud je potřeba doplnit business kontext |

### Výzvy

| Sloupec | Povinnost | Poznámka |
| --- | --- | --- |
| `kod` | povinný | technický identifikátor |
| `nazev` | povinný | zobrazovaný název |
| `rok` | povinný | rok výzvy |

### Harmonogram

#### `dbo.harmonogram_sablony`

| Sloupec | Povinnost | Poznámka |
| --- | --- | --- |
| `nazev` | povinný | název šablony |
| `barva_zpozdeni` | povinná | barva pro zpoždění |

#### `dbo.ciselnik_harmonogram_typu`

| Sloupec | Povinnost | Poznámka |
| --- | --- | --- |
| `kod` | povinný | technický identifikátor kroku |
| `nazev` | povinný | název kroku |
| `krok_poradi` | povinné | pořadí v harmonogramu |
| `je_zpozdeni` | povinné | `0/1` indikace zpoždění |
| `barva` | volitelná | barva kroku |

## Co není součást baseline seedu ani vstupní matice

Tyto tabulky jsou provozní nebo historické a seed cleanup se jich netýká:

- `dbo.projekty`
- `dbo.obsazeni_projektu`
- `dbo.projekt_subsystemy`
- `dbo.obsazeni_subsystemu_projektu`
- `dbo.jednani`
- `dbo.projektove_zaznamy`
- `dbo.ucast`
- `dbo.vyjadreni`
- všechny `dbo.zaznam_historie_*`
- `dbo.zaznam_spoluprace`
- `dbo.zaznam_externi_odkazy`
- `authz.user_roles`
- `authz.superadmins`
- `authz.audit_log`
- `authz.role_permission_projects`

## Explicitně vyřazené seed položky

Tyto položky nepatří do produkční baseline:

- lokální osoba `Pavel Admin`
- seed superadmin navázaný na konkrétní osobu
- seed `APP_ADMIN` navázaný na konkrétní osobu
- projektové přiřazení lidí
- subsystemové přiřazení lidí
- testovací a demo osoby
- projektová role `VED_SUB`

Poznámka:
- `VED_SUB` je historický artefakt ve projektových rolích,
- skutečná subsystemová logika patří do `dbo.ciselnik_roli_subsystemu`.
