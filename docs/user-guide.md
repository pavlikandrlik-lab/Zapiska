# PM Tracker - Uživatelská příručka

## 1) Účel
Uživatelská příručka popisuje běžnou práci v aplikaci PM Tracker: projekty, záznamy, jednání, osoby, číselníky a exporty.

## 2) Rychlá orientace
- `Projekty`: centrální vstup do práce s projektem.
- `Jednání`: plánování a evidence porad.
- `Osoby`: přehled osob dle oprávnění.
- `Číselníky`: referenční data (dle role).
- `Nastavení`: správa oprávnění (jen oprávněné role).
- Poznámka: osoby se z AD nepřenášejí automaticky; pro přístup musí být osoba založena i v aplikaci.

## 3) Projekty
- V přehledu vidíš jen projekty, ke kterým máš přístup.
- Detail projektu obsahuje záznamy, jednání a tým.
- Editační prvky se zobrazují pouze při odpovídajícím oprávnění.

## 4) Záznamy a vyjádření
- Záznam obsahuje stav, vlastníka, termíny, subsystém a externí vazby.
- Vyjádření podporuje více řádků a vazbu na konkrétní jednání.
- U uzavřeného jednání může být zápis vyjádření omezen.

## 5) Jednání
- Číslo jednání musí být unikátní v rámci projektu.
- Uzavřené jednání je standardně read-only.
- Účastníci a stavy účasti jsou řízeny číselníky.

## 6) Exporty
Podporované exporty:
- Tisk projektu
- Tisk jednání
- Tisk úkolu/záznamu
- Výstupy: PDF/HTML a Word (.docx)

## 7) Nejčastější problémy uživatele
- Nevidíš tlačítko `Upravit`: chybí oprávnění nebo scope.
- Nelze uložit změnu: položka je zamčená (`is_locked=1`) nebo chybí právo editace.
- Nefunguje externí odkaz: vazba neobsahuje validní ticket ID.

## 8) Podpora
- FIS: 973 200 840
- ISSP: 973 225 500
- ŠIS: 973 211 111
- Servicedesk: [https://servicedesk.fis.acr](https://servicedesk.fis.acr)

## 9) Související dokumenty
- Technická dokumentace (in-app): `/Dokumentace/Technicka/Strom-dokumentace`
- Q and A: `/Dokumentace/qa`
