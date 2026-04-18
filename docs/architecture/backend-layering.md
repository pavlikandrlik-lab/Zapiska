# Backend layering — thin controller

## Pravidla

1. Controller ≤ 200 řádků — jen routing, validace, mapping na view model
2. Service layer — business logika, DI lifetime: Scoped
3. Repository layer — EF Core queries; závislost jen na `DbContext`
4. Soubor ≤ 500 řádků — měkký limit, kontrola přes `scripts/check-file-sizes.sh`

## Controller anti-patterns

- EF Core query přímo v akci controlleru — NE
- Business pravidlo (if-else nad doménou) v akci — NE
- Přímé volání `DbContext.SaveChanges()` bez service — NE
- Mapping entit na view model v controlleru (AutoMapper / explicitní mapper místo toho)

## Správná delegace

```csharp
public async Task<IActionResult> Update(ProjektUpdateCommand command, CancellationToken ct)
{
    if (!ModelState.IsValid)
        return View(command);

    var result = await _projektService.UpdateAsync(command, CurrentUserContext, ct);
    if (result.IsFailure)
    {
        ModelState.AddModelError("", result.Error);
        return View(command);
    }
    return RedirectToAction("Detail", new { id = result.Value.Id });
}
```

## Existující god-files

Fáze 1 NErozbíjí existující 500+ řádkové soubory. Fáze 3 je systematicky rozdělí
podle zodpovědnosti. Do té doby:

- Nové funkce v existujících velkých souborech pouze když není alternativa
- Preferuj rozdělení při dotyku (boy-scout rule)
- Nový kód nesmí mít >500 řádků souboru
