# C1 — Šipka ← v breadcrumbs vrací na místo původu (spec)

**Datum:** 2026-07-10 · **Stav:** čeká na schválení · **Náročnost:** malá

## Problém

Šipka ← v drobečkové liště je dnes vždy URL předposledního drobečku
(`BreadcrumbTrail.ParentUrl`). Pro detail jednání je to `/Projekty/Detail/{id}`
bez `tab` → fallback na záložku Záznamy. Uživatel přitom přišel:

- ze záložky **Jednání** na detailu projektu, nebo
- z **aplikační stránky Jednání** (`/Jednani`, přehled napříč projekty), nebo
- z dashboardu (novinky už dnes posílají `returnUrl=/dashboard/news`).

Stejný problém mají stránky **Návrhů** (← vrací na Záznamy místo záložky
Návrhy) a **editor záznamu otevřený cross-nav odkazem z Harmonogramu**.

## Chování (schváleno uživatelem 2026-07-10)

1. **← vrací na místo, odkud uživatel přišel** — pro všechny záložky/entity.
2. **Klik na projekt-drobeček = homepage projektu (Záznamy)** — beze změny,
   `/Projekty/Detail/{id}` bez tab parametru.
3. Cíl ← se tedy odděluje od URL drobečku.

## Návrh

### Model
`BreadcrumbTrail` dostane volitelný explicitní cíl šipky:

```csharp
public sealed record BreadcrumbTrail(IReadOnlyList<Breadcrumb> Items, string? BackUrl = null)
{
    /// Cíl šipky ←: explicitní BackUrl (origin/kanonická záložka),
    /// jinak URL předposledního drobečku.
    public string? ParentUrl => BackUrl ?? (Items.Count >= 2 ? Items[^2].Url : null);
}
```

`_BreadcrumbBar.cshtml` beze změny (čte `ParentUrl`).

### Priorita cíle ← (origin > kanonická záložka > drobeček)

1. **Origin** — validovaný `returnUrl` query parametr (mechanismus „from"):
   `Url.IsLocalUrl(returnUrl)`, jinak ignorovat. Přesně zachytí i filtry
   a `?asUser` v původní URL. Pozn.: `JednaniController.Detail` už
   `returnUrl` přijímá a validuje — nově ho promítne do breadcrumb ←
   (dnes plní jen nerenderovaný `model.BackUrl` — relikt uklidit).
2. **Kanonická záložka entity** — když origin chybí:
   - detail jednání → `/Projekty/Detail/{id}?tab=jednani`
   - stránky návrhů (`NavrhyController`, 4 akce) → `?tab=navrhy`
   - detail/editor záznamu → `?tab=zaznamy` (= dnešní default, nově explicitně)
3. Bez obojího → dosavadní `Items[^2].Url`.

### Odkazy, které začnou posílat `returnUrl`

- `/Jednani` přehled → detail jednání: `returnUrl` = aktuální URL přehledu
  (včetně `projektId` filtru).
- Cross-nav z Harmonogramu (gant) → editor záznamu:
  `returnUrl=/Projekty/Detail/{id}?tab=harmonogram`.
- Dashboard novinky → detail jednání: už posílá `/dashboard/news` — nově
  ho ← respektuje.

Odkazy ze záložek projektu (Záznamy/Jednání/Návrhy na detailu projektu)
`returnUrl` neposílají — kanonická záložka je totožná s originem.

## Bezpečnost

`returnUrl` výhradně přes `Url.IsLocalUrl` (open-redirect ochrana);
nevalidní → kanonický fallback. Žádné čtení Refereru.

## Testy

- **Unit:** `BreadcrumbTrail.ParentUrl` — BackUrl přednost, fallback [^2].
- **Api render:** detail jednání bez returnUrl → ← obsahuje `tab=jednani`;
  s `returnUrl=/Jednani` → ← je `/Jednani`; s externím URL → kanonický
  fallback; stránky návrhů → `tab=navrhy`; projekt-drobeček zůstává bez tab.
- **E2E:** projekt ▸ Jednání ▸ detail ▸ ← → aktivní záložka Jednání;
  `/Jednani` ▸ detail ▸ ← → `/Jednani`.

## Mimo scope

Historie prohlížeče (browser back funguje, neměníme). Vícekrokové
origin řetězce (returnUrl je jednoúrovňový — stačí).
