using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using ORM_v1.core;
using ORM_v1.Configuration;
using ORM_v1.Attributes;
using ORM_v1.Mapping;
using SQLitePCL;

namespace SafetyAndCacheDemo
{
    [Table("Users")]
    public class User
    {
        [Key]
        public int Id { get; set; }
        public string? Username { get; set; }
    }

    public class SafetyContext : DbContext
    {
        public DbSet<User> Users { get; set; }

        public SafetyContext(DbConfiguration config) : base(config)
        {
            Users = Set<User>();
        }
    }

    class Program
    {
        static void Main()
        {
            SQLitePCL.Batteries.Init();

            var builder = new MetadataStoreBuilder();
            builder.AddAssembly(Assembly.GetExecutingAssembly());
            builder.UseNamingStrategy(new PascalCaseNamingStrategy());
            var metadata = builder.Build();

            var config = new DbConfiguration(
                connectionString: "Data Source=safety_test.db",
                metadataStore: metadata
            );

            using var db = new SafetyContext(config);

            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();

            Console.WriteLine("=== 1. TEST IDENTITY MAP (Cache) ===");
            
            var u1 = new User { Username = "TestUser" };
            db.Users.Add(u1);
            db.SaveChanges();
            var id = u1.Id;
            Console.WriteLine($"Zapisano usera ID: {id}");

            // Pobieramy ten sam obiekt dwa razy
            var userA = db.Users.Find(id);
            var userB = db.Users.Find(id);

            // Sprawdzamy referencje
            if (ReferenceEquals(userA, userB))
            {
                Console.WriteLine("SUKCES: userA i userB to ten sam obiekt w pamięci (Identity Map działa).");
            }
            else
            {
                Console.WriteLine("OSTRZEŻENIE: userA i userB to dwie różne instancje! (Brak cache).");
            }

            // Test logiczny
            userA.Username = "ZmienionyPrzezA";
            Console.WriteLine($"UserB.Username = {userB.Username}"); 
            // Jeśli IdentityMap działa, userB też powinien widzieć zmianę natychmiast, bez zapytania do bazy!

            Console.WriteLine("\n=== 2. TEST SQL INJECTION ===");
            
            // Próba ataku: Zamykamy string, kończymy instrukcję, usuwamy tabelę
            string hackerName = "Hacker'); DROP TABLE Users; --";
            
            var hacker = new User { Username = hackerName };
            
            try 
            {
                db.Users.Add(hacker);
                db.SaveChanges();
                Console.WriteLine("Zapisano użytkownika o dziwnej nazwie (SQL Injection zablokowane).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"BŁĄD: Wystąpił wyjątek (czyżby SQL Injection przeszło?): {ex.Message}");
            }

            // Weryfikacja czy tabela nadal istnieje
            try
            {
                // Próbujemy pobrać dane. Jeśli tabela została usunięta, tu poleci błąd SQLite.
                var users = db.Users.ToList();
                var h = users.FirstOrDefault(u => u.Username.StartsWith("Hacker"));
                
                if (h != null && h.Username == hackerName)
                {
                    Console.WriteLine("SUKCES: Tabela istnieje, a nazwa została zapisana dosłownie.");
                }
            }
            catch
            {
                Console.WriteLine("PORAŻKA: Tabela Users chyba nie istnieje!");
            }
            
            Console.WriteLine("\n=== DONE ===");
        }
    }
}