# Dokumentační standard

## Co sem patří

Jaké dokumenty aplikace vede, kde leží a kdo je aktualizuje. Zápiska má tuto strukturu
odladěnou provozem — přebírá se beze změny.

## Struktura převzatá ze Zápisky

```
docs/
├── README.md              # rozcestník
├── technical/             # technická dokumentace dle ISO 26514 + SOP
│   ├── 00-documentation-tree.md
│   ├── 01-system-context.md
│   ├── 02-architecture.md
│   ├── 03-runtime-configuration.md
│   ├── 04-installation-deployment.md
│   ├── 06-database-bootstrap-migrations.md
│   ├── 07-security-authz.md
│   ├── 08-operations-runbooks.md
│   ├── 09-testing-quality.md
│   └── 10-troubleshooting-recovery.md
├── specs/                 # funkční specifikace modulů
├── plans/                 # implementační plány
├── known-issues/          # evidované známé problémy
├── architecture/          # stavební pravidla UI komponent a vrstev
├── changelog/releases/    # podklady pro CHANGELOG
└── wiki/                  # uživatelská wiki knihovna publikovaná v aplikaci
```

Každý dokument ve `technical/` má povinné sekce: Účel · Publikum a role · Závislosti
a předpoklady · Vstupy a výstupy · Detailní postup · Verifikace · Rollback ·
Troubleshooting · Audit a traceability.

## Wiki knihovna

Markdown soubory ve `docs/wiki/`, renderované přímo v aplikaci pod routou `/Dokumentace/*`.
Zápiska používá `MarkdownDocumentationService` (Markdig). Harvest:
`PmTracker.Web/Services/Documentation/`.

## Known issues

Jeden soubor na problém, název `YYYY-MM-DD-<kratky-popis>.md`.
Vzor: `docs/known-issues/2026-09-06-fk-zhvv-externi-odkaz-delete-order.md`.
