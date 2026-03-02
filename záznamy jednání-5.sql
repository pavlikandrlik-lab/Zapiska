CREATE TABLE [ciselnik_stavu_projektu] (
  [id] int PRIMARY KEY IDENTITY(1, 1),
  [kod] nvarchar(255) UNIQUE NOT NULL,
  [nazev] nvarchar(255) NOT NULL
)
GO

CREATE TABLE [ciselnik_stavu_ukolu] (
  [id] int PRIMARY KEY IDENTITY(1, 1),
  [kod] nvarchar(255) UNIQUE NOT NULL,
  [nazev] nvarchar(255) NOT NULL,
  [is_final] bit NOT NULL
)
GO

CREATE TABLE [ciselnik_kategorii_zaznamu] (
  [id] int PRIMARY KEY IDENTITY(1, 1),
  [kod] nvarchar(255) UNIQUE NOT NULL,
  [nazev] nvarchar(255) NOT NULL
)
GO

CREATE TABLE [ciselnik_typu_ukolu] (
  [id] int PRIMARY KEY IDENTITY(1, 1),
  [kod] nvarchar(255) UNIQUE NOT NULL,
  [nazev] nvarchar(255) NOT NULL
)
GO

CREATE TABLE [ciselnik_typu_externich_odkazu] (
  [id] int PRIMARY KEY IDENTITY(1, 1),
  [kod] nvarchar(255) UNIQUE NOT NULL,
  [nazev] nvarchar(255) NOT NULL
)
GO

CREATE TABLE [ciselnik_roli_projektu] (
  [id] int PRIMARY KEY IDENTITY(1, 1),
  [kod] nvarchar(255) UNIQUE NOT NULL,
  [nazev] nvarchar(255) NOT NULL
)
GO

CREATE TABLE [ciselnik_stavu_ucasti] (
  [id] int PRIMARY KEY IDENTITY(1, 1),
  [kod] nvarchar(255) UNIQUE NOT NULL,
  [nazev] nvarchar(255) NOT NULL
)
GO

CREATE TABLE [ciselnik_organizace] (
  [id] int PRIMARY KEY IDENTITY(1, 1),
  [kod] nvarchar(255) UNIQUE NOT NULL,
  [nazev] nvarchar(255) NOT NULL
)
GO

CREATE TABLE [ciselnik_organizacni_celky] (
  [id] int PRIMARY KEY IDENTITY(1, 1),
  [kod] nvarchar(255) UNIQUE NOT NULL,
  [nazev] nvarchar(255) NOT NULL
)
GO

CREATE TABLE [ciselnik_vyzvy] (
  [id] int PRIMARY KEY IDENTITY(1, 1),
  [kod] nvarchar(255) UNIQUE NOT NULL,
  [nazev] nvarchar(255) NOT NULL,
  [rok] date NOT NULL
)
GO

CREATE TABLE [subsystemy] (
  [id] int PRIMARY KEY IDENTITY(1, 1),
  [kód] nvarchar(255) UNIQUE NOT NULL,
  [nazev] nvarchar(255) NOT NULL,
  [vedouci_osoba_id] int NOT NULL
)
GO

CREATE TABLE [osoby] (
  [id] int PRIMARY KEY IDENTITY(1, 1),
  [jmeno] nvarchar(255) NOT NULL,
  [prijmeni] nvarchar(255) NOT NULL,
  [titul] nvarchar(255),
  [organizacni_celek_id] int,
  [organizace_id] int NOT NULL,
  [Guid_AD] int UNIQUE
)
GO

CREATE TABLE [projekty] (
  [id] int PRIMARY KEY IDENTITY(1, 1),
  [zkratka] nvarchar(255) UNIQUE NOT NULL,
  [cely_nazev] nvarchar(255) NOT NULL,
  [stav_id] int NOT NULL
)
GO

CREATE TABLE [obsazeni_projektu] (
  [id] int PRIMARY KEY IDENTITY(1, 1),
  [projekt_id] int NOT NULL,
  [osoba_id] int NOT NULL,
  [role_id] int NOT NULL
)
GO

