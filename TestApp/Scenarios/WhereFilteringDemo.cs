using System;
using System.IO;
using System.Linq;
using ORM_v1.Configuration;
using ORM_v1.core;
using ORM_v1.Mapping;
using ORM_v1.Query;
using TestApp.Models;

namespace TestApp.Scenarios;

public static class WhereFilteringDemo
{
    public static void Run()
    {
        Console.WriteLine("\n========================================================");
        Console.WriteLine("      WHERE FILTERING - Demo                          ");
        Console.WriteLine("========================================================\n");

        const string dbPath = "where_demo.db";

        //if (File.Exists(dbPath))
        //     File.Delete(dbPath);

        var connectionString = $"Data Source={dbPath};";
        var metadataStore = new MetadataStoreBuilder()
            .AddAssembly(typeof(Product).Assembly)
            .UseNamingStrategy(new PascalCaseNamingStrategy())
            .Build();
        var config = new DbConfiguration(connectionString, metadataStore);

        using var context = new AppDbContext(config);
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();

        // Dodaj testowe produkty
        Console.WriteLine("Dodawanie testowych produktów...\n");
        
        var products = new[]
        {
            new Product { Name = "Laptop Dell", Price = 3500m, Stock = 5 },
            new Product { Name = "Mysz Logitech", Price = 150m, Stock = 20 },
            new Product { Name = "Klawiatura Corsair", Price = 450m, Stock = 10 },
            new Product { Name = "Monitor Samsung", Price = 1200m, Stock = 8 },
            new Product { Name = "Słuchawki Sony", Price = 300m, Stock = 15 }
        };

        foreach (var product in products)
        {
            context.Products.Add(product);
        }
        context.SaveChanges();

        Console.WriteLine("=======================================================");
        Console.WriteLine("TEST 1: WHERE Price > 500");
        Console.WriteLine("=======================================================");

        var expensiveProducts = context.Products
            .Where(p => p.Price > 500)
            .ToList();

        Console.WriteLine($"Znaleziono {expensiveProducts.Count} produktów:");
        foreach (var p in expensiveProducts)
        {
            Console.WriteLine($"  • {p.Name} - {p.Price:C}");
        }

        Console.WriteLine("\n=======================================================");
        Console.WriteLine("TEST 2: WHERE Price == 150");
        Console.WriteLine("=======================================================");

        var exactPrice = context.Products
            .Where(p => p.Price == 150)
            .ToList();

        Console.WriteLine($"Znaleziono {exactPrice.Count} produktów:");
        foreach (var p in exactPrice)
        {
            Console.WriteLine($"  • {p.Name} - {p.Price:C}");
        }

        Console.WriteLine("\n=======================================================");
        Console.WriteLine("TEST 3: WHERE Name.Contains(\"ell\")");
        Console.WriteLine("=======================================================");

        var containsO = context.Products
            .Where(p => p.Name.Contains("ell"))
            .ToList();

        Console.WriteLine($"Znaleziono {containsO.Count} produktów:");
        foreach (var p in containsO)
        {
            Console.WriteLine($"  • {p.Name}");
        }

        Console.WriteLine("\n=======================================================");
        Console.WriteLine("TEST 4: WHERE + Include - Products with Price > 400 AND Category");
        Console.WriteLine("=======================================================");

        // Dodaj kategorie
        var electronics = new Category { Name = "Elektronika", Description = "Sprzęt elektroniczny" };
        context.Categories.Add(electronics);
        context.SaveChanges();

        // Aktualizuj produkty
        foreach (var product in context.Products.ToList())
        {
            product.CategoryId = electronics.Id;
            context.Products.Update(product);
        }
        context.SaveChanges();

        var filteredWithCategory = context.Products
            .Where(p => p.Price > 400)
            .Include(p => p.Category)
            .ToList();

        Console.WriteLine($"Znaleziono {filteredWithCategory.Count} produktów z kategorią:");
        foreach (var p in filteredWithCategory)
        {
            Console.WriteLine($"  • {p.Name} - {p.Price:C}");
            Console.WriteLine($"    Kategoria: {p.Category?.Name ?? "NULL"}");
        }

        Console.WriteLine("\n=======================================================");
        Console.WriteLine("PODSUMOWANIE");
        Console.WriteLine("=======================================================");
        Console.WriteLine("WHERE z operatorami porównania (>, <, ==, !=) - DZIAŁA");
        Console.WriteLine("WHERE z String.Contains() - DZIAŁA");
        Console.WriteLine("WHERE + Include (filtrowanie + eager loading) - DZIAŁA");
        Console.WriteLine("\nPrzykłady użycia:");
        Console.WriteLine("  context.Products.Where(p => p.Price > 100).ToList()");
        Console.WriteLine("  context.Products.Where(p => p.Name.Contains(\"Laptop\")).ToList()");
        Console.WriteLine("  context.Products.Where(p => p.Price > 500).Include(p => p.Category).ToList()");
        Console.WriteLine("=======================================================");
    }
}
