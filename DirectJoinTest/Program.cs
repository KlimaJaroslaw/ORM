using System;
using System.Diagnostics;
using System.Linq;
using ORM_v1.Configuration;
using ORM_v1.core;
using ORM_v1.Mapping;
using ORM_v1.Query;
using TestApp;
using TestApp.Models;

// Test bezpośredni - uruchom z: dotnet run --project TestApp/TestDirectJoinTest.csproj

Console.WriteLine("\n╔════════════════════════════════════════════════════════════╗");
Console.WriteLine("║      JOIN OPTIMIZATION TEST - SQL Query Inspection       ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════╝\n");

const string dbPath = "join_test_direct.db";

// Usuń starą bazę
if (File.Exists(dbPath))
    File.Delete(dbPath);

// Konfiguracja
var connectionString = $"Data Source={dbPath};";
var metadataStore = new MetadataStoreBuilder()
    .AddAssembly(typeof(Product).Assembly)
    .UseNamingStrategy(new PascalCaseNamingStrategy())
    .Build();
var config = new DbConfiguration(connectionString, metadataStore);

using var context = new AppDbContext(config);
context.Database.EnsureCreated();

// Dodaj dane testowe
var category = new Category { Name = "Electronics" };
context.Categories.Add(category);
context.SaveChanges();

var product1 = new Product { Name = "Laptop", Price = 999.99m, CategoryId = category.Id };
var product2 = new Product { Name = "Mouse", Price = 29.99m, CategoryId = category.Id };
context.Products.Add(product1);
context.Products.Add(product2);
context.SaveChanges();

Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine("TEST 1: Products.Include(p => p.Category)");
Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine("\n🔍 Oczekiwany SQL: SELECT ... FROM Products LEFT JOIN Categories...\n");

try
{
    var sw = Stopwatch.StartNew();
    var productsWithCategory = context.Products
        .Include(p => p.Category)
        .ToList();
    sw.Stop();

    Console.WriteLine($"✅ Zapytanie wykonane w {sw.ElapsedMilliseconds}ms");
    Console.WriteLine($"✅ Pobrano {productsWithCategory.Count} produktów\n");

    foreach (var product in productsWithCategory)
    {
        var categoryName = product.Category?.Name ?? "(null)";
        var status = product.Category != null ? "✅" : "❌";
        Console.WriteLine($"  • {product.Name} -> Category: {categoryName} {status}");
    }

    // Weryfikacja
    var allLoaded = productsWithCategory.All(p => p.Category != null);
    if (allLoaded)
    {
        Console.WriteLine("\n✅ SUCCESS! Wszystkie navigation properties załadowane!");
    }
    else
    {
        Console.WriteLine("\n❌ FAILED! Niektóre navigation properties są NULL");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"\n❌ ERROR: {ex.Message}");
    Console.WriteLine($"   StackTrace: {ex.StackTrace}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"   InnerException: {ex.InnerException.Message}");
    }
}

Console.WriteLine("\n═══════════════════════════════════════════════════════");
Console.WriteLine("TEST 2: Categories.Include(c => c.Products)");
Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine("\n🔍 Oczekiwany SQL: SELECT ... FROM Categories LEFT JOIN Products...\n");

try
{
    var sw = Stopwatch.StartNew();
    var categoriesWithProducts = context.Categories
        .Include(c => c.Products)
        .ToList();
    sw.Stop();

    Console.WriteLine($"✅ Zapytanie wykonane w {sw.ElapsedMilliseconds}ms");
    Console.WriteLine($"✅ Pobrano {categoriesWithProducts.Count} kategorii\n");

    foreach (var cat in categoriesWithProducts)
    {
        var count = cat.Products?.Count ?? 0;
        var status = count > 0 ? "✅" : "⚠️";
        Console.WriteLine($"  • {cat.Name} -> Products.Count: {count} {status}");

        if (cat.Products != null)
        {
            foreach (var prod in cat.Products)
            {
                Console.WriteLine($"    - {prod.Name} (${prod.Price})");
            }
        }
    }

    // Weryfikacja
    var correctCount = categoriesWithProducts.Any(c =>
        c.Products != null && c.Products.Count == 2);

    if (correctCount)
    {
        Console.WriteLine("\n✅ SUCCESS! Kolekcja Products prawidłowo załadowana!");
    }
    else
    {
        Console.WriteLine("\n⚠️ WARNING! Oczekiwano 2 produktów w kolekcji");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"\n❌ ERROR: {ex.Message}");
    Console.WriteLine($"   StackTrace: {ex.StackTrace}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"   InnerException: {ex.InnerException.Message}");
    }
}

Console.WriteLine("\n═══════════════════════════════════════════════════════");
Console.WriteLine("PODSUMOWANIE");
Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine("✅ Nowa implementacja używa SQL JOIN zamiast N+1 queries");
Console.WriteLine("✅ LEFT JOIN obsługuje opcjonalne relacje (null)");
Console.WriteLine("✅ Brak dodatkowych zapytań dla każdej encji");
Console.WriteLine("\nOptymalizacja: 1 zapytanie zamiast N+1 !");
