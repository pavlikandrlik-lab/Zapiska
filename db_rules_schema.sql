/*
    PM Tracker - rozšíření oprávnění a rolí
    Schéma: authz
    Režim: idempotentní script (bez zásahu do existujících dbo tabulek)
*/

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'authz')
BEGIN
    EXEC(N'CREATE SCHEMA authz');
END
GO

IF OBJECT_ID(N'authz.superadmins', N'U') IS NULL
BEGIN
    CREATE TABLE authz.superadmins (
        osoba_id INT NOT NULL PRIMARY KEY,
        poznamka NVARCHAR(500) NULL,
        created_at DATETIME2 NOT NULL CONSTRAINT DF_authz_superadmins_created_at DEFAULT (SYSUTCDATETIME()),
        created_by INT NULL,
        CONSTRAINT FK_authz_superadmins_osoba FOREIGN KEY (osoba_id) REFERENCES dbo.osoby(id),
        CONSTRAINT FK_authz_superadmins_created_by FOREIGN KEY (created_by) REFERENCES dbo.osoby(id)
    );
END
GO

IF OBJECT_ID(N'authz.permission_categories', N'U') IS NULL
BEGIN
    CREATE TABLE authz.permission_categories (
        id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        kod NVARCHAR(100) NOT NULL,
        nazev NVARCHAR(255) NOT NULL,
        sort_order INT NOT NULL CONSTRAINT DF_authz_permission_categories_sort_order DEFAULT (100),
        is_active BIT NOT NULL CONSTRAINT DF_authz_permission_categories_is_active DEFAULT (1),
        CONSTRAINT UQ_authz_permission_categories_kod UNIQUE (kod)
    );
END
GO

IF OBJECT_ID(N'authz.permissions', N'U') IS NULL
BEGIN
    CREATE TABLE authz.permissions (
        id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        klic NVARCHAR(150) NOT NULL,
        nazev NVARCHAR(255) NOT NULL,
        category_id INT NOT NULL,
        scope_level NVARCHAR(20) NOT NULL,
        is_active BIT NOT NULL CONSTRAINT DF_authz_permissions_is_active DEFAULT (1),
        is_system BIT NOT NULL CONSTRAINT DF_authz_permissions_is_system DEFAULT (1),
        CONSTRAINT UQ_authz_permissions_klic UNIQUE (klic),
        CONSTRAINT FK_authz_permissions_category FOREIGN KEY (category_id) REFERENCES authz.permission_categories(id),
        CONSTRAINT CK_authz_permissions_scope_level CHECK (scope_level IN (N'GLOBAL', N'PROJECT'))
    );
END
GO

IF OBJECT_ID(N'authz.roles', N'U') IS NULL
BEGIN
    CREATE TABLE authz.roles (
        id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        kod NVARCHAR(100) NOT NULL,
        nazev NVARCHAR(255) NOT NULL,
        popis NVARCHAR(1000) NULL,
        is_system BIT NOT NULL CONSTRAINT DF_authz_roles_is_system DEFAULT (0),
        is_active BIT NOT NULL CONSTRAINT DF_authz_roles_is_active DEFAULT (1),
        CONSTRAINT UQ_authz_roles_kod UNIQUE (kod)
    );
END
GO

IF OBJECT_ID(N'authz.role_permissions', N'U') IS NULL
BEGIN
    CREATE TABLE authz.role_permissions (
        id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        role_id INT NOT NULL,
        permission_id INT NOT NULL,
        scope_mode NVARCHAR(20) NOT NULL,
        is_allowed BIT NOT NULL CONSTRAINT DF_authz_role_permissions_is_allowed DEFAULT (1),
        CONSTRAINT FK_authz_role_permissions_role FOREIGN KEY (role_id) REFERENCES authz.roles(id),
        CONSTRAINT FK_authz_role_permissions_permission FOREIGN KEY (permission_id) REFERENCES authz.permissions(id),
        CONSTRAINT UQ_authz_role_permissions_role_permission UNIQUE (role_id, permission_id),
        CONSTRAINT CK_authz_role_permissions_scope_mode CHECK (scope_mode IN (N'ALL', N'INCLUDE'))
    );
