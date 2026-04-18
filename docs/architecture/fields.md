# pm-field

Kompletní formulářové pole: label + input + help/error message. Wrapper nad gov-form-control.

## Použití

```razor
<pm-field name="email" label="E-mail" input-type="email" required="true"
          help="Slouží k zasílání notifikací" />

<pm-field name="vek" label="Věk" input-type="number"
          error="@Model.Errors["vek"]" value="@Model.Vek" />
```

## Atributy

| Atribut | Typ | Default |
|---|---|---|
| `name` | string | — |
| `label` | string | — |
| `input-type` | text/email/number/date/... | text |
| `value` | string | — |
| `placeholder` | string | — |
| `help` | string | — (non-error pomoc) |
| `error` | string | — (pokud je, vykreslí error message + invalid) |
| `required` | bool | false |
| `disabled` | bool | false |
| `size` | Small/Medium/Large | Medium |

## Validace

Pokud Razor má `ModelState`, mapování chyb na `error` atribut:

```razor
<pm-field name="email" label="E-mail"
          error="@(ViewData.ModelState["email"]?.Errors.FirstOrDefault()?.ErrorMessage)" />
```

V budoucnu (fáze 2+) zvážíme model-binding variantu `<pm-field asp-for="Email" />`.

## XSS bezpečnost

Error, Help a Label jsou HTML-encoded přes `HtmlEncoder` s Unicode ranges — zachová diakritiku,
escapuje `<>&"'`. Hodnoty z ModelState nebo DB jsou tedy bezpečné.
