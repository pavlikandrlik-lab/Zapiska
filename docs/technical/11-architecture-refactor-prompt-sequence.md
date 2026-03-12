# PM Tracker - Sekvence dotazu pro bezpecny architektonicky refaktor

## 1. Ucel
Tento dokument je sada copy/paste dotazu, ktere mi budes postupne posilat.
Cil: prepsat architekturu na modularnejsi, ale zachovat aplikaci funkcne presne jako dnes.

## 2. Jak to pouzivat (pro neprogramatora)
1. Zkopiruj presne jeden sprintovy dotaz z casti 5.
2. Posli ho do chatu.
3. Pockej, az vratim vysledek + overeni.
4. Teprve potom posli dalsi sprint.
5. Kdyz cokoliv neprojde, neposilej dalsi sprint, ale posli `FIX SPRINT`.

## 3. Tvrda pravidla pro kazdy sprint
- Zadna zmena chovani bez explicitniho souhlasu.
- Zadna zmena URL rout, API kontraktu a DB schema mimo dedikovany sprint.
- Zadny destructive git prikaz.
- Po kazdem sprintu musi probehnout testy a byt uvedeny vysledky.
- Vystup musi vzdy obsahovat sekce: `Umisteni`, `DI`, `Test`, `Audit`, `Souhrn a detail`.

## 4. Povinne overeni po kazdem sprintu
Minimalni gate:
- `./scripts/test-unit.sh`
- `./scripts/test-api.sh`
- `./scripts/test-integration.sh`

Plny gate (kazdy 3. sprint nebo pred merge):
- `./scripts/test-prereq-check.sh`
- `./scripts/test-unit.sh`
- `./scripts/test-api.sh`
- `./scripts/test-integration.sh`
- `./scripts/test-e2e.sh`

## 5. Copy/paste dotazy po sprintech

### SPRINT 0 - Baseline a bezpecnostni sit
```text
SPRINT 0
Udelej pouze baseline pripravu pro architektonicky refaktor bez zmeny chovani.

Ukol:
1) Zmapuj aktualni architekturu a nejvetsi hotspoty.
2) Zkontroluj aktualni test gate.
3) Priprav check-list pro "no behavior change".

Povolene zmeny:
- pouze dokumentace v docs/technical

Zakazane zmeny:
- zadny C# runtime kod, zadna DB schema zmena

Povinne overeni:
- ./scripts/test-unit.sh
- ./scripts/test-api.sh
- ./scripts/test-integration.sh

Vystup:
- Umisteni, DI, Test, Audit, Souhrn a detail
- seznam rizik + presny plan sprintu 1-3
```

### SPRINT 1 - Modularni skeleton bez zmeny chovani
```text
SPRINT 1
Vytvor modularni skeleton v ramci aktualniho projektu bez zmeny chovani.

Ukol:
1) Priprav modulove boundaries (Projects, Meetings, Records, Settings, Export).
2) Pridej DI extension metody po modulech.
3) Nech implementaci stale delegovanou na stavajici kod.

Povolene zmeny:
- struktura slozek
- nove extension tridy pro DI
- drobny wiring v Program.cs

Zakazane zmeny:
- zadna zmena business logiky
- zadna zmena DB
- zadna zmena frontendu

Povinne overeni:
- ./scripts/test-unit.sh
- ./scripts/test-api.sh
- ./scripts/test-integration.sh
```

### SPRINT 2 - Export modul (read-only)
```text
SPRINT 2
Vyclen read-only Export modul z monolitickeho data-store bez zmeny vystupu.

Ukol:
1) Oddel export query/use-case tridy.
2) Nech stejne endpointy a stejny HTML/Word vystup.
3) Zachovej stejne DI kontrakty navenek.

Zakazane zmeny:
- zadna zmena HTML sablon
- zadna zmena route
- zadna zmena DB schema

Povinne overeni:
- ./scripts/test-unit.sh
- ./scripts/test-api.sh -- pokud script neumi parametr, pust cely ./scripts/test-api.sh
- ./scripts/test-integration.sh
```

