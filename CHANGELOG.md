# Changelog

Tento soubor je generován skriptem `scripts/generate-changelog.sh` z verzovaných podkladů v `docs/changelog/releases/`.

## 1.1.0 - 2026-03-03

### Přidáno
- Hezká přístupová stránka pro běžné HTML requesty bez přístupu do aplikace.
- Osy a datumové tick marks do editorového mini-ganttu, harmonogramu a GANTTu.
- Historie změn typu úkolu v kartě záznamu.
- Centrální verzování řešení přes `Directory.Build.props`.

### Změněno
- Harmonogram a GANTT používají signed skutečnost místo jednostranného zpoždění.
- GANTT zobrazuje planned vs actual v jedné stopě a používá signed odchylku na úrovni kroku.
- Vlastník v harmonogramu a GANTTu se zobrazuje bez e-mailu, jen jako jméno a kód org. celku.
- Dokumentace už nepůsobí jako podsekce projektů v breadcrumb navigaci.
- Uživatelské menu už neobsahuje redundantní akci `Vzhled: přepnout`.
- Uzamčené systémové položky číselníků může měnit nebo odemykat jen superadmin.

### Opraveno
- Úkoly s nulovým plánovaným trváním se neukazují v harmonogramu ani v GANTTu.
- Browser request bez přístupu už nevrací syrovou JSON chybu.
- Filtry zůstávají nadřazené seznamu úkolů i časovým přehledům.

### Známé limity / Co ještě chybí
- Neprovádí se backfill staré historie typu úkolu pro přechody `null -> typ` nebo `typ -> null`.
- Dřívější skutečný začátek kroku je mimo scope; skutečnost ovlivňuje jen konec kroku.
- Dokumentace stále nemá vlastní top-level položku v hlavním menu.
- E-mailové adresy osob se odebírají jen z harmonogramu a GANTTu, ne globálně z celé aplikace.

## 0.3 - 2026-03-02

### Přidáno
- Aplikace zobrazuje svou verzi ve footeru.
- Detail projektu má nové klientské filtry `Jen mé záznamy` a `Jen mé úkoly` pro panely Záznamy, Harmonogram a GANTT.
- Aktivní projektové filtry se zobrazují jako odebíratelné čipy.
- Uživatel si může uložit výchozí projektové filtry do local storage a smazat je v profilu.

### Změněno
- Přepínač `Seskupit dle subsystému` byl přesunut z hlavičky panelu do sekce filtrů a sjednocen do gov switch stylu.
- Viditelnost hlavních navigačních záložek `Osoby`, `Číselníky` a `Nastavení` se nově řídí prefixy oprávnění `people.*`, `ciselniky.*` a `settings.*`.

