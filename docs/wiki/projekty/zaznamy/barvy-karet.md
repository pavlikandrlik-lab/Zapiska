---
title: Barvy karet záznamů
description: Význam barevného proužku vlevo u karty záznamu — kategorie a stav úkolu vůči termínu.
---

# Barvy karet záznamů

Každá karta záznamu má vlevo **barevný svislý proužek**. Nese dvojí význam:
**kategorii** záznamu a u úkolů navíc **stav vůči termínu**.

> Pozn.: barva se řídí **kategorií** záznamu (číselník kategorií: Úkol / Informace /
> Rozhodnutí), ne typem (NES/PMP/PNF). Viz [Typy záznamů](typy-zaznamu.md).

## Podle kategorie

| Kategorie | Kód | Barva |
|---|---|---|
| Úkol | `U` | 🟡 jantarová |
| Informace | `I` | 🔵 modrá |
| Rozhodnutí | `D` | 🟣 fialová |
| ostatní / vlastní | — | ⚪ neutrální šedá |

## Úkoly — stavová eskalace (semafor)

U **úkolů** proužek navíc reaguje na stav a termín úkolu:

| Stav úkolu | Barva | Význam |
|---|---|---|
| Běží, v termínu | 🟡 jantarová | aktivní, vyžaduje pozornost |
| **Po termínu** | 🔴 **červená** | urgentní — prošvihnutý termín úkolu |
| Hotový (finální stav) | 🟢 zelená | dokončeno |

Pravidla:

- **„Po termínu"** = *termín úkolu* (datum ukončení) je **před dnešním dnem**
  (`termín < dnes`; v den termínu je proužek ještě jantarový). Bere se termín
  **úkolu**, ne dílčí termíny harmonogramu.
- Červená platí jen pro **aktivní** úkoly. **Hotový úkol je vždy zelený**, i když
  byl dokončen po termínu (dokončeno = vyřešeno).
- Eskalace se týká jen kategorie **Úkol**. Informace a Rozhodnutí termín nemají,
  drží svou barvu (modrá / fialová). Zelená/červená jsou rezervované pro stav úkolu.

## Tmavý režim

Barvy používají design tokeny gov-design-system (`--gov-color-*`), takže se
automaticky přizpůsobí světlému i tmavému režimu (zůstávají čitelné na obou pozadích).
