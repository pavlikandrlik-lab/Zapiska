# DRY + OOP Refaktor (Zápiska)

## 1. Cíle
- Jedno business pravidlo na jednom místě.
- Tenké controllery (validace + orchestrace).
- Sdílené policy/helper služby místo duplikovaných helper metod.
- Čitelná, rozšiřitelná správa číselníků bez reflexe.

## 2. Co je již zavedeno
- `BaseController.ExecuteCommand` a `BaseController.ExecuteValidatedCommand` jako jednotná cesta pro POST akce.
- Sdílené služby v `PmTracker.Web/Services/Common`:
  - `ITextNormalizer` / `TextNormalizer`
  - `IPersonIdentityMatcher` / `PersonIdentityMatcher`
  - `IPermissionEvaluationService` / `PermissionEvaluationService`
  - `ICommentAuthorizationPolicy` / `CommentAuthorizationPolicy`
- `SqlServerDataStore` používá sdílené helper/policy služby místo lokálních duplikátů.
- Číselníky používají handler dispatch mapy (`_ciselnikSaveHandlers`, `_ciselnikDeleteHandlers`) místo velkých switch větví v save/delete flow.
- Reflexní CRUD helper byl nahrazen explicitními `assign/create` lambda strategií.

## 3. Nastavení (akce/klíče)
- Katalog klíčů (`PermissionKeys.BuildCatalog`) obsahuje metadata:
  - doporučená kategorie,
  - scope,
  - popis.
- Modal „Nová/Upravit akci“ (`PermissionModal`) má napojení klíč -> metadata.
- Backend při ukládání akce normalizuje kategorii/scope podle katalogu klíče (zdroj pravdy je server).

## 4. Testy
- Unit testy pro:
  - `TextNormalizer`
  - `PersonIdentityMatcher`
  - `PermissionEvaluationService`
  - `CommentAuthorizationPolicy`

## 5. Doporučené navazující kroky
1. Rozdělit `SqlServerDataStore` na use-case služby (`Records`, `Meetings`, `People`, `Settings`, `Dictionaries`) za zachování stávajícího HTTP kontraktu.
2. Extrahovat shared `CommentThread` partial pro projekty/jednání.
3. Rozdělit `site.js` do modulů (`modal-ajax`, `filters`, `pickers`, `settings`) a ponechat jeden bootstrap.
4. Rozdělit `DbContext` mapování do `IEntityTypeConfiguration<T>`.
