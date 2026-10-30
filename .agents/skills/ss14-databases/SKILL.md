---
name: ss14-databases
description: SS14 Database System Guide (PostgreSQL and SQLite)
---

# 🗄️ Databases SS14 (Databases)

This skill describes the database architecture in Space Station 14, including dual engine support (PostgreSQL and SQLite), `DbContext` inheritance, and best practices for data models.

## 🎯 When to use this skill
- When creating new tables or database entities.
- When changing existing data models.
- When writing requests to obtain or save player data, bans or logs.
- When solving problems related to data types (IP addresses, JSON, etc.).

## 🏗️ Architecture Review

SS14 uses **Entity Framework Core** with a dual engine architecture to support both **PostgreSQL** (production) and **SQLite** (dev/test).

### Key Components
1. **`IServerDbManager`**: The main interface for database operations (`Content.Server/Database/ServerDbManager.cs`).
2. **`ServerDbBase`**: Abstract base class containing common logic for both engines.
3. **`ServerDbSqlite` and `ServerDbPostgres`**: Specific implementations for specific engines.
4. **`ServerDbContext`**: An abstract EF Core context that defines `DbSet`'s.
5. **`SqliteServerDbContext` and `PostgresServerDbContext`**: Specific contexts that manage configuration (for example, Value Converters).

### Database Logic vs Models
- **Logic**: Located in `Content.Server/Database`. Contains `ServerDbManager`, `ServerDbBase`, etc.
- **Models**: Found in `Content.Server.Database`. Contains `Model.cs` (tables), migrations, and context definitions.

## 📝 Working with Data Models

### Defining Entities
Entities are defined in `Content.Server.Database/Model.cs` (or in separate files in that namespace).

```csharp
public class MyNewEntity
{
    [Key] // Primary key
    public int Id { get; set; }

    [Required] // This field is required
    public string Name { get; set; } = default!;

    // Foreign key or just user ID
    public Guid PlayerUserId { get; set; }
}
```

### Adding to Context
Add `DbSet` to the abstract `ServerDbContext` to `Content.Server.Database/Model.cs`:

```csharp
public abstract class ServerDbContext : DbContext
{
    // ... existing sets
    public DbSet<MyNewEntity> MyNewEntities { get; set; } = default!;
}
```

## ⚠️ Engine Limitations and Data Types

Since SQLite and PostgreSQL support different functions, you need to be careful with types.

### 1. IP addresses 🌐
- **Postgres**: Has native type `inet` (`NpgsqlInet`).
- **SQLite**: **Doesn't** support IP. Must be stored as `TEXT`.
- **Solution**: Use `ValueConverter` in `SqliteServerDbContext.OnModelCreating`.

### 2. JSON Data 📜
- **Postgres**: Has the native type `jsonb`.
- **SQLite**: Must be stored as `TEXT` or `BLOB`.
- **Solution**: Use separate calls to `db.PgDbContext.Add()` and `db.SqliteDbContext.Add()` if complex types are used, or ValueConverters for shared logic.

