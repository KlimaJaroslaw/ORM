# Refaktoryzacja - Database API podobne do Entity Framework

## Problem

Pierwotna implementacja **TestApp** bezpoœrednio u¿ywa³a `SqliteConnection` do tworzenia bazy danych:

```csharp
// ? Z£E - aplikacja zna szczegó³y implementacji bazy danych
using (var connection = new SqliteConnection(connectionString))
{
    DatabaseHelper.EnsureCreated(connection, metadataStore);
}
```

### Dlaczego to by³o z³e?

1. **Naruszenie abstrakcji** - aplikacja musi wiedzieæ, ¿e u¿ywamy SQLite
2. **Brak separacji warstw** - TestApp bezpoœrednio operuje na warstwie dostêpu do danych
3. **Niezgodne z wzorcami ORM** - Entity Framework tego nie robi
4. **Trudnoœæ w zmianie bazy** - gdybyœmy chcieli zmieniæ SQLite na PostgreSQL, musielibyœmy zmieniæ kod aplikacji

## Rozwi¹zanie - Database Facade

Dodaliœmy do `DbContext` w³aœciwoœæ `Database` z metodami wzorowanymi na **Entity Framework Core**:

```csharp
public class DbContext : IDisposable
{
    // ...
    public DatabaseFacade Database { get; }
    // ...
}

public class DatabaseFacade
{
    public void EnsureCreated()   // Tworzy schemat bazy danych
    public void EnsureDeleted()   // Usuwa wszystkie tabele
}
```

## Nowe API - Zgodne z Entity Framework

### Przed (Z£E ?)

```csharp
// Aplikacja musi znaæ SQLite
using (var connection = new SqliteConnection(connectionString))
{
    DatabaseHelper.DropAllTables(connection, metadataStore);
    DatabaseHelper.EnsureCreated(connection, metadataStore);
}

using (var context = new AppDbContext(configuration))
{
    // Operacje na danych
}
```

### Po (DOBRE ?)

```csharp
// Aplikacja u¿ywa tylko ORM API
using (var context = new AppDbContext(configuration))
{
    context.Database.EnsureDeleted();  // Usuñ star¹ bazê
    context.Database.EnsureCreated();  // Utwórz now¹
    
    // Operacje na danych
}
```

## Porównanie z Entity Framework Core

| Operacja | Entity Framework Core | ORM-v1 | Status |
|----------|----------------------|---------|---------|
| Tworzenie schematu | `context.Database.EnsureCreated()` | `context.Database.EnsureCreated()` | ? Identyczne |
| Usuwanie bazy | `context.Database.EnsureDeleted()` | `context.Database.EnsureDeleted()` | ? Identyczne |
| Migracje | `context.Database.Migrate()` | ? Nie zaimplementowane | ?? Przysz³oœæ |
| Tworzenie migracji | `Add-Migration` | ? Nie zaimplementowane | ?? Przysz³oœæ |

## Implementacja

### DbContext - Dodana w³aœciwoœæ Database

```csharp
public class DbContext : IDisposable
{
    public DatabaseFacade Database { get; }
    
    public DbContext(DbConfiguration configuration)
    {
        // ...
        Database = new DatabaseFacade(this);
    }
    
    // Metody pomocnicze dla DatabaseFacade
    protected internal IDbConnection GetConnection() { /* ... */ }
    protected internal DbConfiguration GetConfiguration() { /* ... */ }
}
```

### DatabaseFacade - Implementacja operacji na bazie

```csharp
public class DatabaseFacade
{
    private readonly DbContext _context;

    public void EnsureCreated()
    {
        var connection = _context.GetConnection();
        var metadataStore = _context.GetConfiguration().MetadataStore;

        foreach (var map in metadataStore.GetAllMaps())
        {
            CreateTable(connection, map);
        }
    }

    public void EnsureDeleted()
    {
        var connection = _context.GetConnection();
        var metadataStore = _context.GetConfiguration().MetadataStore;

        foreach (var map in metadataStore.GetAllMaps())
        {
            DropTable(connection, map);
        }
    }
    
    private void CreateTable(IDbConnection connection, EntityMap map)
    {
        // Generuje i wykonuje CREATE TABLE IF NOT EXISTS
    }
    
    private void DropTable(IDbConnection connection, EntityMap map)
    {
        // Wykonuje DROP TABLE IF EXISTS
    }
}
```

## Zmiany w TestApp

### 1. Usuniêto DatabaseHelper

```diff
- TestApp/Helpers/DatabaseHelper.cs  ? USUNIÊTY
```

Funkcjonalnoœæ przeniesiona do `DbContext.Database`.

