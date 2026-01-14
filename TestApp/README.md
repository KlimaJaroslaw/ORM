# ORM-v1 - Aplikacja Demonstracyjna

## Opis

Kompleksowa aplikacja demonstracyjna prezentuj¹ca wszystkie mo¿liwoœci ORM-v1 - custom Object-Relational Mapper dla .NET 9.

## Funkcjonalnoœci ORM-v1

### 1. **Mapowanie Encji**
- Atrybuty `[Table]`, `[Column]`, `[Key]`, `[Ignore]`, `[ForeignKey]`
- Automatyczne mapowanie nazw (PascalCase, SnakeCase)
- Wsparcie dla kluczy g³ównych z AUTOINCREMENT
- Rozpoznawanie w³aœciwoœci nawigacyjnych

### 2. **Operacje CRUD**
- **Create**: `DbSet<T>.Add(entity)` + `SaveChanges()`
- **Read**: `Find(id)`, `All()`, zapytania LINQ
- **Update**: `DbSet<T>.Update(entity)` + `SaveChanges()`
- **Delete**: `DbSet<T>.Remove(entity)` + `SaveChanges()`

### 3. **Change Tracker**
- Automatyczne œledzenie stanu encji
- Stany: Added, Unchanged, Modified, Deleted
- Detekcja zmian i optymalizacja zapytañ
- Wsparcie dla transakcji

### 4. **Typy Danych**
- `int`, `long`, `decimal`, `double`, `float`
- `string`, `bool`, `DateTime`
- Enumeracje (zapisywane jako INTEGER)
- Typy nullable

### 5. **Generowanie SQL**
- SELECT, INSERT, UPDATE, DELETE
- JOIN (Inner, Left, Right, Full Outer)
- WHERE, GROUP BY, HAVING, ORDER BY
- DISTINCT, LIMIT, OFFSET
- Funkcje agregacyjne (COUNT, SUM, AVG, MIN, MAX)

### 6. **Baza Danych**
- SQLite jako backend
- Automatyczne tworzenie schematu (`DatabaseHelper.EnsureCreated`)
- Parametryzowane zapytania (ochrona przed SQL Injection)
- Transakcje z automatycznym rollback w przypadku b³êdów

## Struktura Projektu

```
TestApp/
??? Models/
?   ??? Product.cs          # Model produktu z [Table], [Column]
?   ??? Category.cs         # Model kategorii
?   ??? Customer.cs         # Model klienta z enum CustomerStatus
?   ??? Order.cs            # Model zamówienia z relacj¹ do Customer
?
??? Scenarios/
?   ??? BasicCrudDemo.cs           # Demo podstawowych operacji CRUD
?   ??? ChangeTrackingDemo.cs      # Demo Change Trackera
?   ??? DataTypesDemo.cs           # Demo ró¿nych typów danych
?   ??? AttributeMappingDemo.cs    # Demo atrybutów mapowania
?   ??? TransactionDemo.cs         # Demo transakcji
?
??? Helpers/
?   ??? DatabaseHelper.cs   # Pomocnik do tworzenia schematu bazy
?
??? AppDbContext.cs         # Kontekst aplikacji dziedzicz¹cy po DbContext
??? Program.cs              # G³ówny program z menu
```

## Modele Encji

### Product
```csharp
[Table("Products")]
public class Product
{
    [Key]
    public int Id { get; set; }
    
    [Column("product_name")]
    public string Name { get; set; }
    
    public decimal Price { get; set; }
    public int Stock { get; set; }
    
    [Column("category_id")]
    public int CategoryId { get; set; }
    
    [Ignore]  // W³aœciwoœæ nawigacyjna
    public Category? Category { get; set; }
}
```

### Customer
```csharp
[Table("Customers")]
public class Customer
{
    [Key]
    public int Id { get; set; }
    
    [Column("first_name")]
    public string FirstName { get; set; }
    
    [Column("last_name")]
    public string LastName { get; set; }
    
    public string Email { get; set; }
    
    [Column("registration_date")]
    public DateTime RegistrationDate { get; set; }
    
    public CustomerStatus Status { get; set; }
    
    [Ignore]  // W³aœciwoœæ obliczana
    public string FullName => $"{FirstName} {LastName}";
}

public enum CustomerStatus
{
    Inactive = 0,
    Active = 1,
    Premium = 2
}
```

## Przyk³ady U¿ycia

### Konfiguracja DbContext

```csharp
var connectionString = "Data Source=demo.db;Version=3;";
var metadataStore = new MetadataStoreBuilder()
    .AddAssembly(typeof(Product).Assembly)
    .UseNamingStrategy(new PascalCaseNamingStrategy())
    .Build();

var configuration = new DbConfiguration(connectionString, metadataStore);

using var context = new AppDbContext(configuration);
```

### Tworzenie Schematu

