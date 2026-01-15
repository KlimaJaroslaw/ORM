using ORM_v1.core;
using ORM_v1.Configuration;
using ORM_v1.Mapping;
using TestApp.Models;

namespace TestApp.Scenarios;

/// <summary>
/// Demonstracja podstawowych operacji CRUD (Create, Read, Update, Delete)
/// </summary>
public static class BasicCrudDemo
{
    public static void Run()
    {
        Console.WriteLine("\n=== DEMONSTRACJA CRUD - Podstawowe operacje ===\n");

        var connectionString = "Data Source=crud_demo.db;";
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

            // CREATE - Dodawanie nowych encji
            Console.WriteLine("1. CREATE - Dodawanie nowych produkt�w:");
            var laptop = new Product
            {
                Name = "Laptop HP",
                Price = 3499.99m,
                Stock = 5,
                CategoryId = 1
            };

            var mouse = new Product
            {
                Name = "Mysz Logitech",
                Price = 89.99m,
                Stock = 50,
                CategoryId = 1
            };

            context.Products.Add(laptop);
            context.Products.Add(mouse);
            context.SaveChanges();

            Console.WriteLine($"   ? Dodano laptop (ID: {laptop.Id})");
            Console.WriteLine($"   ? Dodano mysz (ID: {mouse.Id})\n");

            // READ - Odczytywanie danych
            Console.WriteLine("2. READ - Wyszukiwanie produktu po ID:");
            var foundProduct = context.Products.Find(laptop.Id);
            if (foundProduct != null)
            {
                Console.WriteLine($"   Znaleziono: {foundProduct.Name}");
                Console.WriteLine($"   Cena: {foundProduct.Price:C}");
                Console.WriteLine($"   Stan magazynowy: {foundProduct.Stock}\n");
            }

            // UPDATE - Aktualizacja danych
            Console.WriteLine("3. UPDATE - Modyfikacja produktu:");
            if (foundProduct != null)
            {
                Console.WriteLine($"   Przed: Stock = {foundProduct.Stock}, Price = {foundProduct.Price:C}");
                
                foundProduct.Stock = 10;
                foundProduct.Price = 3299.99m;
                context.Products.Update(foundProduct);
                context.SaveChanges();

                Console.WriteLine($"   Po:    Stock = {foundProduct.Stock}, Price = {foundProduct.Price:C}\n");
            }

            // DELETE - Usuwanie danych
            Console.WriteLine("4. DELETE - Usuwanie produktu:");
            var productToDelete = context.Products.Find(mouse.Id);
            if (productToDelete != null)
            {
                Console.WriteLine($"   Usuwanie: {productToDelete.Name}");
                context.Products.Remove(productToDelete);
                context.SaveChanges();
                Console.WriteLine($"   ? Produkt usuni�ty\n");
            }

            // Weryfikacja
            Console.WriteLine("5. Weryfikacja stanu bazy:");
            var allProducts = context.Products.All().ToList();
            Console.WriteLine($"   Liczba produktow: {allProducts.Count}");
            foreach (var p in allProducts)
            {
                Console.WriteLine($"   - {p.Name} (ID: {p.Id})");
            }
        }

        Console.WriteLine("\n=== CRUD Demo zako�czone ===");
    }
}
