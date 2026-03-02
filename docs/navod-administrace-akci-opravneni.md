# Administrace akci a opravneni (Nastaveni)

Tento dokument je prakticky navod pro spravu prav v aplikaci.
Cil: nastavit prava spravne napoprve a neudelat kombinaci, ktera je sice technicky platna, ale provozne nebezpecna.

## Obsah

1. [Rychly start (5 kroku)](#rychly-start-5-kroku)
2. [Hlavni pilire modelu opravneni](#hlavni-pilire-modelu-opravneni)
3. [Slovnik pojmu](#slovnik-pojmu)
4. [UI kategorie vs interni kody](#ui-kategorie-vs-interni-kody)
5. [Podporovane klice akci](#podporovane-klice-akci)
6. [Doporucene kombinace](#doporucene-kombinace)
7. [Kombinace ktere projdou, ale jsou spatne](#kombinace-ktere-projdou-ale-jsou-spatne)
8. [Kombinace ktere backend odmitne](#kombinace-ktere-backend-odmitne)
9. [Systemove polozky (is_system)](#systemove-polozky-is_system)
10. [Jak cist Efektivni prava](#jak-cist-efektivni-prava)
11. [Troubleshooting](#troubleshooting)
12. [Reference na kod](#reference-na-kod)
13. [Provozni checklist pred nasazenim](#provozni-checklist-pred-nasazenim)

## Rychly start (5 kroku)

1. Vytvor (nebo uprav) akci v `Nastaveni -> Akce`.
2. Namapuj akci na roli v `Nastaveni -> Role -> Akce`.
3. Prirad roli uzivateli v `Nastaveni -> Uzivatele -> Role`.
4. Over vysledek v `Nastaveni -> Efektivni prava`.
5. Otestuj realny scenar v aplikaci pod konkretnim uzivatelem.

Bez kroku 2 a 3 samotna akce nic neumozni.

## Hlavni pilire modelu opravneni

### Pilir 1: Klic akce je zdroj pravdy

- Aplikace rozhoduje podle `klic` (napr. `projects.create`).
- Klic musi byt v seznamu podporovanych klicu (`PermissionKeys`).
- Volny vlastni text klice backend odmitne.

### Pilir 2: Kategorie je organizacni metadata

- Kategorie slouzi pro prehled v admin UI.
- Kategorie se nepouziva v runtime autorizaci.
- Spatna kategorie obvykle nic nerozbije, ale dela chaos ve sprave.

### Pilir 3: Rozsah se sklada ze dvou vrstev

- Na akci (`authz.permissions.scope_level`): `GLOBAL` nebo `PROJECT`.
- Na mapovani role->akce (`authz.role_permissions.scope_mode`): `ALL` nebo `INCLUDE`.

Koncove vyhodnoceni prava pouziva obe vrstvy.

### Pilir 4: Pravo je aktivni az po mapovani

- `Akce` sama o sobe je jen katalog.
- Musi existovat vazba `role -> akce`.
- U `INCLUDE` musi byt vybrane projekty.
- Uzivatel musi mit prirazenu roli.

## Slovnik pojmu

- `Akce` (permission): jednotlive pravo, napriklad `meetings.edit`.
- `Role`: sada akci, ktera se prideluje uzivatelum.
- `ScopeLevel`: `GLOBAL` nebo `PROJECT` na urovni akce.
- `ScopeMode`: `ALL` nebo `INCLUDE` na urovni vazby role->akce.
- `INCLUDE`: pravo plati jen pro vybrane projekty.
- `ALL`: pravo plati pro vsechny projekty.

## UI kategorie vs interni kody

V modalu vidis lidske nazvy, v DB jsou ulozene kody:

- `Projekty` -> `PROJECTS`
- `Projektove zaznamy` -> `RECORDS`
- `Jednani` -> `MEETINGS`
- `Ciselniky a osoby` -> `MASTER`
- `Nastaveni` -> `SETTINGS`

## Podporovane klice akci

Aktualne podporovane:

- `projects.create`
- `projects.edit`
- `projects.delete`
- `records.edit`
- `records.comment.subsystemlead`
- `meetings.create`
- `meetings.edit`
- `team.manage`
- `people.manage`
- `ciselniky.edit`
- `settings.view`
- `settings.manage`

Poznamka:

- Klic mimo tento seznam backend odmitne.

## Doporucene kombinace

Pouzivej tyto kombinace `klic -> kategorie -> scope_level`:

- `projects.create` -> `PROJECTS` -> `PROJECT`
- `projects.edit` -> `PROJECTS` -> `PROJECT`
- `projects.delete` -> `PROJECTS` -> `PROJECT`
- `team.manage` -> `PROJECTS` -> `PROJECT`
- `records.edit` -> `RECORDS` -> `PROJECT`
- `records.comment.subsystemlead` -> `RECORDS` -> `PROJECT`
- `meetings.create` -> `MEETINGS` -> `PROJECT`
- `meetings.edit` -> `MEETINGS` -> `PROJECT`
- `people.manage` -> `MASTER` -> `GLOBAL`
- `ciselniky.edit` -> `MASTER` -> `GLOBAL`
- `settings.view` -> `SETTINGS` -> `GLOBAL`
- `settings.manage` -> `SETTINGS` -> `GLOBAL`

## Kombinace ktere projdou, ale jsou spatne

Tyto veci backend dovoli, ale nejsou doporucene:

- Dat `projects.create` do kategorie `SETTINGS`.
  - Funkcne to pojede, ale bude to neprehledne.
- Dat projektove pravo na `ScopeMode = ALL`, kdyz chcete omezeni jen na cast projektu.
  - Uzivatel dostane sirsi pravo, nez cekate.
- Pouzit `INCLUDE` a nevybrat zadny projekt.
  - Pravo je prakticky nepouzitelne.

## Kombinace ktere backend odmitne

Pri ukladani spadne validace, pokud:

- `klic` neni podporovany.
- `klic` nebo `nazev` je prazdny.
- `scope_level` neni `GLOBAL` ani `PROJECT`.
- kategorie neexistuje nebo neni aktivni.
- klic akce je duplicitni.
- mapovani `role -> akce` je duplicitni.
- `scope_mode` neni `ALL` nebo `INCLUDE`.
- `INCLUDE` obsahuje neexistujici projekt.

## Systemove polozky (is_system)

Polozky se `is_system = 1` jsou chranene:

- systemove role nejde deaktivovat,
- systemove akce nejde editovat ani deaktivovat.

Chcete-li zmenu chovani:

1. zaloz vlastni nesystemovou akci,
2. namapuj ji na vlastni roli.

## Jak cist Efektivni prava

- `Povoleno` znamena, ze `HasPermission(...)` vratilo `true`.
- Pro volbu `Projekt = Vsechny projekty` plati striktni pravidlo:
  - bere se `GLOBAL` nebo `ALL`,
  - `INCLUDE` bez konkretniho projektu se nepocita.

## Troubleshooting

### Problem: Uzivatel "ma roli", ale v aplikaci nic nemuze

Zkontroluj:

1. role je aktivni,
2. akce je aktivni,
3. existuje mapovani `role -> akce`,
4. u `INCLUDE` je spravne vybrany projekt,
5. uzivatel ma prirazeni role aktivni.

### Problem: V Efektivnich pravech je "zakazano" pro Vsechny projekty

- Typicky je jen `INCLUDE` bez `ALL`.
- Ocekavane chovani: globalne je to zakazane.

### Problem: Nelze ulozit novou akci

- Nejcasteji nepodporovany klic nebo duplicitni klic.

## Reference na kod

- Seznam podporovanych klicu a vyhodnoceni prava:
  - `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/Models/ViewModels/SecurityViewModels.cs`
- Ukladani akci:
  - `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/Services/Data/SqlServerDataStore.cs`
  - metoda `SaveAuthzPermission(...)`
- Ukladani mapovani role->akce:
  - `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/Services/Data/SqlServerDataStore.cs`
  - metoda `SaveRolePermission(...)`
- Ukladani prirazeni role uzivateli:
  - `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/Services/Data/SqlServerDataStore.cs`
  - metoda `SaveUserRolesForUser(...)`
- Kontroler admin sekce:
  - `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/Controllers/NastaveniController.cs`

## Provozni checklist pred nasazenim

1. Klice jsou jen ze seznamu podporovanych.
2. Kategorie odpovida domene akce.
3. `scope_level` odpovida realne potrebe.
4. `ScopeMode = ALL` je pouzit jen tam, kde je to opravdu zamer.
5. `INCLUDE` ma konkretni projekty.
6. Uzivatel ma opravdu prirazene role.
7. Otestovano v `Efektivni prava` pro konkretniho uzivatele a projekt.