```csharp
using var connection = new SQLiteConnection(connectionString);
DatabaseHelper.EnsureCreated(connection, metadataStore);
```

### Operacje CRUD

```csharp
// CREATE
var product = new Product 
{ 
    Name = "Laptop", 
    Price = 3000m, 
    Stock = 10 
};
context.Products.Add(product);
context.SaveChanges();
Console.WriteLine($"Nowy ID: {product.Id}");

// READ
var found = context.Products.Find(1);
var allProducts = context.Products.All().ToList();

// UPDATE
product.Price = 2800m;
context.Products.Update(product);
context.SaveChanges();

// DELETE
context.Products.Remove(product);
context.SaveChanges();
```

### Praca z Change Trackerem

```csharp
// Sprawdzenie stanu
var hasChanges = context.ChangeTracker.HasChanges();

// Wyœwietlenie œledzonych encji
foreach (var entry in context.ChangeTracker.Entries)
{
    Console.WriteLine($"{entry.Entity.GetType().Name}: {entry.State}");
}
```

### Transakcje

```csharp
// Wszystkie operacje wykonywane w SaveChanges s¹ atomowe
context.Products.Add(product1);
context.Products.Add(product2);
context.Products.Update(product3);
context.Products.Remove(product4);

// Jeœli którakolwiek operacja siê nie powiedzie,
// wszystkie s¹ wycofywane (rollback)
context.SaveChanges();
```

## Uruchomienie

1. Uruchom aplikacjê:
   ```bash
   dotnet run --project TestApp
   ```

2. Wybierz scenariusz z menu:
   - **1** - Pe³na demonstracja wszystkich mo¿liwoœci
   - **2** - Podstawowe operacje CRUD
   - **3** - Change Tracker
   - **4** - Typy danych
   - **5** - Atrybuty mapowania
   - **6** - Transakcje

## Scenariusze Demonstracyjne

### 1. Pe³na Demonstracja
Kompleksowy przyk³ad prezentuj¹cy:
- Tworzenie schematu bazy danych
- Dodawanie danych (kategorie, produkty, klienci, zamówienia)
- Wyszukiwanie po ID
- Pobieranie wszystkich rekordów
- Aktualizacja danych
- Usuwanie danych
- Podsumowania i statystyki

### 2. Basic CRUD Demo
Podstawowe operacje Create, Read, Update, Delete na prostych przyk³adach.

### 3. Change Tracking Demo
Demonstracja mechanizmu œledzenia zmian:
- Stany encji (Added, Unchanged, Modified, Deleted)
- Automatyczne œledzenie po Find() i All()
- Wizualizacja stanu Change Trackera

### 4. Data Types Demo
Praca z ró¿nymi typami danych:
- Enumy (CustomerStatus)
- DateTime (daty rejestracji, zamówieñ)
- Decimal (ceny, kwoty)
- Filtrowanie i agregacje

### 5. Attribute Mapping Demo
Demonstracja atrybutów:
- `[Table]` - mapowanie nazwy tabeli
- `[Column]` - mapowanie nazwy kolumny
- `[Key]` - klucz g³ówny
- `[Ignore]` - pomijanie w³aœciwoœci
- Analiza metadanych mapowania

### 6. Transaction Demo
Demonstracja transakcji:
- Wiele operacji w jednej transakcji
- Ró¿ne typy operacji (ADD, UPDATE, DELETE) atomowo
- Rollback w przypadku b³êdu
- Wielokrotne SaveChanges

## Pliki Bazy Danych

Aplikacja tworzy nastêpuj¹ce pliki SQLite:
- `demo.db` - pe³na demonstracja
- `crud_demo.db` - demo CRUD
- `changetracker_demo.db` - demo Change Trackera
- `datatypes_demo.db` - demo typów danych
- `mapping_demo.db` - demo mapowania
- `transaction_demo.db` - demo transakcji

## Technologie

- **.NET 9.0**
- **C# 13.0**
- **SQLite** (System.Data.SQLite.Core)
- **ORM-v1** (custom ORM)

## Funkcje ORM-v1

? Automatyczne mapowanie encji  
? Generowanie SQL (SELECT, INSERT, UPDATE, DELETE)  
? Change Tracker  
? Transakcje  
? Parametryzowane zapytania  
? Wsparcie dla LINQ (podstawowe)  
? Eager loading (JOIN)  
? Lazy loading (nawigacja)  
? Migracje schematu  
? Ró¿ne strategie nazewnictwa  

## Wnioski

ORM-v1 to funkcjonalny, ³atwy w u¿yciu ORM, który:
- Upraszcza pracê z baz¹ danych
- Zapewnia type-safety na poziomie C#
- Automatyzuje tworzenie SQL
- Œledzi zmiany encji
- Wspiera transakcje i obs³ugê b³êdów
- Jest rozszerzalny i konfigurowalny
