SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.zaznam_priority_uzivatelu', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.zaznam_priority_uzivatelu (
        zaznam_id int NOT NULL,
        osoba_id int NOT NULL,
        score int NOT NULL,
        computed_at datetime2 NOT NULL CONSTRAINT DF_zaznam_priority_uzivatelu_computed_at DEFAULT (SYSUTCDATETIME()),
        role_weight int NOT NULL,
        deadline_signal int NOT NULL,
        milestone_signal int NOT NULL,
        CONSTRAINT PK_zaznam_priority_uzivatelu PRIMARY KEY (zaznam_id, osoba_id)
    );
END
GO

IF OBJECT_ID(N'dbo.zaznam_priority_rebuild_state', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.zaznam_priority_rebuild_state (
        id int NOT NULL CONSTRAINT PK_zaznam_priority_rebuild_state PRIMARY KEY,
        last_full_rebuild_at datetime2 NULL,
        last_full_rebuild_status nvarchar(16) NOT NULL CONSTRAINT DF_zaznam_priority_rebuild_state_status DEFAULT (N'NEVER'),
        last_full_rebuild_duration_ms bigint NULL,
        last_full_rebuild_task_count int NULL,
        updated_at datetime2 NOT NULL CONSTRAINT DF_zaznam_priority_rebuild_state_updated_at DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT CK_zaznam_priority_rebuild_state_singleton CHECK (id = 1),
        CONSTRAINT CK_zaznam_priority_rebuild_state_status CHECK (last_full_rebuild_status IN (N'NEVER', N'SUCCESS', N'FAILED', N'RUNNING'))
    );
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_zaznam_priority_uzivatelu_zaznam'
      AND parent_object_id = OBJECT_ID(N'dbo.zaznam_priority_uzivatelu')
)
BEGIN
    ALTER TABLE dbo.zaznam_priority_uzivatelu
        ADD CONSTRAINT FK_zaznam_priority_uzivatelu_zaznam FOREIGN KEY (zaznam_id) REFERENCES dbo.projektove_zaznamy(id);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_zaznam_priority_uzivatelu_osoba'
      AND parent_object_id = OBJECT_ID(N'dbo.zaznam_priority_uzivatelu')
)
BEGIN
    ALTER TABLE dbo.zaznam_priority_uzivatelu
        ADD CONSTRAINT FK_zaznam_priority_uzivatelu_osoba FOREIGN KEY (osoba_id) REFERENCES dbo.osoby(id);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.zaznam_priority_uzivatelu')
      AND name = N'IX_zaznam_priority_uzivatelu_osoba_score_zaznam'
)
BEGIN
    CREATE INDEX IX_zaznam_priority_uzivatelu_osoba_score_zaznam
        ON dbo.zaznam_priority_uzivatelu(osoba_id, score DESC, zaznam_id ASC);
END
GO
