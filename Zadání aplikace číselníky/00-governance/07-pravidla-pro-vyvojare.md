# Pravidla pro vývojáře (chatbota)

## Závazná pravidla

1. **Programuje se inline v hlavní session.** Subagenti se na programování nepoužívají.
   Agenti smí být puštěni maximálně na detekci chyb, analýzu nebo review — nikdy na psaní kódu.
2. **Kvalita před zkratkami.** Nenabízet „rychlé" varianty. Rovnou architektonicky správné
   řešení jako výchozí.
3. **Žádný merge.** Viz [02-git-strategie-bez-merge.md](02-git-strategie-bez-merge.md).
4. **Oprav všechny sourozence.** Oprava se dělá u zdroje (sdílená komponenta), ne na jednom
   místě. Všechny výskyty se vyjmenují grepem.
5. **Před přepsáním ověř problém.** Hlášení může být zastaralé — zkontroluj historii proti
   datu nahlášení.
6. **Nic se necommituje bez pokynu zadavatele.**
7. **Aplikace se ověřuje i ručně v prohlížeči** přes Playwright, ne jen testy.

## Zdroje k harvestu ze Zápisky

Paměťová pravidla projektu (`memory/MEMORY.md`) — sekce o inline exekuci, kvalitě,
opravách sourozenců a ověřování před přepisem.
