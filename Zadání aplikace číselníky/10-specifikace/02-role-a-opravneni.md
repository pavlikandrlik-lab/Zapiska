# Role a oprávnění

## Základní pravidlo

> **Role je obecná. Datovým rozsahem role je konkrétní číselník.**
>
> Přibude-li padesátý číselník, **nesmí kvůli němu vznikat nová role**. Přidělí se pouze
> rozsah k existující roli.

Toto pravidlo je závazné a je hlavním kritériem správnosti autorizačního modelu.
Model Zápisky ho splňuje — má dvouúrovňový rozsah: na úrovni akce (globální nebo vázaná
na objekt) a na úrovni přiřazení role (všechny objekty, nebo výčet konkrétních).
Číselníky přebírají tentýž model, jen objektem není projekt, ale číselník.

---

## Role

### Prohlížeč — implicitní, nepřiděluje se

Každý příchozí uživatel. **Nemá v aplikaci žádný záznam a žádnou přidělenou roli.**
Prohlíží číselníky.

> **Odchylka od Zápisky.** Zápiska vyžaduje, aby přihlášený doménový uživatel měl záznam
> osoby v databázi — bez něj nemá identitu ani práva. Číselníky tuto podmínku pro čtení
> **mít nesmí**. Neznámý doménový uživatel dostává práva prohlížeče automaticky.
> Rozsah je rozhodnutý (N1): **vidí vše** — hodnoty, verze i historii. Nesmí jen nic měnit.

### Editor — přiděluje se, má datový rozsah

Zakládá, mění a vyřazuje hodnoty **v číselnících, které má v rozsahu**.
Rozsah je buď výčet číselníků, nebo „všechny".

### Navrhovatel — připravit, zatím neaktivní

Zadává **návrhy změn** místo přímých úprav. Návrh schvaluje ten, kdo má
roli editora na tentýž číselník.

> Role se v prvním nasazení **nezavádí, ale datový model a autorizační vrstva ji musí
> unést bez přestavby.** Konkrétně: stav návrhu u položky, vazba návrh → schvalovatel,
> a oprávnění navázané na tentýž datový rozsah.

### Správce číselníků

Zakládá číselníky, definuje jejich strukturu, spravuje zdroje, vydává verze.
Rozsah přidělený stejným způsobem jako editorovi.

> **Samostatná role záměrně** (rozhodnutí E1). Odděluje toho, kdo číselníky den co den
> aktualizuje, od toho, kdo je navrhuje a zakládá. Změna struktury se dotýká konzumujících
> aplikací — nepatří ke každodenní editaci hodnot.

### Správce aplikace

Přiděluje role, spravuje nastavení, vidí auditní log a efektivní práva.
Globální role bez datového rozsahu. **Tohle je běžná provozní role** — má klíče
jako každá jiná a řídí se stejnými pravidly.

### Superadmin — nouzový klíč, ne role

Samostatný mechanismus převzatý ze Zápisky. **Není to role a nemá klíče.**
Je to řádek v tabulce `superadmini`, který **zkratuje každou kontrolu oprávnění**:
kdo je v ní uvedený, smí všechno bez ohledu na to, co říkají role a rozsahy.

**K čemu to je:** k odemčení aplikace, když je porušená sama data o oprávněních.
Kdyby superadmin závisel na tabulkách rolí, nefungoval by právě ve chvíli, kdy je potřeba.
Proto je mimo ně.

**Provozní pravidla:**

| Pravidlo | Důvod |
|---|---|
| Aspoň dva aktivní superadmini | Jeden je jediný bod selhání |
| Nikdy sdílené účty | Auditní stopa musí ukázat na člověka |
| Změny v tabulce jdou do provozního auditu | Je to nejsilnější oprávnění v aplikaci |
| **K běžné práci se nepoužívá** | Na to je role správce aplikace |

Zdroj k harvestu: `PmTracker.Web/Services/Security/AuthorizationSnapshot.cs` — pole
`IsSuperAdmin` a jeho vyhodnocení na začátku každé kontroly.

---

## Model oprávnění

Přebírá se **seed-only RBAC ze Zápisky** — role, klíče oprávnění a jejich mapování jsou
definované v kódu a verzované v gitu, ne skládané za běhu přes uživatelské prostředí.
Zdůvodnění a technický popis:
[30-prevzate-moduly/02-autorizace-rbac.md](../30-prevzate-moduly/02-autorizace-rbac.md).

Konvence klíčů zůstává: jeden klíč = jedna mutující akce, ne hrubý klíč „editace".

Předběžný náčrt klíčů (dopřesní se při návrhu obrazovek):

| Kategorie | Klíče |
|---|---|
| Číselníky | `ciselniky.create`, `ciselniky.edit`, `ciselniky.definice.edit`, `ciselniky.deactivate` |
| Hodnoty | `hodnoty.create`, `hodnoty.edit`, `hodnoty.deactivate` |
| Verze | `verze.publish` |
| Zdroje *(etapa 2)* | `zdroje.configure`, `zdroje.run` |
| Import | `import.json` |
| Návrhy *(připraveno, zatím neaktivní)* | `navrhy.create`, `navrhy.approve`, `navrhy.reject` |
| Nastavení | `settings.roles.assign`, `settings.audit.view`, `search.reindex` |

**Čtení nemá klíč.** Prohlížení číselníků není chráněná akce — vidí je každý (rozhodnutí N1).
Klíče existují výhradně pro akce, které něco mění.

Klíče vázané na číselník se vyhodnocují proti datovému rozsahu. Klíče správy aplikace
se vyhodnocují globálně.

---

## Výhled: centrální systém řízení přístupů

Aplikace se má v čase napojit na **centrální systém řízení přístupů**, který bude dodávat
kombinaci **uživatel × role × datové rozsahy**.

Důsledek pro návrh, závazný od prvního bloku:

- Sestavení efektivních práv uživatele je **za rozhraním** se dvěma implementacemi —
  dnes z vlastní databáze, později z centrálního systému.
- **Datový rozsah je prvotřídní pojem modelu**, ne dodatek přilepený k roli. Přesně proto,
  že centrální systém ho bude dodávat jako samostatný údaj.
- Obrazovka „Efektivní práva" zobrazuje u každého oprávnění i **zdroj** — odkud grant přišel.
  Po napojení na centrální systém přibude další zdroj a obrazovka se nemění.