### 2. Zaktualizowano Program.cs

```csharp
// BY£O ?
using (var connection = new SqliteConnection(connectionString))
{
    DatabaseHelper.DropAllTables(connection, metadataStore);
    DatabaseHelper.EnsureCreated(connection, metadataStore);
}

// JEST ?
using (var context = new AppDbContext(configuration))
{
    context.Database.EnsureDeleted();
    context.Database.EnsureCreated();
    
    // ... operacje na danych ...
}
```

### 3. Zaktualizowano wszystkie Scenarios

- `BasicCrudDemo.cs` ?
- `ChangeTrackingDemo.cs` ?
- `DataTypesDemo.cs` ?
- `AttributeMappingDemo.cs` ?
- `TransactionDemo.cs` ?

Wszystkie u¿ywaj¹ teraz `context.Database` zamiast `DatabaseHelper`.

### 4. Usuniêto niepotrzebne importy

```diff
- using Microsoft.Data.Sqlite;  ? NIE POTRZEBNE
- using TestApp.Helpers;        ? NIE POTRZEBNE
```

Aplikacja **NIE** wie ju¿ nic o SQLite!

## Korzyœci

### 1. **Czystszy kod**
```csharp
// Jedno wywo³anie zamiast trzech
context.Database.EnsureCreated();
```

### 2. **Separacja warstw**
- Aplikacja u¿ywa tylko ORM API
- ORM wie o bazie danych
- £atwa zmiana bazy danych w przysz³oœci

### 3. **Zgodnoœæ z Entity Framework**
- Developerzy znaj¹cy EF od razu wiedz¹ jak u¿ywaæ
- £atwa migracja z/do Entity Framework

### 4. **Lepsze testowanie**
- Mo¿liwoœæ mockowania `DatabaseFacade`
- Testowanie bez prawdziwej bazy danych

### 5. **Przysz³oœæ - migracje**
```csharp
// Przygotowane na przysz³oœæ
context.Database.Migrate();
context.Database.GetPendingMigrations();
```

## Przyk³ad u¿ycia - Pe³ny cykl ¿ycia

```csharp
var connectionString = "Data Source=myapp.db;";
var metadataStore = new MetadataStoreBuilder()
    .AddAssembly(typeof(Product).Assembly)
    .UseNamingStrategy(new PascalCaseNamingStrategy())
    .Build();

var configuration = new DbConfiguration(connectionString, metadataStore);

using (var context = new AppDbContext(configuration))
{
    // 1. Przygotowanie bazy
    context.Database.EnsureDeleted();   // Usuñ star¹ (dev/testing)
    context.Database.EnsureCreated();   // Utwórz now¹
    
    // 2. Dodanie danych
    var product = new Product 
    { 
        Name = "Laptop", 
        Price = 3000m, 
        Stock = 10 
    };
    
    context.Products.Add(product);
    context.SaveChanges();
    
    Console.WriteLine($"Nowy produkt ID: {product.Id}");
    
    // 3. Odczyt
    var found = context.Products.Find(product.Id);
    Console.WriteLine($"Znaleziono: {found?.Name}");
    
    // 4. Aktualizacja
    found.Price = 2800m;
    context.Products.Update(found);
    context.SaveChanges();
    
    // 5. Usuwanie
    context.Products.Remove(found);
    context.SaveChanges();
}

// ? Nigdzie nie ma SqliteConnection!
// ? Tylko ORM API!
```

## Wnioski

### ? Co zyskaliœmy?

1. **API zgodne z Entity Framework** - znajome dla developerów
2. **Separacja warstw** - aplikacja nie wie o SQLite
3. **Czystszy kod** - mniej boilerplate
4. **£atwiejsze testowanie** - mo¿liwoœæ mockowania
5. **Przysz³oœciowoœæ** - gotowe na migracje

### ?? Co mo¿na dodaæ w przysz³oœci?

1. **Migracje** - `context.Database.Migrate()`
2. **Tworzenie skryptów** - `context.Database.GenerateCreateScript()`
3. **Connection resilience** - automatyczne ponowne próby
4. **Pooling** - zarz¹dzanie po³¹czeniami
5. **Multiple databases** - ró¿ne providery

## Status

? **Zaimplementowane** - Database API (EnsureCreated, EnsureDeleted)  
? **Zaktualizowane** - Wszystkie scenariusze demonstracyjne  
? **Usuniête** - DatabaseHelper (niepotrzebny)  
? **Build** - Pomyœlny  
? **Testy** - Przechodz¹  

**ORM-v1 jest teraz bardziej zgodny z Entity Framework Core!** ??
