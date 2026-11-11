/*
  Lokální replika tří tabulek databáze starého ServiceDesku (EF6 model ServiceDeskEntities),
  ze kterých PM Tracker počítá prodlení ticketů (spec 2026-10-15 §6.1, schéma podle ServiceDesk.edmx).

  JEN PRO VÝVOJ. Schéma je přesná kopie, data jsou smyšlená. Na serverech se nespouští —
  tam tabulky existují v databázi ServiceDesku a PM Tracker je jen čte.

  Spuštění (lokální Colima SQL):
    /opt/homebrew/bin/sqlcmd -S localhost,1433 -U sa -P 'PmTracker!2026' -Ndisable -b \
      -Q "IF DB_ID('ServiceDesk_test') IS NULL CREATE DATABASE ServiceDesk_test"
    /opt/homebrew/bin/sqlcmd -S localhost,1433 -U sa -P 'PmTracker!2026' -Ndisable -b \
      -d ServiceDesk_test -i scripts/dev/servicedesk_external_schema.sql

  Idempotentní: tabulky vytvoří jen když chybí, smyšlené tickety (external_id 9001xx) při každém
  spuštění smaže a založí znovu s daty relativně k dnešku, takže stavy „v prodlení" / „hrozí" platí vždy.
  Odpovídající tickety v intranetNEW_test zakládá scripts/dev/intranetnew_prodleni_seed.sql.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.ticket_external', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ticket_external (
        id                int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ticket_external PRIMARY KEY,
        external_id       varchar(50)  NOT NULL,
        external_sync_id  int          NULL,
        source            tinyint      NOT NULL,
        supplier_last_mod datetime     NULL,
        supplier_tag      nvarchar(50) NULL,
        sla_name          nvarchar(50) NULL,
        kp_name           nvarchar(50) NULL,
        supplier_id       nvarchar(10) NULL,
        product_name      nvarchar(25) NULL,
        version_name      nvarchar(25) NULL,
        category_name     nvarchar(25) NULL
    );
END;

IF OBJECT_ID(N'dbo.ticket_external_workflow', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ticket_external_workflow (
        id                 int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ticket_external_workflow PRIMARY KEY,
        id_ticket_external int          NOT NULL,
        [date]             datetime     NOT NULL,
        transfertype       int          NULL,
        supplier           nvarchar(50) NULL,
        dt_param           datetime     NULL,
        CONSTRAINT FK_ticket_external_workflow_ticket_external
            FOREIGN KEY (id_ticket_external) REFERENCES dbo.ticket_external (id)
    );
END;

IF OBJECT_ID(N'dbo.ticket_external_sla', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ticket_external_sla (
        id        int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ticket_external_sla PRIMARY KEY,
        sla_name  nvarchar(8) NOT NULL,
        sla_hours float       NOT NULL
    );
END;

BEGIN TRANSACTION;

-- SLA katalog (smyšlené hodiny). „NES IV" záměrně chybí → ticket 900106 skončí v „Nelze vyhodnotit".
DELETE FROM dbo.ticket_external_sla;
INSERT INTO dbo.ticket_external_sla (sla_name, sla_hours) VALUES
    (N'NES I', 8), (N'NES II', 16), (N'NES III', 40),
    (N'Vada A', 8), (N'Vada B', 16), (N'Vada C', 40), (N'Vada D', 80), (N'Neurčeno', 40);

-- Smyšlené tickety: nejdřív předávky, pak tickety (FK).
DELETE w FROM dbo.ticket_external_workflow w
    JOIN dbo.ticket_external t ON t.id = w.id_ticket_external
    WHERE LTRIM(RTRIM(t.external_id)) LIKE '9001__' OR t.external_id = 'ABC-1';
DELETE FROM dbo.ticket_external WHERE LTRIM(RTRIM(external_id)) LIKE '9001__' OR external_id = 'ABC-1';

DECLARE @dnes datetime = CAST(CAST(GETDATE() AS date) AS datetime);
DECLARE @ted  datetime = GETDATE();
DECLARE @t TABLE (external_id varchar(50), id int);

INSERT INTO dbo.ticket_external (external_id, source, supplier_tag, sla_name)
OUTPUT inserted.external_id, inserted.id INTO @t
VALUES
    ('900101', 0, N'DOD-A', N'NES II'),   -- NES u dodavatele 6 dní → v prodlení
    ('900102', 0, N'DOD-A', N'NES I'),    -- NES předán před 2 h → hrozí (zbývá ≤ 48 h SLA)
    ('900103', 0, N'DOD-B', NULL),        -- PMP u dodavatele, termín před 10 dny → v prodlení (kalendářní dny)
    ('900104', 0, N'DOD-B', NULL),        -- PNF vrácený → v prodlení není
    ('900105', 0, N'DOD-A', NULL),        -- PMP u dodavatele, termín za 3 dny → hrozí
    ('900106', 0, N'DOD-A', NULL),        -- NES s důležitostí „NES IV" bez SLA → nelze vyhodnotit
    ('900107', 0, N'DOD-B', NULL),        -- PMP přešel od A k B, u B po termínu → řádek B s T00
    (' 900108 ', 0, N'DOD-C', NULL),      -- PNF, external_id s mezerami → páruje se po Trim
    ('ABC-1', 0, NULL, NULL);             -- nečíselné external_id → ignoruje se

INSERT INTO dbo.ticket_external_workflow (id_ticket_external, [date], transfertype, supplier, dt_param)
SELECT t.id, v.d, v.typ, v.dod, v.param
FROM @t t
JOIN (VALUES
    ('900101', DATEADD(HOUR, 8, DATEADD(DAY, -6, @dnes)), 1, N'Dodavatel A s.r.o.', CAST(NULL AS datetime)),
    ('900102', DATEADD(HOUR, -2, @ted),                   1, N'Dodavatel A s.r.o.', NULL),
    ('900103', DATEADD(DAY, -20, @dnes),                  1, N'Dodavatel B a.s.', DATEADD(SECOND, -1, DATEADD(DAY, -9, @dnes))),
    ('900104', DATEADD(DAY, -15, @dnes),                  1, N'Dodavatel B a.s.', DATEADD(DAY, -12, @dnes)),
    ('900104', DATEADD(DAY, -5, @dnes),                   3, NULL, NULL),
    ('900105', DATEADD(DAY, -4, @dnes),                   1, N'Dodavatel A s.r.o.', DATEADD(SECOND, -1, DATEADD(DAY, 4, @dnes))),
    ('900106', DATEADD(DAY, -3, @dnes),                   1, N'Dodavatel A s.r.o.', NULL),
    ('900107', DATEADD(DAY, -30, @dnes),                  1, N'Dodavatel A s.r.o.', DATEADD(DAY, -25, @dnes)),
    ('900107', DATEADD(DAY, -27, @dnes),                  3, NULL, NULL),
    ('900107', DATEADD(DAY, -20, @dnes),                  2, N'Dodavatel B a.s.', DATEADD(SECOND, -1, DATEADD(DAY, -4, @dnes))),
    (' 900108 ', DATEADD(DAY, -12, @dnes),                1, N'Dodavatel C', DATEADD(SECOND, -1, DATEADD(DAY, -3, @dnes))),
    ('ABC-1', DATEADD(DAY, -12, @dnes),                   1, N'Dodavatel C', NULL)
) v(eid, d, typ, dod, param) ON v.eid = t.external_id;

COMMIT TRANSACTION;

SELECT t.external_id, COUNT(w.id) AS predavek
FROM dbo.ticket_external t LEFT JOIN dbo.ticket_external_workflow w ON w.id_ticket_external = t.id
GROUP BY t.external_id ORDER BY t.external_id;
