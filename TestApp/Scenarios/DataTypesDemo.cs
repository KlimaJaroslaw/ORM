using ORM_v1.core;
using ORM_v1.Configuration;
using ORM_v1.Mapping;
using TestApp.Models;

namespace TestApp.Scenarios;

/// <summary>
/// Demonstracja pracy z r�nymi typami danych: Enum, DateTime, Decimal
/// </summary>
public static class DataTypesDemo
{
    public static void Run()
    {
        Console.WriteLine("\n=== DEMONSTRACJA TYP�W DANYCH ===\n");

        var connectionString = "Data Source=datatypes_demo.db;";
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

            Console.WriteLine("1. Praca z typem ENUM (CustomerStatus):");
            var customers = new[]
            {
                new Customer
                {
                    FirstName = "Jan",
                    LastName = "Kowalski",
                    Email = "jan@example.com",
                    RegistrationDate = DateTime.Now,
                    Status = CustomerStatus.Inactive
                },
                new Customer
                {
                    FirstName = "Anna",
                    LastName = "Nowak",
                    Email = "anna@example.com",
                    RegistrationDate = DateTime.Now.AddMonths(-6),
                    Status = CustomerStatus.Active
                },
                new Customer
                {
                    FirstName = "Piotr",
                    LastName = "Wi�niewski",
                    Email = "piotr@example.com",
                    RegistrationDate = DateTime.Now.AddYears(-1),
                    Status = CustomerStatus.Premium
                }
            };

            foreach (var customer in customers)
            {
                context.Customers.Add(customer);
            }
            context.SaveChanges();

            Console.WriteLine("   Statusy klient�w:");
            foreach (var customer in customers)
            {
                Console.WriteLine($"   - {customer.FullName}: {customer.Status} ({(int)customer.Status})");
            }

            Console.WriteLine("\n2. Praca z typem DATETIME:");
            var allCustomers = context.Customers.All().ToList();
            foreach (var customer in allCustomers)
            {
                var daysSinceRegistration = (DateTime.Now - customer.RegistrationDate).Days;
                Console.WriteLine($"   - {customer.FullName}: zarejestrowany {daysSinceRegistration} dni temu");
            }

            Console.WriteLine("\n3. Praca z typem DECIMAL (ceny produkt�w):");
            var products = new[]
            {
                new Product { Name = "Produkt A", Price = 99.99m, Stock = 10, CategoryId = 1 },
                new Product { Name = "Produkt B", Price = 1234.56m, Stock = 5, CategoryId = 1 },
                new Product { Name = "Produkt C", Price = 0.99m, Stock = 100, CategoryId = 1 },
                new Product { Name = "Produkt D", Price = 9999.99m, Stock = 1, CategoryId = 1 }
            };

            foreach (var product in products)
            {
                context.Products.Add(product);
            }
            context.SaveChanges();

            Console.WriteLine("   Produkty z cenami:");
            var allProducts = context.Products.All().ToList();
            var totalValue = 0m;
            foreach (var product in allProducts)
            {
                var stockValue = product.Price * product.Stock;
                totalValue += stockValue;
                Console.WriteLine($"   - {product.Name}: {product.Price:C} � {product.Stock} = {stockValue:C}");
            }
            Console.WriteLine($"   ��czna warto�� magazynu: {totalValue:C}");

            Console.WriteLine("\n4. Aktualizacja r�nych typ�w danych:");
            var customerToUpdate = context.Customers.Find(1);
            if (customerToUpdate != null)
            {
                Console.WriteLine($"   Przed:");
                Console.WriteLine($"   - Status: {customerToUpdate.Status}");
                Console.WriteLine($"   - Data rejestracji: {customerToUpdate.RegistrationDate:yyyy-MM-dd HH:mm:ss}");

                customerToUpdate.Status = CustomerStatus.Premium;
                customerToUpdate.RegistrationDate = DateTime.Now.AddMonths(-12);
                context.Customers.Update(customerToUpdate);
                context.SaveChanges();

                var updated = context.Customers.Find(1);
                Console.WriteLine($"\n   Po:");
                Console.WriteLine($"   - Status: {updated?.Status}");
                Console.WriteLine($"   - Data rejestracji: {updated?.RegistrationDate:yyyy-MM-dd HH:mm:ss}");
            }

            Console.WriteLine("\n5. Filtrowanie po typach (r�czne):");
            var premiumCustomers = context.Customers.All()
                .Where(c => c.Status == CustomerStatus.Premium)
                .ToList();
            Console.WriteLine($"   Klienci Premium: {premiumCustomers.Count}");
            foreach (var c in premiumCustomers)
            {
                Console.WriteLine($"   - {c.FullName}");
            }

            var expensiveProducts = context.Products.All()
                .Where(p => p.Price > 1000m)
                .ToList();
            Console.WriteLine($"\n   Produkty dro�sze ni� 1000 PLN: {expensiveProducts.Count}");
            foreach (var p in expensiveProducts)
            {
                Console.WriteLine($"   - {p.Name}: {p.Price:C}");
            }
        }

        Console.WriteLine("\n=== Data Types Demo zako�czone ===");
    }
}
