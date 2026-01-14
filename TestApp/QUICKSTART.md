# Szybki Start - ORM-v1 Demo

## Uruchomienie aplikacji

```bash
cd TestApp
dotnet run
```

## Co zobaczysz?

Po uruchomieniu pojawi siê menu z opcjami:

```
??????????????????????????????????????????????????????????????
?     ORM-v1 - Kompleksowa Aplikacja Demonstracyjna        ?
??????????????????????????????????????????????????????????????

??????????????????????????????????????????????????????????
? Wybierz scenariusz demonstracyjny:                    ?
??????????????????????????????????????????????????????????
? 1. Pe³na demonstracja - wszystkie mo¿liwoœci ORM      ?
? 2. Podstawowe operacje CRUD                            ?
? 3. Change Tracker - œledzenie zmian                    ?
? 4. Typy danych (Enum, DateTime, Decimal)              ?
? 5. Atrybuty mapowania                                  ?
? 6. Transakcje i SaveChanges                            ?
? 0. Wyjœcie                                             ?
??????????????????????????????????????????????????????????
```

## Rekomendowane kolejnoœæ scenariuszy

Dla najlepszego zrozumienia ORM-v1, zalecamy nastêpuj¹c¹ kolejnoœæ:

1. **Opcja 5** - Atrybuty mapowania
   - Zrozumienie jak encje s¹ mapowane na tabele
   - Poznanie atrybutów [Table], [Column], [Key], [Ignore]

2. **Opcja 2** - Podstawowe operacje CRUD
   - Dodawanie, odczytywanie, aktualizacja, usuwanie
   - Podstawy pracy z DbContext

3. **Opcja 3** - Change Tracker
   - Jak ORM œledzi zmiany
   - Stany encji (Added, Modified, Deleted, Unchanged)

4. **Opcja 4** - Typy danych
   - Praca z Enum, DateTime, Decimal
   - Filtrowanie i agregacje

5. **Opcja 6** - Transakcje
   - Atomowoœæ operacji
   - Rollback w przypadku b³êdów

6. **Opcja 1** - Pe³na demonstracja
   - Wszystkie mo¿liwoœci w jednym przyk³adzie
   - Realistyczny scenariusz biznesowy

## Kluczowe Funkcje do Przetestowania

### 1. Tworzenie bazy i tabel (PRZEZ ORM!)
```csharp
using var context = new AppDbContext(configuration);

// Usuñ star¹ bazê (opcjonalnie)
context.Database.EnsureDeleted();

// Utwórz schemat
context.Database.EnsureCreated();
```

**? W³aœciwe podejœcie** - jak w Entity Framework!  
**? Nie u¿ywamy** bezpoœrednio `SqliteConnection`

### 2. Dodawanie danych
```csharp
var product = new Product { Name = "Laptop", Price = 3000m, Stock = 10 };
context.Products.Add(product);
context.SaveChanges();
// ID jest automatycznie przypisywane!
Console.WriteLine($"Nowy ID: {product.Id}");
```

### 3. Wyszukiwanie
```csharp
// Po ID
var product = context.Products.Find(1);

// Wszystkie rekordy
var allProducts = context.Products.All().ToList();
```

### 4. Aktualizacja
```csharp
var product = context.Products.Find(1);
product.Price = 2800m;
context.Products.Update(product);
context.SaveChanges();
```

### 5. Usuwanie
```csharp
var product = context.Products.Find(1);
context.Products.Remove(product);
context.SaveChanges();
```

### 6. Change Tracker
```csharp
// SprawdŸ czy s¹ zmiany
if (context.ChangeTracker.HasChanges())
{
    context.SaveChanges();
}

// Zobacz œledzone encje
foreach (var entry in context.ChangeTracker.Entries)
{
    Console.WriteLine($"{entry.Entity.GetType().Name}: {entry.State}");
}
```

## Pliki Utworzone

Po uruchomieniu aplikacji zostan¹ utworzone pliki bazy danych:
- `demo.db` - g³ówna baza demonstracyjna
- `crud_demo.db` - demo CRUD
- `changetracker_demo.db` - demo Change Trackera
- `datatypes_demo.db` - demo typów danych
- `mapping_demo.db` - demo mapowania
- `transaction_demo.db` - demo transakcji

Mo¿esz je otworzyæ w SQLite Browser, aby zobaczyæ strukturê tabel i dane.

## Najczêstsze Pytania

**Q: Jak dodaæ now¹ encjê?**  
A: Utwórz klasê z atrybutami [Table] i [Key], dodaj DbSet<T> w AppDbContext, uruchom `context.Database.EnsureCreated()`.

**Q: Jak dzia³a automatyczne ID?**  
A: W³aœciwoœæ z atrybutem [Key] typu int jest automatycznie ustawiana jako AUTOINCREMENT w SQLite.

**Q: Czy mogê u¿yæ ró¿nych nazw kolumn?**  
A: Tak! U¿yj atrybutu [Column("nazwa_kolumny")].

**Q: Co to jest Change Tracker?**  
A: Mechanizm œledz¹cy zmiany w encjach, dziêki czemu SaveChanges() wie, które operacje SQL wykonaæ.

**Q: Czy wszystkie operacje s¹ transakcyjne?**  
A: Tak! Ka¿de wywo³anie SaveChanges() wykonuje wszystkie operacje w jednej transakcji.

**Q: Czy muszê u¿ywaæ SqliteConnection bezpoœrednio?**  
A: **NIE!** To jest w³aœnie przewaga ORM - wszystko robisz przez `context.Database`.

## Porównanie z Entity Framework

| Operacja | Entity Framework | ORM-v1 |
|----------|------------------|---------|
| Tworzenie bazy | `context.Database.EnsureCreated()` | `context.Database.EnsureCreated()` ? |
| Usuwanie bazy | `context.Database.EnsureDeleted()` | `context.Database.EnsureDeleted()` ? |
| Dodawanie | `context.Products.Add(product)` | `context.Products.Add(product)` ? |
| Zapis | `context.SaveChanges()` | `context.SaveChanges()` ? |
| Find | `context.Products.Find(id)` | `context.Products.Find(id)` ? |

**ORM-v1 u¿ywa tego samego API co Entity Framework!** ??

## Wsparcie

Wiêcej informacji w plikach:
- `TestApp/README.md` - pe³na dokumentacja
- Kod Ÿród³owy w `TestApp/Scenarios/` - przyk³ady u¿ycia
- Testy w `ORM.Tests/` - dodatkowe przyk³ady

---

**Mi³ego testowania ORM-v1! ??**

**PAMIÊTAJ:** Aplikacja **NIE** u¿ywa bezpoœrednio SQLite - wszystko przez ORM API!
