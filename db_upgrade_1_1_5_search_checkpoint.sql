SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.search_reindex_checkpoint', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.search_reindex_checkpoint (
        id int NOT NULL CONSTRAINT PK_search_reindex_checkpoint PRIMARY KEY,
        last_processed_audit_id bigint NOT NULL CONSTRAINT DF_search_reindex_checkpoint_last_id DEFAULT (0),
        last_processed_at datetime2 NULL,
        updated_at datetime2 NOT NULL CONSTRAINT DF_search_reindex_checkpoint_updated_at DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT CK_search_reindex_checkpoint_singleton CHECK (id = 1)
    );

    INSERT INTO dbo.search_reindex_checkpoint (id, last_processed_audit_id) VALUES (1, 0);
END
GO