END
GO

IF OBJECT_ID(N'authz.role_permission_projects', N'U') IS NULL
BEGIN
    CREATE TABLE authz.role_permission_projects (
        role_permission_id INT NOT NULL,
        projekt_id INT NOT NULL,
        CONSTRAINT PK_authz_role_permission_projects PRIMARY KEY (role_permission_id, projekt_id),
        CONSTRAINT FK_authz_role_permission_projects_role_permission FOREIGN KEY (role_permission_id) REFERENCES authz.role_permissions(id),
        CONSTRAINT FK_authz_role_permission_projects_projekt FOREIGN KEY (projekt_id) REFERENCES dbo.projekty(id)
    );
END
GO

IF OBJECT_ID(N'authz.user_roles', N'U') IS NULL
BEGIN
    CREATE TABLE authz.user_roles (
        id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        osoba_id INT NOT NULL,
        role_id INT NOT NULL,
        is_active BIT NOT NULL CONSTRAINT DF_authz_user_roles_is_active DEFAULT (1),
        created_at DATETIME2 NOT NULL CONSTRAINT DF_authz_user_roles_created_at DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_authz_user_roles_osoba FOREIGN KEY (osoba_id) REFERENCES dbo.osoby(id),
        CONSTRAINT FK_authz_user_roles_role FOREIGN KEY (role_id) REFERENCES authz.roles(id),
        CONSTRAINT UQ_authz_user_roles_osoba_role UNIQUE (osoba_id, role_id)
    );
END
GO

IF OBJECT_ID(N'authz.audit_log', N'U') IS NULL
BEGIN
    CREATE TABLE authz.audit_log (
        id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        actor_osoba_id INT NULL,
        entity_type NVARCHAR(120) NOT NULL,
        entity_id NVARCHAR(120) NOT NULL,
        action NVARCHAR(120) NOT NULL,
        old_value NVARCHAR(MAX) NULL,
        new_value NVARCHAR(MAX) NULL,
        created_at DATETIME2 NOT NULL CONSTRAINT DF_authz_audit_log_created_at DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_authz_audit_log_actor FOREIGN KEY (actor_osoba_id) REFERENCES dbo.osoby(id)
    );
END
GO

/* ---- Seed: kategorie oprávnění ---- */
MERGE authz.permission_categories AS target
USING (VALUES
    (N'PROJECTS', N'Projekty', 10),
    (N'RECORDS', N'Projektové záznamy', 20),
    (N'MEETINGS', N'Jednání', 30),
    (N'MASTER', N'Číselníky a osoby', 40),
    (N'SETTINGS', N'Nastavení', 50)
) AS source(kod, nazev, sort_order)
ON target.kod = source.kod
WHEN NOT MATCHED BY TARGET THEN
    INSERT (kod, nazev, sort_order, is_active)
    VALUES (source.kod, source.nazev, source.sort_order, 1)
WHEN MATCHED THEN
    UPDATE SET
        target.nazev = source.nazev,
        target.sort_order = source.sort_order,
        target.is_active = 1;
GO

