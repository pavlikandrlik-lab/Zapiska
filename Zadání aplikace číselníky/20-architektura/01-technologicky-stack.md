# Technologický stack

Rozhodnuto v kole 1 — zdůvodnění v
[90-rozhodnuti/rozhodnuti-log.md](../90-rozhodnuti/rozhodnuti-log.md).

| Vrstva | Volba |
|---|---|
| Runtime | **.NET 10 (LTS)**, ASP.NET Core |
| Hosting | **IIS na Windows**, in-process |
| Databáze | **Microsoft SQL Server** — instance se určuje připojovacím řetězcem v nastavení |
| Přístup k datům | **EF Core (poskytovatel SQL Server)** |
| Migrace schématu | **Ručně psané očíslované SQL skripty** + kontrolní skript stavu instance |
| Autentizace | **Windows Authentication**, identity z Active Directory |
| Frontend | **React SPA**, Vite + TypeScript, React Router, dotazovací knihovna se serverovou mezipamětí |
| Nasazovací jednotka | **Jeden artefakt** — backend servíruje hotový build SPA i rozhraní |
| Vzhled | **gov design system** (web komponenty), vlastní wrappery `pm-*` jako React komponenty |
| Vyhledávání | **OpenSearch** |
| Dokumenty | Export do PDF a tisk |
| Testy | xUnit — jednotkové, integrační, HTTP, koncové; **Playwright v .NET**; **Vitest + React Testing Library** pro komponenty |

## Proč .NET 10 a ne .NET 8 jako Zápiska

.NET 10 je aktuální vydání s dlouhodobou podporou (do listopadu 2028). Zápiska běží
na .NET 8, jehož podpora skončí dřív. Pro nově zakládanou aplikaci není důvod začínat
na starším vydání.

Rozdíly mezi 8 a 10 nejsou pro tento typ aplikace rušivé — kód přebíraný ze Zápisky
se portuje bez přepisování. Balíček pro hostování na IIS existuje pro obě vydání.

## Proč jeden nasazovací artefakt

Backend servíruje jak rozhraní, tak hotový build prohlížečové aplikace ze statických
souborů. Důsledky:

- jedno publikování, jedno místo nasazení, jeden certifikát, jedno nastavení,
- **Windows Authentication funguje bez obcházení** — prohlížečová aplikace a rozhraní
  jsou na téže adrese, takže odpadá sdílení přihlášení mezi doménami,
- offline pravidlo se hlídá na jednom místě.

## Hranice vůči IIS

Aplikace nesmí obsahovat kód závislý na IIS mimo samotné navázání autentizace.
Pozdější přesun do kontejneru pak stojí konfiguraci, ne přepis.

## Reference ze Zápisky

`README.md` (tabulka stacku), `docs/technical/02-architecture.md`,
`docs/specs/offline-deployment.md`.
