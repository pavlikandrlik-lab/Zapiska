# Etapy nasazení

Zadavatel rozhodl (kolo 3): **velký ERP se napojuje až později, jeden číselník po druhém.**
První nasazení tedy stojí na ručně spravovaných číselnících.

---

## Etapa 1 — plnohodnotná aplikace bez vnějších zdrojů

Aplikace je po etapě 1 **hotová a použitelná**, ne polotovar. Chybí jí jen automatické
přebírání dat.

| Oblast | Obsah |
|---|---|
| Číselníky | Založení, definice struktury (atributy, vazby), hierarchie |
| Hodnoty | Hromadná editace v tabulce, výhradní zámek, platnost od–do, vyřazení ukončením platnosti |
| Verzování | Rozpracované změny, publikování verze, dvojkové číslo, rozdíl mezi verzemi |
| Import | Nahrání souboru JSON → detekce změn → promítnutí do editační tabulky |
| Rozhraní | REST pro konzumující aplikace, JSON Schema, stálé identifikátory |
| Oprávnění | Role s datovým rozsahem, efektivní práva |
| Průřezově | Auditní log, globální vyhledávání, export do PDF a tisk, wiki, profil, vzhled |

### Co se z etapy 2 staví už teď a proč

| Věc | Staví se v etapě 1 | Důvod |
|---|---|---|
| `ciselnik.rezim_spravy` (RUCNI / EXTERNI) | **ano** | Je to doménový fakt a řídí, kdo smí editovat. Doplňovat ho zpětně by znamenalo měnit pravidla editace u existujících dat. |
| Porovnávací a verzovací stroj | **ano** | Postaví ho import JSON — potřebuje přesně totéž: porovnej se stavem, sestav změny, ukaž je, ulož |
| Rozhraní konektoru | **ne** | Rozhraní bez jediné implementace je jen dohad o tom, co budou zdroje potřebovat. Napíše se s prvním konektorem. |
| Obrazovka zdrojů, plánované spouštění, prahová pojistka | **ne** | Nemá co obsluhovat |

> **Klíčové zjištění:** import JSON v etapě 1 postaví přesně ten stroj, který konektory
> v etapě 2 jen využijí. Etapa 2 nepřidává mechaniku, jen další zdroj dat.
> Proto je pořadí etap správné a ne opačné.

---

## Etapa 2 — napojení vnějších zdrojů

Postupné, **jeden číselník po druhém**. Každé napojení je samostatná dodávka:
napsat konektor, ověřit na datech, spustit, sledovat.

| Obsah |
|---|
| Rozhraní konektoru + první konektor na ERP |
| Evidence zdrojů a jejich nastavení |
| Spouštění: ručně, periodicky, na událost |
| Prahová pojistka proti tichému vyprázdnění (E4) |
| Automatické vydání verze při nalezeném rozdílu (V6) |
| Obrazovka stavu běhů |

Číselníky převedené do režimu EXTERNI přestávají být ručně editovatelné (E3).

---

## Etapa 3 — výhled

| Obsah | Poznámka |
|---|---|
| Role *navrhovatel* a schvalování návrhů | Model ji nese od etapy 1, jen se nezavádí |
| Napojení na centrální systém řízení přístupů | Sestavení efektivních práv je za rozhraním od etapy 1 |
| Převod dokumentace do XWiki | Až bude XWiki nasazená |

---

## Co z etapizace plyne pro plán implementace

Bloky etapy 1 se dělí na **funkční řezy**, ne na vrstvy. Aplikace je spustitelná
a vyzkoušitelná po každém bloku. Etapa 2 je samostatná sada bloků, která se etapy 1
nedotýká — jen do ní zapojuje nový zdroj dat.
