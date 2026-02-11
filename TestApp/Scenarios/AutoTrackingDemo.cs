using System;
using System.Linq;
using ORM_v1.Configuration;
using ORM_v1.core;
using ORM_v1.Mapping;
using ORM_v1.Query;
using TestApp.Models;

namespace TestApp.Scenarios;

/// <summary>
/// Demo: Auto-tracking navigation properties po Include().
/// Zmiany w encjach załadowanych przez Include są automatycznie śledzone.
/// </summary>
public static class AutoTrackingDemo
{
    public static void Run()
    {
        Console.WriteLine("\n╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║      AUTO-TRACKING DEMO - Navigation Properties          ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝\n");

        const string dbPath = "autotracking_demo.db";

        if (File.Exists(dbPath))
            File.Delete(dbPath);

        var connectionString = $"Data Source={dbPath};";
        var metadataStore = new MetadataStoreBuilder()
            .AddAssembly(typeof(Product).Assembly)
            .UseNamingStrategy(new PascalCaseNamingStrategy())
            .Build();
        var config = new DbConfiguration(connectionString, metadataStore);

        using var context = new AppDbContext(config);
        context.Database.EnsureCreated();

        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("SETUP: Tworzenie danych testowych");
        Console.WriteLine("═══════════════════════════════════════════════════════\n");

        // Dodaj kategorię i produkty
        var category = new Category { Name = "Electronics" };
        context.Categories.Add(category);
        context.SaveChanges();
        Console.WriteLine($"✅ Kategoria utworzona: {category.Name} (ID: {category.Id})");

        var product1 = new Product { Name = "Laptop Dell", Price = 4999.99m, CategoryId = category.Id };
        var product2 = new Product { Name = "Mouse Logitech", Price = 149.99m, CategoryId = category.Id };
        context.Products.Add(product1);
        context.Products.Add(product2);
        context.SaveChanges();
        Console.WriteLine($"✅ Produkty utworzone: {product1.Name} i {product2.Name}");

        Console.WriteLine("\n═══════════════════════════════════════════════════════");
        Console.WriteLine("TEST 1: Modyfikacja Navigation Property (Many-to-One)");
        Console.WriteLine("═══════════════════════════════════════════════════════\n");

        // Pobierz produkt z kategorią (Include)
        Console.WriteLine($"🔍 Pobieranie produktu z Include(p => p.Category)...");
        var productWithCategory = context.Products
            .Include(p => p.Category)
            .ToList()
            .FirstOrDefault(p => p.Id == product1.Id);

        if (productWithCategory == null)
        {
            Console.WriteLine("\n❌ BŁĄD: Produkt nie został znaleziony!");
            return;
        }

        Console.WriteLine($"📦 Pobrany produkt: {productWithCategory.Name}");
        Console.WriteLine($"   Kategoria: {productWithCategory.Category?.Name ?? "(null)"}");

        // Sprawdź czy encje są śledzone
        var productEntry = context.ChangeTracker.GetEntry(productWithCategory!);
        var categoryEntry = context.ChangeTracker.GetEntry(productWithCategory.Category!);

        Console.WriteLine($"\n🔍 Stan przed modyfikacją:");
        Console.WriteLine($"   Product EntityState: {productEntry?.State ?? EntityState.Detached}");
        Console.WriteLine($"   Category EntityState: {categoryEntry?.State ?? EntityState.Detached}");

        if (categoryEntry == null || categoryEntry.State == EntityState.Detached)
        {
            Console.WriteLine("\n❌ BŁĄD: Category NIE jest śledzona przez ChangeTracker!");
            return;
        }

        Console.WriteLine("✅ Category jest śledzona jako Unchanged!");

        // Modyfikuj nazwę kategorii przez navigation property
        var oldCategoryName = productWithCategory.Category.Name;
        Console.WriteLine($"\n🔧 Modyfikuję Category.Name: \"{oldCategoryName}\" → \"Electronics & Gadgets\"");
        productWithCategory.Category.Name = "Electronics & Gadgets";

        // ⚠️ WAŻNE: Bez .Update() EntityState pozostaje Unchanged!
        Console.WriteLine($"   EntityState (bez Update): {context.ChangeTracker.GetEntry(productWithCategory.Category)?.State}");
        Console.WriteLine("   ⚠️  Bez wywołania .Update() zmiany NIE zostaną zapisane!");

        // ✅ Oznacz jako Modified używając .Update()
        Console.WriteLine("\n✅ Wywołuję context.Categories.Update(category)...");
        context.Categories.Update(productWithCategory.Category);
        Console.WriteLine($"   EntityState (po Update): {context.ChangeTracker.GetEntry(productWithCategory.Category)?.State}");

        // Zapisz zmiany
        Console.WriteLine("\n💾 Wywołuję SaveChanges()...");
        context.SaveChanges();

        // Weryfikacja - pobierz ponownie z bazy
        Console.WriteLine("\n🔍 Weryfikacja: Pobieranie kategorii z bazy po zapisie...");
        var updatedCategory = context.Categories.ToList().FirstOrDefault(c => c.Id == category.Id);
        Console.WriteLine($"   Nazwa w bazie: {updatedCategory?.Name}");

        if (updatedCategory?.Name == "Electronics & Gadgets")
        {
            Console.WriteLine("\n✅ SUCCESS! Zmiana w navigation property została zapisana!");
        }
        else
        {
            Console.WriteLine($"\n❌ BŁĄD: Oczekiwano 'Electronics & Gadgets', otrzymano '{updatedCategory?.Name}'");
            return;
        }

        Console.WriteLine("\n✅ SUCCESS: Zmiany w navigation property zostały zapisane!");

        Console.WriteLine("\n═══════════════════════════════════════════════════════");
        Console.WriteLine("TEST 2: Modyfikacja kolekcji (One-to-Many)");
        Console.WriteLine("═══════════════════════════════════════════════════════\n");

        // Pobierz kategorię z produktami
        Console.WriteLine($"🔍 Pobieranie kategorii z Include(c => c.Products)...");
        var categoryWithProducts = context.Categories
            .Include(c => c.Products)
            .ToList()
            .FirstOrDefault(c => c.Id == category.Id);

        Console.WriteLine($"📚 Pobrana kategoria: {categoryWithProducts?.Name}");
        Console.WriteLine($"   Produkty w kolekcji: {categoryWithProducts?.Products?.Count ?? 0}");

        if (categoryWithProducts?.Products != null)
        {
            Console.WriteLine("\n🔍 Stan produktów z kolekcji:");
            foreach (var prod in categoryWithProducts.Products)
            {
                var prodEntry = context.ChangeTracker.GetEntry(prod);
                Console.WriteLine($"   - {prod.Name} (${prod.Price}): {prodEntry?.State ?? EntityState.Detached}");
            }
        }

        // Modyfikuj pierwszy produkt z kolekcji
        if (categoryWithProducts?.Products != null && categoryWithProducts.Products.Any())
        {
            var firstProduct = categoryWithProducts.Products.First();
            var oldPrice = firstProduct.Price;
            var newPrice = 1299.99m;

            Console.WriteLine($"\n🔧 Modyfikuję {firstProduct.Name}.Price: ${oldPrice} → ${newPrice}");
            firstProduct.Price = newPrice;

            Console.WriteLine($"   EntityState (bez Update): {context.ChangeTracker.GetEntry(firstProduct)?.State}");

            // Oznacz jako Modified
            Console.WriteLine("\n✅ Wywołuję context.Products.Update(product)...");
            context.Products.Update(firstProduct);
            Console.WriteLine($"   EntityState (po Update): {context.ChangeTracker.GetEntry(firstProduct)?.State}");

            // Zapisz
            Console.WriteLine("\n💾 Wywołuję SaveChanges()...");
            context.SaveChanges();

            // Weryfikacja
            Console.WriteLine("\n🔍 Weryfikacja: Pobieranie produktu z bazy po zapisie...");
            var updatedProduct = context.Products.ToList().FirstOrDefault(p => p.Id == firstProduct.Id);
            Console.WriteLine($"   Cena w bazie: ${updatedProduct?.Price}");

            if (updatedProduct?.Price == newPrice)
            {
                Console.WriteLine("\n✅ SUCCESS! Zmiana w elemencie kolekcji została zapisana!");
            }
            else
            {
                Console.WriteLine($"\n❌ BŁĄD: Oczekiwano ${newPrice}, otrzymano ${updatedProduct?.Price}");
                return;
            }
        }

        Console.WriteLine("\n═══════════════════════════════════════════════════════");
        Console.WriteLine("TEST 3: Identity Map - ta sama instancja");
        Console.WriteLine("═══════════════════════════════════════════════════════\n");

        // Pobierz ten sam produkt dwa razy
        Console.WriteLine("🔍 Pierwsze pobranie produktu...");
        var prod1 = context.Products.Include(p => p.Category).ToList().FirstOrDefault(p => p.Id == product1.Id);
        Console.WriteLine("🔍 Drugie pobranie tego samego produktu...");
        var prod2 = context.Products.Include(p => p.Category).ToList().FirstOrDefault(p => p.Id == product1.Id);

        Console.WriteLine($"Product 1: {prod1?.Name} (HashCode: {prod1?.GetHashCode()})");
        Console.WriteLine($"Product 2: {prod2?.Name} (HashCode: {prod2?.GetHashCode()})");

        if (ReferenceEquals(prod1, prod2))
        {
            Console.WriteLine("\n✅ SUCCESS: Identity Map działa - ta sama instancja!");
        }
        else
        {
            Console.WriteLine("\n❌ BŁĄD: Różne instancje dla tego samego klucza!");
        }

        if (ReferenceEquals(prod1?.Category, prod2?.Category))
        {
            Console.WriteLine("✅ SUCCESS: Category też jest tą samą instancją!");
        }
        else
        {
            Console.WriteLine("❌ BŁĄD: Category ma różne instancje!");
        }

        Console.WriteLine("\n═══════════════════════════════════════════════════════");
        Console.WriteLine("PODSUMOWANIE");
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("✅ Include() automatycznie śledzi encje jako Unchanged");
        Console.WriteLine("✅ ToList() używa Identity Map (jedna instancja per klucz)");
        Console.WriteLine("✅ .Update() oznacza encję jako Modified");
        Console.WriteLine("✅ SaveChanges() zapisuje tylko Modified/Added/Deleted");
        Console.WriteLine("✅ Workflow identyczny jak Entity Framework Core!");
        Console.WriteLine("\n📝 Przykład:");
        Console.WriteLine("   var product = context.Products.Include(p => p.Category).ToList().First();");
        Console.WriteLine("   product.Category.Name = \"New Name\";  // Zmiana właściwości");
        Console.WriteLine("   context.Categories.Update(product.Category);  // Oznacz jako Modified");
        Console.WriteLine("   context.SaveChanges();  // Zapisz do bazy");
    }
}
