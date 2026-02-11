# Navigation Properties w ORM-v1

## Przegląd

ORM-v1 wspiera automatyczne mapowanie **navigation properties** (właściwości nawigacyjnych), które reprezentują relacje między encjami. System automatycznie wykrywa te relacje i odpowiednio je konfiguruje.

## Atrybuty

### `[ForeignKey(propertyName)]`

Główny atrybut używany do definiowania navigation properties.

**Parametry:**
- `propertyName` - nazwa właściwości zawierającej wartość klucza obcego (FK)

**Użycie:**
```csharp
public class Order
{
    public int Id { get; set; }
    
    public int CustomerId { get; set; }  // Klucz obcy
    
    [ForeignKey("CustomerId")]           // Wskazuje na właściwość FK
    public Customer Customer { get; set; }
}
```

## Wzorce relacji

### 1. ONE-TO-MANY (Jeden do wielu)

**Kolekcje są automatycznie rozpoznawane** jako navigation properties bez potrzeby używania atrybutów.

```csharp
[Table("Blogs")]
public class Blog
{
    [Key]
    public int Id { get; set; }
    
    public string Name { get; set; }
    
    // Automatycznie wykrywana jako navigation property
    public List<Post> Posts { get; set; } = new();
}
```

**Wymagania:**
- Właściwość musi być typu `IEnumerable<T>` (np. `List<T>`, `ICollection<T>`)
- `T` musi być typem encji (klasą oznaczoną `[Table]`)

### 2. MANY-TO-ONE (Wiele do jednego)

Wymaga atrybutu `[ForeignKey]` wskazującego na właściwość z kluczem obcym.

```csharp
[Table("Posts")]
public class Post
{
    [Key]
    public int Id { get; set; }
    
    public string Title { get; set; }
    
    // Właściwość z kluczem obcym
    public int BlogId { get; set; }
    
    // Navigation property z atrybutem
    [ForeignKey("BlogId")]
    public Blog Blog { get; set; }
}
```

**Wymagania:**
- Właściwość FK musi istnieć w klasie (np. `BlogId`)
- Atrybut `[ForeignKey]` musi wskazywać na tę właściwość
- Właściwość FK powinna być tego samego typu co klucz główny w powiązanej encji

### 3. MULTIPLE FOREIGN KEYS (Wiele kluczy obcych)

Klasa może mieć wiele navigation properties wskazujących na różne encje.

```csharp
[Table("Comments")]
public class Comment
{
    [Key]
    public int Id { get; set; }
    
    public string Text { get; set; }
    
    // Pierwszy klucz obcy i navigation property
    public int AuthorId { get; set; }
    
    [ForeignKey("AuthorId")]
    public User Author { get; set; }
    
    // Drugi klucz obcy i navigation property
    public int PostId { get; set; }
    
    [ForeignKey("PostId")]
    public Post Post { get; set; }
}
```

**Ważne:**
- Każda navigation property musi mieć własny, unikalny klucz obcy
- Nazwy właściwości FK muszą być różne

### 4. SELF-REFERENCING (Samoodniesienie)

Encja może odnosić się do samej siebie.

```csharp
[Table("Employees")]
public class Employee
{
    [Key]
    public int Id { get; set; }
    
    public string Name { get; set; }
    
    // Opcjonalny FK do menedżera (też Employee)
    public int? ManagerId { get; set; }
    
    // Navigation property do menedżera
    [ForeignKey("ManagerId")]
    public Employee? Manager { get; set; }
    
    // Kolekcja podwładnych (inverse navigation)
    public List<Employee> Subordinates { get; set; } = new();
}
```

**Zastosowania:**
- Struktury hierarchiczne (menedżer-pracownik)
- Drzewa kategorii
- Struktury organizacyjne

### 5. OPTIONAL RELATIONSHIPS (Relacje opcjonalne)

Relacja może być opcjonalna poprzez użycie nullable FK.

```csharp
[Table("Orders")]
public class Order
{
    [Key]
    public int Id { get; set; }
    
    public decimal TotalAmount { get; set; }
    
    // Opcjonalny klucz obcy (nullable)
    public int? ShippingAddressId { get; set; }
    
    // Opcjonalna navigation property
    [ForeignKey("ShippingAddressId")]
    public Address? ShippingAddress { get; set; }
}
```

**Wymagania:**
- Właściwość FK musi być nullable (`int?`)
- Navigation property powinna być nullable (`Address?`)

## Automatyczna detekcja

System ORM automatycznie wykrywa navigation properties na podstawie:

1. **Typ właściwości:**
   - Jeśli jest to typ encji (klasa z `[Table]`) → *single navigation property*
   - Jeśli jest to `IEnumerable<EntityType>` → *collection navigation property*

2. **Atrybut `[ForeignKey]`:**
   - Wskazuje który FK odpowiada za daną navigation property
   - Pozwala ORM poprawnie skonfigurować relację

3. **Wykluczenia:**
   - Właściwości oznaczone `[Ignore]` nie są mapowane
   - `string` i `byte[]` są traktowane jako typy proste, nie navigation properties

## Właściwości PropertyMap dla navigation properties

```csharp
public class PropertyMap
{
    public bool IsNavigation { get; }      // true dla navigation properties
    public bool IsCollection { get; }      // true dla List<T>, IEnumerable<T>
    public Type? TargetType { get; }       // typ docelowej encji
    public string? ForeignKeyName { get; } // nazwa właściwości z FK
    public string? ColumnName { get; }     // null dla navigation properties
}
```

**Ważne:**
- Navigation properties **NIE mają** `ColumnName` (zawsze `null`)
- Nie są mapowane bezpośrednio na kolumny w bazie danych
- `ForeignKeyName` wskazuje na właściwość, która zawiera wartość FK

