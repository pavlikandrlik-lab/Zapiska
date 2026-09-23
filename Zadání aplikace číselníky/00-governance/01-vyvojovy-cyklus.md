# Vývojový cyklus: specifikace → plán → implementace

Trojfázový cyklus, kterým prochází **každá** funkční změna. Žádná fáze se nepřeskakuje,
ani u „malých" změn — právě u nich se přeskakuje nejsnáz a stojí to nejvíc.

---

## Tři fáze

### 1. Návrh specifikace — *co se má stát a proč*

Věcné chování, ne kód. Odpovídá na otázky: k čemu to je, kdo to bude používat,
co se stane, když se to pokazí, podle čeho poznáme, že je to hotové.

**Výstup:** `docs/specs/RRRR-MM-DD-<téma>-design.md`

**Fáze končí, když** zadavatel specifikaci přečetl a schválil. Ne dřív.

### 2. Plán implementace — *jak se to udělá*

Rozpad na **funkční bloky, ne na vrstvy**. Každý blok je svislý řez celou aplikací
(databáze → služba → rozhraní → prohlížeč → testy) a končí stavem, který jde spustit
a ručně vyzkoušet.

**Výstup:** `docs/plans/RRRR-MM-DD-<téma>.md`

**Fáze končí, když** je plán bez zástupných textů — každý krok obsahuje skutečný kód
nebo skutečný příkaz, ne popis toho, co by se mělo udělat.

### 3. Implementace — *blok po bloku*

Inline v hlavní session. Po každém bloku plná sada testů a nahlášený výsledek.

---

## Proč zrovna takhle

| Kdyby chyběla fáze | Co se stane |
|---|---|
| **specifikace** | Programuje se něco, o čem si zadavatel myslel něco jiného. Zjistí se to na konci. |
| **plán** | Vzniká po kouskách bez pořadí; polovina práce se dělá dvakrát, protože pozdější krok vyvrátí dřívější. |
| **rozpad na bloky** | Vývojář jede vícedenní nepřetržitou session, drží celou aplikaci v hlavě a po zkrácení kontextu ztrácí rozdělané věci. |

---

## Formát plánu

Vzor: [../40-plan-implementace/P1-zaklad-a-identita.md](../40-plan-implementace/P1-zaklad-a-identita.md).

Povinné části:

```
Cíl              — jednou větou, co bude po plánu fungovat
Architektura     — 2–3 věty o přístupu
Stack            — použité technologie
Specifikace      — odkaz na dokument, ze kterého plán argumentuje
Global Constraints — pravidla platná pro všechny bloky, neopakují se v nich
Přehled bloků    — tabulka blok → co po něm funguje
Bloky            — kroky se zaškrtávací syntaxí
Ruční ověření    — co má zadavatel po dokončení vidět
Dluhy            — co se odložilo a který plán to uzavře
```

## Formát bloku

```
Cíl bloku    — jednou větou
Soubory      — vytvoř / uprav / test, s cestami
Rozhraní     — co blok poskytuje dalším a co používá z předchozích
Kroky        — 2–5 minut každý, se skutečným kódem
```

Pořadí kroků uvnitř bloku je vždy stejné:

```
1. napiš padající test
2. spusť ho a ověř, že padá
3. doplň nejmenší implementaci, která ho rozsvítí
4. spusť a ověř, že prochází
5. plná sada testů
6. commit
```

> **Krok 2 není formalita.** Test, který projde i na nerozbité verzi, nic nedrží —
> a to se pozná jedině tím, že ho člověk uvidí spadnout.

---

## Tabulka přenášených dluhů

Každý plán má na konci tabulku dluhů: **co se odložilo, kdo to vzniklo, který plán to uzavře.**

Zapisuje se **průběžně při exekuci**, ne na konci. Je to jediná obrana proti tomu, aby se
rozdělané věci ztratily při zkrácení kontextu — a zároveň doklad, že odložení bylo
rozhodnutí, ne opomenutí.

Stav dluhů celé etapy: [../40-plan-implementace/README.md](../40-plan-implementace/README.md).