CREATE TABLE [projektove_zaznamy] (
  [id] int PRIMARY KEY IDENTITY(1, 1),
  [projekt_id] int NOT NULL,
  [kategorie_id] int NOT NULL,
  [aktualni_typ_ukolu_id] int,
  [stav_ukolu_id] int,
  [cislo_zaznamu] int NOT NULL,
  [nazev] nvarchar(255) NOT NULL,
  [popis] text,
  [vlastnik_id] int NOT NULL,
  [datum_zalozeni] date NOT NULL,
  [datum_ukonceni] date NOT NULL,
  [subsystem_id] int NOT NULL
)
GO

CREATE TABLE [zaznam_historie_zmen_typu] (
  [id] int PRIMARY KEY IDENTITY(1, 1),
  [zaznam_id] int NOT NULL,
  [puvodni_typ_id] int NOT NULL,
  [novy_typ_id] int NOT NULL,
  [datum_zmeny] datetime NOT NULL,
  [zmenil_osoba_id] int NOT NULL
)
GO

CREATE TABLE [zaznam_historie_terminu] (
  [id] int PRIMARY KEY IDENTITY(1, 1),
  [zaznam_id] int NOT NULL,
  [puvodni_datum] date NOT NULL,
  [nove_datum] date NOT NULL,
  [datum_zmeny] datetime NOT NULL,
  [duvod] text NOT NULL
)
GO

CREATE TABLE [zaznam_historie_vlastnik] (
  [id] int PRIMARY KEY IDENTITY(1, 1),
  [zaznam_id] int NOT NULL,
  [puvodni_vlastnik] int NOT NULL,
  [novy_vlastnik] int NOT NULL,
  [datum_zmeny] datetime NOT NULL
)
GO

CREATE TABLE [zaznam_historie_subsystem] (
  [id] int PRIMARY KEY IDENTITY(1, 1),
  [zaznam_id] int NOT NULL,
  [puvodni_subsystem] int NOT NULL,
  [novy_subsystem] int NOT NULL,
  [datum_zmeny] datetime NOT NULL
)
GO

CREATE TABLE [zaznam_historie_stavu_zaznamu] (
  [id] int PRIMARY KEY IDENTITY(1, 1),
  [zaznam_id] int NOT NULL,
  [puvodni_stav] int NOT NULL,
  [novy_stav] int NOT NULL,
  [datum_zmeny] datetime NOT NULL
)
GO

CREATE TABLE [zaznam_historie_stavu_projektu] (
  [id] int PRIMARY KEY IDENTITY(1, 1),
  [zaznam_id] int NOT NULL,
  [puvodni_stav] int NOT NULL,
  [novy_stav] int NOT NULL,
  [datum_zmeny] datetime NOT NULL
)
GO

CREATE TABLE [zaznam_externi_odkazy] (
  [id] int PRIMARY KEY IDENTITY(1, 1),
  [zaznam_id] int NOT NULL,
  [typ_odkazu_id] int NOT NULL,
  [cislo] nvarchar(255) NOT NULL,
  [datum_objednani] date,
  [plan_dodani] date,
  [datum_dodani] date,
  [datum_prevzeti] date,
  [vyzva] int
)
GO

CREATE TABLE [zaznam_spoluprace] (
  [zaznam_id] int NOT NULL,
  [osoba_id] int NOT NULL,
  PRIMARY KEY ([zaznam_id], [osoba_id])
)
GO

CREATE TABLE [ciselnik_stavu_jednani] (
  [id] int PRIMARY KEY IDENTITY(1, 1),
  [kod] nvarchar(255) NOT NULL,
  [nazev] nvarchar(255) NOT NULL
)
GO

CREATE TABLE [jednani] (
  [id] int PRIMARY KEY IDENTITY(1, 1),
  [projekt_id] int NOT NULL,
  [cislo_jednani] int NOT NULL,
  [datum_planovane] date NOT NULL,
  [cas_zacatek] datetime NOT NULL,
  [misto] nvarchar(255),
  [stav_jednani_id] int NOT NULL,
  [uzamkl_osoba_id] int
)
GO

CREATE TABLE [ucast] (
  [jednani_id] int,
  [osoba_id] int,
  [stav_ucasti_id] int NOT NULL,
  PRIMARY KEY ([jednani_id], [osoba_id])
)
GO

CREATE TABLE [vyjadreni] (
  [id] int PRIMARY KEY IDENTITY(1, 1),
  [zaznam_id] int NOT NULL,
  [jednani_id] int NOT NULL,
  [text_vyjadreni] text NOT NULL,
  [datum_vyjadreni] datetime NOT NULL
)
GO

