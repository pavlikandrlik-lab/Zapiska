---
title: Přihlášení
description: Jak se do PM Trackeru přihlásíš (Active Directory, IIS Windows authentication).
---

# Přihlášení do PM Trackeru

PM Tracker **nemá vlastní přihlašovací formulář**. Identitu přebírá z firemního
Active Directory přes IIS Windows Authentication. Pokud máš funkční doménový
účet, do PM Trackeru se přihlásíš automaticky **single sign-on** — stačí
otevřít URL aplikace.

## Jak to technicky funguje

1. Otevřeš v prohlížeči URL PM Trackeru
2. Browser pošle HTTP požadavek
3. IIS si vyžádá NTLM / Kerberos token z prohlížeče
4. Pokud jsi přihlášený do domény, browser token pošle automaticky
5. Aplikace dostane `User.Identity.Name` ve formátu `DOMÉNA\login`
6. Aplikace dohledá osobu v `dbo.osoby` podle `Guid_AD`
7. Pokud najde a osoba je aktivní → tě pustí dovnitř
8. Pokud nenajde → 401/403

## Co potřebuješ mít

- **Doménový účet** v firemním AD
- **Záznam v `dbo.osoby`** s vyplněným `Guid_AD` odpovídajícím tvému AD účtu
- **Přístup k intranet síti** (PM Tracker není veřejně dostupný)

## Časté problémy

### Dostávám 401 / 403 hned po otevření URL

Nejčastější příčina: **nemáš záznam v `dbo.osoby`** nebo `Guid_AD` neodpovídá
tvému AD účtu. Obrátit se na [administrátora](../pomoc/kontakty.md), aby ti
záznam založil.

### Browser pořád ptá na heslo

- Prohlížeč nepoznává PM Tracker URL jako *trusted intranet zone*. Přidat URL
  do *Local intranet* nebo *Trusted sites* v IE / Edge / Chrome enterprise
  policy.
- Není podporovaná verze prohlížeče (např. Firefox bez konfigurace
  `network.negotiate-auth.trusted-uris`).

### Po přihlášení vidím prázdnou stránku / 500

- Aplikace má vnitřní chybu. Pošli admin podpoře screenshot a `Trace-Id` ze
  stavového řádku odpovědi.

### Přihlásil jsem se, ale nic nemůžu dělat (samá chyba 403 na akce)

Aplikace tě poznala, ale **nemáš žádné role**. Admin tě musí přiřadit do
projektů s rolí. Detail: [Role a oprávnění](role-a-prava-prehled.md).

## Související

- [Active Directory — propojení](../integrace/active-directory/prihlaseni-ad.md) — admin pohled
- [Pomoc → Řešení problémů](../pomoc/reseni-problemu.md)
- [Profil](../profil/) — co o tobě aplikace ví po přihlášení
