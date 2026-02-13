using ORM_v1.core;
using ORM_v1.Configuration;
using ORM_v1.Mapping;
using TestApp.Models;

namespace TestApp.Scenarios;

/// <summary>
/// Demonstracja atrybutów mapowania: [Table], [Column], [Key], [Ignore]
/// </summary>
public static class AttributeMappingDemo
{
    public static void Run()
    {
        Console.WriteLine("\n=== DEMONSTRACJA ATRYBUTÓW MAPOWANIA ===\n");

        var connectionString = "Data Source=mapping_demo.db;";
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

            Console.WriteLine("1. Atrybut [Table] - mapowanie nazwy tabeli:");
            Console.WriteLine("   Klasa 'Product' -> Tabela 'Products'");
            Console.WriteLine("   Klasa 'Customer' -> Tabela 'Customers'");
            Console.WriteLine("   Klasa 'Category' -> Tabela 'Categories'");

            Console.WriteLine("\n2. Atrybut [Column] - mapowanie nazw kolumn:");
            Console.WriteLine("   Product:");
            Console.WriteLine("   - Właściwość 'Name' -> Kolumna 'product_name'");
            Console.WriteLine("   - Właściwość 'CategoryId' -> Kolumna 'category_id'");

            Console.WriteLine("\n   Customer:");
            Console.WriteLine("   - Właściwość 'FirstName' -> Kolumna 'first_name'");
            Console.WriteLine("   - Właściwość 'LastName' -> Kolumna 'last_name'");
            Console.WriteLine("   - Właściwość 'RegistrationDate' -> Kolumna 'registration_date'");

            Console.WriteLine("\n3. Atrybut [Key] - klucz główny:");
            Console.WriteLine("   Wszystkie encje mają właściwość 'Id' jako klucz główny");
            Console.WriteLine("   (z AUTOINCREMENT)");

            Console.WriteLine("\n4. Demonstracja zapisu i odczytu:");
            var product = new Product
            {
                Name = "Testowy produkt",
                Price = 99.99m,
                Stock = 10,
                CategoryId = 1
            };

            context.Products.Add(product);
            context.SaveChanges();

            Console.WriteLine($"    Zapisano produkt z ID: {product.Id}");
            Console.WriteLine($"     Nazwa (property 'Name'): {product.Name}");
            Console.WriteLine($"     - Zapisana w kolumnie 'product_name'");

            // Odczyt
            var foundProduct = context.Products.Find(product.Id);
            if (foundProduct != null)
            {
                Console.WriteLine($"\n    Odczytano produkt:");
                Console.WriteLine($"     ID: {foundProduct.Id}");
                Console.WriteLine($"     Nazwa: {foundProduct.Name}");
            }

            Console.WriteLine("\n5. Atrybut [Ignore] - pomijanie właściwości:");
            var customer = new Customer
            {
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = "jan@example.com",
                RegistrationDate = DateTime.Now,
                Status = CustomerStatus.Active
            };

            context.Customers.Add(customer);
            context.SaveChanges();

            Console.WriteLine($"    Zapisano klienta: {customer.FullName}");
            Console.WriteLine($"     Właściwość 'FullName' (computed): {customer.FullName}");
            Console.WriteLine($"     - NIE jest zapisywana w bazie (atrybut [Ignore])");

            // Odczyt i weryfikacja
            var foundCustomer = context.Customers.Find(customer.Id);
            if (foundCustomer != null)
            {
                Console.WriteLine($"\n    Po odczycie:");
                Console.WriteLine($"     FirstName: {foundCustomer.FirstName}");
                Console.WriteLine($"     LastName: {foundCustomer.LastName}");
                Console.WriteLine($"     FullName (obliczony): {foundCustomer.FullName}");
            }

            Console.WriteLine("\n6. Demonstracja nawigacji (właściwości z [Ignore]):");
            product.Category = new Category
            {
                Name = "Test Category",
                Description = "Category for testing"
            };

            Console.WriteLine($"   Produkt ma przypisaną kategorię: {product.Category.Name}");
            Console.WriteLine($"   - Właściwość 'Category' NIE jest zapisywana w bazie");
            Console.WriteLine($"   - Tylko 'CategoryId' (klucz obcy) jest zapisywane");

            Console.WriteLine("\n7. Sprawdzenie metadanych mapowania:");
            var productMap = metadataStore.GetMap<Product>();
            Console.WriteLine($"\n   Product mapping:");
            Console.WriteLine($"   - Tabela: {productMap.TableName}");
            Console.WriteLine($"   - Klucz: {productMap.KeyProperty.PropertyInfo.Name} -> {productMap.KeyProperty.ColumnName}");
            Console.WriteLine($"   - Właściwości skalarne:");
            foreach (var prop in productMap.ScalarProperties)
            {
                Console.WriteLine($"     - {prop.PropertyInfo.Name} -> {prop.ColumnName}");
            }
            Console.WriteLine($"   - Właściwości nawigacyjne:");
            foreach (var prop in productMap.NavigationProperties)
            {
                Console.WriteLine($"     - {prop.PropertyInfo.Name} (ignorowana w bazie)");
            }

            var customerMap = metadataStore.GetMap<Customer>();
            Console.WriteLine($"\n   Customer mapping:");
            Console.WriteLine($"   - Tabela: {customerMap.TableName}");
            Console.WriteLine($"   - Właściwości skalarne:");
            foreach (var prop in customerMap.ScalarProperties)
            {
                Console.WriteLine($"     - {prop.PropertyInfo.Name} -> {prop.ColumnName}");
            }
        }

        Console.WriteLine("\n=== Attribute Mapping Demo zakończone ===");
    }
}
