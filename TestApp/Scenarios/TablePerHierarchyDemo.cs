using ORM_v1.core;
using ORM_v1.Configuration;
using ORM_v1.Mapping;
using TestApp.Models.Inheritance;

namespace TestApp.Scenarios;

public static class TablePerHierarchyDemo
{
    public static void Run()
    {
        const int w = 54;
        
        Console.WriteLine("\n" + new string('=', w + 4));
        Console.WriteLine($"  {"TABLE PER HIERARCHY (TPH) - Demonstracja",-w}");
        Console.WriteLine(new string('=', w + 4) + "\n");

        var connectionString = "Data Source=tph_demo.db;";
        var metadataStore = new MetadataStoreBuilder()
            .AddAssembly(typeof(Animal).Assembly)
            .UseNamingStrategy(new PascalCaseNamingStrategy())
            .Build();

        var configuration = new DbConfiguration(connectionString, metadataStore);

        using (var context = new AppDbContext(configuration))
        {
            Console.WriteLine("|" + new string('-', w + 2) + "|");
            Console.WriteLine($"| {"1. STRATEGIA TPH",-w} |");
            Console.WriteLine("|" + new string('-', w + 2) + "|");
            Console.WriteLine($"| {"- Jedna tabela dla całej hierarchii",-w} |");
            Console.WriteLine($"| {"- Kolumna 'Discriminator' rozróżnia typy",-w} |");
            Console.WriteLine($"| {"- Animal (bazowa) -> Dog, Cat (pochodne)",-w} |");
            Console.WriteLine("|" + new string('-', w + 2) + "|\n");

            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

            Console.WriteLine("|" + new string('-', w + 2) + "|");
            Console.WriteLine($"| {"2. TWORZENIE DANYCH",-w} |");
            Console.WriteLine("|" + new string('-', w + 2) + "|");
            
            var dog1 = new Dog
            {
                Name = "Burek",
                Age = 3,
                Breed = "German Shepherd",
                IsGoodBoy = true
            };

            var dog2 = new Dog
            {
                Name = "Rex",
                Age = 5,
                Breed = "Labrador",
                IsGoodBoy = true
            };

            var cat1 = new Cat
            {
                Name = "Mruczek",
                Age = 2,
                LivesRemaining = 9,
                LikesLasers = true
            };

            var cat2 = new Cat
            {
                Name = "Filemon",
                Age = 4,
                LivesRemaining = 7,
                LikesLasers = false
            };

            context.Set<Dog>().Add(dog1);
            context.Set<Dog>().Add(dog2);
            context.Set<Cat>().Add(cat1);
            context.Set<Cat>().Add(cat2);
            
            context.SaveChanges();

            Console.WriteLine($"| {" Dodano psy: " + dog1.Name + " (ID: " + dog1.Id + "), " + dog2.Name + " (ID: " + dog2.Id + ")",-w} |");
            Console.WriteLine($"| {" Dodano koty: " + cat1.Name + " (ID: " + cat1.Id + "), " + cat2.Name + " (ID: " + cat2.Id + ")",-w} |");
            Console.WriteLine($"| {" Wszystkie w jednej tabeli 'Animals'",-w} |");
            Console.WriteLine("|" + new string('-', w + 2) + "|\n");

            Console.WriteLine("|" + new string('-', w + 2) + "|");
            Console.WriteLine($"| {"3. ODCZYT DANYCH (Find)",-w} |");
            Console.WriteLine("|" + new string('-', w + 2) + "|");
            
            var foundDog = context.Set<Dog>().Find(dog1.Id);
            if (foundDog != null)
            {
                Console.WriteLine($"| {"Znaleziono psa:",-w} |");
                Console.WriteLine($"| {"  - Imię: " + foundDog.Name,-w} |");
                Console.WriteLine($"| {"  - Wiek: " + foundDog.Age + " lat",-w} |");
                Console.WriteLine($"| {"  - Rasa: " + foundDog.Breed,-w} |");
                Console.WriteLine($"| {"  - Good boy: " + (foundDog.IsGoodBoy ? "TAK" : "NIE"),-w} |");
            }

            var foundCat = context.Set<Cat>().Find(cat1.Id);
            if (foundCat != null)
            {
                Console.WriteLine($"| {"",-w} |");
                Console.WriteLine($"| {"Znaleziono kota:",-w} |");
                Console.WriteLine($"| {"  - Imię: " + foundCat.Name,-w} |");
                Console.WriteLine($"| {"  - Wiek: " + foundCat.Age + " lat",-w} |");
                Console.WriteLine($"| {"  - Pozostałe życia: " + foundCat.LivesRemaining,-w} |");
            }
            Console.WriteLine("|" + new string('-', w + 2) + "|\n");

            Console.WriteLine("|" + new string('-', w + 2) + "|");
            Console.WriteLine($"| {"4. ODCZYT WSZYSTKICH (All)",-w} |");
            Console.WriteLine("|" + new string('-', w + 2) + "|");
            
            var allDogs = context.Set<Dog>().All().ToList();
            Console.WriteLine($"| {"Wszystkie psy (" + allDogs.Count + "):",-w} |");
            foreach (var dog in allDogs)
            {
                Console.WriteLine($"| {"  - " + dog.Name + " - " + dog.Breed,-w} |");
            }

            var allCats = context.Set<Cat>().All().ToList();
            Console.WriteLine($"| {"",-w} |");
            Console.WriteLine($"| {"Wszystkie koty (" + allCats.Count + "):",-w} |");
            foreach (var cat in allCats)
            {
                Console.WriteLine($"| {"  - " + cat.Name + " - " + cat.LivesRemaining + " żyć",-w} |");
            }
            Console.WriteLine("|" + new string('-', w + 2) + "|\n");

            Console.WriteLine("|" + new string('-', w + 2) + "|");
            Console.WriteLine($"| {"5. EDYCJA DANYCH (Update)",-w} |");
            Console.WriteLine("|" + new string('-', w + 2) + "|");
            
            if (foundDog != null)
            {
                Console.WriteLine($"| {"Przed: " + foundDog.Name + " - " + foundDog.Age + " lat",-w} |");
                foundDog.Age = 4;
                foundDog.Breed = "Golden Retriever";
                context.Set<Dog>().Update(foundDog);
                context.SaveChanges();
                Console.WriteLine($"| {"Po:    " + foundDog.Name + " - " + foundDog.Age + " lat - " + foundDog.Breed,-w} |");
            }

            if (foundCat != null)
            {
                Console.WriteLine($"| {"",-w} |");
                Console.WriteLine($"| {"Przed: " + foundCat.Name + " - " + foundCat.LivesRemaining + " żyć",-w} |");
                foundCat.LivesRemaining = 8;
                context.Set<Cat>().Update(foundCat);
                context.SaveChanges();
                Console.WriteLine($"| {"Po:    " + foundCat.Name + " - " + foundCat.LivesRemaining + " żyć",-w} |");
            }
            Console.WriteLine("|" + new string('-', w + 2) + "|\n");

            Console.WriteLine("|" + new string('-', w + 2) + "|");
            Console.WriteLine($"| {"6. USUWANIE DANYCH (Delete)",-w} |");
            Console.WriteLine("|" + new string('-', w + 2) + "|");
            
            var dogToDelete = context.Set<Dog>().Find(dog2.Id);
            if (dogToDelete != null)
            {
                Console.WriteLine($"| {"Usuwanie: " + dogToDelete.Name,-w} |");
                context.Set<Dog>().Remove(dogToDelete);
                context.SaveChanges();
                Console.WriteLine($"| {" Pies " + dogToDelete.Name + " został usunięty",-w} |");
            }

            var remainingDogs = context.Set<Dog>().All().ToList();
            Console.WriteLine($"| {"Pozostałe psy: " + remainingDogs.Count,-w} |");
            foreach (var dog in remainingDogs)
            {
                Console.WriteLine($"| {"  - " + dog.Name,-w} |");
            }
            Console.WriteLine("|" + new string('-', w + 2) + "|\n");

            Console.WriteLine("|" + new string('-', w + 2) + "|");
            Console.WriteLine($"| {"7. PODSUMOWANIE TPH",-w} |");
            Console.WriteLine("|" + new string('-', w + 2) + "|");
            Console.WriteLine($"| {"- Wszystkie zwierzęta w jednej tabeli: Animals",-w} |");
            Console.WriteLine($"| {"- Psy w bazie: " + context.Set<Dog>().All().Count(),-w} |");
            Console.WriteLine($"| {"- Koty w bazie: " + context.Set<Cat>().All().Count(),-w} |");
            Console.WriteLine($"| {"- Zalety: wydajność, brak JOIN-ów",-w} |");
            Console.WriteLine($"| {"- Wady: kolumny NULL dla specyficznych właściwości",-w} |");
            Console.WriteLine("|" + new string('-', w + 2) + "|");
        }

        Console.WriteLine("\n" + new string('=', w + 4));
        Console.WriteLine($"  {" Demonstracja TPH zakończona",-w}");
        Console.WriteLine(new string('=', w + 4));
    }
}
