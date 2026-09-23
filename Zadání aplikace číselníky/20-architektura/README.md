# 20 — Architektura aplikace Číselníky

> **Stav: 🟢 hotová.**

Architektura Zápisky se přebírá s **jednou zásadní změnou: frontend v Reactu místo Razor šablon**
a **databází SQL Server místo SQL Serveru**. Vše ostatní (vrstvení backendu, seed-only RBAC,
offline-first pravidlo, gov design system) zůstává.

| Dokument | Obsah | Stav |
|---|---|---|
| [01-technologicky-stack.md](01-technologicky-stack.md) | Runtime, hosting, DB, UI, testy | 🟢 |
| [02-backend-vrstveni.md](02-backend-vrstveni.md) | Členění řešení, vrstvení, oblasti služeb, autorizace | 🟢 |
| [03-frontend-react.md](03-frontend-react.md) | Sestava, směrování, komponenty `pm-*`, sestavení a nasazení | 🟢 |
| [04-datovy-model.md](04-datovy-model.md) | Schéma, verzování, zámek, konvence | 🟢 varianta C |
| [07-konvence-mssql.md](07-konvence-mssql.md) | Dialekt SQL Serveru, převodní tabulka, tři sémantické rozdíly | 🟢 |
| [05-api-kontrakt.md](05-api-kontrakt.md) | Vnitřní kontrakt React ↔ backend | 🟢 |
| [06-propojena-data-skos.md](06-propojena-data-skos.md) | Co přebíráme ze SKOS, co ne, a co odmítáme | 🟢 |
