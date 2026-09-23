# Verzování aplikace a changelog

## Model převzatý ze Zápisky

- Verze aplikace v jednom místě, zobrazená v UI.
- Podklady pro release ve `docs/changelog/releases/<verze>.md`
  se sekcemi **Přidáno / Změněno / Opraveno**.
- Kořenový `CHANGELOG.md` se **generuje skriptem** z podkladů —
  needituje se ručně (`scripts/generate-changelog.sh`).
- Povýšení verze je vždy samostatný závěrečný blok implementačního plánu.

## Zdroje k harvestu ze Zápisky

- `CHANGELOG.md`, `docs/changelog/releases/0.8.md`
- `scripts/generate-changelog.sh`
