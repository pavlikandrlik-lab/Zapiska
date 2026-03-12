# PM Tracker - Q and A

## 1) Proč nevidím tlačítko „Upravit"?
Nejčastěji chybí příslušné oprávnění (`projects.edit`, `records.edit`) nebo je právo omezené scope režimem `INCLUDE` mimo aktuální projekt.

## 2) Proč nevidím záložku Nastavení?
Sekce je dostupná jen pro role s právem `settings.view` nebo pro superadmina.

## 3) Proč nejde upravit některý řádek v číselníku?
Položka může být systémově uzamčená (`is_locked=1`) nebo nemáš právo `ciselniky.edit`.

## 4) Proč je jednání read-only?
Jednání ve stavu `CLOSED` je standardně uzavřené pro běžné úpravy.

## 5) Proč při založení jednání dostávám chybu duplicitního čísla?
V rámci jednoho projektu musí být číslo jednání unikátní.

## 6) Kam vede klik na externí vazbu PMP/PNF/NES?
Na detail tiketu v ServiceDesk:
`https://servicedesk.fis.acr/Hotline/Ticket/Details/{ticketId}`

## 7) Proč některé externí vazby nejsou klikací?
Pokud hodnota neobsahuje číselné ticket ID, odkaz zůstane jen informativní text.

## 8) Jak ověřím, co uživatel opravdu může?
V `Nastavení -> Efektivní práva` pro konkrétního uživatele a projekt.

## 9) Co dělat, když se uživatel nepřihlásí přes AD?
- ověř existenci osoby v `dbo.osoby`,
- ověř `Guid_AD`,
- ověř IIS Windows Authentication.

## 10) Synchronizují se osoby z AD automaticky do aplikace?
Ne. Aplikace nemá automatický AD sync do `dbo.osoby`. Každý uživatel, který se má přihlásit, musí mít odpovídající záznam založený v aplikaci (včetně `Guid_AD`).

## 11) Kde najdu detailní instalační a provozní dokumentaci?
- In-app technická dokumentace: `/Dokumentace/Technicka/Strom-dokumentace`
- Repo instalace: `/Users/Pavel.Andrlik/Documents/PM Tracker/install.md`

## 12) Kde hlásit incident?
Primárně přes [Servicedesk](https://servicedesk.fis.acr).
