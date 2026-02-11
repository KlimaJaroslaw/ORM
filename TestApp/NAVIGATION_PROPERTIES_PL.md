# Jak używać Navigation Properties - Szybki przewodnik

## Podstawy

**Navigation Property** = właściwość klasy reprezentująca relację z inną encją (lub kolekcją encji).

## Atrybuty

### `[ForeignKey(nazwaWłaściwościFK)]`

Główny atrybut do definiowania relacji między encjami.

## Scenariusze

### 1️⃣ Jeden do wielu (One-to-Many)

**Rodzic ma kolekcję dzieci** - wykrywane automatycznie!

```csharp
public class Category
{
    [Key]
    public int Id { get; set; }
    public string Name { get; set; }
    
    // Automatycznie rozpoznane jako navigation property
    public List<Product> Products { get; set; } = new();
}
```

❗ **Nie trzeba** dodawać żadnego atrybutu dla kolekcji!

---

### 2️⃣ Wiele do jednego (Many-to-One)

**Dziecko wskazuje na rodzica** - wymaga `[ForeignKey]`

```csharp
public class Product
{
    [Key]
    public int Id { get; set; }
    public string Name { get; set; }
    
    // 1. Właściwość z wartością klucza obcego
    public int CategoryId { get; set; }
    
    // 2. Navigation property z atrybutem
    [ForeignKey("CategoryId")]
    public Category? Category { get; set; }
}
```

**Kroki:**
1. Dodaj właściwość FK (np. `CategoryId`)
2. Dodaj navigation property z atrybutem `[ForeignKey("CategoryId")]`

---

### 3️⃣ Wiele kluczy obcych

**Encja wskazuje na kilka innych encji**

```csharp
public class Comment
{
    [Key]
    public int Id { get; set; }
    
    // Pierwszy FK
    public int AuthorId { get; set; }
    [ForeignKey("AuthorId")]
    public User Author { get; set; }
    
    // Drugi FK
    public int PostId { get; set; }
    [ForeignKey("PostId")]
    public Post Post { get; set; }
}
```

---

### 4️⃣ Relacja opcjonalna

**FK może być null**

```csharp
public class Order
{
    [Key]
    public int Id { get; set; }
    
    // Opcjonalny FK (int?)
    public int? ShippingAddressId { get; set; }
    
    // Opcjonalna navigation property
    [ForeignKey("ShippingAddressId")]
    public Address? ShippingAddress { get; set; }
}
```

**Pamiętaj:** Zarówno FK jak i navigation property muszą być nullable!

---

### 5️⃣ Samoodniesienie (Self-referencing)

**Encja wskazuje na siebie**

```csharp
public class Employee
{
    [Key]
    public int Id { get; set; }
    public string Name { get; set; }
    
    // FK do menedżera (też Employee)
    public int? ManagerId { get; set; }
    
    // Navigation do menedżera
    [ForeignKey("ManagerId")]
    public Employee? Manager { get; set; }
    
    // Kolekcja podwładnych
    public List<Employee> Subordinates { get; set; } = new();
}
```

---

## Checklist przed użyciem

- [ ] **Dla kolekcji:** Nic nie rób - automatycznie wykrywane!
- [ ] **Dla pojedynczej relacji:**
  - [ ] Dodałem właściwość FK (np. `CustomerId`)
  - [ ] Dodałem navigation property
  - [ ] Oznaczyłem navigation property: `[ForeignKey("CustomerId")]`
  - [ ] Typ FK pasuje do klucza głównego w powiązanej encji
- [ ] **Dla opcjonalnych relacji:**
  - [ ] FK jest nullable (`int?`)
  - [ ] Navigation property jest nullable (`Customer?`)
- [ ] **Inicjalizacja kolekcji:**
  - [ ] `public List<Order> Orders { get; set; } = new();`

---

## Przykład kompletny

```csharp
// KATEGORIA (rodzic)
[Table("Categories")]
public class Category
{
    [Key]
    public int Id { get; set; }
    
    public string Name { get; set; }
    
    // Kolekcja - auto wykrywana
    public List<Product> Products { get; set; } = new();
}

// PRODUKT (dziecko)
[Table("Products")]
public class Product
{
    [Key]
    public int Id { get; set; }
    
    public string Name { get; set; }
    public decimal Price { get; set; }
    
    // FK + Navigation property
    public int CategoryId { get; set; }
    
    [ForeignKey("CategoryId")]
    public Category? Category { get; set; }
}
```

---

## ❌ Błędy do unikania

**1. Brak właściwości FK**
```csharp
// ❌ ZŁE
[ForeignKey("CategoryId")]  // Gdzie jest CategoryId??
public Category Category { get; set; }
```

**2. Używanie [Ignore] na navigation properties**
```csharp
// ❌ ZŁE - relacja nie będzie działać
[Ignore]
public Customer Customer { get; set; }

// ✅ DOBRZE
[ForeignKey("CustomerId")]
public Customer Customer { get; set; }
```

**3. Niezgodność typów**
```csharp
// ❌ ZŁE - jeśli Customer.Id to int, to CustomerId też musi być int
public string CustomerId { get; set; }

[ForeignKey("CustomerId")]
public Customer Customer { get; set; }
```

---

## Uruchomienie demo

```bash
cd TestApp
dotnet run
# Wybierz opcję: A (Navigation Properties)
```

---

## Status implementacji

✅ **Gotowe:**
- Automatyczna detekcja navigation properties
- Atrybut `[ForeignKey]`
- Mapowanie relacji w EntityMap
- Rozróżnienie ScalarProperties vs NavigationProperties

⏳ **W planach:**
- Eager loading (`.Include()`)
- Lazy loading
- Automatyczne JOIN-y w SQL Generator
- Materializacja powiązanych obiektów

---

## Podsumowanie

| Typ relacji | Potrzebujesz | Przykład |
|------------|--------------|----------|
| 1 → wiele (kolekcja) | Nic! | `List<Product> Products` |
| wiele → 1 | `[ForeignKey]` | `[ForeignKey("CategoryId")]` |
| Opcjonalna | `[ForeignKey]` + nullable | `int? ManagerId` |

**Pamiętaj:** Nawet jeśli SQL Generator jeszcze nie wspiera eager loading, **już teraz** możesz dodawać `[ForeignKey]` - system je rozpozna i będą gotowe na przyszłość!
