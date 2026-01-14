// See https://aka.ms/new-console-template for more information
using ORM_v1.Configuration;
using ORM_v1.core;
using ORM_v1.Mapping;
using TestApp;
using TestApp.Models;
using TestApp.Scenarios;

namespace TestApp;

class Program
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║     ORM-v1 - Kompleksowa Aplikacja Demonstracyjna        ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");

        while (true)
        {
            Console.WriteLine("\n┌────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ Wybierz scenariusz demonstracyjny:                    │");
            Console.WriteLine("├────────────────────────────────────────────────────────┤");
            Console.WriteLine("│ 1. Pełna demonstracja - wszystkie możliwości ORM      │");
            Console.WriteLine("│ 2. Podstawowe operacje CRUD                            │");
            Console.WriteLine("│ 3. Change Tracker - śledzenie zmian                    │");
            Console.WriteLine("│ 4. Typy danych (Enum, DateTime, Decimal)              │");
            Console.WriteLine("│ 5. Atrybuty mapowania                                  │");
            Console.WriteLine("│ 6. Transakcje i SaveChanges                            │");
            Console.WriteLine("│ 0. Wyjście                                             │");
            Console.WriteLine("└────────────────────────────────────────────────────────┘");
            Console.Write("\nTwój wybór: ");

            var choice = Console.ReadLine();

            Console.Clear();

            switch (choice)
            {
                case "1":
                    RunFullDemo();
                    break;
                case "2":
                    BasicCrudDemo.Run();
                    break;
                case "3":
                    ChangeTrackingDemo.Run();
                    break;
                case "4":
                    DataTypesDemo.Run();
                    break;
                case "5":
                    AttributeMappingDemo.Run();
                    break;
                case "6":
                    TransactionDemo.Run();
                    break;
                case "0":
                    Console.WriteLine("\nDziękujemy za użycie ORM-v1 Demo!");
                    return;
                default:
                    Console.WriteLine("\n⚠ Nieprawidłowy wybór. Spróbuj ponownie.");
                    continue;
            }

            Console.WriteLine("\n\nNaciśnij dowolny klawisz, aby wrócić do menu...");
            Console.ReadKey();
            Console.Clear();
        }
    }

    static void RunFullDemo()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("  PEŁNA DEMONSTRACJA - Wszystkie możliwości ORM-v1");
        Console.WriteLine("═══════════════════════════════════════════════════════════\n");

        // 1. Konfiguracja i inicjalizacja
        var connectionString = "Data Source=demo.db;";
        var metadataStore = new MetadataStoreBuilder()
            .AddAssembly(typeof(Product).Assembly)
            .UseNamingStrategy(new PascalCaseNamingStrategy())
            .Build();

        var configuration = new DbConfiguration(connectionString, metadataStore);

        using (var context = new AppDbContext(configuration))
        {
            // 2. Tworzenie schematu bazy danych - PRZEZ ORM!
            Console.WriteLine("┌─ 1. TWORZENIE SCHEMATU BAZY DANYCH ───────────────────┐");
            context.Database.EnsureDeleted();  // Czyścimy starą bazę
            context.Database.EnsureCreated();  // Tworzymy nową
            Console.WriteLine("│ ✓ Schemat utworzony (4 tabele)                        │");
            Console.WriteLine("└────────────────────────────────────────────────────────┘\n");

            // 3. Dodawanie danych - Kategorie
            Console.WriteLine("┌─ 2. DODAWANIE KATEGORII PRODUKTÓW ────────────────────┐");
            var electronics = new Category 
            { 
                Name = "Elektronika", 
                Description = "Urządzenia elektroniczne i akcesoria" 
            };
            var books = new Category 
            { 
                Name = "Książki", 
                Description = "Książki i publikacje" 
            };
            var clothing = new Category 
            { 
                Name = "Odzież", 
                Description = "Ubrania i akcesoria" 
            };

            context.Categories.Add(electronics);
            context.Categories.Add(books);
            context.Categories.Add(clothing);
            context.SaveChanges();
            Console.WriteLine($"│ ✓ Dodano 3 kategorie (IDs: {electronics.Id}, {books.Id}, {clothing.Id})        │");
            Console.WriteLine("└────────────────────────────────────────────────────────┘\n");

            // 4. Dodawanie produktów
            Console.WriteLine("┌─ 3. DODAWANIE PRODUKTÓW ──────────────────────────────┐");
            var products = new[]
            {
                new Product { Name = "Laptop Dell XPS", Price = 5999.99m, Stock = 10, CategoryId = electronics.Id },
                new Product { Name = "Smartfon Samsung", Price = 2499.00m, Stock = 25, CategoryId = electronics.Id },
                new Product { Name = "Słuchawki Sony", Price = 399.99m, Stock = 50, CategoryId = electronics.Id },
                new Product { Name = "Clean Code", Price = 89.99m, Stock = 100, CategoryId = books.Id },
                new Product { Name = "Design Patterns", Price = 119.99m, Stock = 75, CategoryId = books.Id },
                new Product { Name = "Koszulka bawełniana", Price = 49.99m, Stock = 200, CategoryId = clothing.Id }
            };

            foreach (var product in products)
            {
                context.Products.Add(product);
            }
            context.SaveChanges();
            Console.WriteLine($"│ ✓ Dodano {products.Length} produktów                                 │");
            Console.WriteLine("└────────────────────────────────────────────────────────┘\n");

            // 5. Dodawanie klientów
            Console.WriteLine("┌─ 4. DODAWANIE KLIENTÓW ───────────────────────────────┐");
            var customers = new[]
            {
                new Customer 
                { 
                    FirstName = "Jan", 
                    LastName = "Kowalski", 
                    Email = "jan.kowalski@example.com",
                    RegistrationDate = DateTime.Now.AddMonths(-6),
                    Status = CustomerStatus.Premium
                },
                new Customer 
                { 
                    FirstName = "Anna", 
                    LastName = "Nowak", 
                    Email = "anna.nowak@example.com",
                    RegistrationDate = DateTime.Now.AddMonths(-3),
                    Status = CustomerStatus.Active
                },
                new Customer 
                { 
                    FirstName = "Piotr", 
                    LastName = "Wiśniewski", 
                    Email = "piotr.wisniewski@example.com",
                    RegistrationDate = DateTime.Now.AddDays(-10),
                    Status = CustomerStatus.Active
                }
            };

            foreach (var customer in customers)
            {
                context.Customers.Add(customer);
            }
            context.SaveChanges();
            Console.WriteLine($"│ ✓ Dodano {customers.Length} klientów                                │");
            Console.WriteLine("└────────────────────────────────────────────────────────┘\n");

            // 6. Dodawanie zamówień
            Console.WriteLine("┌─ 5. DODAWANIE ZAMÓWIEŃ ───────────────────────────────┐");
            var orders = new[]
            {
                new Order 
                { 
                    CustomerId = customers[0].Id, 
                    OrderDate = DateTime.Now.AddDays(-5),
                    TotalAmount = 6489.97m 
                },
                new Order 
                { 
                    CustomerId = customers[1].Id, 
                    OrderDate = DateTime.Now.AddDays(-2),
                    TotalAmount = 209.98m 
                },
                new Order 
                { 
                    CustomerId = customers[2].Id, 
                    OrderDate = DateTime.Now.AddDays(-1),
                    TotalAmount = 2499.00m 
                }
            };

            foreach (var order in orders)
            {
                context.Orders.Add(order);
            }
            context.SaveChanges();
            Console.WriteLine($"│ ✓ Dodano {orders.Length} zamówień                                 │");
            Console.WriteLine("└────────────────────────────────────────────────────────┘\n");

            // 7. Odczytywanie danych - Find
            Console.WriteLine("┌─ 6. WYSZUKIWANIE PO ID (Find) ────────────────────────┐");
            var foundProduct = context.Products.Find(1);
            if (foundProduct != null)
            {
                Console.WriteLine($"│ ✓ Produkt: {foundProduct.Name,-35}│");
                Console.WriteLine($"│   Cena: {foundProduct.Price:C}, Stock: {foundProduct.Stock,-25}│");
            }

            var foundCustomer = context.Customers.Find(2);
            if (foundCustomer != null)
            {
                Console.WriteLine($"│ ✓ Klient: {foundCustomer.FullName,-36}│");
                Console.WriteLine($"│   Email: {foundCustomer.Email,-37}│");
            }
            Console.WriteLine("└────────────────────────────────────────────────────────┘\n");

            // 8. Pobieranie wszystkich rekordów
            Console.WriteLine("┌─ 7. POBIERANIE WSZYSTKICH PRODUKTÓW (All) ────────────┐");
            var allProducts = context.Products.All().ToList();
            Console.WriteLine($"│ ✓ Liczba produktów: {allProducts.Count}                              │");
            foreach (var p in allProducts.Take(3))
            {
                Console.WriteLine($"│   • {p.Name,-30} {p.Price,10:C} │");
            }
            Console.WriteLine($"│   ... i {allProducts.Count - 3} więcej                                    │");
            Console.WriteLine("└────────────────────────────────────────────────────────┘\n");

            // 9. Aktualizacja danych
            Console.WriteLine("┌─ 8. AKTUALIZACJA DANYCH ──────────────────────────────┐");
            var productToUpdate = context.Products.Find(1);
            if (productToUpdate != null)
            {
                var oldStock = productToUpdate.Stock;
                var oldPrice = productToUpdate.Price;
                
                productToUpdate.Stock = 15;
                productToUpdate.Price = 5799.99m;
                context.Products.Update(productToUpdate);
                context.SaveChanges();
                
                Console.WriteLine($"│ ✓ {productToUpdate.Name,-44}│");
                Console.WriteLine($"│   Stock: {oldStock} → {productToUpdate.Stock}                                    │");
                Console.WriteLine($"│   Cena: {oldPrice:C} → {productToUpdate.Price:C}                    │");
            }
            Console.WriteLine("└────────────────────────────────────────────────────────┘\n");

            // 10. Aktualizacja statusu klienta
            Console.WriteLine("┌─ 9. ZMIANA STATUSU KLIENTA ───────────────────────────┐");
            var customerToUpdate = context.Customers.Find(3);
            if (customerToUpdate != null)
            {
                var oldStatus = customerToUpdate.Status;
                customerToUpdate.Status = CustomerStatus.Premium;
                context.Customers.Update(customerToUpdate);
                context.SaveChanges();
                
                Console.WriteLine($"│ ✓ {customerToUpdate.FullName,-44}│");
                Console.WriteLine($"│   Status: {oldStatus} → {customerToUpdate.Status}                  │");
            }
            Console.WriteLine("└────────────────────────────────────────────────────────┘\n");

            // 11. Usuwanie danych
            Console.WriteLine("┌─ 10. USUWANIE PRODUKTU ───────────────────────────────┐");
            var productToDelete = context.Products.Find(6);
            if (productToDelete != null)
            {
                Console.WriteLine($"│ Usuwanie: {productToDelete.Name,-37}│");
                context.Products.Remove(productToDelete);
                context.SaveChanges();
                Console.WriteLine($"│ ✓ Produkt usunięty                                    │");
            }
            Console.WriteLine("└────────────────────────────────────────────────────────┘\n");

            // 12. Weryfikacja usunięcia
            Console.WriteLine("┌─ 11. WERYFIKACJA USUNIĘCIA ───────────────────────────┐");
            var deletedProduct = context.Products.Find(6);
            var status = deletedProduct == null ? "NIE ZNALEZIONO ✓" : "NADAL ISTNIEJE ✗";
            Console.WriteLine($"│ Produkt ID=6: {status,-37}│");
            Console.WriteLine("└────────────────────────────────────────────────────────┘\n");

            // 13. Podsumowanie danych
            Console.WriteLine("┌─ 12. PODSUMOWANIE BAZY DANYCH ────────────────────────┐");
            Console.WriteLine($"│ • Kategorie:  {context.Categories.All().Count(),3}                                  │");
            Console.WriteLine($"│ • Produkty:   {context.Products.All().Count(),3}                                  │");
            Console.WriteLine($"│ • Klienci:    {context.Customers.All().Count(),3}                                  │");
            Console.WriteLine($"│ • Zamówienia: {context.Orders.All().Count(),3}                                  │");
            Console.WriteLine("└────────────────────────────────────────────────────────┘\n");

            // 14. Demonstracja Change Trackera
            Console.WriteLine("┌─ 13. CHANGE TRACKER ──────────────────────────────────┐");
            var trackedCount = context.ChangeTracker.Entries.Count();
            var modifiedCount = context.ChangeTracker.Entries.Count(e => e.State == EntityState.Modified);
            var unchangedCount = context.ChangeTracker.Entries.Count(e => e.State == EntityState.Unchanged);
            
            Console.WriteLine($"│ • Śledzonych encji:    {trackedCount,3}                            │");
            Console.WriteLine($"│ • Zmienionych:         {modifiedCount,3}                            │");
            Console.WriteLine($"│ • Niemodyfikowanych:   {unchangedCount,3}                            │");
            Console.WriteLine("└────────────────────────────────────────────────────────┘\n");

            // 15. Wyświetlanie kategorii z produktami
            Console.WriteLine("┌─ 14. KATEGORIE Z PRODUKTAMI ──────────────────────────┐");
            var allCategories = context.Categories.All().ToList();
            var allProductsList = context.Products.All().ToList();
            
            foreach (var cat in allCategories)
            {
                var productCount = allProductsList.Count(p => p.CategoryId == cat.Id);
                Console.WriteLine($"│ • {cat.Name,-30} ({productCount} produktów) │");
            }
            Console.WriteLine("└────────────────────────────────────────────────────────┘\n");

            // 16. Klienci z zamówieniami
            Console.WriteLine("┌─ 15. KLIENCI Z ZAMÓWIENIAMI ──────────────────────────┐");
            var allCustomers = context.Customers.All().ToList();
            var allOrders = context.Orders.All().ToList();
            
            foreach (var cust in allCustomers)
            {
                var customerOrders = allOrders.Where(o => o.CustomerId == cust.Id).ToList();
                var totalSpent = customerOrders.Sum(o => o.TotalAmount);
                Console.WriteLine($"│ • {cust.FullName,-25} │");
                Console.WriteLine($"│   {customerOrders.Count} zamówień, wartość: {totalSpent,10:C}          │");
            }
            Console.WriteLine("└────────────────────────────────────────────────────────┘\n");
        }

        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("  ✓ Demonstracja zakończona pomyślnie!");
        Console.WriteLine("  📁 Plik bazy danych: demo.db");
        Console.WriteLine("═══════════════════════════════════════════════════════════");
    }
}

