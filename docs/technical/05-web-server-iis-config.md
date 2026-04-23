# PM Tracker - Technická dokumentace 05: Konfigurace web serveru IIS

## 1. Účel
Dokument definuje standardní konfiguraci IIS pro PM Tracker tak, aby deployment byl opakovatelný a auditovatelný.

## 2. Publikum a role
- Ops administrátor: konfigurace IIS role, site, app pool, certifikátů.
- Bezpečnostní administrátor: validace autentizace a TLS.

## 3. Závislosti a předpoklady
- Nainstalovaný IIS a ASP.NET Core Hosting Bundle 8.x.
- Deploy složka s publish výstupem aplikace.
- Platný DNS/hostname a certifikát pro HTTPS binding.

## 4. Vstupy a výstupy
### Vstupy
- `publish/web.config`
- IIS Manager nebo PowerShell (`WebAdministration`)

### Výstupy
- Funkční IIS site s `AspNetCoreModuleV2` a in-process hostingem.

## 5. Detailní postup
### 5.1 IIS role
```powershell
Install-WindowsFeature Web-Server,Web-Mgmt-Console,Web-Windows-Auth
```

### 5.2 Hosting bundle
```powershell
Start-Process -FilePath "C:\installs\dotnet-hosting-8.0.x-win.exe" -ArgumentList "/quiet /norestart" -Wait
iisreset
```

### 5.3 App Pool standard
- Name: `PmTrackerPool`
- .NET CLR: `No Managed Code`
- Pipeline: `Integrated`
- Identity: service účet nebo ApplicationPoolIdentity (dle bezpečnostní politiky)

### 5.4 Site standard
- Site name: `PmTracker`
- Physical path: `C:\apps\PmTracker.Web`
- Bindings:
  - HTTP (jen pokud je interně požadováno)
  - HTTPS (preferované)

### 5.5 Authentication standard
- `Windows Authentication = Enabled`
- `Anonymous Authentication = Disabled` — **MUSÍ zůstat vypnuté**.
- AD identity sama o sobě nestačí; uživatel musí být založen v `dbo.osoby` (`Guid_AD`).
- AD synchronizace do aplikace není automatická.

#### 5.5.1 Trust boundary pro `LOGON_USER` header (bezpečnost)

Aplikace odvozuje identitu uživatele z `HttpContext.User.Identity.Name`, kterou IIS
plní z proměnné `LOGON_USER` poskytnuté Windows Authentication modulem. Tato hodnota
je **autoritativní pouze pokud pochází přímo z IIS Windows Auth handshaku** (Kerberos/NTLM).

**NIKDY nepovolit:**
- `Anonymous Authentication = Enabled` v kombinaci s nasazením za reverse proxy
  (ARR, nginx, HAProxy, IIS front-end) bez explicitního stripu `LOGON_USER` hlavičky.
  Útočník by mohl poslat vlastní `LOGON_USER: admin@domena.cz` header → proxy by ho předal
  k IIS back-endu → IIS by ho promoval na autoritativní identitu → aplikace by ho akceptovala.
- `IIS Anonymous Auth` bez nastaveného `authentication/anonymousAuthentication` restrictu v `web.config`.
- Forward `LOGON_USER` / `REMOTE_USER` / `HTTP_X-*-USER` z reverse proxy bez whitelistingu.

**Pokud je aplikace za reverse proxy:**
- Reverse proxy **musí stripovat** příchozí `LOGON_USER`, `REMOTE_USER`, `X-MS-CLIENT-*`, a všechny ostatní hlavičky,
  které by mohly být promoted na identitu (whitelist principle, ne blacklist).
- IIS na back-endu má mít stále `Windows Auth` enabled (aby handshake pokračoval normálně)
  a `Anonymous Auth` disabled.
- Reverse proxy nesmí propagovat hlavičky nedůvěryhodných prefixů (`X-Forwarded-*-User`).

**Související middleware:**
- `PmTracker.Web/Middleware/UserContextMiddleware.cs` resolvuje uživatele pokud je
  `Identity.Name` neprázdný (IIS fallback scenario). Pokud je identita forgovaná přes
  nestrípnutou hlavičku, middleware ji promoved bez dalšího auth checku — čistota
  LOGON_USER je tedy _deployment-level guarantee_, kterou code nemůže zkontrolovat.

### 5.6 Referenční `web.config`
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet" arguments=".\PmTracker.Web.dll" stdoutLogEnabled="false" stdoutLogFile=".\logs\stdout" hostingModel="inprocess" />
    </system.webServer>
  </location>
</configuration>
```

### 5.7 File system oprávnění
- App pool identita musí mít `Read & Execute` na deploy složce.
- Pokud jsou aktivované stdout logy, musí mít i write právo do `logs`.

### 5.8 Verifikace bezpečnostní konfigurace autentikace

Po každé změně IIS konfigurace nebo přidání reverse proxy před aplikaci:

```powershell
# 1. Ověř, že Anonymous Auth je disabled
Import-Module WebAdministration
Get-WebConfigurationProperty -PSPath 'IIS:\Sites\PmTracker' `
  -Filter 'system.webServer/security/authentication/anonymousAuthentication' `
  -Name 'enabled'
# Expected: Value = False
```

```bash
# 2. Ověř, že proxy stripuje LOGON_USER (z external machine mimo trusted síť)
curl -H "LOGON_USER: fake@domena.cz" https://pmtracker.intranet/Home/WhoAmI
# Expected: 401 Unauthorized NEBO přihlášení jako anonymní attacker, NE jako fake@domena.cz
```

Pokud druhý test ukáže přihlášení jako `fake@domena.cz` → **KRITICKÁ CHYBA**, reverse proxy
propaguje nedůvěryhodnou hlavičku a umožňuje identity forging. Okamžitě stripovat na proxy.

## 6. Verifikace
- Ověř, že site startuje bez `HTTP Error 500.30`.
- Ověř validní autentizaci (uživatel vidí vlastní identitu v aplikaci).
- Ověř HTTPS handshake a platnost certifikátu.

## 7. Rollback
- Vrať předchozí konfiguraci site/app pool z exportu IIS config.
- Vrať předchozí publish balíček.
- Proveď `iisreset` a ověř health check.

## 8. Troubleshooting
- `502.5 Process Failure`:
  - ověř hosting bundle, runtime verzi, chybějící DLL.
- Neplatná autentizace:
  - ověř Windows Auth flagy a doménové trusty.
- Logy nejsou dostupné:
  - dočasně povol `stdoutLogEnabled=true`, pak vrať na `false`.

## 9. Audit a traceability
- IIS runtime konfig: `/Users/Pavel.Andrlik/Documents/PM Tracker/publish/web.config`
- Startup middleware pořadí: `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/Program.cs`
- Deployment runbook: `/Users/Pavel.Andrlik/Documents/PM Tracker/docs/technical/04-installation-deployment-iis.md`
