---
title: Uzavřené jednání
description: Read-only chování po přepnutí stavu na CLOSED.
---

# Uzavřené jednání

Jednání ve stavu `CLOSED` je **finální**. Aplikace ho považuje za **uzavřený
zápis**, který by se neměl měnit. Read-only chování chrání integritu
historických dat.

## Kdy uzavřít

Typicky:

- **Po skončení porady** — leader nahrá zápis a označí jako uzavřené
- **Po doschválení** zápisu všemi účastníky
- **Při archivaci** projektu

## Jak uzavřít

V detailu jednání → tlačítko **Uzavřít jednání**. Vyžaduje:

- Permission `meetings.close` (typicky leader nebo svolavatel)
- Confirm dialog ("Opravdu uzavřít? Editace pak nebude možná bez admin
  zásahu.")

Po uzavření:

- Stav se změní z `OTEVRENO` na `CLOSED`
- Audit log: `MEETING_CLOSED`
- UI se přepne do read-only módu

## Co lze v uzavřeném jednání

- ✓ **Číst** všechno (záznam, účastníky, vyjádření)
- ✓ **Tisknout zápis** (s vodoznakem "UZAVŘENO")
- ✓ **Vidět audit log**

## Co **NEjde**

- ✗ Editovat datum / čas / místo
- ✗ Přidat / odebrat účastníky
- ✗ Změnit stav účasti
- ✗ Editovat / přidat / smazat **záznamy z jednání**
- ✗ Přidat **vyjádření** (pokud existuje samostatná diskuse)

Aplikace **tlačítka skrývá nebo zobrazuje jako disabled**.

## Editace záznamů vázaných na jednání

Důležité: **uzavření jednání se týká jeho metadat a strukturálních dat**
(účastníci, body programu). **Záznamy projektu** vázané na jednání lze i
nadále editovat — pokud má user `records.edit` na záznamu.

Příklad:

- Jednání 8201 je uzavřené
- Záznam #123 byl vytvořený během jednání 8201 (vazba)
- User Jan Novák má `records.edit` → může záznam #123 normálně editovat
- Vazba na jednání 8201 zůstává, jen je informativní

## Otevření zpět (reopen)

Permission `meetings.reopen` — typicky **jen leader projektu nebo admin**.

V detailu uzavřeného jednání → tlačítko **Otevřít zpět**:

1. Confirm dialog s důvodem (volný text — proč otevíráš)
2. Po potvrzení stav se změní na `OTEVRENO`
3. Audit log: `MEETING_REOPENED` s důvodem
4. UI se přepne na editovatelný režim

⚠️ **Reopen je výjimečná akce.** Default business pravidlo: uzavřené jednání
zůstává historicky stabilní. Pokud potřebuješ jen opravu drobnosti
(překlep), zvaž jestli to stojí za reopen + close cyklus + audit.

## Tisk uzavřeného jednání

[Tisk jednání](../../export/tisk-jednani.md) má pro uzavřená jednání:

- **Vodoznak / razítko "UZAVŘENO"** v rohu nebo přes obsah
- **Datum uzavření** v patičce
- **Kdo uzavřel** v patičce

To odlišuje finální zápis od work-in-progress verze.

## Audit a compliance

Uzavřené jednání:

- Zůstává v audit logu se všemi předchozími změnami
- Historie kdy a kdo uzavřel je viditelná
- Eventuální reopen + nový close vytvoří audit chain

Pro audit / compliance je důležité, že **uzavřené dokumenty mají stabilní
verzi** — z toho důvodu je reopen výjimečná akce.

## Permissions

| Akce | Klíč |
|---|---|
| Vidět uzavřené jednání | `meetings.view` (implicitní) |
| Tisknout zápis | `meetings.view` (implicitní) |
| Uzavřít jednání | `meetings.close` |
| Otevřít zpět | `meetings.reopen` |

## Související

- [Účastníci](ucastnici.md)
- [Tisk jednání](../../export/tisk-jednani.md)
- [Pomoc → FAQ](../../pomoc/faq.md)
