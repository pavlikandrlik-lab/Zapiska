---
title: Komentáře / vyjádření na záznamu
description: Diskuze pod záznamem — autor, datum, vazba na jednání.
---

# Komentáře / vyjádření

Záznam v PM Trackeru má **panel komentářů** kde tým diskutuje pokrok, nahlášené
problémy, dotazy. Komentáře jsou **interní** — žijí jen v PM Trackeru, nikdy
se neposílají do ServiceDesku.

## Komentář versus vyjádření

Důležité rozlišení:

| Komentář | Vyjádření |
|---|---|
| **Interní** v PM Trackeru | **Externí** ze ServiceDesku |
| Tabulka `zaznam_komentare` | Tabulka `HOT_VYJADRENI` (intranetNEW) |
| Editovatelné | Read-only |
| Bez vazby na SD | Drag-drop na harmonogram |
| Panel pod záznamem | [Chat modal](../../integrace/servicedesk/chat-vyjadreni.md) |

V této wiki sekci se bavíme o **komentářích**.

## Layout panelu

Pod editor záznamu (na detail stránce / pod modal formulářem) je panel
komentářů:

```
┌─────────────────────────────────────────┐
│ Komentáře (3)            [+ Komentář]   │
├─────────────────────────────────────────┤
│ Jan Novák · 25.4.2026 10:15             │
│ Vázáno na jednání 8201/2026-04-25       │
│                                         │
│ Dodavatel slíbil dodávku do konce dubna.│
│ [Upravit] [Smazat]                      │
├─────────────────────────────────────────┤
│ Marie Svobodová · 24.4.2026 16:30       │
│                                         │
│ Můžeme prosím ověřit subsystém?         │
│ [Upravit nelze (cizí)] [Odpovědět]      │
├─────────────────────────────────────────┤
│ ... další komentáře ...                 │
└─────────────────────────────────────────┘
```

## Co eviduje komentář

| Pole | Popis |
|---|---|
| **Autor** | Osoba PM Trackeru (z `dbo.osoby`) |
| **Datum a čas** | Vytvoření |
| **Text** | Rich-text (paragraph, bullet, bold, italic) |
| **Vazba na jednání** | Volitelně, pokud byl zapsán během porady |
| **Vázáno na záznam** | Implicitně z kontextu |

## Vytvoření komentáře

Tlačítko **+ Komentář** otevře editor:

1. Napiš text (rich-text)
2. Volitelně **přiřaď k jednání** (dropdown otevřených jednání projektu)
3. Klikni *Uložit*

Po uložení komentář se objeví v panelu (default chronologicky nejnovější
nahoře nebo nejstarší — záleží na default sort, lze přepnout).

## Vazba na jednání

Když je projekt **na otevřeném jednání**, kontext se uchovává — všechny
komentáře vytvořené během jednání mají automaticky vazbu na to jednání.

Vazba má účel:

- V **exportu zápisu jednání** se komentáře vystoupí pod správnými body
- V detailu jednání lze vidět "co se diskutovalo k záznamu X během jednání"

## Editace cizích komentářů

Komentář může editovat:

- **Vlastní autor** — pokud má permission `comments.edit`
- **Admin** — typicky `comments.admin.edit`

Cizí (ne vlastní) komentář **nelze běžně editovat**. Důvod: editovaný komentář
má v audit logu zachycenou změnu, ale není to "co kolega řekl" → mělo by být
to read-only.

## Odpovědi (reply)

V aktuální verzi UI se podpora **threadingu / nested replies** liší podle
verze. Default = ploché chronologické komentáře, ne zanořené.

Pokud chceš odpovědět na konkrétní komentář, **citaci si ručně přepiš** do
nového komentáře:

```
> Marie Svobodová: Můžeme prosím ověřit subsystém?

Ano, jedná se o R_DAN, právě jsem ověřil.
```

## Smazání

Tlačítko *Smazat* u vlastního komentáře (s confirm dialog). Soft-delete —
v audit logu zůstává, panel se vyfiltruje.

## Filtrace a řazení

- **Řazení**: nejnovější / nejstarší (toggle)
- **Filtrace**: podle autora, podle vazby na jednání

## Permissions

| Akce | Klíč |
|---|---|
| Vidět komentáře | `comments.view` (typicky implicitní s `records.view`) |
| Vytvořit komentář | `comments.create` |
| Upravit vlastní | `comments.edit` |
| Upravit cizí | `comments.admin.edit` |
| Smazat vlastní | `comments.delete` |
| Smazat cizí | `comments.admin.delete` |

## Vztah ke chat modalu

Pokud má záznam **externí vazbu na SD ticket**, na záznamu jsou **dvě
diskuse**:

1. **Komentáře** (interní) — tady, panel pod záznamem
2. **Vyjádření** (externí ze SD) — v [chat modalu](../../integrace/servicedesk/chat-vyjadreni.md)

Tato dvě jsou **separátní** a nemíchají se. Komentář v PM Trackeru se
neuloží do SD a obráceně.

## Související

- [Externí vazba na SD](externi-vazba-sd.md)
- [Chat vyjádření z SD](../../integrace/servicedesk/chat-vyjadreni.md)
- [Jednání](../jednani/)
