/*
  Smyšlené tickety v lokální replice intranetNEW pro výpočet prodlení (spec 2026-10-15 §6).
  Protějšek skriptu scripts/dev/servicedesk_external_schema.sql — ten zakládá předávky (ticket_external*)
  ke stejným číslům ticketů 9001xx.

  JEN PRO VÝVOJ. Na serverech se nespouští.

  Spuštění (lokální Colima SQL):
    /opt/homebrew/bin/sqlcmd -S localhost,1433 -U sa -P 'PmTracker!2026' -Ndisable -b \
      -d intranetNEW_test -i scripts/dev/intranetnew_prodleni_seed.sql

  Idempotentní: moduly „PRODL-*" a tickety 9001xx při každém spuštění smaže a založí znovu.
  Tickety patří informačnímu systému 1 (FIS). Aby je dashboard projektu ukázal, musí mít projekt
  v PM Trackeru nastavený IS = FIS (úprava projektu v přehledu projektů).
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DELETE FROM dbo.HOT_ZAZNAMY WHERE id BETWEEN 900100 AND 900199;
DELETE FROM dbo.HOT_MODULY WHERE zkratka LIKE N'PRODL-%';

INSERT INTO dbo.HOT_MODULY (subsystem, modul, zkratka, aktivita, dodavatel, id_IS) VALUES
    (N'FIS', N'Prodlení – účetnictví (smyšlený)', N'PRODL-UCT', N'ano', N'Dodavatel A s.r.o.', 1),
    (N'FIS', N'Prodlení – mzdy (smyšlený)',       N'PRODL-MZD', N'ano', N'Dodavatel B a.s.',   1);

DECLARE @dnes smalldatetime = CAST(CAST(GETDATE() AS date) AS smalldatetime);

-- typ_zaznamu je v ostrých datech doplněný mezerami (CHAR) → část řádků ho tak má i tady.
INSERT INTO dbo.HOT_ZAZNAMY
    (subsystem, typ_zaznamu, id, rok, pid, modul, datum, strucne, stav, schvaleno, priznak_zamceni, dodavatel, dulezitost)
VALUES
    (N'FIS', N'NES',   900101, YEAR(@dnes), N'FIS-900101', N'PRODL-UCT', DATEADD(DAY, -7,  @dnes), N'Chyba výpočtu zůstatků po uzávěrce',        N'Řešení', 1, 0, N'Dodavatel A s.r.o.', N'Vada B'),
    (N'FIS', N'NES',   900102, YEAR(@dnes), N'FIS-900102', N'PRODL-UCT', DATEADD(DAY, -1,  @dnes), N'Nejde spustit tisk sestavy závazků',        N'Řešení', 1, 0, N'Dodavatel A s.r.o.', N'Vada A'),
    (N'FIS', N'PMP  ', 900103, YEAR(@dnes), N'FIS-900103', N'PRODL-MZD', DATEADD(DAY, -22, @dnes), N'Úprava výpočtu příplatků',                  N'Řešení', 1, 0, N'Dodavatel B a.s.',   NULL),
    (N'FIS', N'PNF',   900104, YEAR(@dnes), N'FIS-900104', N'PRODL-MZD', DATEADD(DAY, -16, @dnes), N'Nový číselník středisek',                   N'Řešení', 1, 0, N'Dodavatel B a.s.',   NULL),
    (N'FIS', N'PMP',   900105, YEAR(@dnes), N'FIS-900105', N'PRODL-UCT', DATEADD(DAY, -5,  @dnes), N'Doplnění filtru v přehledu faktur',         N'Řešení', 1, 0, N'Dodavatel A s.r.o.', NULL),
    (N'FIS', N'NES',   900106, YEAR(@dnes), N'FIS-900106', N'PRODL-UCT', DATEADD(DAY, -4,  @dnes), N'Chybná částka v exportu (důležitost bez SLA)', N'Řešení', 1, 0, N'Dodavatel A s.r.o.', N'NES IV'),
    (N'FIS', N'PMP',   900107, YEAR(@dnes), N'FIS-900107', N'PRODL-MZD', DATEADD(DAY, -31, @dnes), N'Převod agendy mezi dodavateli',             N'Řešení', 1, 0, N'Dodavatel B a.s.',   NULL),
    (N'FIS', N'PNF  ', 900108, YEAR(@dnes), N'FIS-900108', N'PRODL-MZD', DATEADD(DAY, -13, @dnes), N'Rozšíření výkazu o nový sloupec',           N'Řešení', 1, 0, N'Dodavatel C',        NULL),
    (N'FIS', N'PMP',   900109, YEAR(@dnes), N'FIS-900109', N'PRODL-UCT', DATEADD(DAY, -26, @dnes), N'Úprava rozhraní na registr smluv (prodloužený termín)', N'Řešení', 1, 0, N'Dodavatel A s.r.o.', NULL);

COMMIT TRANSACTION;

SELECT id, typ_zaznamu, modul, dulezitost, stav FROM dbo.HOT_ZAZNAMY WHERE id BETWEEN 900100 AND 900199 ORDER BY id;
