# Zápiska – Uživatelská příručka

## 1. K čemu aplikace slouží

Zápiska je nástroj pro řízení projektů, zápisy z jednání a průběžnou evidenci úkolů.

Hlavní části:

- **Projekty** – přehled projektů a jejich detail,
- **Záznamy** – úkoly, informace a rozhodnutí v projektu,
- **Jednání** – porady a zápis úkolů,
- **Osoby** – seznam uživatelů a kontaktů,
- **Číselníky** – referenční hodnoty (jen dle oprávnění).

## 2. Základní orientace

- Horní menu přepíná moduly.
- Vpravo nahoře je profil uživatele a tmavý režim.
- V patičce jsou odkazy na dokumentaci, informace a podporu.

## 3. Práce s projekty

### 3.1 Přehled projektů

- vidíš jen projekty, ke kterým máš přístup,
- kliknutím na dlaždici otevřeš detail projektu.

### 3.2 Detail projektu

Záložky:

- **Záznamy**
- **Jednání**
- **Osoby (tým)**

Co se stane po změně:

- úpravy projektu se ukládají okamžitě po potvrzení formuláře,
- chybné nebo nevalidní hodnoty vrátí chybovou hlášku.

## 4. Práce se záznamy (úkoly)

Na kartě záznamu vidíš:

- stav, typ, vlastníka, termín, subsystém,
- historii změn (vlastník/termín/subsystém),
- externí vazby (PMP/PNF/NES),
- vyjádření.

### Externí vazby

- najetí myší zobrazí detail vazby (tooltip),
- klik na vazbu otevře ServiceDesk detail tiketu v nové kartě.

### Vyjádření

- podporují více řádků,
- mohou být navázaná na konkrétní jednání,
- řazení může být podle jednání.

## 5. Práce s jednáními

V modulu `Jednání` můžeš:

- založit nové jednání (pokud máš právo),
- upravit stav jednání,
- zapisovat účast a vyjádření k úkolům.

Důležité:

- uzavřené jednání je read-only pro zápis,
- číslo jednání musí být v projektu unikátní.

## 6. Tisk a export

K dispozici jsou tiskové varianty:

- tisk projektu,
- tisk konkrétního jednání,
- tisk jednoho úkolu.

Podporované formáty:

- PDF/HTML tisk,
- Word export (.docx).

## 7. Co nedělat

1. Neměň systémové číselníky bez konzultace se správcem.
2. Nezadávej testovací nebo duplicitní čísla jednání v produkci.
3. Nemazej významné texty ve vyjádřeních bez dohody v týmu.
4. Nezakládej osoby ručně, pokud má být účet vedený přes AD.

## 8. Nejčastější situace

### Nevidím tlačítko „Upravit“

Pravděpodobně nemáš potřebné oprávnění pro daný projekt/modul.

### Nejdu přidat vyjádření

- jednání může být uzavřené,
- nebo nemáš právo zápisu.

### Odkaz na externí vazbu nefunguje

Číslo vazby možná neobsahuje ticket ID (číslice). V takovém případě je vazba pouze informativní.

## 9. Podpora

- FIS: 973 200 840
- ISSP: 973 225 500
- ŠIS: 973 211 111
