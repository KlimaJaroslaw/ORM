using System;
using System.Linq;
using System.Collections.Generic;
using ORM_v1.Attributes;
using ORM_v1.Configuration;
using ORM_v1.core;
using ORM_v1.Mapping;
using ORM_v1.Mapping.Strategies;
using ORM_v1.Query;
using ORM_Presentation;

namespace Prezentacja
{
    class Program
    {
        // Konfiguracja dostępna dla wszystkich metod
        static DbConfiguration _dbConfig;

        static void Main(string[] args)
        {
            // 1. Inicjalizacja (Raz na start)
            var metadataBuilder = new MetadataStoreBuilder();
            metadataBuilder.AddAssembly(typeof(Program).Assembly);
            
            // Upewnij się, że masz tę klasę SnakeCaseNamingStrategy. 
            // Jeśli nie, zmień na: new PascalCaseNamingStrategy()
            metadataBuilder.UseNamingStrategy(new SnakeCaseNamingStrategy()); 
            
            var metadataStore = metadataBuilder.Build();
            _dbConfig = new DbConfiguration("Data Source=school.db", metadataStore);

            // 2. Menu Główne
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\n================ ORM TEST MENU ================");
                Console.WriteLine("1. [RESET & SEED] Wyczyść bazę i wstaw dane testowe");
                Console.WriteLine("2. [TEST TPC] Polimorfizm (Pobierz wszystkich Person)");
                Console.WriteLine("3. [TEST RELACJI] Include / ThenInclude (Student -> Courses)");
                Console.WriteLine("4. [TEST UPDATE/DELETE] Modyfikacja danych");
                Console.WriteLine("0. Wyjście");
                Console.WriteLine("===============================================");
                Console.ResetColor();
                Console.Write("Wybierz opcję: ");

                var key = Console.ReadLine();
                Console.Clear();

                try
                {
                    switch (key)
                    {
                        case "1": RunSeeding(); break;
                        case "2": TestPolymorphismTPC(); break;
                        case "3": TestRelationships(); break;
                        case "4": TestModifications(); break;
                        case "0": return;
                        default: Console.WriteLine("Nieznana opcja."); break;
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"BŁĄD KRYTYCZNY: {ex.Message}");
                    Console.WriteLine(ex.StackTrace);
                    Console.ResetColor();
                }
            }
        }

