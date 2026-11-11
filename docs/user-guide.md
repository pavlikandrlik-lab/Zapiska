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

### Dashboard projektu
Otevřeš ho z detailu projektu. Nahoře je název projektu, jeho stav a informační systém (IS) ve ServiceDesku.

**Souhrnný pás** ukazuje stav k dnešku, nezávisle na období:
- *Úkoly po termínu* – úkoly, kterým propadl plánovaný krok harmonogramu.
- *Čeká na skutečnost* – krok s propadlým plánem, u kterého chybí skutečné datum.
- *Tickety v prodlení* a *Hrozí prodlení* – jen u projektu napojeného na IS.

Klik na dlaždici otevře příslušnou záložku a nastaví filtr. Dlaždice vidíš jen u záložek, ke kterým máš oprávnění.

**Záložka Záznamy** – úkoly s problémem v harmonogramu. Filtruješ tlačítky s počty (V prodlení, Čeká na skutečnost, Blíží se termín). Klik nebo Enter na řádku rozbalí problematické kroky a odkazy na záznam a harmonogram.

**Záložka NES v prodlení** – tickety IS, které jsou **právě u dodavatele** a mají penalizované dny. Počítá se stejně jako ve starém ServiceDesku:
- NES: SLA hodiny podle důležitosti, pak pracovní dny penále.
- PMP: kalendářní dny po termínu plnění.
- PNF: pracovní dny po termínu plnění.
- Schválené prodloužení termínu dny do nového data promíjí.

Ticket vrácený zpět k nám v seznamu není. Ticket, který přešel mezi dodavateli, má u dodavatele hvězdičku. Penále se pak počítá každému dodavateli zvlášť.

Rozbalený řádek ukáže časovou osu (kdy byl ticket u dodavatele, termín, prodloužení, penalizované dny, dnešek) a odůvodnění po krocích. Pod tabulkou jsou sekce *Hrozí prodlení* (NES do 48 h SLA, PMP/PNF do 5 dní do termínu) a *Nelze vyhodnotit* (chyba dat ve ServiceDesku, např. důležitost bez SLA). **Export do Excelu** stáhne tabulku s penalizovanými dny, typem dní, odůvodněním a daty dnů v komentářích buněk a druhý list s tickety, které nejde vyhodnotit.

**Záložka Statistiky** – report za zvolené období (rok, případně kvartál). Trendy se srovnávají s předchozím obdobím stejné délky (popisek „Srovnání s:“). Sekce:
- *Přehled* – otevřené úkoly, dokončeno, včasnost, průměrný posun termínu, tickety v prodlení, rozpočet IS.
- *Průběh práce* – založené a dokončené úkoly, stav úkolů, stav podle subsystému.
- *Termíny* – dodržení původního a posledního termínu, posun podle subsystému, stáří otevřených úkolů.
- *Lidé a spolupráce* – vytížení vlastníků, účast na jednáních, vyjádření podle subsystému.
- *ServiceDesk a dodavatelé* – otevřené tickety, penalizované dny podle dodavatele a po měsících, akceptované kalkulace.

Úkolové grafy počítají jen kategorii Úkol. U každého grafu je „Zobrazit data“ s tabulkou čísel. Projekt bez IS má v sekci ServiceDesk jen odkaz na nastavení napojení.

## 4) Záznamy a vyjádření
- Záznam obsahuje stav, vlastníka, termíny, subsystém a externí vazby.
- Vyjádření podporuje více řádků a vazbu na konkrétní jednání.
- U uzavřeného jednání může být zápis vyjádření omezen.
- **Záznam se na dobu úprav zamyká.** Když ho někdo edituje, ostatním se editor neotevře
  a uvidí, kdo na něm pracuje a odkdy. Zámek padá uložením nebo odchodem z editoru;
  po 15 minutách bez aktivity vyprší sám (např. když prohlížeč spadne).

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
- „Tento záznam upravuje jiný uživatel": kolega má otevřenou úpravu. Počkej, až uloží,
  nebo se s ním domluv; po 15 minutách jeho nečinnosti se záznam uvolní sám.
- „Záznam mezitím uložil jiný uživatel": tvůj formulář stojí na starší verzi. Použij
  **Obnovit stránku**, zkontroluj, co kolega změnil, a zadej svoji úpravu znovu.

## 8) Podpora
- FIS: 973 200 840
- ISSP: 973 225 500
- ŠIS: 973 211 111
- Servicedesk: [https://servicedesk.fis.acr](https://servicedesk.fis.acr)

## 9) Související dokumenty
- Technická dokumentace (in-app): `/Dokumentace/Technicka/Strom-dokumentace`
- Q and A: `/Dokumentace/qa`
