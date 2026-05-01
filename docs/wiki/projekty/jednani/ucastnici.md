---
title: Účastníci jednání
description: Pozváni / přítomni / omluveni — evidence účasti.
---

# Účastníci jednání

Pro každé jednání aplikace eviduje **kdo měl být** a **kdo skutečně byl**.
Slouží to pro:

- **Plánování porady** (kdo má dorazit, posílání pozvánek)
- **Zápis ze jednání** (kdo skutečně byl, kdo se omluvil)
- **Reporting** (statistiky účasti za projekt / osobu)

## Stavy účasti

Konkrétní hodnoty jsou v číselníku, mohou se lišit. Typicky:

| Stav | Význam |
|---|---|
| **Pozván** | Má dorazit, ještě nevíme zda dorazí |
| **Přítomen** | Dorazil, je nebo byl na jednání |
| **Omluven** | Neúčastní se, ale dal vědět + důvod |
| **Nepřítomen** | Neúčastní se, bez omluvy |
| **Online** | Účastní se vzdáleně (Teams / Zoom) |

## Přidání účastníka

V detailu jednání → sekce *Účastníci* → tlačítko **Přidat účastníka**:

1. Otevře se modal s [AD pickerem](../../integrace/active-directory/ad-picker.md)
2. Vyhledej osobu podle jména / loginu / emailu
3. Vyber roli (volitelně, pokud má číselník rolí účastníka)
4. Default stav účasti = **Pozván**
5. Ulož

Po uložení účastník je v seznamu, ale stav je *Pozván* — předpokládá se že
přijde ale ještě nevíme zda dorazí.

## Změna stavu účasti

V detailu jednání:

- Klik na řádek účastníka → otevře se inline editor
- Změň stav z dropdownu (Pozván → Přítomen / Omluven / atd.)
- Ulož

Akce se zaznamenává do audit logu.

## Vlastní účast

User může označit svoji **vlastní účast** (i když není leader):

- Ve seznamu účastníků klikne na svůj řádek
- Změní stav

Vyžaduje permission `meetings.attendance.set`. Default by to měl mít každý
member projektu (typicky).

## Hromadné nastavení

V hlavičce sekce *Účastníci* je tlačítko **Označit všechny jako Přítomné**
(`meetings.attendance.bulk-set`). Slouží:

- Po jednání leader rychle označí kdo dorazil
- Default = "všichni přítomní", pak ručně označí omluvené / nepřítomné

## Permissions

| Akce | Klíč |
|---|---|
| Vidět účastníky | `meetings.view` (implicitní) |
| Přidat účastníka | `meetings.attendance.add` |
| Změnit stav cizí osoby | `meetings.attendance.set-other` |
| Změnit stav vlastní | `meetings.attendance.set` |
| Hromadné označení | `meetings.attendance.bulk-set` |

## Synchronizace s tymem projektu

Účastníci jednání **nemusí být členy projektového týmu**:

- **Pozvaní z týmu** — typicky přidávaní AD pickerem z týmu
- **Externí účastníci** — třetí strana (klient, dodavatel) — také přidaní AD pickerem
- **Pravidelní účastníci** — při vytvoření jednání lze nastavit *default
  pozvánku celému týmu* (pokud to verze podporuje)

Aplikace **nezajišťuje synchronizaci členství** — když přidáš někoho do
jednání, **netvoří** to z něj člena projektového týmu. To jsou separátní
struktury.

## Notifikace

V aktuální verzi aplikace **nemá email / push notifikace**. Pozvánka na
jednání se nedoručuje automaticky — leader to typicky řeší externě (kalendářová
pozvánka v Outlook, mail, MS Teams).

## V exportu zápisu

[Tisk jednání](../../export/tisk-jednani.md) obsahuje **tabulku účastníků** s:

- DisplayName
- Stav účasti (formálně, např. "Přítomni: Jan Novák, Marie Svobodová;
  Omluveni: ...")
- Volitelně role v jednání (pokud existuje)

## Vztah k uzavřenému jednání

Když je jednání **uzavřené**, účast je **read-only**. Změna stavu vyžaduje
otevřít jednání zpět (permission `meetings.reopen`).

## Související

- [AD picker](../../integrace/active-directory/ad-picker.md)
- [Uzavřené jednání](uzavrene-jednani.md)
- [Tisk jednání](../../export/tisk-jednani.md)