EXEC sp_addextendedproperty
@name = N'Table_Description',
@value = 'Běží, Čeká se, Hotovo, Zrušeno',
@level0type = N'Schema', @level0name = 'dbo',
@level1type = N'Table',  @level1name = 'ciselnik_stavu_ukolu';
GO

EXEC sp_addextendedproperty
@name = N'Table_Description',
@value = 'UKOL, INFO, ROZHODNUTI',
@level0type = N'Schema', @level0name = 'dbo',
@level1type = N'Table',  @level1name = 'ciselnik_kategorii_zaznamu';
GO

EXEC sp_addextendedproperty
@name = N'Table_Description',
@value = 'RU, A, P, mP',
@level0type = N'Schema', @level0name = 'dbo',
@level1type = N'Table',  @level1name = 'ciselnik_typu_ukolu';
GO

EXEC sp_addextendedproperty
@name = N'Column_Description',
@value = 'DRAFT, OPEN, CLOSED',
@level0type = N'Schema', @level0name = 'dbo',
@level1type = N'Table',  @level1name = 'ciselnik_stavu_jednani',
@level2type = N'Column', @level2name = 'kod';
GO

EXEC sp_addextendedproperty
@name = N'Column_Description',
@value = 'Příprava, Otevřeno pro zápis, Uzavřeno',
@level0type = N'Schema', @level0name = 'dbo',
@level1type = N'Table',  @level1name = 'ciselnik_stavu_jednani',
@level2type = N'Column', @level2name = 'nazev';
GO

ALTER TABLE [subsystemy] ADD FOREIGN KEY ([vedouci_osoba_id]) REFERENCES [osoby] ([id])
GO

ALTER TABLE [osoby] ADD FOREIGN KEY ([organizacni_celek_id]) REFERENCES [ciselnik_organizacni_celky] ([id])
GO

ALTER TABLE [osoby] ADD FOREIGN KEY ([organizace_id]) REFERENCES [ciselnik_organizace] ([id])
GO

ALTER TABLE [projekty] ADD FOREIGN KEY ([stav_id]) REFERENCES [ciselnik_stavu_projektu] ([id])
GO

ALTER TABLE [obsazeni_projektu] ADD FOREIGN KEY ([projekt_id]) REFERENCES [projekty] ([id])
GO

ALTER TABLE [obsazeni_projektu] ADD FOREIGN KEY ([osoba_id]) REFERENCES [osoby] ([id])
GO

ALTER TABLE [obsazeni_projektu] ADD FOREIGN KEY ([role_id]) REFERENCES [ciselnik_roli_projektu] ([id])
GO

ALTER TABLE [projektove_zaznamy] ADD FOREIGN KEY ([projekt_id]) REFERENCES [projekty] ([id])
GO

ALTER TABLE [projektove_zaznamy] ADD FOREIGN KEY ([kategorie_id]) REFERENCES [ciselnik_kategorii_zaznamu] ([id])
GO

ALTER TABLE [projektove_zaznamy] ADD FOREIGN KEY ([aktualni_typ_ukolu_id]) REFERENCES [ciselnik_typu_ukolu] ([id])
GO

ALTER TABLE [projektove_zaznamy] ADD FOREIGN KEY ([stav_ukolu_id]) REFERENCES [ciselnik_stavu_ukolu] ([id])
GO

ALTER TABLE [projektove_zaznamy] ADD FOREIGN KEY ([vlastnik_id]) REFERENCES [osoby] ([id])
GO

ALTER TABLE [projektove_zaznamy] ADD FOREIGN KEY ([subsystem_id]) REFERENCES [subsystemy] ([id])
GO

ALTER TABLE [zaznam_historie_zmen_typu] ADD FOREIGN KEY ([zaznam_id]) REFERENCES [projektove_zaznamy] ([id])
GO

ALTER TABLE [zaznam_historie_zmen_typu] ADD FOREIGN KEY ([puvodni_typ_id]) REFERENCES [ciselnik_typu_ukolu] ([id])
GO

ALTER TABLE [zaznam_historie_zmen_typu] ADD FOREIGN KEY ([novy_typ_id]) REFERENCES [ciselnik_typu_ukolu] ([id])
GO

ALTER TABLE [zaznam_historie_zmen_typu] ADD FOREIGN KEY ([zmenil_osoba_id]) REFERENCES [osoby] ([id])
GO