### 3. Arrays/Lists 📚
- **Postgres**: Supports primitive arrays (for example, `List<string>` maps to `text[]`).
- **SQLite**: **Does** not support arrays.
-   **Solution**:
    - **Preferred**: Use a separate table with a 1:N relationship.
    - **Alternative**: Serialize the string to JSON (if you don't need to search inside the list).

## 💡 Usage Examples

### 1. Data Saving (Difference Handling)
When saving data that uses specific types (like IP), you may need to override the method in `ServerDbSqlite` and `ServerDbPostgres`.

**Abstract Base (`ServerDbBase.cs`)**:
```csharp
public abstract Task AddServerBanAsync(ServerBanDef serverBan);
```

**Postgres implementation (`ServerDbPostgres.cs`)**:
```csharp
public override async Task AddServerBanAsync(ServerBanDef serverBan)
{
    await using var db = await GetDbImpl();
    // Postgres supports NpgsqlInet directly, no conversion needed
    db.PgDbContext.Ban.Add(new ServerBan
    {
        Address = serverBan.Address.ToNpgsqlInet(),
        // ... other fields
    });
    await db.PgDbContext.SaveChangesAsync();
}
```

**SQLite implementation (`ServerDbSqlite.cs`)**:
```csharp
public override async Task AddServerBanAsync(ServerBanDef serverBan)
{
    await using var db = await GetDbImpl();
    // SQLite requires a conversion, which is handled by the ValueConverter in context
    db.SqliteDbContext.Ban.Add(new ServerBan
    {
        Address = serverBan.Address.ToNpgsqlInet(),
        // ... other fields
    });
    await db.SqliteDbContext.SaveChangesAsync();
}
```

### 2. Data Loading (General Logic)
Reading data is often identical for both engines and can be implemented in `ServerDbBase`.

```csharp
// Content.Server/Database/ServerDbBase.cs
public async Task<PlayerPreferences?> GetPlayerPreferencesAsync(NetUserId userId, CancellationToken cancel)
{
    await using var db = await GetDb(cancel);

    // Using .Include() to greedily load related data
    var prefs = await db.DbContext
        .Preference
        .Include(p => p.Profiles)
        .ThenInclude(h => h.Jobs) // Loading nested links (Profiles -> Jobs)
        .AsSplitQuery() // Optimization: Avoids "explosion" of the Cartesian product with multiple include
        .SingleOrDefaultAsync(p => p.UserId == userId.UserId, cancel);

    if (prefs is null) return null;

    // Converting the database model into a Shared model (game logic)
    return new PlayerPreferences(...);
}
```

### 3. Queries with Search 🔍
For text search, use `EF.Functions` to support engine-specific features where possible, or a fallback to standard LINQ.

```csharp
// ServerDbContext.cs (Abstract Method)
public virtual IQueryable<AdminLog> SearchLogs(IQueryable<AdminLog> query, string searchText)
{
    // Default/SQLite: Simple LIKE
    return query.Where(log => EF.Functions.Like(log.Message, "%" + searchText + "%"));
}

// PostgresServerDbContext.cs (Override)
public override IQueryable<AdminLog> SearchLogs(IQueryable<AdminLog> query, string searchText)
{
    // Postgres: Full text search using ToTsVector
    return query.Where(log => EF.Functions.ToTsVector("english", log.Message).Matches(searchText));
}
```

## 🚫 Antipatterns (What NOT to do)

### ❌ 1. Using Postgres-specific types in general logic
**Bad**: Trying to store `NpgsqlInet` or an array of strings directly into `ServerDbBase` without checking the engine.
**Why**: This will break SQLite since it doesn't know these types.
**How to**: Use abstract methods or ValueConverters.

### ❌ 2. Synchronous database calls (`.Result`, `.Wait()`)
**Bad**: `db.DbContext.SaveChangeAsync().Result;`
**Why**: This blocks the main server thread, causing lags (freezes) for players.
**Do it**: Always use `await` and asynchronous methods (`ToListAsync`, `FirstOrDefaultAsync`).

### ❌ 3. Loading the entire table into memory
**Bad**: `var allUsers = db.Player.ToList();` then filtering `allUsers.Where(...)`.
**Why**: A table can contain tens of thousands of records. This will kill memory and CPU.
**How to**: Filter **before** request materialization: `db.Player.Where(...).ToListAsync();`.

### ❌ 4. Lack of indexes for frequent queries
**Bad**: Search for player by `LastSeenUserName` without index on this field.
**Why**: Full Table Scan on every player input will create load.
**Do it**: Add indexing to `OnModelCreating`.

## ✅ Best Practices
1. **Always support both engines**: Never write code that only works on Postgres. SQLite is required for local development.
2. **Use `CCVars`**: The database configuration (host, port, engine) is managed through `CCVars.Database*`.
3. **Validation**: Use the `[Required]`, `[MaxLength]` and `[Key]` attributes in models.
4. **Metrics**: Use `DbReadOpsMetric.Inc()` and `DbWriteOpsMetric.Inc()` in `ServerDbManager` to track activity.
