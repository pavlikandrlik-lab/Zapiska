# Zápiska – Q and A

## 1) Proč nevidím tlačítko „Upravit“ u projektu nebo úkolu?

Tlačítko se zobrazuje jen uživatelům s příslušným oprávněním (např. `projects.edit`, `records.edit`) a případně ve správném projektovém rozsahu.

## 2) Proč nevidím záložku Nastavení?

Záložka je dostupná jen pro superadmina nebo pro role s právem `settings.view`.

## 3) Proč nejde změnit některý řádek v číselníku?

Řádek může být uzamčen (`is_locked = 1`) nebo nemáš oprávnění `ciselniky.edit`.

## 4) Co znamená stav jednání „Uzavřeno“?

Uzavřené jednání je považováno za zamčené pro běžné úpravy zápisu. Pro další změny musí být jednání znovu otevřeno.

## 5) Proč při založení jednání hlásí aplikace duplicitní číslo?

V rámci jednoho projektu musí být číslo jednání unikátní.

## 6) Kam vede klik na externí vazbu PMP/PNF/NES?

Na detail tiketu v ServiceDesk:

`https://servicedesk.fis.acr/Hotline/Ticket/Details/{ticketId}`

Ticket ID se bere z číslic externího čísla vazby.

## 7) Proč některé externí vazby nejsou klikací?

Pokud číslo vazby neobsahuje číslice, aplikace nemá z čeho složit ticket ID, takže vazba zůstane jen informační.

## 8) Dá se v komentáři použít více řádků?

Ano. Nové řádky se ukládají i zobrazují ve všech hlavních pohledech i v exportech.

## 9) Jak se určuje, co uživatel opravdu může?

Výsledek je kombinace:

- přiřazených rolí,
- mapování role -> akce,
- scope (`ALL`/`INCLUDE`) a případně projektového omezení.

## 10) Co dělat, když se uživatel nepřihlásí přes AD?

Zkontrolovat:

- že existuje v `dbo.osoby`,
- že má vyplněné `Guid_AD`,
- že je v IIS zapnutá Windows Authentication.

## 11) Kde najdu instalační postup?

V dokumentaci:

- `Dokumentace -> Technická dokumentace` (v aplikaci),
- nebo přímo `/Users/Pavel.Andrlik/Documents/PM Tracker/install.md` (instalační markdown mimo aplikaci).

## 12) Kde mám hlásit incident?

Primárně přes Servicedesk: `https://servicedesk.fis.acr`.