ALTER TABLE [zaznam_historie_terminu] ADD FOREIGN KEY ([zaznam_id]) REFERENCES [projektove_zaznamy] ([id])
GO

ALTER TABLE [zaznam_historie_vlastnik] ADD FOREIGN KEY ([zaznam_id]) REFERENCES [projektove_zaznamy] ([id])
GO

ALTER TABLE [zaznam_historie_vlastnik] ADD FOREIGN KEY ([puvodni_vlastnik]) REFERENCES [osoby] ([id])
GO

ALTER TABLE [zaznam_historie_vlastnik] ADD FOREIGN KEY ([novy_vlastnik]) REFERENCES [osoby] ([id])
GO

ALTER TABLE [zaznam_historie_subsystem] ADD FOREIGN KEY ([zaznam_id]) REFERENCES [projektove_zaznamy] ([id])
GO

ALTER TABLE [zaznam_historie_subsystem] ADD FOREIGN KEY ([puvodni_subsystem]) REFERENCES [subsystemy] ([id])
GO

ALTER TABLE [zaznam_historie_subsystem] ADD FOREIGN KEY ([novy_subsystem]) REFERENCES [subsystemy] ([id])
GO

ALTER TABLE [zaznam_historie_stavu_zaznamu] ADD FOREIGN KEY ([zaznam_id]) REFERENCES [projektove_zaznamy] ([id])
GO

ALTER TABLE [zaznam_historie_stavu_zaznamu] ADD FOREIGN KEY ([puvodni_stav]) REFERENCES [ciselnik_stavu_ukolu] ([id])
GO

ALTER TABLE [zaznam_historie_stavu_zaznamu] ADD FOREIGN KEY ([novy_stav]) REFERENCES [ciselnik_stavu_ukolu] ([id])
GO

ALTER TABLE [zaznam_historie_stavu_projektu] ADD FOREIGN KEY ([zaznam_id]) REFERENCES [projektove_zaznamy] ([id])
GO

ALTER TABLE [zaznam_historie_stavu_projektu] ADD FOREIGN KEY ([puvodni_stav]) REFERENCES [ciselnik_stavu_projektu] ([id])
GO

ALTER TABLE [zaznam_historie_stavu_projektu] ADD FOREIGN KEY ([novy_stav]) REFERENCES [ciselnik_stavu_projektu] ([id])
GO

ALTER TABLE [zaznam_externi_odkazy] ADD FOREIGN KEY ([zaznam_id]) REFERENCES [projektove_zaznamy] ([id])
GO

ALTER TABLE [zaznam_externi_odkazy] ADD FOREIGN KEY ([typ_odkazu_id]) REFERENCES [ciselnik_typu_externich_odkazu] ([id])
GO

ALTER TABLE [zaznam_externi_odkazy] ADD FOREIGN KEY ([vyzva]) REFERENCES [ciselnik_vyzvy] ([id])
GO

ALTER TABLE [zaznam_spoluprace] ADD FOREIGN KEY ([zaznam_id]) REFERENCES [projektove_zaznamy] ([id])
GO

ALTER TABLE [zaznam_spoluprace] ADD FOREIGN KEY ([osoba_id]) REFERENCES [osoby] ([id])
GO

ALTER TABLE [jednani] ADD FOREIGN KEY ([stav_jednani_id]) REFERENCES [ciselnik_stavu_jednani] ([id])
GO

ALTER TABLE [jednani] ADD FOREIGN KEY ([uzamkl_osoba_id]) REFERENCES [osoby] ([id])
GO

ALTER TABLE [jednani] ADD FOREIGN KEY ([projekt_id]) REFERENCES [projekty] ([id])
GO

ALTER TABLE [ucast] ADD FOREIGN KEY ([jednani_id]) REFERENCES [jednani] ([id])
GO

ALTER TABLE [ucast] ADD FOREIGN KEY ([osoba_id]) REFERENCES [osoby] ([id])
GO

ALTER TABLE [ucast] ADD FOREIGN KEY ([stav_ucasti_id]) REFERENCES [ciselnik_stavu_ucasti] ([id])
GO

ALTER TABLE [vyjadreni] ADD FOREIGN KEY ([zaznam_id]) REFERENCES [projektove_zaznamy] ([id])
GO

ALTER TABLE [vyjadreni] ADD FOREIGN KEY ([jednani_id]) REFERENCES [jednani] ([id])
GO
