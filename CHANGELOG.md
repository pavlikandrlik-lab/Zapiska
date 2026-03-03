# Changelog

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