/* ---- Seed: akce (permission katalog) ---- */
MERGE authz.permissions AS target
USING (
    SELECT
        s.klic,
        s.nazev,
        c.id AS category_id,
        s.scope_level
    -- Bootstrap baseline: minimální sada klíčů pro SUPERADMIN/APP_ADMIN. Aplikace
    -- (PermissionSeeder při Program.cs startu) upsertne kompletní per-action katalog
    -- (76 klíčů). Tenhle seznam musí obsahovat jen AKTUÁLNÍ (per-action) klíče —
    -- NIKDY pre-redesign (viz db_upgrade_1_3_8_authz_per_action_redesign.sql).
    FROM (VALUES
        (N'projects.create', N'Vytvářet projekty', N'PROJECTS', N'GLOBAL'),
        (N'projects.edit', N'Upravovat projekty', N'PROJECTS', N'PROJECT'),
        (N'projects.delete', N'Mazat projekty (soft-delete)', N'PROJECTS', N'PROJECT'),
        (N'records.edit', N'Upravovat projektové záznamy', N'RECORDS', N'PROJECT'),
        (N'records.schedule.edit', N'Upravovat harmonogram úkolu', N'RECORDS', N'PROJECT'),
        (N'meetings.create', N'Zakládat jednání', N'MEETINGS', N'PROJECT'),
        (N'meetings.edit', N'Upravovat jednání', N'MEETINGS', N'PROJECT'),
        (N'settings.view', N'Zobrazit nastavení', N'SETTINGS', N'GLOBAL')
    ) AS s(klic, nazev, category_kod, scope_level)
    INNER JOIN authz.permission_categories c ON c.kod = s.category_kod
) AS source
ON target.klic = source.klic
WHEN NOT MATCHED BY TARGET THEN
    INSERT (klic, nazev, category_id, scope_level, is_active, is_system)
    VALUES (source.klic, source.nazev, source.category_id, source.scope_level, 1, 1)
WHEN MATCHED THEN
    UPDATE SET
        target.nazev = source.nazev,
        target.category_id = source.category_id,
        target.scope_level = source.scope_level,
        target.is_active = 1;
GO

/* ---- Seed: systémové role ---- */
MERGE authz.roles AS target
USING (VALUES
    (N'SUPERADMIN', N'Superadmin', N'Pevná role s plnými oprávněními.', 1),
    (N'APP_ADMIN', N'Administrátor aplikace', N'Správa aplikace a základních entit.', 1)
) AS source(kod, nazev, popis, is_system)
ON target.kod = source.kod
WHEN NOT MATCHED BY TARGET THEN
    INSERT (kod, nazev, popis, is_system, is_active)
    VALUES (source.kod, source.nazev, source.popis, source.is_system, 1)
WHEN MATCHED THEN
    UPDATE SET
        target.nazev = source.nazev,
        target.popis = source.popis,
        target.is_system = source.is_system,
        target.is_active = 1;
GO

/* ---- Seed: role -> permissions ---- */
-- Bootstrap baseline role_permissions: minimální sada pro SUPERADMIN/APP_ADMIN,
-- navazuje na baseline permission seznam výše. Plný katalog (76 klíčů) doseedová
-- aplikační PermissionSeeder při Program.cs startu (idempotentní UPSERT).
;WITH role_perm_source AS (
    SELECT r.id AS role_id, p.id AS permission_id, CAST(N'ALL' AS NVARCHAR(20)) AS scope_mode
    FROM authz.roles r
    INNER JOIN authz.permissions p ON p.klic IN (
        N'projects.create',
        N'projects.edit',
        N'projects.delete',
        N'records.edit',
        N'records.schedule.edit',
        N'meetings.create',
        N'meetings.edit',
        N'settings.view'
    )
    WHERE r.kod = N'SUPERADMIN'

    UNION ALL

    SELECT r.id AS role_id, p.id AS permission_id, CAST(N'ALL' AS NVARCHAR(20)) AS scope_mode
    FROM authz.roles r
    INNER JOIN authz.permissions p ON p.klic IN (
        N'projects.create',
        N'projects.edit',
        N'records.edit',
        N'records.schedule.edit',
        N'meetings.create',
        N'meetings.edit',
        N'settings.view'
    )
    WHERE r.kod = N'APP_ADMIN'
)
MERGE authz.role_permissions AS target
USING role_perm_source AS source
ON target.role_id = source.role_id
   AND target.permission_id = source.permission_id
WHEN NOT MATCHED BY TARGET THEN
    INSERT (role_id, permission_id, scope_mode, is_allowed)
    VALUES (source.role_id, source.permission_id, source.scope_mode, 1)
WHEN MATCHED THEN
    UPDATE SET
        target.scope_mode = source.scope_mode,
        target.is_allowed = 1;
GO

/*
    Seed superadmin uživatelů zde záměrně NENÍ.
    Superadmin osoby se nastavují ručně vložením do authz.superadmins.
*/
