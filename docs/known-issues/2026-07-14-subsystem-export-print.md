# Subsystémové role: doplněn export/tisk (2026-07-14)

Redesign 2026-04-23 (`authz-target-matrix.xlsx`) vynechal export z rolí
`VEDOUCI_SUBSYSTEMU` / `ZASTUPCE_VEDOUCIHO_SUBSYSTEMU` / `METODIK_SUBSYSTEMU`.

Rozhodnutí vlastníka (2026-07-14): kdo smí projekt číst, smí tisknout. Doplněno
6 export klíčů každé roli: `export.pdf.{projekt,jednani,ukol}` +
`export.word.{projekt,jednani,ukol}`. Přes MEDIUM-1 propagaci (subsystémový grant
→ `PerProjectPermissions`) tak funguje tisk projektu i jednání.

Zdroj pravdy v kódu: `PermissionSeedConfiguration.RoleMappings` +
`SubsystemRolePermissionMatrixTests`. Seeder je idempotentní upsert → doseeduje se
i do existující produkční DB při příštím startu (offline deployment, žádná EF migrace).

Binární `authz-target-matrix.xlsx` je nutné ručně srovnat při příští revizi matice —
za běhu ho žádný test neparsuje, takže není zdrojem vynucené pravdy.

## Souvislost

Součást opravy „tisk jednání nefunguje projektovým rolím" (viz
`docs/superpowers/plans/2026-07-14-tisk-jednani-projekt-authz.md`). Hlavní bug:
route `Jednani/{jednaniId}/Tisk` nenesla `projektId`, takže policy handler dělal
globální kontrolu a per-projektové role (ADM_PROJ ap.) dostaly 403.
