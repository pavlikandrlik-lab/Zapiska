---
title: Přihlášení přes AD
description: Jak aplikace identifikuje uživatele z AD a co je k tomu potřeba.
---

# Přihlášení přes AD (admin / technický pohled)

Tato stránka popisuje **jak aplikace identifikuje uživatele**. Pro user-friendly
úvod (jak se přihlásíš) viz [Začátek → Přihlášení](../../zacatek/prihlaseni.md).

## Mechanismus krok po kroku

### 1. HTTP request s identitou

Když uživatel otevře URL PM Trackeru, browser:

- V intranet zone pošle automaticky **NTLM** nebo **Kerberos** token (single
  sign-on)
- Mimo intranet zone — IIS odmítne s 401 a browser zobrazí dialog "Sign in"

### 2. IIS Windows Authentication

IIS zpracuje token a do ASP.NET Core pipeline injektne:

- `User.Identity.IsAuthenticated = true`
- `User.Identity.Name = "DOMÉNA\login"`

### 3. Aplikace dohledá osobu

V middleware `UserContextMiddleware` aplikace:

1. Vezme login z `User.Identity.Name`
2. Dohledá v `dbo.osoby` podle **`Guid_AD`** (ne podle login stringu — login
   se může měnit, GUID je stabilní)
3. Pokud najde aktivní osobu → naplní `HttpContext.Items[osobaId]`
4. Pokud ne → 401 / 403

### 4. Authorization handler

Před každou akcí běží `PermissionAuthorizationHandler`:

- Načte permission keys osoby (z rolí)
- Porovná s `[Authorize(Policy = "permission:xxx")]` na controlleru / akci
- Povolí nebo zamítne

## Co potřebuješ mít (pro úspěšné přihlášení)

### Doménový účet

Aktivní AD účet ve firemní doméně.

### Záznam v `dbo.osoby`

```sql
SELECT * FROM dbo.osoby WHERE Guid_AD = '...';
```

`Guid_AD` musí přesně odpovídat `objectGuid` z AD. Pokud chybí, založí ho
admin nebo se vytvoří automaticky při prvním přiřazení do týmu (přes
[AD picker](ad-picker.md)).

### `is_active = 1`

Deaktivovaný účet aplikace neuzná — vrátí 403.

### IIS app pool — Kerberos / NTLM podpora

Konfigurace `iis.webconfig` musí mít:

```xml
<authentication>
  <windowsAuthentication enabled="true">
    <providers>
      <add value="Negotiate" />
      <add value="NTLM" />
    </providers>
  </windowsAuthentication>
</authentication>
```

Detail v `docs/technical/05-web-server-iis-config.md`.

## Diagnostika problémů

### `User.Identity.IsAuthenticated` je false

- Browser nepošle token (mimo intranet zone)
- IIS Windows Auth není zapnuté
- Constrained Delegation chybí (delegace tokenu z reverse proxy)

### Token přijde, ale `dbo.osoby` lookup selže

- `Guid_AD` neodpovídá — overit `SELECT objectGuid FROM AD WHERE samAccountName = ?`
- Záznam v `dbo.osoby` chybí — admin musí založit
- Záznam je `is_active = 0`

### Login funguje, ale akce vrací 403

- User existuje, ale **nemá role** s potřebnými permission keys
- Diagnostika: [Nastavení → Efektivní práva](../../nastaveni-administrace/efektivni-prava/)

## Logování

Přihlašovací události se logují do **audit logu**:

- `LOGIN_SUCCESS` (když user prošel)
- `LOGIN_DENIED_NO_PERSON` (token validní, ale `Guid_AD` v `osoby` není)
- `LOGIN_DENIED_INACTIVE` (user existuje ale `is_active=0`)

Admin diagnostika přes audit log v DB.

## Časté problémy (recap)

Pro user-friendly seznam: [Začátek → Přihlášení — Časté problémy](../../zacatek/prihlaseni.md#%C4%8Dast%C3%A9-probl%C3%A9my).

## Související

- [Začátek → Přihlášení](../../zacatek/prihlaseni.md) — user pohled
- [AD picker](ad-picker.md)
- [Synchronizace osob](synchronizace-osob.md)
- [Osoby](../../osoby/)
