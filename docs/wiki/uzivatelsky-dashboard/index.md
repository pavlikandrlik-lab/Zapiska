---
title: Uživatelský dashboard
description: Hlavní stránka po přihlášení — moje priority, novinky, globální vyhledávání.
---

# Uživatelský dashboard

Dashboard je **rozcestník pro denní práci**. Je to první stránka, kterou vidíš
po přihlášení. Na rozdíl od [projektového dashboardu](../projekty/projektovy-dashboard/),
který se týká jednoho konkrétního projektu, **uživatelský dashboard agreguje
data napříč všemi projekty**, ke kterým máš přístup.

## Pro koho je

Pro každého přihlášeného uživatele. Co konkrétně vidíš závisí na:

- Tvých přiřazeních k projektům (collaborator, vlastník, member)
- Tvých rolích a permission keys
- Konfiguraci aplikace (časový horizont, počet zobrazených položek)

Co je za přiřazením k jinému projektu, se ti **vůbec nezobrazí** — ani v
priorities, ani v novinkách, ani ve vyhledávání.

## Hlavní panely

### [Moje priority](moje-priority.md)

Záznamy seřazené podle **urgentnosti**. Aplikace bere v úvahu termín, stav
záznamu, tvé role a překročený deadline. Cíl: jediný pohled "co mám dnes
udělat".

### [Novinky](novinky.md)

Co se v projektech dělo. Změny záznamů, nová jednání, dodaná řešení. Filtrované
na projekty ke kterým máš přístup. Časový horizont řízený konfigurací (typicky
posledních N dnů).

### [Globální vyhledávání](globalni-vyhledavani.md)

Full-text vyhledávání napříč entitami: projekty, záznamy, jednání, osoby.
Výsledky filtrované přístupovými právy.

## Vztah k projektům

Z dashboardu vždycky vede cesta **do konkrétního projektu**:

- Klikni na záznam v "Moje priority" → otevře se editor záznamu
- Klikni na položku v Novinkách → projektový dashboard nebo editor
- Vyhledávání → výsledek tě dovede do detailu

Dashboard sám nedovoluje editaci. Je to jen vstupní pohled.

## Související

- [Profil → Moje práva](../profil/moje-prava.md) — co můžeš dělat
- [Projekty](../projekty/) — centrální oblast aplikace
