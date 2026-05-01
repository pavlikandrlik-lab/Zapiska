---
title: Řešení problémů
description: Strukturovaný troubleshooting podle příznaků.
---

# Řešení problémů

Tato stránka je rozcestník podle **příznaku problému**. Najdi svoji situaci a
následuj odkazy.

## Mám problém s přihlášením

### Aplikace mě nepustí dovnitř (401 / 403)

→ [Přihlášení — časté problémy](../zacatek/prihlaseni.md#%C4%8Dast%C3%A9-probl%C3%A9my)

### Browser pořád ptá na heslo

→ [Přihlášení — Browser pořád ptá na heslo](../zacatek/prihlaseni.md#browser-po%C5%99%C3%A1d-pt%C3%A1-na-heslo)

### Po přihlášení vidím prázdnou stránku / 500

- Interní chyba aplikace
- Pošli admin podpoře screenshot + **Trace-Id** ze stavového řádku
- Zkus znovu po několika minutách (možná restart app pool)

## Mám problém s oprávněními

### Nevidím tlačítko *Upravit / Smazat / Vytvořit*

- Default: **chybí permission key**
- Jdi na Profil → [Moje práva](../profil/moje-prava.md)
- Pokud klíč chybí, kontaktuj admina nebo si přečti
  [Role a oprávnění](../zacatek/role-a-prava-prehled.md)

### Nevidím sekci v menu

- **Nastavení** → vyžaduje `settings.view`
- **Konkrétní projekt** → vyžaduje přiřazení k projektu

### Vidím tlačítko ale po kliku 403

- **Bug v aplikaci** — UI ukazuje tlačítko ale server odmítá. Nahlas adminovi
  s **Trace-Id**.

## Mám problém s ServiceDesk integrací

### Externí vazba říká *"Ticket nenalezen"*

Možnosti:

1. Číslo ticketu **neexistuje v `HOT_ZAZNAMY`**
2. Překlep — ověř 6 cifer
3. Ticket existuje ale je v **archivované DB** (mimo aktivní set)
4. **SD connection nefunguje** — ověř na `/SDConnector` že stav je *Zapnutá*

Detail: [SD řešení problémů](../integrace/servicedesk/reseni-problemu-sd.md).

### Chat modal je prázdný / nezobrazuje vyjádření

- Ticket existuje ale **ještě nemá vyjádření** v SD
- **Harvest ještě neproběhl** — zkus *Re-harvest* z chat modalu

### NES panel projektu je prázdný

- **Projekt nemá propojení na IS** → graceful state s odkazem do editace
- V daném IS **opravdu nejsou žádné NES** s překročeným termínem
- `Ticketing.Enabled` je false v konfiguraci

Detail: [SD řešení problémů](../integrace/servicedesk/reseni-problemu-sd.md).

### Auto-fill harmonogramu nedoplňuje skutečnost

- Vyjádření jsou tam ale **klasifikované jako None** (text neodpovídá K3/K4_K7/K6/K10/Plán dodání)
- Krok harmonogramu **nemá nastavený typ delay**
- Režim je **Ručně** → aplikace cíleně nepřepisuje

Detail: [Auto-fill skutečnosti](../integrace/servicedesk/auto-fill-skutecnosti.md).

## Mám problém s tiskem / exportem

### Tisk dlouho trvá / timeout

- Velký projekt (> 500 záznamů)
- Zkus omezit volby (vyloučit komentáře, jen aktivní jednání)

### Tisk vrací prázdný PDF

- Browser blokuje stahování — zkontroluj download notifikace
- Pop-up blocker — povolit pro PM Tracker

### Word soubor se neotevírá / má rozsypané formátování

- Verze MS Office < 2016 — některé feature nepodporuje
- Zkus LibreOffice nebo Pages
- Alternativně použij PDF

## Mám problém s daty

### Něco jsem omylem smazal

- **Soft-delete** — admin obnoví přes DB (`is_deleted=0`)
- Pokud audit log existuje, podle něj poznat co a kdy smazáno

### Aplikace přepsala moje data

- Pravděpodobně **paralelní úprava jiným uživatelem**
- Audit log ukáže kdo / kdy / co přepsal
- Aplikace nemá optimistic concurrency check (zatím) — last-write-wins

### Skutečnost harmonogramu se přepisuje sama

- Krok je **v režimu Auto** → sync služba aktualizuje
- Přepni na **Ručně** pokud chceš zafixovat hodnotu
- Detail: [Manuální skutečnost](../projekty/harmonogram/manualni-skutecnost.md)

## Aplikace nereaguje / je pomalá

### Stránky se načítají dlouho

- **První load po deployi** — JIT compilation, EF model build, atd. Druhé
  načtení je rychlejší.
- **Velký projekt s mnoha záznamy** — pomáhá omezit filtry
- **Výpadek SD** — síťový timeout může zpomalit načítání záznamů s externí
  vazbou. Diagnostika přes `/SDConnector/Diag`.

### Aplikace nereaguje na klikání

- **Browser cache** drží polámanou starou verzi JS — zkus Ctrl+Shift+R nebo
  inkognito okno
- **JS chyba na stránce** — F12 → Console → screenshot pošli adminovi

## Známé limity

Tyhle věci nejsou bug, jsou design choice:

- Osoby z AD se nezakládají automaticky
- Soft-delete nelze obnovit z UI
- Číselníky obvykle nejdou rozšiřovat z UI
- Role nejdou změnit z UI
- PM Tracker do SD nezapisuje
- Aplikace je intranet (offline od veřejného internetu)
- Hromadné akce (multi-select) nejsou většinou podporované

## Eskalace

Pokud nic z výše uvedeného nepomohlo, viz [Kontakty](kontakty.md).

Při kontaktu uveď:

- Co jsi dělal (URL, akce)
- Co se stalo
- Trace-Id (z chyby)
- Browser + verze
