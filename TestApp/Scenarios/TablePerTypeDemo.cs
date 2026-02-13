using ORM_v1.core;
using ORM_v1.Configuration;
using ORM_v1.Mapping;
using TestApp.Models.Inheritance;

namespace TestApp.Scenarios;

public static class TablePerTypeDemo
{
    public static void Run()
    {
        const int w = 54;
        
        Console.WriteLine("\n" + new string('=', w + 4));
        Console.WriteLine($"  {"TABLE PER TYPE (TPT) - Demonstracja",-w}");
        Console.WriteLine(new string('=', w + 4) + "\n");

        var connectionString = "Data Source=tpt_demo.db;";
        var metadataStore = new MetadataStoreBuilder()
            .AddAssembly(typeof(Vehicle).Assembly)
            .UseNamingStrategy(new PascalCaseNamingStrategy())
            .Build();

        var configuration = new DbConfiguration(connectionString, metadataStore);

        using (var context = new AppDbContext(configuration))
        {
            Console.WriteLine("|" + new string('-', w + 2) + "|");
            Console.WriteLine($"| {"1. STRATEGIA TPT",-w} |");
            Console.WriteLine("|" + new string('-', w + 2) + "|");
            Console.WriteLine($"| {"- Osobna tabela dla każdego typu w hierarchii",-w} |");
            Console.WriteLine($"| {"- JOIN do odczytu danych pochodnych",-w} |");
            Console.WriteLine($"| {"- Vehicle (bazowa) -> Car, Truck (pochodne)",-w} |");
            Console.WriteLine($"| {"- Tabele: Vehicles, Cars, Trucks",-w} |");
            Console.WriteLine("└" + new string('-', w + 2) + "|\n");

            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

            Console.WriteLine("|" + new string('-', w + 2) + "|");
            Console.WriteLine($"| {"2. TWORZENIE DANYCH",-w} |");
            Console.WriteLine("|" + new string('-', w + 2) + "|");
            
            var car1 = new Car
            {
                Brand = "Toyota",
                Year = 2022,
                Price = 120000m,
                NumberOfDoors = 4,
                EngineType = "Hybrid"
            };

            var car2 = new Car
            {
                Brand = "BMW",
                Year = 2023,
                Price = 250000m,
                NumberOfDoors = 2,
                EngineType = "Electric"
            };

            var truck1 = new Truck
            {
                Brand = "Volvo",
                Year = 2021,
                Price = 350000m,
                PayloadCapacity = 20000m,
                NumberOfAxles = 3
            };

            var truck2 = new Truck
            {
                Brand = "Scania",
                Year = 2023,
                Price = 450000m,
                PayloadCapacity = 25000m,
                NumberOfAxles = 4
            };

            context.Set<Car>().Add(car1);
            context.Set<Car>().Add(car2);
            context.Set<Truck>().Add(truck1);
            context.Set<Truck>().Add(truck2);
            
            context.SaveChanges();

            Console.WriteLine($"| {" Dodano samochody:",-w} |");
            Console.WriteLine($"| {"  - " + car1.Brand + " (ID: " + car1.Id + ") - " + car1.Price.ToString("C"),-w} |");
            Console.WriteLine($"| {"  - " + car2.Brand + " (ID: " + car2.Id + ") - " + car2.Price.ToString("C"),-w} |");
            Console.WriteLine($"| {" Dodano ciężarówki:",-w} |");
            Console.WriteLine($"| {"  - " + truck1.Brand + " (ID: " + truck1.Id + ") - " + truck1.Price.ToString("C"),-w} |");
            Console.WriteLine($"| {"  - " + truck2.Brand + " (ID: " + truck2.Id + ") - " + truck2.Price.ToString("C"),-w} |");
            Console.WriteLine($"| {" Dane w tabelach: Vehicles, Cars, Trucks",-w} |");
            Console.WriteLine("└" + new string('-', w + 2) + "|\n");

            Console.WriteLine("|" + new string('-', w + 2) + "|");
            Console.WriteLine($"| {"3. ODCZYT DANYCH (Find)",-w} |");
            Console.WriteLine("|" + new string('-', w + 2) + "|");
            
            var foundCar = context.Set<Car>().Find(car1.Id);
            if (foundCar != null)
            {
                Console.WriteLine($"| {"Znaleziono samochód:",-w} |");
                Console.WriteLine($"| {"  - Marka: " + foundCar.Brand,-w} |");
                Console.WriteLine($"| {"  - Rok: " + foundCar.Year,-w} |");
                Console.WriteLine($"| {"  - Cena: " + foundCar.Price.ToString("C"),-w} |");
                Console.WriteLine($"| {"  - Drzwi: " + foundCar.NumberOfDoors,-w} |");
                Console.WriteLine($"| {"  - Silnik: " + foundCar.EngineType,-w} |");
            }

            var foundTruck = context.Set<Truck>().Find(truck1.Id);
            if (foundTruck != null)
            {
                Console.WriteLine($"| {"",-w} |");
                Console.WriteLine($"| {"Znaleziono ciężarówkę:",-w} |");
                Console.WriteLine($"| {"  - Marka: " + foundTruck.Brand,-w} |");
                Console.WriteLine($"| {"  - Rok: " + foundTruck.Year,-w} |");
                Console.WriteLine($"| {"  - Ładowność: " + foundTruck.PayloadCapacity + " kg",-w} |");
                Console.WriteLine($"| {"  - Osie: " + foundTruck.NumberOfAxles,-w} |");
            }
            Console.WriteLine($"| {" Dane pobrane z JOIN Vehicles + Cars/Trucks",-w} |");
            Console.WriteLine("└" + new string('-', w + 2) + "|\n");

            Console.WriteLine("|" + new string('-', w + 2) + "|");
            Console.WriteLine($"| {"4. ODCZYT WSZYSTKICH (All)",-w} |");
            Console.WriteLine("|" + new string('-', w + 2) + "|");
            
            var allCars = context.Set<Car>().All().ToList();
            Console.WriteLine($"| {"Wszystkie samochody (" + allCars.Count + "):",-w} |");
            foreach (var car in allCars)
            {
                Console.WriteLine($"| {"  - " + car.Brand + " " + car.Year + " - " + car.EngineType,-w} |");
            }

            var allTrucks = context.Set<Truck>().All().ToList();
            Console.WriteLine($"| {"",-w} |");
            Console.WriteLine($"| {"Wszystkie ciężarówki (" + allTrucks.Count + "):",-w} |");
            foreach (var truck in allTrucks)
            {
                Console.WriteLine($"| {"  - " + truck.Brand + " - " + truck.PayloadCapacity + "kg ładowności",-w} |");
            }
            Console.WriteLine("└" + new string('-', w + 2) + "|\n");

            Console.WriteLine("|" + new string('-', w + 2) + "|");
            Console.WriteLine($"| {"5. EDYCJA DANYCH (Update)",-w} |");
            Console.WriteLine("|" + new string('-', w + 2) + "|");
            
            if (foundCar != null)
            {
                Console.WriteLine($"| {"Przed: " + foundCar.Brand + " - " + foundCar.Price.ToString("C"),-w} |");
                foundCar.Price = 115000m;
                foundCar.EngineType = "Plug-in Hybrid";
                context.Set<Car>().Update(foundCar);
                context.SaveChanges();
                Console.WriteLine($"| {"Po:    " + foundCar.Brand + " - " + foundCar.Price.ToString("C") + " - " + foundCar.EngineType,-w} |");
            }

            if (foundTruck != null)
            {
                Console.WriteLine($"| {"",-w} |");
                Console.WriteLine($"| {"Przed: " + foundTruck.Brand + " - " + foundTruck.PayloadCapacity + "kg",-w} |");
                foundTruck.PayloadCapacity = 22000m;
                context.Set<Truck>().Update(foundTruck);
                context.SaveChanges();
                Console.WriteLine($"| {"Po:    " + foundTruck.Brand + " - " + foundTruck.PayloadCapacity + "kg",-w} |");
            }
            Console.WriteLine($"| {" UPDATE wykonany na wielu tabelach",-w} |");
            Console.WriteLine("└" + new string('-', w + 2) + "|\n");

            Console.WriteLine("|" + new string('-', w + 2) + "|");
            Console.WriteLine($"| {"6. USUWANIE DANYCH (Delete)",-w} |");
            Console.WriteLine("|" + new string('-', w + 2) + "|");
            
            var carToDelete = context.Set<Car>().Find(car2.Id);
            if (carToDelete != null)
            {
                Console.WriteLine($"| {"Usuwanie: " + carToDelete.Brand + " " + carToDelete.Year,-w} |");
                context.Set<Car>().Remove(carToDelete);
                context.SaveChanges();
                Console.WriteLine($"| {" Samochód " + carToDelete.Brand + " został usunięty",-w} |");
            }

            var remainingCars = context.Set<Car>().All().ToList();
            Console.WriteLine($"| {"Pozostałe samochody: " + remainingCars.Count,-w} |");
            foreach (var car in remainingCars)
            {
                Console.WriteLine($"| {"  - " + car.Brand + " " + car.Year,-w} |");
            }
            Console.WriteLine("└" + new string('-', w + 2) + "|\n");

            Console.WriteLine("|" + new string('-', w + 2) + "|");
            Console.WriteLine($"| {"7. PODSUMOWANIE TPT",-w} |");
            Console.WriteLine("|" + new string('-', w + 2) + "|");
            Console.WriteLine($"| {"- Tabele: Vehicles (bazowa), Cars, Trucks",-w} |");
            Console.WriteLine($"| {"- Samochodów w bazie: " + context.Set<Car>().All().Count(),-w} |");
            Console.WriteLine($"| {"- Ciężarówek w bazie: " + context.Set<Truck>().All().Count(),-w} |");
            Console.WriteLine($"| {"- Zalety: normalizacja, brak NULL-i",-w} |");
            Console.WriteLine($"| {"- Wady: konieczność JOIN-ów, wolniejszy odczyt",-w} |");
            Console.WriteLine("└" + new string('-', w + 2) + "|");
        }

        Console.WriteLine("\n" + new string('=', w + 4));
        Console.WriteLine($"  {" Demonstracja TPT zakończona",-w}");
        Console.WriteLine(new string('=', w + 4));
    }
}
