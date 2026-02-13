using ORM_v1.core;
using ORM_v1.Configuration;
using ORM_v1.Mapping;
using TestApp.Models.Inheritance;

namespace TestApp.Scenarios;

public static class TablePerConcreteClassDemo
{
    public static void Run()
    {
        const int w = 54;
        
        Console.WriteLine("\n" + new string('=', w + 4));
        Console.WriteLine($"  {"TABLE PER CONCRETE CLASS (TPC) - Demonstracja",-w}");
        Console.WriteLine(new string('=', w + 4) + "\n");

        var connectionString = "Data Source=tpc_demo.db;";
        var metadataStore = new MetadataStoreBuilder()
            .AddAssembly(typeof(Shape).Assembly)
            .UseNamingStrategy(new PascalCaseNamingStrategy())
            .Build();

        var configuration = new DbConfiguration(connectionString, metadataStore);

        using (var context = new AppDbContext(configuration))
        {
            Console.WriteLine("|" + new string('-', w + 2) + "|");
            Console.WriteLine($"| {"1. STRATEGIA TPC",-w} |");
            Console.WriteLine("|" + new string('-', w + 2) + "|");
            Console.WriteLine($"| {"- Osobna tabela tylko dla klas konkretnych",-w} |");
            Console.WriteLine($"| {"- Każda tabela zawiera wszystkie kolumny",-w} |");
            Console.WriteLine($"| {"- Shape (bazowa) -> Circle, Rectangle (konkretne)",-w} |");
            Console.WriteLine($"| {"- Tabele: Circles, Rectangles",-w} |");
            Console.WriteLine("|" + new string('-', w + 2) + "|\n");

            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

            Console.WriteLine("|" + new string('-', w + 2) + "|");
            Console.WriteLine($"| {"2. TWORZENIE DANYCH",-w} |");
            Console.WriteLine("|" + new string('-', w + 2) + "|");
            
            var circle1 = new Circle
            {
                Color = "Czerwony",
                Radius = 5.5
            };

            var circle2 = new Circle
            {
                Color = "Niebieski",
                Radius = 10.0
            };

            var rect1 = new Rectangle
            {
                Color = "Zielony",
                Width = 15.0,
                Height = 8.0
            };

            var rect2 = new Rectangle
            {
                Color = "Żółty",
                Width = 20.0,
                Height = 12.0
            };

            context.Set<Circle>().Add(circle1);
            context.Set<Circle>().Add(circle2);
            context.Set<Rectangle>().Add(rect1);
            context.Set<Rectangle>().Add(rect2);
            
            context.SaveChanges();

            Console.WriteLine($"| {" Dodano okręgi:",-w} |");
            Console.WriteLine($"| {"  - " + circle1.Color + " (ID: " + circle1.Id + ") - promień " + circle1.Radius,-w} |");
            Console.WriteLine($"| {"  - " + circle2.Color + " (ID: " + circle2.Id + ") - promień " + circle2.Radius,-w} |");
            Console.WriteLine($"| {" Dodano prostokąty:",-w} |");
            Console.WriteLine($"| {"  - " + rect1.Color + " (ID: " + rect1.Id + ") - " + rect1.Width + "x" + rect1.Height,-w} |");
            Console.WriteLine($"| {"  - " + rect2.Color + " (ID: " + rect2.Id + ") - " + rect2.Width + "x" + rect2.Height,-w} |");
            Console.WriteLine($"| {" Dane w oddzielnych tabelach: Circles, Rectangles",-w} |");
            Console.WriteLine("|" + new string('-', w + 2) + "|\n");

            Console.WriteLine("|" + new string('-', w + 2) + "|");
            Console.WriteLine($"| {"3. ODCZYT DANYCH (Find)",-w} |");
            Console.WriteLine("|" + new string('-', w + 2) + "|");
            
            var foundCircle = context.Set<Circle>().Find(circle1.Id);
            if (foundCircle != null)
            {
                Console.WriteLine($"| {"Znaleziono okrąg:",-w} |");
                Console.WriteLine($"| {"  - Kolor: " + foundCircle.Color,-w} |");
                Console.WriteLine($"| {"  - Promień: " + foundCircle.Radius,-w} |");
                var area = Math.PI * foundCircle.Radius * foundCircle.Radius;
                Console.WriteLine($"| {"  - Pole: " + area.ToString("F2"),-w} |");
            }

            var foundRect = context.Set<Rectangle>().Find(rect1.Id);
            if (foundRect != null)
            {
                Console.WriteLine($"| {"",-w} |");
                Console.WriteLine($"| {"Znaleziono prostokąt:",-w} |");
                Console.WriteLine($"| {"  - Kolor: " + foundRect.Color,-w} |");
                Console.WriteLine($"| {"  - Wymiary: " + foundRect.Width + " x " + foundRect.Height,-w} |");
                var area = foundRect.Width * foundRect.Height;
                Console.WriteLine($"| {"  - Pole: " + area.ToString("F2"),-w} |");
            }
            Console.WriteLine($"| {" Każdy typ z własnej tabeli",-w} |");
            Console.WriteLine("|" + new string('-', w + 2) + "|\n");

            Console.WriteLine("|" + new string('-', w + 2) + "|");
            Console.WriteLine($"| {"4. ODCZYT WSZYSTKICH (All)",-w} |");
            Console.WriteLine("|" + new string('-', w + 2) + "|");
            
            var allCircles = context.Set<Circle>().All().ToList();
            Console.WriteLine($"| {"Wszystkie okręgi (" + allCircles.Count + "):",-w} |");
            foreach (var circle in allCircles)
            {
                Console.WriteLine($"| {"  - " + circle.Color + " - r=" + circle.Radius,-w} |");
            }

            var allRectangles = context.Set<Rectangle>().All().ToList();
            Console.WriteLine($"| {"",-w} |");
            Console.WriteLine($"| {"Wszystkie prostokąty (" + allRectangles.Count + "):",-w} |");
            foreach (var rect in allRectangles)
            {
                Console.WriteLine($"| {"  - " + rect.Color + " - " + rect.Width + "x" + rect.Height,-w} |");
            }
            Console.WriteLine("|" + new string('-', w + 2) + "|\n");

            Console.WriteLine("|" + new string('-', w + 2) + "|");
            Console.WriteLine($"| {"5. EDYCJA DANYCH (Update)",-w} |");
            Console.WriteLine("|" + new string('-', w + 2) + "|");
            
            if (foundCircle != null)
            {
                Console.WriteLine($"| {"Przed: Okrąg " + foundCircle.Color + " - promień " + foundCircle.Radius,-w} |");
                foundCircle.Radius = 7.5;
                foundCircle.Color = "Pomarańczowy";
                context.Set<Circle>().Update(foundCircle);
                context.SaveChanges();
                Console.WriteLine($"| {"Po:    Okrąg " + foundCircle.Color + " - promień " + foundCircle.Radius,-w} |");
            }

            if (foundRect != null)
            {
                Console.WriteLine($"| {"",-w} |");
                Console.WriteLine($"| {"Przed: Prostokąt " + foundRect.Color + " - " + foundRect.Width + "x" + foundRect.Height,-w} |");
                foundRect.Width = 18.0;
                foundRect.Height = 10.0;
                context.Set<Rectangle>().Update(foundRect);
                context.SaveChanges();
                Console.WriteLine($"| {"Po:    Prostokąt " + foundRect.Color + " - " + foundRect.Width + "x" + foundRect.Height,-w} |");
            }
            Console.WriteLine($"| {" UPDATE tylko w jednej tabeli (prostsze)",-w} |");
            Console.WriteLine("|" + new string('-', w + 2) + "|\n");

            Console.WriteLine("|" + new string('-', w + 2) + "|");
            Console.WriteLine($"| {"6. USUWANIE DANYCH (Delete)",-w} |");
            Console.WriteLine("|" + new string('-', w + 2) + "|");
            
            var circleToDelete = context.Set<Circle>().Find(circle2.Id);
            if (circleToDelete != null)
            {
                Console.WriteLine($"| {"Usuwanie: Okrąg " + circleToDelete.Color,-w} |");
                context.Set<Circle>().Remove(circleToDelete);
                context.SaveChanges();
                Console.WriteLine($"| {" Okrąg " + circleToDelete.Color + " został usunięty",-w} |");
            }

            var remainingCircles = context.Set<Circle>().All().ToList();
            Console.WriteLine($"| {"Pozostałe okręgi: " + remainingCircles.Count,-w} |");
            foreach (var circle in remainingCircles)
            {
                Console.WriteLine($"| {"  - " + circle.Color + " - r=" + circle.Radius,-w} |");
            }
            Console.WriteLine("|" + new string('-', w + 2) + "|\n");

            Console.WriteLine("|" + new string('-', w + 2) + "|");
            Console.WriteLine($"| {"7. PODSUMOWANIE TPC",-w} |");
            Console.WriteLine("|" + new string('-', w + 2) + "|");
            Console.WriteLine($"| {"- Tabele: Circles, Rectangles",-w} |");
            Console.WriteLine($"| {"- Okręgów w bazie: " + context.Set<Circle>().All().Count(),-w} |");
            Console.WriteLine($"| {"- Prostokątów w bazie: " + context.Set<Rectangle>().All().Count(),-w} |");
            Console.WriteLine($"| {"- Zalety: niezależne tabele, brak JOIN-ów",-w} |");
            Console.WriteLine($"| {"- Wady: duplikacja kolumn bazowych, brak polimorfizmu",-w} |");
            Console.WriteLine("|" + new string('-', w + 2) + "|");
        }

        Console.WriteLine("\n" + new string('=', w + 4));
        Console.WriteLine($"  {" Demonstracja TPC zakończona",-w}");
        Console.WriteLine(new string('=', w + 4));
    }
}
