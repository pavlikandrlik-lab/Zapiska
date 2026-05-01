---
title: Pomoc
description: FAQ, řešení nejčastějších problémů, kontakty na podporu.
---

# Pomoc

Tato sekce je první místo, kam jít, když si nevíš rady. Postupuj podle této
hierarchie:

## Hierarchie pomoci

1. **[FAQ](faq.md)** — projdi nejčastější otázky. Většinu situací řeší.
2. **[Řešení problémů](reseni-problemu.md)** — strukturovaný troubleshooting
   podle příznaků. Pokud máš konkrétní problém ("nevidím tlačítko",
   "import selhává", …), podívej se sem.
3. **[Kontakty](kontakty.md)** — pokud si nepomůžeš sám, zde jsou kontakty
   na hotline FIS / ISSP, ServiceDesk a aplikační podporu.

## Typické problémy a kam jít

| Problém | Kde to řeší |
|---|---|
| Nevidím tlačítko *Upravit* | [Role a oprávnění](../zacatek/role-a-prava-prehled.md) → typicky chybí permission |
| Nevidím sekci *Nastavení* | [Role a oprávnění](../zacatek/role-a-prava-prehled.md) — chybí `settings.view` |
| Nelze upravit řádek v číselníku | [Číselníky → Editace](../ciselniky/editace.md) — možná systémově uzamčený |
| Jednání je read-only | [Uzavřené jednání](../projekty/jednani/uzavrene-jednani.md) |
| Externí vazba říká "Ticket nenalezen" | [SD řešení problémů](../integrace/servicedesk/reseni-problemu-sd.md) |
| NES panel je prázdný | [SD řešení problémů](../integrace/servicedesk/reseni-problemu-sd.md) — typicky chybí propojení projektu na IS |
| Auto-fill skutečnosti nefunguje | [SD řešení problémů](../integrace/servicedesk/reseni-problemu-sd.md) |
| Nepřihlásím se přes AD | [Přihlášení přes AD](../integrace/active-directory/prihlaseni-ad.md) |

## Pro koho je sekce

Pro každého. Jednotlivé stránky můžou mít poznámku "tato situace je relevantní
jen pro admina", ale obecně sekci vidí všichni.

## Kdy eskalovat

- Po projití [FAQ](faq.md) a [Řešení problémů](reseni-problemu.md) jsi nenašel řešení
- Aplikace je nedostupná / 500 error
- Změna business logiky (potřebuješ jinou roli, jiné oprávnění, jiný číselník)

Eskalace cesty: viz [Kontakty](kontakty.md).

## Známé limity aplikace

Tyhle věci nejsou bug, jsou business rozhodnutí nebo design choice:

- **Osoby z AD se nezakládají automaticky** — admin musí založit záznam
- **Soft-delete** projektů a záznamů nelze obnovit z UI (jen DB)
- **Číselníky nejdou rozšiřovat z UI** — přes deploy
- **Role nejdou změnit z UI** — přes deploy
- **PM Tracker do SD nezapisuje** — jen čte
- **Wiki je intranet** — žádné externí internet zdroje