## EntityMap

Klasa `EntityMap` rozdziela właściwości na:

```csharp
public IReadOnlyList<PropertyMap> Properties { get; }           // Wszystkie
public IReadOnlyList<PropertyMap> ScalarProperties { get; }     // Tylko skalarne (FK, zwykłe pola)
public IReadOnlyList<PropertyMap> NavigationProperties { get; } // Tylko navigation
```

## Przykłady użycia

### Kompletny przykład Blog-Post-Comment

```csharp
// Blog (1) -> (*) Post
[Table("Blogs")]
public class Blog
{
    [Key]
    public int Id { get; set; }
    
    public string Name { get; set; }
    
    public List<Post> Posts { get; set; } = new();
}

// Post (*) -> (1) Blog, Post (1) -> (*) Comment
[Table("Posts")]
public class Post
{
    [Key]
    public int Id { get; set; }
    
    public string Title { get; set; }
    
    public int BlogId { get; set; }
    
    [ForeignKey("BlogId")]
    public Blog Blog { get; set; }
    
    public List<Comment> Comments { get; set; } = new();
}

// Comment (*) -> (1) Post, Comment (*) -> (1) User
[Table("Comments")]
public class Comment
{
    [Key]
    public int Id { get; set; }
    
    public string Text { get; set; }
    
    public int PostId { get; set; }
    
    [ForeignKey("PostId")]
    public Post Post { get; set; }
    
    public int AuthorId { get; set; }
    
    [ForeignKey("AuthorId")]
    public User Author { get; set; }
}
```

## Dobre praktyki

### ✅ DO (Rób tak):

1. **Zawsze inicjalizuj kolekcje:**
   ```csharp
   public List<Order> Orders { get; set; } = new();
   ```

2. **Stosuj nullable dla opcjonalnych relacji:**
   ```csharp
   public int? ManagerId { get; set; }
   public Employee? Manager { get; set; }
   ```

3. **Używaj spójnej konwencji nazewnictwa:**
   ```csharp
   public int CustomerId { get; set; }        // FK
   [ForeignKey("CustomerId")]
   public Customer Customer { get; set; }      // Navigation property
   ```

4. **Definiuj obie strony relacji (opcjonalnie):**
   ```csharp
   // W Order
   public Customer Customer { get; set; }
   
   // W Customer
   public List<Order> Orders { get; set; }
   ```

### ❌ DON'T (Unikaj):

1. **Nie używaj `[Ignore]` na navigation properties:**
   ```csharp
   // ❌ ZŁE - navigation property nie będzie działać
   [Ignore]
   public Customer Customer { get; set; }
   ```
   
   Obecnie w modelach TestApp navigation properties są oznaczane jako `[Ignore]` - to należy zmienić na `[ForeignKey]`.

2. **Nie pomijaj właściwości FK:**
   ```csharp
   // ❌ ZŁE - brak CustomerId
   [ForeignKey("CustomerId")]  // Ta właściwość nie istnieje!
   public Customer Customer { get; set; }
   ```

3. **Nie używaj złych typów dla FK:**
   ```csharp
   // ❌ ZŁE - jeśli Customer.Id to int, to CustomerId też musi być int
   public string CustomerId { get; set; }
   ```

## Migracja istniejących modeli

Jeśli masz modele z `[Ignore]` na navigation properties:

**Przed:**
```csharp
public int CategoryId { get; set; }

[Ignore]
public Category? Category { get; set; }
```

**Po:**
```csharp
public int CategoryId { get; set; }

[ForeignKey("CategoryId")]
public Category? Category { get; set; }
```

## Uruchomienie demo

Scenariusz demonstracyjny znajduje się w `TestApp/Scenarios/NavigationPropertiesDemo.cs`.

```bash
dotnet run --project TestApp
# Wybierz opcję "A" z menu
```

Demo pokazuje:
- ✓ One-to-Many relations
- ✓ Many-to-One relations  
- ✓ Multiple foreign keys
- ✓ Self-referencing relations
- ✓ Optional relations

## Obecne ograniczenia

⚠ **Uwaga:** Navigation properties są obecnie wykrywane i mapowane przez system, ale:

1. **SQL Generator jeszcze nie obsługuje:**
   - Generowania JOIN-ów dla navigation properties
   - Automatycznego ładowania powiązanych encji (eager loading)
   - Lazy loading

2. **Materialization:**
   - Navigation properties nie są automatycznie wypełniane podczas materializacji obiektów z bazy

3. **Planowane funkcje:**
   - `.Include(x => x.NavigationProperty)` dla eager loading
   - Automatyczne generowanie JOIN-ów
   - Tracking relacji w Change Tracker

Mimo tych ograniczeń, **można już teraz** dodawać atrybuty `[ForeignKey]` do klas - system poprawnie je rozpozna i będą gotowe gdy zostanie dodana pełna implementacja.

## Podsumowanie

| Wzorzec | Atrybut wymagany | Automatyczna detekcja |
|---------|------------------|----------------------|
| One-to-Many (kolekcja) | ❌ Nie | ✅ Tak |
| Many-to-One | ✅ Tak - `[ForeignKey]` | ❌ Nie |
| Multiple FK | ✅ Tak - na każdej | ❌ Nie |
| Self-referencing | ✅ Tak - `[ForeignKey]` | ❌ Nie |
| Optional | ✅ Tak - `[ForeignKey]` | ❌ Nie |

**Klucz do sukcesu:**
- Kolekcje → automatycznie
- Pojedyncze navigation properties → `[ForeignKey("PropertyName")]`