        // --- OPCJA 1: TWORZENIE BAZY I DANYCH ---
        static void RunSeeding()
        {
            Console.WriteLine(">>> Rozpoczynam SEEDING (Insert danych)...");

            using (var context = new DbContext(_dbConfig))
            {
                context.Database.EnsureDeleted();
                context.Database.EnsureCreated();
            }

            // A. Tworzenie Osób (Student, Teacher, Employee)
            var student = new Student { FirstName = "Alex", LastName = "Alt", Semester = 3 };
            var student2 = new Student { FirstName = "Emily", LastName = "Evans", Semester = 5 };
            
            var teacher1 = new Teacher { FirstName = "Bruno", LastName = "Brando", Title = "Dr.", Salary = 5000 };
            var teacher2 = new Teacher { FirstName = "Clara", LastName = "Clara", Title = "Prof.", Salary = 7000 };
            
            var employee = new Employee { FirstName = "David", LastName = "Davidson", Salary = 3000 };

            using (var context = new DbContext(_dbConfig))
            {
                // Testujemy dodawanie przez klasę bazową (Person) - TPC to musi obsłużyć
                context.Set<Person>().Add(student);
                context.Set<Person>().Add(student2);
                context.Set<Person>().Add(teacher1);
                context.Set<Person>().Add(teacher2);
                context.Set<Person>().Add(employee);
                context.SaveChanges();
                Console.WriteLine("-> Dodano osoby (Students, Teachers, Employees).");
            }

            // B. Relacja 1:1 (EmployeeNavi) + Update FK
            using (var context = new DbContext(_dbConfig))
            {
                // Pobieramy Davida
                var e1 = context.Set<Employee>().Where(e => e.FirstName == "David").ToList().FirstOrDefault();
                
                var employeeNavi = new EmployeeNavi();

                // --- FIX: Przypisz ID pracownika PRZED zapisem ---
                // Skoro baza pilnuje kluczy obcych, to pole nie może być 0!
                employeeNavi.EmployeeNaviKey = e1.Key; 
                // ------------------------------------------------

                context.Set<EmployeeNavi>().Add(employeeNavi);
                context.SaveChanges(); // Teraz przejdzie, bo wskazujemy na Davida (ID=1)
                
                // Aktualizujemy drugą stronę relacji (u pracownika)
                e1.EmployeeNaviKey = employeeNavi.Key;
                context.Set<Employee>().Update(e1);
                context.SaveChanges();
                Console.WriteLine("-> Dodano EmployeeNavi i zaktualizowano Davida.");
            }

            // C. Dodawanie Kursów i Nauczycieli
            // Musimy pobrać ID nauczycieli
            int t1Key, t2Key;
            using (var context = new DbContext(_dbConfig))
            {
                t1Key = context.Set<Teacher>().Where(t => t.FirstName == "Bruno").ToList().First().Key;
                t2Key = context.Set<Teacher>().Where(t => t.FirstName == "Clara").ToList().First().Key;
            }

            var course1 = new Course { Name = "Mathematics", ECTS = 5, TeacherId = t1Key };
            var course2 = new Course { Name = "Computer Science", ECTS = 6, TeacherId = t2Key };

            using (var context = new DbContext(_dbConfig))
            {
                context.Set<Course>().Add(course1);
                context.Set<Course>().Add(course2);
                context.SaveChanges();
                Console.WriteLine("-> Dodano Kursy.");
            }

            // D. Many-to-Many (StudentCourses)
            int s1Key, s2Key, c1Key, c2Key;
            using (var context = new DbContext(_dbConfig))
            {
                s1Key = context.Set<Student>().Where(s => s.FirstName == "Alex").ToList().First().Key;
                s2Key = context.Set<Student>().Where(s => s.FirstName == "Emily").ToList().First().Key;
                c1Key = context.Set<Course>().Where(c => c.Name == "Mathematics").ToList().First().Key;
                c2Key = context.Set<Course>().Where(c => c.Name == "Computer Science").ToList().First().Key;
            }

            using (var context = new DbContext(_dbConfig))
            {                
                context.Set<StudentCourse>().Add(new StudentCourse { StudentId = s1Key, CourseId = c1Key });
                context.Set<StudentCourse>().Add(new StudentCourse { StudentId = s1Key, CourseId = c2Key });
                context.Set<StudentCourse>().Add(new StudentCourse { StudentId = s2Key, CourseId = c1Key });
                context.SaveChanges();
                Console.WriteLine("-> Dodano relacje StudentCourses (M:N).");
            }

            Console.WriteLine("\n✅ SEEDING ZAKOŃCZONY SUKCESEM.");
            Console.WriteLine("Naciśnij Enter, aby wrócić do menu...");
            Console.ReadLine();
        }

        // --- OPCJA 2: TEST POLIMORFIZMU (TPC) ---
        static void TestPolymorphismTPC()
        {
            Console.WriteLine(">>> TEST TPC: Pobieranie wszystkich Person (Students + Teachers + Employees)...");

            using (var context = new DbContext(_dbConfig))
            {
                // Tutaj ORM musi wykonać skomplikowany SQL (UNION ALL) lub materializować niezależnie
                var allPeople = context.Set<Person>().ToList();
                
                Console.WriteLine($"Znaleziono {allPeople.Count} osób:");
                foreach (var p in allPeople)
                {
                    // Wypisujemy typ C#, żeby sprawdzić czy ORM poprawnie rozpoznał klasę
                    Console.WriteLine($" - [{p.GetType().Name}] {p.FirstName} {p.LastName}");
                }
            }
            Console.WriteLine("\nNaciśnij Enter...");
            Console.ReadLine();
        }

