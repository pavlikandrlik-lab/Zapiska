# K otestování — hromadné ruční ověření

Průběžný seznam změn, které jsou v lokálním `main`, prošly automatickými testy a čekají na ruční ověření (i15 / Edge, IIS). Po otestování položku odškrtni nebo smaž.

Nasazení: kód je jen v lokálním `main` (bez pushe). Žádná změna DB schématu, pokud není u položky uvedeno jinak.

---

## 1. Pohled jako (superadmin vidí aplikaci očima jiného uživatele)

**Hotovo 2026-10-20:** merge `062e371`, plán `docs/superpowers/plans/2026-10-20-pohled-jako.md`, popis v `docs/authorization.md` (sekce „Pohled jako“).

**Co to dělá:**
- Superadmin má v menu účtu (vpravo nahoře) položku **Zobrazit jako…**. Otevře modal s výběrem osoby.
- Aplikace se pak chová jako pro vybranou osobu: role, práva, viditelné projekty, menu i „Moje práva“.
- Pod hlavičkou je žlutá lišta „Prohlížíte aplikaci jako X, jen pro čtení. Váš účet: Y“ s tlačítkem **Zrušit pohled jako**.
- Režim je **jen pro čtení**: žádné uložení neprojde, zámek karty se nezakládá ani neprodlužuje.
- Pohled končí zrušením nebo zavřením prohlížeče a platí ve všech záložkách.
- Zahájení a ukončení se zapisuje do `authz.audit_log` (`entity_type = 'pohled_jako'`).
- Vedlejší oprava: položka v rozbalovacím menu, která otevírá modal, menu zavře (dřív zůstávalo otevřené nad dialogem).

**Otestovat (přihlášen jako superadmin):**
- [ ] Menu účtu → je tam „Zobrazit jako…“. Běžný uživatel bez superadmina ji **nevidí**.
- [ ] Klik na „Zobrazit jako…“ → menu se zavře, otevře se modal „Zobrazit aplikaci jako jiný uživatel“.
- [ ] V modalu vyhledat osobu (jméno, e-mail, login), vybrat, „Zobrazit jako“ → přesměruje na Přehled a ukáže lištu.
- [ ] Sám sebe vybrat nejde: modal ukáže chybu u pole.
- [ ] Hlavička vpravo ukazuje jméno cílové osoby a hlavní menu jen její sekce (např. bez Nastavení a Číselníků).
- [ ] Projekty: vidíš jen projekty cílové osoby. Profil → „Moje práva“ ukazuje její role.
- [ ] Projděte detail projektu (všechny záložky), záznam, editor záznamu, Jednání, Hledání: vše se načte, nic nespadne.
- [ ] Editor záznamu → „Uložit“: červená hláška „V režimu ‚Zobrazit jako‘ nelze nic ukládat…“ (ne „Relace vypršela“, ne „Obnovit stránku“).
- [ ] Jiný modal s uložením (např. nové jednání, člen týmu, vyjádření): uložení odmítne se stejnou hláškou.
- [ ] Záznam, který právě edituje někdo jiný: v pohledu se ukáže stránka „záznam upravuje X“, stejně jako by ji viděla cílová osoba. Druhému uživateli se zámek nezmění.
- [ ] Tisk / PDF exportu v pohledu funguje (je to čtení).
- [ ] „Zrušit pohled jako“ → zůstaneš na stejné stránce, lišta zmizí, vidíš zase vše jako admin.
- [ ] Nová záložka během pohledu je taky v pohledu. Po zavření a otevření prohlížeče už pohled neběží.
- [ ] Vzhled lišty na i15 / Edge: světlý i tmavý motiv, úzké okno (tlačítko spadne pod text, nic nepřetéká).
- [ ] V DB: `SELECT * FROM authz.audit_log WHERE entity_type = N'pohled_jako' ORDER BY id DESC` → záznamy `view_as_start` / `view_as_stop`, actor = ty, entity_id = cílová osoba.

**Známá omezení (vědomě):**
- Stránka, kam cílová osoba nesmí (prázdná 403), lištu nemá. Stačí se vrátit zpět nebo na Přehled.
- Některé starší akce mimo modaly (chat vyjádření, synchronizace tiketu, přetažení vazby) při pokusu o změnu ukážou obecnou chybu místo konkrétní hlášky. Zápis je i tak zablokovaný.
- Vyhledávání osob v modalu nemusí najít jméno, když hledáš bez diakritiky (převzatý existující dotaz).
