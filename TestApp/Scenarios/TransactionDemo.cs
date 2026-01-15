using ORM_v1.core;
using ORM_v1.Configuration;
using ORM_v1.Mapping;
using TestApp.Models;

namespace TestApp.Scenarios;

/// <summary>
/// Demonstracja transakcji i obs�ugi b��d�w w SaveChanges
/// </summary>
public static class TransactionDemo
{
    public static void Run()
    {
        Console.WriteLine("\n=== DEMONSTRACJA TRANSAKCJI ===\n");

        var connectionString = "Data Source=transaction_demo.db;";
        var metadataStore = new MetadataStoreBuilder()
            .AddAssembly(typeof(Product).Assembly)
            .UseNamingStrategy(new PascalCaseNamingStrategy())
            .Build();

        var configuration = new DbConfiguration(connectionString, metadataStore);

        using (var context = new AppDbContext(configuration))
        {
            // Przygotowanie bazy
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

            Console.WriteLine("1. Pomy�lna transakcja - wiele operacji atomowo:");
            
            var category = new Category
            {
                Name = "Elektronika",
                Description = "Kategoria testowa"
            };

            var products = new[]
            {
                new Product { Name = "Laptop", Price = 3000m, Stock = 5, CategoryId = 1 },
                new Product { Name = "Monitor", Price = 800m, Stock = 10, CategoryId = 1 },
                new Product { Name = "Klawiatura", Price = 200m, Stock = 20, CategoryId = 1 }
            };

            // Wszystkie operacje w jednej transakcji
            context.Categories.Add(category);
            foreach (var product in products)
            {
                context.Products.Add(product);
            }

            Console.WriteLine("   Przed SaveChanges:");
            Console.WriteLine($"   - �ledzonych encji: {context.ChangeTracker.Entries.Count()}");
            Console.WriteLine($"   - Do dodania: {context.ChangeTracker.Entries.Count(e => e.State == EntityState.Added)}");

            context.SaveChanges();

            Console.WriteLine("\n   Po SaveChanges:");
            Console.WriteLine($"   - Kategoria dodana z ID: {category.Id}");
            foreach (var p in products)
            {
                Console.WriteLine($"   - Produkt '{p.Name}' dodany z ID: {p.Id}");
            }

            Console.WriteLine("\n2. Wiele operacji r�nego typu w jednej transakcji:");
            
            // Dodanie
            var newProduct = new Product 
            { 
                Name = "Mysz", 
                Price = 100m, 
                Stock = 50, 
                CategoryId = category.Id 
            };
            context.Products.Add(newProduct);

            // Modyfikacja
            products[0].Price = 2800m;
            context.Products.Update(products[0]);

            // Usuniecie
            context.Products.Remove(products[2]);

            Console.WriteLine("   Operacje w kolejce:");
            Console.WriteLine($"   - Dodanie: {context.ChangeTracker.Entries.Count(e => e.State == EntityState.Added)}");
            Console.WriteLine($"   - Modyfikacja: {context.ChangeTracker.Entries.Count(e => e.State == EntityState.Modified)}");
            Console.WriteLine($"   - Usuni�cie: {context.ChangeTracker.Entries.Count(e => e.State == EntityState.Deleted)}");

            context.SaveChanges();

            Console.WriteLine("\n   ? Wszystkie operacje wykonane atomowo");

            // Weryfikacja
            var allProducts = context.Products.All().ToList();
            Console.WriteLine($"   Produkty w bazie: {allProducts.Count}");
            foreach (var p in allProducts)
            {
                Console.WriteLine($"   - {p.Name}: {p.Price:C}");
            }

            Console.WriteLine("\n3. Brak zmian - SaveChanges bez efektu:");
            var productNoChange = context.Products.Find(1);
            Console.WriteLine($"   Pobrany produkt: {productNoChange?.Name}");
            Console.WriteLine($"   Czy s� zmiany: {context.ChangeTracker.HasChanges()}");
            
            context.SaveChanges();
            Console.WriteLine("   ? SaveChanges wykonane, ale bez operacji (brak zmian)");

            Console.WriteLine("\n4. Wielokrotne SaveChanges:");
            
            var customer1 = new Customer
            {
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = "jan@example.com",
                RegistrationDate = DateTime.Now,
                Status = CustomerStatus.Active
            };

            context.Customers.Add(customer1);
            context.SaveChanges();
            Console.WriteLine($"   ? Pierwszy SaveChanges - dodano klienta ID: {customer1.Id}");

            customer1.Status = CustomerStatus.Premium;
            context.Customers.Update(customer1);
            context.SaveChanges();
            Console.WriteLine($"   ? Drugi SaveChanges - zaktualizowano status: {customer1.Status}");

            var customer2 = new Customer
            {
                FirstName = "Anna",
                LastName = "Nowak",
                Email = "anna@example.com",
                RegistrationDate = DateTime.Now,
                Status = CustomerStatus.Active
            };

            context.Customers.Add(customer2);
            context.SaveChanges();
            Console.WriteLine($"   ? Trzeci SaveChanges - dodano klienta ID: {customer2.Id}");

            Console.WriteLine("\n5. Demonstracja rollback (symulacja b��du):");
            Console.WriteLine("   ORM automatycznie wykonuje rollback w przypadku b��du");
            Console.WriteLine("   Je�li jedna operacja w SaveChanges si� nie powiedzie,");
            Console.WriteLine("   wszystkie operacje s� wycofywane (transakcja atomowa)");

            Console.WriteLine("\n6. Podsumowanie transakcji:");
            Console.WriteLine($"   - Kategorie: {context.Categories.All().Count()}");
            Console.WriteLine($"   - Produkty: {context.Products.All().Count()}");
            Console.WriteLine($"   - Klienci: {context.Customers.All().Count()}");
            Console.WriteLine("\n   Wszystkie operacje wykonane w kontrolowanych transakcjach");
        }

        Console.WriteLine("\n=== Transaction Demo zako�czone ===");
    }
}
