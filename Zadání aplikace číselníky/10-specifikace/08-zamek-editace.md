# Výhradní zámek na editaci číselníku

Rozhodnutí V4. **Jeden číselník upravuje v jeden okamžik nejvýše jeden člověk.**

Souběžná živá spolupráce se zamítá vědomě: vyžadovala by průběžné ukládání a živý přenos
změn všem, a stejně by zůstal problém, kdy jeden pracuje a druhý mu práci odešle.
Zámek je jednodušší a poctivější.

---

## Pravidla

| Pravidlo | |
|---|---|
| Jednotka zamčení | **Jeden číselník** — táž jednotka jako editace a publikování (V5) |
| Platnost | **5 hodin od poslední skutečné aktivity** — pohybu myší nebo kliknutí |
| Vlastník | **Osoba**, ne okno prohlížeče ani přihlášení |
| Kdo nedostane zámek | Vidí číselník ke čtení a hlášku, **kdo ho drží a od kdy** |
| Kontrola | **Při otevření editace i při každém uložení** |
| Vynucení | **V databázi**, ne jen v aplikaci |

## Uložení

```
zamek_ciselniku(ciselnik_id PRIMARY KEY,   -- databáze fyzicky nedovolí dva zámky
                osoba_id,
                ziskan_kdy,
                posledni_aktivita_kdy,
                platnost_do)               -- posledni_aktivita_kdy + 5 h
```

Primární klíč na `ciselnik_id` je to, co pravidlo skutečně drží. Kdyby zámek hlídala jen
aplikace, dva souběžné požadavky by ho obešly.

### Získání zámku je atomické

SQL Server nemá podmíněné vložení jedním příkazem. Atomicita se drží **zámkem rozsahu
uvnitř transakce** — `HOLDLOCK` na neexistujícím řádku zabrání druhému požadavku vložit
řádek mezi kontrolou a vložením.

```sql
BEGIN TRANSACTION;

UPDATE zamek_ciselniku WITH (UPDLOCK, HOLDLOCK)
   SET osoba_id = @osoba, ziskan_kdy = SYSUTCDATETIME(),
       posledni_aktivita_kdy = SYSUTCDATETIME(),
       platnost_do = DATEADD(MINUTE, @minut, SYSUTCDATETIME())
 WHERE ciselnik_id = @ciselnik
   AND (osoba_id = @osoba                       -- můj vlastní zámek beru zpátky
        OR platnost_do < SYSUTCDATETIME());     -- cizí vypršelý přebírám

IF @@ROWCOUNT = 0
   AND NOT EXISTS (SELECT 1 FROM zamek_ciselniku WITH (UPDLOCK, HOLDLOCK)
                    WHERE ciselnik_id = @ciselnik)
BEGIN
    INSERT INTO zamek_ciselniku
        (ciselnik_id, osoba_id, ziskan_kdy, posledni_aktivita_kdy, platnost_do)
    VALUES (@ciselnik, @osoba, SYSUTCDATETIME(), SYSUTCDATETIME(),
            DATEADD(MINUTE, @minut, SYSUTCDATETIME()));
END

COMMIT TRANSACTION;
```

Nedotkne-li se ani jeden příkaz řádku, zámek drží někdo jiný a platí.
Bez `HOLDLOCK` by dva souběžné požadavky mohly oba projít kontrolou a oba vložit —
to je celý důvod té konstrukce.

---

## Proč reload stránky nikoho neblokuje

Toto byla výslovná obava a řeší ji jedna věta v návrhu:

> **Zámek patří osobě, ne oknu prohlížeče.**

Důsledky:

| Situace | Co se stane |
|---|---|
| Uživatel načte stránku znovu | Týž člověk → podmínka `osoba_id = :ja` platí → zámek dostane zpátky |
| Otevře si druhou záložku | Totéž. Zámek je jeho, obě záložky editují týž rozpracovaný stav. |
| Spadne mu prohlížeč a otevře ho znovu | Totéž |
| Vypne počítač a jde domů | Zámek vyprší 5 h po poslední skutečné aktivitě |

Kdyby byl zámek vázaný na okno nebo na přihlášení, každý z těchto případů by uživatele
zablokoval proti jeho vlastní práci. Přesně tomu se návrh vyhýbá.

## Prodlužování

Prohlížeč hlásí **skutečnou aktivitu** — pohyb myší, kliknutí, psaní. Ne tikot časovače.
Hlášení se posílá nejvýš jednou za minutu, aby se server nezahltil.

Rozdíl je podstatný: otevřená a zapomenutá záložka zámek **neprodlužuje**.
Vyprší 5 hodin po poslední chvíli, kdy u ní někdo skutečně byl.

## Uvolnění

| Kdy | Jak |
|---|---|
| Uživatel editaci zavře nebo zruší | Výslovné uvolnění |
| Uživatel publikuje verzi | Výslovné uvolnění |
| Uživatel zavře okno | Pokus o uvolnění při zavírání — **nespoléhá se na něj** |
| Nic z toho nenastane | Vypršení. Jediná spolehlivá pojistka. |
| Zámek visí a je potřeba dřív | **Správce ho může odebrat silou.** Zapíše se do auditního logu. |

## Rozpracované změny zámku nepatří

Zámek řídí **kdo smí psát**. Rozpracované změny patří **číselníku**.

Když zámek vyprší a přijde jiný editor, **pokračuje v rozpracovaných změnách svého
předchůdce**. Nic se nezahazuje. Publikuje se vždy celý rozpracovaný balík daného
číselníku (V5), bez ohledu na to, kdo které změny udělal — u každé je v záznamu změn
zapsáno kdo a kdy.

To je zároveň odpověď na otázku, proč rozpracovaný stav nepatří osobě: kdyby patřil,
odchod jednoho editora by práci uzamkl nadobro.

## Kontrola při uložení

Zámek se ověřuje **i při ukládání a publikování**, ne jen při otevření editace.

Bez toho by ten, komu zámek mezitím vypršel a číselník převzal někdo jiný, přesto odeslal
svá data a přepsal cizí práci. Server takový požadavek odmítne a uživateli sdělí,
že zámek už nemá — s tím, že jeho rozpracované změny jsou uložené a neztratily se.
