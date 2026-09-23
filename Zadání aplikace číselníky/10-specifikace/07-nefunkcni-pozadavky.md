# Nefunkční požadavky

## Prostředí

| Požadavek | Hodnota |
|---|---|
| Síť | Uzavřená, **bez připojení k internetu** |
| Provoz aplikace | IIS na Windows |
| Databáze | Microsoft SQL Server — instance se určuje připojovacím řetězcem v nastavení |
| Přihlášení | Windows Authentication, identity z Active Directory |

### Offline-first — bez výjimek

Žádný požadavek za běhu nesmí opustit síť. Prakticky:

- žádné odkazy na vzdálené knihovny, žádné externí fonty, žádné volání cizích služeb,
- všechny knihovny leží v repozitáři a servírují se z aplikace,
- **React se sestavuje na stroji s internetem**; do produkce jde hotový výstup sestavení,
  nikdy adresář se staženými balíčky,
- **na serveru ani na klientských stanicích není Node potřeba** — server servíruje
  statické soubory, prohlížeč má vlastní běhový modul pro JavaScript,
- kontrolní seznam a testy hlídající toto pravidlo se přebírají ze Zápisky.

## Zátěž

| Cesta | Charakteristika | Návrhová priorita |
|---|---|---|
| Čtení v prohlížeči | Bez omezení počtu uživatelů | Propustnost, mezipaměť |
| Čtení přes rozhraní | Roste s počtem digitalizovaných aplikací | Mezipaměť, podmíněné požadavky |
| Zápis | 5–20 lidí | Správnost a auditovatelnost, ne výkon |

Odpovědi rozhraní jsou mezipaměťovatelné a nesou značku odvozenou od verze číselníku —
opakovaný dotaz nezměněného číselníku nepřenáší data.

## Cílový prohlížeč

**Microsoft Edge na kancelářských sestavách ministerstva.** Tytéž stanice, na kterých
běží Zápiska.

Poučení ze Zápisky, které platí i zde:

- Nosné rozvržení stránky stojí na letitých a bezpečně podporovaných základech.
  Novější vlastnosti jazyka stylů se používají jen jako nepovinné vylepšení, nikdy jako
  nosný prvek.
- **Statické soubory se servírují s výslovně uvedeným kódováním UTF-8.** Bez toho se
  na těchto stanicích čeština rozsype.
- Verdikt o vzhledu vynáší uživatel na cílové stanici, ne vývojářův prohlížeč.

## Vzhled

- **gov design system** — tytéž komponenty a tokeny jako Zápiska.
- Rozvržení počítá se **širokou obrazovkou**.
- Světlý, tmavý a automatický režim.

## Přístupnost

**Rozhodnuto (N2): úroveň 1 — nic navíc.** Cílová skupina uživatelů nemá omezení,
která by zvláštní opatření vyžadovala.

Prakticky: komponenty gov design systemu si nesou to, co dodává jejich autor.
Vlastní části aplikace — tabulka hromadné editace, strom hierarchie, zvýraznění změn —
se v tomto ohledu neřeší. Bez auditu, bez prohlášení o přístupnosti, bez zvláštních
akceptačních kritérií v implementačních blocích.

## Bezpečnost

- Autorizace se vyhodnocuje **vždy na serveru**. Uživatelské prostředí smí ovládací prvky
  jen skrývat — nikdy nesmí být jediným místem, kde se právo kontroluje.
- Přístupové údaje ke zdrojům dat se do repozitáře nezapisují.
- Auditní log je **jen k připsání** — zápisy se nemění ani nemažou.
- Přístup k rozhraní je výhradně přes Active Directory (Z1). **Čtení se nesleduje,
  sleduje se zápis.**

## Provoz

| Oblast | Požadavek |
|---|---|
| Zálohování a obnova | Nastavuje provoz na serveru podle vlastní praxe. Aplikace nic nevyžaduje. |
| Nasazení | Jeden publikovaný výstup, jedno místo nasazení |
| Migrace schématu | Ručně psané očíslované skripty + kontrolní skript stavu instance |
| Sledování stavu | Stav běhů zdrojů dat viditelný v aplikaci |

## Dokumentace

Vede se od prvního bloku, ne na konci:
technická dokumentace, wiki knihovna v aplikaci, evidence známých problémů,
podklady changelogu a auditní log.

Wiki se později převede do XWiki, až bude nasazená — aplikace se tehdy upraví.