### SPRINT 3 - Settings/AuthZ modul
```text
SPRINT 3
Oddel Settings/AuthZ do samostatneho modulu pri zachovani chovani.

Ukol:
1) Vyclen cteni a zapis role/permission do modulove sluzby.
2) Udrz stavajici controller kontrakty beze zmen.
3) Omez coupling na centralni data-store.

Povinne overeni:
- ./scripts/test-prereq-check.sh
- ./scripts/test-unit.sh
- ./scripts/test-api.sh
- ./scripts/test-integration.sh
- ./scripts/test-e2e.sh
```

### SPRINT 4 - Projects modul
```text
SPRINT 4
Vyclen Projects modul (seznam/detail/zakladni prikazy) bez zmeny chovani.

Ukol:
1) Rozdel query a command cast.
2) Zachovej vsechny response modely.
3) Zachovej stejne autorizacni podminky.

Povinne overeni:
- ./scripts/test-unit.sh
- ./scripts/test-api.sh
- ./scripts/test-integration.sh
```

### SPRINT 5 - Meetings modul
```text
SPRINT 5
Vyclen Meetings modul bez zmeny route a bez zmeny vystupu.

Ukol:
1) Presun logiku jednani/ucasti do modulu.
2) Zachovej stejne preconditions a chybove stavy.
3) Zachovej export navaznosti.

Povinne overeni:
- ./scripts/test-unit.sh
- ./scripts/test-api.sh
- ./scripts/test-integration.sh
```

### SPRINT 6 - Records modul
```text
SPRINT 6
Vyclen Records modul (vetsina byznys logiky) inkrementalne, stale bez zmeny chovani.

Ukol:
1) Rozdel velke use-cases na mensi handler/sluzby.
2) Zachovej stejne validace, stejne ErrorCode a stejne texty.
3) Zachovej stejne side-efekty do historie/auditu.

Povinne overeni:
- ./scripts/test-prereq-check.sh
- ./scripts/test-unit.sh
- ./scripts/test-api.sh
- ./scripts/test-integration.sh
- ./scripts/test-e2e.sh
```

### SPRINT 7 - People + Dictionaries modul
```text
SPRINT 7
Vyclen People a Dictionaries modul bez zmeny chovani.

Ukol:
1) Presun CRUD logiku osob/ciselniku do modulovych sluzeb.
2) Zachovej AD behavior a permission checks.
3) Zachovej stejne view modely.

Povinne overeni:
- ./scripts/test-unit.sh
- ./scripts/test-api.sh
- ./scripts/test-integration.sh
```

### SPRINT 8 - Finalni cisteni rozhrani
```text
SPRINT 8
Odstran mrtve adaptery a finalizuj modularni architekturu beze zmeny chovani.

Ukol:
1) Zmensi centralni fat interface.
2) Odstran redundantni delegace.
3) Zkontroluj finalni DI mapu.

Povinne overeni:
- ./scripts/test-prereq-check.sh
- ./scripts/test-unit.sh
- ./scripts/test-api.sh
- ./scripts/test-integration.sh
- ./scripts/test-e2e.sh

Vystup:
- finalni seznam modulu
- seznam odstraneneho technickeho dluhu
- potvrzeni kompatibility
```

## 6. FIX SPRINT (kdyz neco neprojde)
```text
FIX SPRINT
Predchozi sprint neprosel. Nedelaj dalsi refaktor.

Ukol:
1) Oprav pouze regresi vzniklou v poslednim sprintu.
2) Zadna nova architektonicka zmena navic.
3) Vrat aplikaci do plne funkcnosti.

Povinne overeni:
- stejny test gate jako mel posledni sprint

Vystup:
- root cause
- co bylo opraveno
- potvrzeni, ze vse je zpet kompatibilni
```

## 7. Kratky dotaz, ktery muzes poslat kdykoliv
```text
Pokračuj dalsim sprintem podle docs/technical/11-architecture-refactor-prompt-sequence.md.
Drz se presne pravidel "no behavior change", spust povinne testy a vrat sekce:
Umisteni, DI, Test, Audit, Souhrn a detail.
```
