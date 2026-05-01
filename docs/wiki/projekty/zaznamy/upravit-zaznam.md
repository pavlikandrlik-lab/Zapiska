---
title: Upravit záznam
description: Editor záznamu — taby, validace, ukládání.
---

# Upravit záznam

## Předpoklady

- Permission `records.edit` na projektu, kde záznam je
- Záznam **není uzamčený** (`is_locked=0`)

Pokud je záznam uzamčený, editor se otevře **read-only** — vidíš všechno,
ale nemůžeš měnit. Editaci povolí jen admin který lock uvolní.

## Cesta k editoru

### Z detailu projektu

Tab *Záznamy* → klik na řádek nebo tlačítko *Upravit*.

### Z dashboardu *Moje priority*

Klik na záznam v "Moje priority" → otevře se editor.

### Z notifikace nebo přímého URL

Pokud někdo poslal odkaz `https://pmtracker/Zaznamy/Edit/123`, otevře se
editor přímo.

## Modal versus full-page

| Modal | Full-page |
|---|---|
| Rychlejší pro krátké úpravy | Lepší pro dlouhé záznamy |
| Hlavní stránka v pozadí | Vlastní URL, browser history |
| Esc zavře | Esc nic nedělá |
| Návrat = zavření modalu | Návrat = browser back |

Volba [v profilu](../../profil/nastaveni-uzivatele.md).

## Editor — taby

### Tab 1: Základní

Stejná pole jako [Nový záznam](novy-zaznam.md).

Při editaci navíc:

- **Datum vytvoření** (read-only)
- **Vytvořil** (read-only, login + displayName)
- **Datum poslední úpravy** + autor

### Tab 2: Harmonogram

Pro každý krok HS01 / HS02 / …:

- **Plán** (kolik dní trvá krok)
- **Skutečnost** (delay v dnech)
- **Badge zdroje** (🤖 / ✍️ / 📜 / —)
- **Toggle Auto/Ručně** (pokud máš permission)

Detail: [Harmonogram](../harmonogram/).

### Tab 3: Externí vazby

- **Primární vazba** — 6-cifer ticket id (typicky odpovídá typu záznamu)
- **Další referenční vazby** (volitelně, pro křížové odkazy)
- **Otevřít chat** — tlačítko otevře [chat modal](../../integrace/servicedesk/chat-vyjadreni.md)

Pokud změníš primární vazbu → reaktivní harvest pro nový ticket.

### Tab 4: Spolupracující osoby

AD picker pro **collaborators**:

- Přidat
- Odebrat
- Vidět kdo je collaborator

## Komentáře — separátní sekce

Komentáře k záznamu **nejsou součástí editoru**. Mají vlastní panel pod záznamem
(typicky pod editor formulářem). Detail: [Komentáře](komentare.md).

## Dirty-check

Editor sleduje neuložené změny:

- **Modal** → upozornění při zavření (X / Esc / klik mimo)
- **Full-page** → upozornění při browser back / navigation

Pokud klikneš *Pokračovat beze změn*, změny se zahodí. *Zůstat* tě nechá
v editoru.

## Validace

- **Klient-side** při focus-loss pole (povinné, formát)
- **Server-side** při submit (autoritativní)
- Chyby se zobrazí **inline u polí**, nikoli v jedné horní hlášce
- Pokud je víc chyb, aplikace **scrolluje na první** a fokusuje pole

## AJAX submit

Uložení neprovádí reload stránky:

- Klikneš *Uložit*
- Aplikace pošle data na server
- **Úspěch** → toast "Uloženo", zavře modal / refresh full-page
- **Chyba validace** → chyby u polí
- **Chyba serveru** → toast s Trace-Id

## Co po uložení

- Záznam se aktualizuje v `dbo.projektove_zaznamy`
- Audit log: `RECORD_UPDATED` s diff (které pole se změnilo)
- Pokud změnila externí vazba → reaktivní harvest
- Pokud změnil termín → recompute priority pro dashboard *Moje priority*

## Soft-delete z editoru

Některé verze UI mají v editoru tlačítko **Smazat záznam**. Akce odpovídá
[Smazat záznam](smazat-zaznam.md). Vyžaduje confirm dialog + permission.

## Pro koho

- **Member** — typicky edituje vlastní záznamy
- **Leader / Admin** — edituje cizí záznamy
- **HOST / READ_ALL** — vidí read-only

## Související

- [Nový záznam](novy-zaznam.md)
- [Komentáře](komentare.md)
- [Externí vazba na SD](externi-vazba-sd.md)
- [Harmonogram](../harmonogram/)
