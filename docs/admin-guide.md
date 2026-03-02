# Zápiska – Administrační dokumentace

## 1. Účel

Tento dokument je určen pro správce aplikace. Popisuje:

- správu uživatelů, rolí a oprávnění,
- správu číselníků,
- provozní doporučení a bezpečnostní zásady,
- dopad změn nastavení do běžného provozu.

## 2. Role a úrovně oprávnění

### Superadmin

- má plná práva napříč aplikací,
- vidí a spravuje `Nastavení`,
- může měnit i systémové bezpečnostní konfigurace,
- je řízen databázově v tabulce `authz.superadmins`.

### Administrátor

- má jen ta práva, která jsou mu explicitně přidělena,
- typicky spravuje uživatele, role, číselníky nebo konkrétní moduly.

### Běžný uživatel

- má read-only přístup, pokud nedostane právo na editaci.

## 3. Přehled sekce Nastavení

Sekce `Nastavení` je hlavní místo pro správu autorizace.

### 3.1 Role

- vytvoření a úprava provozních rolí,
- systémové role (`is_system = 1`) se neupravují běžným provozem.

### 3.2 Akce (Permission Keys)

- každá akce reprezentuje jedno právo, např. `projects.edit`,
- klíče musí odpovídat podporovanému katalogu v backendu.

### 3.3 Role -> Akce

- mapuje se, co konkrétní role smí dělat,
- rozsah:
  - `ALL` = všechno v daném scope,
  - `INCLUDE` = jen vybrané projekty.

### 3.4 Uživatel -> Role

- přiřazuje role osobám z `dbo.osoby`,
- změny práv se projeví po dalším načtení stránky.

### 3.5 Efektivní práva

- diagnostický náhled výsledných práv (co uživatel skutečně může),
- kontroluje se kombinace všech přiřazených rolí.

## 4. Správa číselníků

Sekce `Číselníky` používá ochranu `is_locked`.

- `is_locked = 1`: položka je chráněná před běžnou editací/smazáním,
- `is_locked = 0`: položku lze spravovat podle oprávnění.

Doporučení:

- zamykat referenční hodnoty, na které se váže aplikační logika (`kod` hodnoty),
- neměnit `kod` u systémových položek bez dopadové analýzy.

## 5. Správa uživatelů

### 5.1 AD osoby

- párování loginu funguje přes `dbo.osoby.Guid_AD`,
- v produkci je vyžadována Windows identity + GUID claim.

### 5.2 Ručně založené osoby

- použitelné pro externisty nebo evidenční záznamy,
- bez `Guid_AD` se nepřihlásí přes Windows autentizaci.

## 6. Co lze a nelze měnit

### Lze

- vytvářet provozní role a mapovat je na akce,
- přiřazovat role uživatelům,
- spravovat nezamčené číselníky,
- upravovat projekty, záznamy, jednání podle oprávnění.

### Nelze (bez zásahu vývoje/DB)

- přidávat libovolné neznámé permission keys mimo podporovaný katalog,
- měnit datový model bez aktualizace `PMTracker_insert_sql`,
- obcházet `is_locked` bez adekvátní role.

## 7. Provozní zásady

1. Mějte minimálně dva aktivní superadmin účty.
2. Každou zásadní změnu práv testujte na testovacím účtu.
3. Neměňte produkční role a číselníky bez evidence změny.
4. Před úpravou systémových položek proveďte export/backup.

## 8. Typické chyby a řešení

### Uživatel „má roli“, ale nevidí tlačítko Upravit

Zkontrolujte:

- má přiřazenou správnou roli,
- role obsahuje správnou akci,
- akce má správný scope (`ALL/INCLUDE`),
- pro `INCLUDE` je zahrnut cílový projekt.

### Nelze uložit změnu číselníku

Zkontrolujte:

- `is_locked` položky,
- oprávnění `ciselniky.edit`,
- systémovou ochranu dané položky.

### Uživatel neprojde přihlášením

Zkontrolujte:

- existenci osoby v `dbo.osoby`,
- správně vyplněné `Guid_AD`,
- Windows Authentication v IIS.

## 9. Odkazy

- Instalační dokumentace (mimo aplikaci): `/Users/Pavel.Andrlik/Documents/PM Tracker/install.md`
- Uživatelská příručka: `/Dokumentace/Uzivatelska-prirucka`
- Q and A: `/Dokumentace/qa`
