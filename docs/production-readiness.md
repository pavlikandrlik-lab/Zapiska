# Zápiska – Production Readiness Checklist

Tento dokument je provozní checklist před nasazením na Windows Server + IIS + MS SQL.

## 1) Build artefakt

- Build: `dotnet build /Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/PmTracker.Web.csproj -c Release`
- Publish (doporučeno): `dotnet publish /Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/PmTracker.Web.csproj -c Release -o /Users/Pavel.Andrlik/Documents/PM Tracker/publish/fdd`
- Offline deploy balíček musí obsahovat:
  - publish výstup (`publish/fdd`)
  - `/Users/Pavel.Andrlik/Documents/PM Tracker/PMTracker_insert_sql`
  - `/Users/Pavel.Andrlik/Documents/PM Tracker/install.md`

## 2) Databáze

- Cílová DB je prázdná při prvním nasazení.
- Spustit jednotný script `PMTracker_insert_sql`.
- Ověřit minimálně:
  - `dbo.ciselnik_stavu_projektu` obsahuje `PLAN, RUN, DONE, DELETED`
  - existuje seed osoba `Pavel Admin`
  - existují tabulky `authz.*`

## 3) Identita a přístupy

- Produkce: IIS `Windows Authentication = Enabled`, `Anonymous = Disabled`.
- App mapuje uživatele přes GUID claim -> `dbo.osoby.Guid_AD`.
- Superadmin se nastavuje v DB přes `authz.superadmins.osoba_id` (INT, FK na `dbo.osoby.id`).
- Hodnota typu `acr\\login` se do `authz.superadmins` nevkládá.

## 4) IIS a app pool

- App Pool: `No Managed Code`, `Integrated`.
- App Pool identita musí mít přístup:
  - k web složce (`Read & Execute`)
  - do SQL (pokud `Trusted_Connection=True`).
- Nastavit environment:
  - `ASPNETCORE_ENVIRONMENT=Production`

## 5) Aplikační konfigurace

- `appsettings.Production.json`:
  - validní `ConnectionStrings:PmTrackerDb`
  - `PmTracker:Data:Provider = SqlServer`
- Aplikace startuje fail-fast:
  - bez DB konektivity nebo bez povinných číselníků nespustí runtime.

## 6) Smoke test po nasazení

- Otevřít `/Projekty`, `/Jednani`, `/Ciselniky`, `/Nastaveni`.
- Ověřit hlavičku aplikace: `Zápiska`.
- Ověřit, že přihlášený provozní účet má správná práva.
- Ověřit exporty:
  - tisk projektu
  - tisk jednání
  - tisk úkolu
  - Word i PDF cesta

## 7) Cutover doporučení

- Mít minimálně 2 superadmin účty před předáním do provozu.
- Před produktivním přepnutím provést test:
  - create/edit/smazání (soft delete) projektu
  - zápis vyjádření včetně více řádků
  - změna stavů jednání a docházky
  - tisk Word/PDF na reálných datech.
