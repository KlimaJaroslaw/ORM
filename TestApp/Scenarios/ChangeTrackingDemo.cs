using ORM_v1.core;
using ORM_v1.Configuration;
using ORM_v1.Mapping;
using TestApp.Models;

namespace TestApp.Scenarios;

/// <summary>
/// Demonstracja œledzenia zmian encji (Change Tracker)
/// </summary>
public static class ChangeTrackingDemo
{
    public static void Run()
    {
        Console.WriteLine("\n=== DEMONSTRACJA CHANGE TRACKERA ===\n");

        var connectionString = "Data Source=changetracker_demo.db;";
        var metadataStore = new MetadataStoreBuilder()
            .AddAssembly(typeof(Product).Assembly)
            .UseNamingStrategy(new PascalCaseNamingStrategy())
            .Build();

        var configuration = new DbConfiguration(connectionString, metadataStore);

        using (var context = new AppDbContext(configuration))
        {
            // Przygotowanie bazy - PRZEZ ORM!
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

            Console.WriteLine("1. Stan pocz¹tkowy Change Trackera:");
            PrintChangeTrackerState(context);

            // Dodanie nowych encji
            Console.WriteLine("\n2. Dodawanie nowych klientów:");
            var customer1 = new Customer
            {
                FirstName = "Adam",
                LastName = "Kowalski",
                Email = "adam@example.com",
                RegistrationDate = DateTime.Now,
                Status = CustomerStatus.Active
            };

            var customer2 = new Customer
            {
                FirstName = "Ewa",
                LastName = "Nowak",
                Email = "ewa@example.com",
                RegistrationDate = DateTime.Now,
                Status = CustomerStatus.Premium
            };

            context.Customers.Add(customer1);
            context.Customers.Add(customer2);

            Console.WriteLine("   Stan po dodaniu (przed SaveChanges):");
            PrintChangeTrackerState(context);

            context.SaveChanges();

            Console.WriteLine("\n   Stan po SaveChanges:");
            PrintChangeTrackerState(context);

            // Modyfikacja encji
            Console.WriteLine("\n3. Modyfikacja klienta:");
            customer1.Email = "adam.kowalski@newmail.com";
            customer1.Status = CustomerStatus.Premium;
            context.Customers.Update(customer1);

            Console.WriteLine("   Stan po modyfikacji (przed SaveChanges):");
            PrintChangeTrackerState(context);

            context.SaveChanges();

            Console.WriteLine("\n   Stan po SaveChanges:");
            PrintChangeTrackerState(context);

            // Usuwanie encji
            Console.WriteLine("\n4. Usuwanie klienta:");
            context.Customers.Remove(customer2);

            Console.WriteLine("   Stan po oznaczeniu do usuniêcia (przed SaveChanges):");
            PrintChangeTrackerState(context);

            context.SaveChanges();

            Console.WriteLine("\n   Stan po SaveChanges:");
            PrintChangeTrackerState(context);

            // Wyszukiwanie i œledzenie
            Console.WriteLine("\n5. Wyszukiwanie i automatyczne œledzenie:");
            var foundCustomer = context.Customers.Find(customer1.Id);
            
            Console.WriteLine($"   Znaleziono: {foundCustomer?.FullName}");
            Console.WriteLine("   Stan Change Trackera:");
            PrintChangeTrackerState(context);

            // Pobieranie wszystkich i œledzenie
            Console.WriteLine("\n6. Pobieranie wszystkich rekordów:");
            var allCustomers = context.Customers.All().ToList();
            Console.WriteLine($"   Pobrano: {allCustomers.Count} klientów");
            Console.WriteLine("   Stan Change Trackera:");
            PrintChangeTrackerState(context);
        }

        Console.WriteLine("\n=== Change Tracker Demo zakoñczone ===");
    }

    private static void PrintChangeTrackerState(AppDbContext context)
    {
        var entries = context.ChangeTracker.Entries.ToList();
        Console.WriteLine($"   Wszystkich encji: {entries.Count}");
        Console.WriteLine($"   - Added: {entries.Count(e => e.State == EntityState.Added)}");
        Console.WriteLine($"   - Unchanged: {entries.Count(e => e.State == EntityState.Unchanged)}");
        Console.WriteLine($"   - Modified: {entries.Count(e => e.State == EntityState.Modified)}");
        Console.WriteLine($"   - Deleted: {entries.Count(e => e.State == EntityState.Deleted)}");
    }
}