        // --- OPCJA 3: TEST RELACJI ---
        static void TestRelationships()
        {
            Console.WriteLine(">>> TEST RELACJI (Include / ThenInclude)...");

            using (var context = new DbContext(_dbConfig))
            {
                Console.WriteLine("\n--- A. Studenci i ich Kursy ---");
                var students = context.Set<Student>()
                    .Include(s => s.StudentCourses)
                    .ThenInclude(sc => sc.Course)
                    .ToList();

                foreach (var s in students)
                {
                    Console.WriteLine($"Student: {s.FirstName} {s.LastName}");
                    if (s.Courses.Any())
                    {
                        foreach (var c in s.Courses)
                        {
                            Console.WriteLine($"\tZapisany na: {c.Name} (ECTS: {c.ECTS})");
                        }
                    }
                    else
                    {
                        Console.WriteLine("\tBrak kursów (Problem z Include?)");
                    }
                }

                Console.WriteLine("\n--- B. Nauczyciele i ich Kursy ---");
                var teachers = context.Set<Teacher>().Include(t => t.Courses).ToList();
                foreach (var t in teachers)
                {
                    Console.WriteLine($"Nauczyciel: {t.Title} {t.LastName}");
                    foreach (var c in t.Courses)
                    {
                        Console.WriteLine($"\tProwadzi: {c.Name}");
                    }
                }

                Console.WriteLine("\n--- C. Pracownicy i Navi (1:1) ---");
                // Dodajemy filtr po pensji, żeby sprawdzić Where + Include
                var employees = context.Set<Employee>()
                    .Where(z => z.Salary < 6000) // David ma 3000
                    .Include(e => e.Navi)
                    .ToList();

                foreach (var e in employees)
                {
                    Console.WriteLine($"Pracownik: {e.FirstName}, NaviKey: {e.EmployeeNaviKey}");
                    if (e.Navi != null)
                        Console.WriteLine($"\t-> Załadowano obiekt Navi (ID: {e.Navi.Key})");
                    else
                        Console.WriteLine($"\t-> Navi jest NULL (Błąd Include?)");
                }
            }
            Console.WriteLine("\nNaciśnij Enter...");
            Console.ReadLine();
        }

        // --- OPCJA 4: MODYFIKACJE ---
        static void TestModifications()
        {
            Console.WriteLine(">>> TEST MODYFIKACJI (Update/Delete)...");

            using (var context = new DbContext(_dbConfig))
            {
                // 1. Pobieramy dane do edycji (używając Include, żeby mieć pełny graf)
                var course = context.Set<Course>()
                    .Include(c => c.StudentCourses)
                    .ThenInclude(sc => sc.Student)
                    .ToList()
                    .First(); // Bierzemy pierwszy kurs (Mathematics)

                var studentOnCourse = course.StudentCourses.First().Student;

                Console.WriteLine($"PRZED: Student {studentOnCourse.FirstName}, Kurs {course.Name} ECTS={course.ECTS}");

                // 2. Modyfikujemy
                studentOnCourse.FirstName = "Zac"; // Zmiana w Person
                course.ECTS = 99;                // Zmiana w Course
                
                // Usuwamy jeden zapis
                var linkToRemove = course.StudentCourses.Last(); 
                
                // 3. Rejestrujemy zmiany
                context.Set<Person>().Update(studentOnCourse);
                context.Set<Course>().Update(course);
                context.Set<StudentCourse>().Remove(linkToRemove);
                
                context.SaveChanges();
                Console.WriteLine("-> Zapisano zmiany (Update + Delete).");
            }

            // 4. Weryfikacja
            using (var context = new DbContext(_dbConfig))
            {
                Console.WriteLine("\n--- WERYFIKACJA ---");
                var zac = context.Set<Student>().Where(s => s.FirstName == "Zac").ToList().FirstOrDefault();
                if (zac != null) 
                    Console.WriteLine($"SUKCES: Znaleziono studenta 'Zac' (był Alex).");
                else 
                    Console.WriteLine($"BŁĄD: Nie znaleziono studenta 'Zac'.");

                var math = context.Set<Course>().Where(c => c.ECTS == 99).ToList().FirstOrDefault();
                if (math != null)
                    Console.WriteLine($"SUKCES: Matematyka ma teraz 99 ECTS.");
                else
                    Console.WriteLine($"BŁĄD: Matematyka nie ma 99 ECTS.");
            }

            Console.WriteLine("\nNaciśnij Enter...");
            Console.ReadLine();
        }
    }
}