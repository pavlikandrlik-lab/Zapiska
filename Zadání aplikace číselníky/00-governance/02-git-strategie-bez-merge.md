# Git strategie — bez mergování

> **Stav:** rozhodnuto (G4): jediná větev, žádné mergování, **vývojář commituje sám**.

## Požadavek zadavatele

Vývojář **nesmí být nucen dělat merge větví** — ani v commitech, ani v pushích,
ani v kódu. Cílem je vyhnout se konfliktům a jejich řešení naslepo.

## Navrhované řešení (k potvrzení)

Trunk-based vývoj:
- Jediná dlouhodobá větev `main`.
- Každý blok = jeden nebo více commitů přímo do `main`.
- Žádné feature větve, žádné PR, žádný `git merge`, žádný `git rebase`.
- Commit dělá **zadavatel** po ruční verifikaci, ne vývojář (převzato ze Zápisky —
  „Commity jsou DRŽENÉ").

## Zdroje k harvestu ze Zápisky

- `docs/superpowers/plans/*.md` sekce „Global Constraints" — pravidlo držených commitů.
- Konvence commit messages: `feat(oblast):`, `fix(oblast):`, `refactor(oblast):`,
  `chore(oblast):` — viz `git log --oneline`.
