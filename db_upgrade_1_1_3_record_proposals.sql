SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.zaznam_navrhy', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.zaznam_navrhy (
        id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_zaznam_navrhy PRIMARY KEY,
        projekt_id int NOT NULL,
        zaznam_id int NULL,
        subsystem_id int NOT NULL,
        typ_navrhu nvarchar(64) NOT NULL,
        stav nvarchar(32) NOT NULL,
        payload_json nvarchar(max) NOT NULL,
        created_by_osoba_id int NOT NULL,
        created_at datetime2 NOT NULL CONSTRAINT DF_zaznam_navrhy_created_at DEFAULT (SYSUTCDATETIME()),
        decided_by_osoba_id int NULL,
        decided_at datetime2 NULL,
        approved_record_id int NULL,
        row_version rowversion NOT NULL,
        CONSTRAINT CK_zaznam_navrhy_typ CHECK (typ_navrhu IN (N'CREATE_RECORD', N'SCHEDULE_PLAN_CHANGE')),
        CONSTRAINT CK_zaznam_navrhy_stav CHECK (stav IN (N'PENDING', N'APPROVED', N'REJECTED'))
    );
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_zaznam_navrhy_projekt'
      AND parent_object_id = OBJECT_ID(N'dbo.zaznam_navrhy')
)
BEGIN
    ALTER TABLE dbo.zaznam_navrhy
        ADD CONSTRAINT FK_zaznam_navrhy_projekt FOREIGN KEY (projekt_id) REFERENCES dbo.projekty(id);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_zaznam_navrhy_zaznam'
      AND parent_object_id = OBJECT_ID(N'dbo.zaznam_navrhy')
)
BEGIN
    ALTER TABLE dbo.zaznam_navrhy
        ADD CONSTRAINT FK_zaznam_navrhy_zaznam FOREIGN KEY (zaznam_id) REFERENCES dbo.projektove_zaznamy(id);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_zaznam_navrhy_subsystem'
      AND parent_object_id = OBJECT_ID(N'dbo.zaznam_navrhy')
)
BEGIN
    ALTER TABLE dbo.zaznam_navrhy
        ADD CONSTRAINT FK_zaznam_navrhy_subsystem FOREIGN KEY (subsystem_id) REFERENCES dbo.subsystemy(id);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_zaznam_navrhy_created_by'
      AND parent_object_id = OBJECT_ID(N'dbo.zaznam_navrhy')
)
BEGIN
    ALTER TABLE dbo.zaznam_navrhy
        ADD CONSTRAINT FK_zaznam_navrhy_created_by FOREIGN KEY (created_by_osoba_id) REFERENCES dbo.osoby(id);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_zaznam_navrhy_decided_by'
      AND parent_object_id = OBJECT_ID(N'dbo.zaznam_navrhy')
)
BEGIN
    ALTER TABLE dbo.zaznam_navrhy
        ADD CONSTRAINT FK_zaznam_navrhy_decided_by FOREIGN KEY (decided_by_osoba_id) REFERENCES dbo.osoby(id);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_zaznam_navrhy_approved_record'
      AND parent_object_id = OBJECT_ID(N'dbo.zaznam_navrhy')
)
BEGIN
    ALTER TABLE dbo.zaznam_navrhy
        ADD CONSTRAINT FK_zaznam_navrhy_approved_record FOREIGN KEY (approved_record_id) REFERENCES dbo.projektove_zaznamy(id);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.zaznam_navrhy')
      AND name = N'IX_zaznam_navrhy_projekt_created_at'
)
BEGIN
    CREATE INDEX IX_zaznam_navrhy_projekt_created_at
        ON dbo.zaznam_navrhy(projekt_id, created_at);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.zaznam_navrhy')
      AND name = N'IX_zaznam_navrhy_zaznam_typ_stav'
)
BEGIN
    CREATE INDEX IX_zaznam_navrhy_zaznam_typ_stav
        ON dbo.zaznam_navrhy(zaznam_id, typ_navrhu, stav);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.zaznam_navrhy')
      AND name = N'UX_zaznam_navrhy_pending_schedule_per_record'
)
BEGIN
    CREATE UNIQUE INDEX UX_zaznam_navrhy_pending_schedule_per_record
        ON dbo.zaznam_navrhy(zaznam_id, typ_navrhu)
        WHERE stav = N'PENDING' AND typ_navrhu = N'SCHEDULE_PLAN_CHANGE';
END
GO
