# Zadání aplikace Číselníky

Kompletní zadání pro novou aplikaci **Číselníky**. Cílem je, aby vývojář (chatbot Opus 5
ve vlastním projektu) **nemusel vymýšlet nic věcného** — pouze technikálie, jak popsané chování
reálně implementovat.

Aplikace Číselníky nemá se Zápiskou (PM Tracker) žádnou funkční vazbu. Přebírá z ní
**governance, architekturu, provozní pravidla a hotové technické moduly** — ne doménu.

---

## Stav rozpracovanosti

| Část | Stav |
|---|---|
| 00-governance — pravidla vývoje | 🟢 **hotová** — 8 dokumentů |
| 10-specifikace — co aplikace dělá | 🟢 **hotová** — 15 dokumentů |
| 20-architektura — jak je postavená | 🟢 **hotová** — 8 dokumentů |
| 30-prevzate-moduly — co se bere ze Zápisky | 🟢 **hotová** — 8 dokumentů |
| 40-plan-implementace — bloky | 🟢 **hotový** — 9 plánů, 68 bloků |
| 90-rozhodnuti — otázky a rozhodnutí | 🟢 sedm kol; **žádná otevřená otázka** |

Legenda: 🔴 nezačato / blokováno · 🟡 rozpracováno · 🟢 hotovo

---

## Struktura zadání

### [00-governance/](00-governance/) — JAK se aplikace vyvíjí
Procesní pravidla závazná pro každý implementační blok. Sem patří cyklus
*specifikace → plán implementace → implementace → testy*, git strategie bez mergování,
testovací strategie, dokumentační standard a Definition of Done.

### [10-specifikace/](10-specifikace/) — CO aplikace dělá
Věcné zadání. Doména, entity, obrazovky, pravidla, role, workflow.
**Zdroj: diktát zadavatele.** Tady se nic nedomýšlí.

### [20-architektura/](20-architektura/) — JAK je postavená
Technologický stack, vrstvení backendu, React frontend, datový model nad SQL Server,
API kontrakt. Obsahuje konkrétní návrhy implementace, ne jen principy.

### [30-prevzate-moduly/](30-prevzate-moduly/) — HOTOVÝ KÓD ze Zápisky
Moduly, které jsou v Zápisce ověřené provozem a přebírají se **jako kompletní kód**,
aby se nevymýšlely a neprogramovaly znovu: přihlašování a AD, RBAC, auditní log,
dokumentační/wiki engine, gov design system, testovací infrastruktura.

### [40-plan-implementace/](40-plan-implementace/) — BLOKY
Rozpad na samostatně spustitelné implementační bloky. Každý blok = svislý řez celou
aplikací (DB → služba → API → React → testy), po kterém je aplikace funkční a ručně
vyzkoušitelná. Blok se vejde do jedné pracovní session.

### [90-rozhodnuti/](90-rozhodnuti/) — OTÁZKY A ROZHODNUTÍ
[otevrene-otazky.md](90-rozhodnuti/otevrene-otazky.md) — co ještě blokuje psaní zadání.
[rozhodnuti-log.md](90-rozhodnuti/rozhodnuti-log.md) — zodpovězené otázky a jejich důsledky.

---

## Jak toto zadání používat

1. Zadavatel nadiktuje věcnou část → zapíše se do `10-specifikace/`.
2. Otevřené otázky se vyřeší v několika kolech → zapíší se do `90-rozhodnuti/rozhodnuti-log.md`.
3. Architektura a převzaté moduly se dopíší podle rozhodnutí.
4. Teprve pak vzniká `40-plan-implementace/` — rozpad na bloky.
5. Celá složka se zkopíruje do nového repozitáře aplikace Číselníky jako výchozí `docs/`.

## Zdrojová aplikace

Zápiska (PM Tracker), tento repozitář. Odkazy na konkrétní soubory Zápisky jsou v jednotlivých
dokumentech relativní vůči kořeni repozitáře Zápisky, aby šly při harvestování dohledat.
