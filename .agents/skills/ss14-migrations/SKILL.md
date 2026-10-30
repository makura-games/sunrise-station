---
name: ss14-migrations
description: Guide to Creating and Managing Database Migrations in SS14 (PostgreSQL and SQLite)
---

# 🚀 Database Migrations

This skill describes the process of creating and applying database migrations in Space Station 14. Since SS14 supports both PostgreSQL and SQLite, **all changes must be migrated for both engines**.

## 🎯 When to use this skill
- When changing database models (`Model.cs`).
- When adding new tables or columns.
- When renaming or deleting existing database structures.
- When the server crashes at startup with the error "Applying Migrations".

## ⚔️ Double Migration System

SS14 supports **two separate sets of migrations**:
1.  **SQLite Config**: `Content.Server.Database/Migrations/Sqlite`
2.  **PostgreSQL Config**: `Content.Server.Database/Migrations/Postgres`

This is necessary because the engines have different data types, features and SQL dialects. One migration usually cannot be applied to both at once.

### 🛠️ Helper scripts
The repository has scripts to simplify adding migrations to both contexts at once:
-   **Windows (PowerShell)**: `Content.Server.Database/add-migration.ps1`
-   **Linux/Mac (Bash)**: `Content.Server.Database/add-migration.sh`

## 📝 Create Migration

### Prerequisites
- Start the database (if using local Postgres).
- Make sure your changes to `Model.cs` compile.

### Steps
1. **Go** to the database project directory:
    ```powershell
    cd Content.Server.Database
    ```

2. **Run the script** with a friendly name (CamelCase):
    ```powershell
    ./add-migration.ps1 MyNewFeature
    ```

    *If the script crashed or manual control is needed, here are the equivalent commands `dotnet ef`:*
    ```powershell
    # Creating a Migration for SQLite
    dotnet ef migrations add --context SqliteServerDbContext -o Migrations/Sqlite MyNewFeature

    # Creating a migration for Postgres
    dotnet ef migrations add --context PostgresServerDbContext -o Migrations/Postgres MyNewFeature
    ```

3. **Check the Created Files**:
    -   `Migrations/Sqlite/YYYYMMDDHHMMSS_MyNewFeature.cs`
    -   `Migrations/Postgres/YYYYMMDDHHMMSS_MyNewFeature.cs`

### ✅ Inspection checklist
- **Data Loss Warnings**: Has EF Core warned that data might be lost (for example, when a column is deleted)?
- **Type Compatibility**:
    - Ensure that SQLite migrations do not attempt to use Postgres types such as `inet` or `jsonb` without conversion (this should be handled in `DbContext`).
- **Raw SQL**: If you used `migrationBuilder.Sql("...")`, make sure the SQL is compatible with the specific engine for that migration.

## 🚫 Antipatterns (What NOT to do)

### ❌ 1. Forget about one of the engines
**Symptom**: The server works locally (SQLite), but crashes on production (Postgres) or vice versa.
**Why**: Database schema out of sync. EF Core won't find the migration you need.
**Do's**: Always make sure that there is a migration file with the same timestamp/name in **both** folders.

### ❌ 2. Complex schema changes in SQLite
**Problem**: SQLite has limited support for `ALTER TABLE` (for example, you cannot rename a column or change its type without rebuilding the table).
**Implications**: EF Core will try to create a temporary table, copy all the data, delete the old one and rename the new one. This is **very slow** on large tables and dangerous.
**Do it**: Avoid complex changes (rename column, change column type) for SQLite if possible. It's better to add a new column and mark the old one as obsolete (Obsolete).

### ❌ 3. Cyclic dependencies during build
**Problem**: Adding a migration requires building the project, but the project does not build because you are using a new field in the code that is not already in the database (or vice versa).
**Correct flow**:
1. Change `Model.cs` (add a property).
2. Build the project (the code compiles, but the database is out of sync).
3. Create a migration.
4. Update the database (happens automatically when the server starts).

### ❌ 4. Changing already added migrations
**Bad**: Editing a migration file that has already been merged into `master` and deployed.
**Why**: This will break the migration history of all other developers and servers. `__EFMigrationsHistory` will contain the hash of the old version and EF Core will crash.
**How to**: If you find a bug in an old migration, create a **new** migration that fixes the problem.

## 🗑️ Deleting (rollback) local migration
If you created a migration, but changed your mind (and haven’t committed/merged it yet):

```powershell
# Removes the last migration from the context
dotnet ef migrations remove --context SqliteServerDbContext
dotnet ef migrations remove --context PostgresServerDbContext
```
