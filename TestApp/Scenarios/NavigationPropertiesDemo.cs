using ORM_v1.Attributes;
using ORM_v1.Configuration;
using ORM_v1.core;
using ORM_v1.Mapping;
using ORM_v1.Query;

namespace TestApp.Scenarios;

/// <summary>
/// Scenariusz demonstracyjny pokazujący różne sposoby używania navigation properties w ORM.
/// </summary>
public class NavigationPropertiesDemo
{
    public static void RunDemo()
    {
        Console.WriteLine("\n╔════════════════════════════════════════════════════════╗");
        Console.WriteLine("║      NAVIGATION PROPERTIES - Demo                      ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════╝\n");

        // Budowanie metadanych
        var naming = new PascalCaseNamingStrategy();
        var builder = new ReflectionModelBuilder(naming);
        var director = new ModelDirector(builder);
        var metadata = director.Construct(typeof(NavigationPropertiesDemo).Assembly);

        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("CZĘŚĆ 1: ANALIZA METADANYCH");
        Console.WriteLine("═══════════════════════════════════════════════════════\n");

        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("1. ONE-TO-MANY (Blog -> Posts)");
        Console.WriteLine("═══════════════════════════════════════════════════════");
        DemoOneToMany(metadata);

        Console.WriteLine("\n═══════════════════════════════════════════════════════");
        Console.WriteLine("2. MANY-TO-ONE (Post -> Blog)");
        Console.WriteLine("═══════════════════════════════════════════════════════");
        DemoManyToOne(metadata);

        Console.WriteLine("\n═══════════════════════════════════════════════════════");
        Console.WriteLine("3. MANY-TO-ONE (Comment -> User & Post)");
        Console.WriteLine("═══════════════════════════════════════════════════════");
        DemoMultipleForeignKeys(metadata);

        Console.WriteLine("\n═══════════════════════════════════════════════════════");
        Console.WriteLine("4. SELF-REFERENCING (Employee -> Manager)");
        Console.WriteLine("═══════════════════════════════════════════════════════");
        DemoSelfReferencing(metadata);

        Console.WriteLine("\n═══════════════════════════════════════════════════════");
        Console.WriteLine("5. OPTIONAL RELATIONSHIP (Order -> ShippingAddress)");
        Console.WriteLine("═══════════════════════════════════════════════════════");
        DemoOptionalRelationship(metadata);

        Console.WriteLine("\n\n═══════════════════════════════════════════════════════");
        Console.WriteLine("CZĘŚĆ 2: OPERACJE NA BAZIE DANYCH");
        Console.WriteLine("═══════════════════════════════════════════════════════\n");

        RunDatabaseDemo();
    }

    private static void DemoOneToMany(IReadOnlyDictionary<Type, EntityMap> metadata)
    {
        var blogMap = metadata[typeof(Blog)];

        Console.WriteLine($"Tabela: {blogMap.TableName}");
        Console.WriteLine("\nNavigation Properties:");

        foreach (var navProp in blogMap.NavigationProperties)
        {
            Console.WriteLine($"  - {navProp.PropertyInfo.Name}");
            Console.WriteLine($"    - IsCollection: {navProp.IsCollection}");
            Console.WriteLine($"    - TargetType: {navProp.TargetType?.Name}");
            Console.WriteLine($"    - ColumnName: {navProp.ColumnName ?? "(null - nie mapowane na kolumnę)"}");
        }

        Console.WriteLine("\nScalar Properties:");
        foreach (var prop in blogMap.ScalarProperties)
        {
            Console.WriteLine($"  - {prop.PropertyInfo.Name} → {prop.ColumnName}");
        }
    }

    private static void DemoManyToOne(IReadOnlyDictionary<Type, EntityMap> metadata)
    {
        var postMap = metadata[typeof(Post)];

        Console.WriteLine($"Tabela: {postMap.TableName}");
        Console.WriteLine("\nNavigation Properties:");

        foreach (var navProp in postMap.NavigationProperties)
        {
            Console.WriteLine($"  - {navProp.PropertyInfo.Name}");
            Console.WriteLine($"    - IsCollection: {navProp.IsCollection}");
            Console.WriteLine($"    - TargetType: {navProp.TargetType?.Name}");
            Console.WriteLine($"    - ForeignKeyName: {navProp.ForeignKeyName ?? "(nie zdefiniowano)"}");
        }

        Console.WriteLine("\nScalar Properties (w tym klucze obce):");
        foreach (var prop in postMap.ScalarProperties)
        {
            var isFk = postMap.NavigationProperties.Any(n => n.ForeignKeyName == prop.PropertyInfo.Name);
            var marker = isFk ? " [FK]" : "";
            Console.WriteLine($"  - {prop.PropertyInfo.Name} → {prop.ColumnName}{marker}");
        }
    }

    private static void DemoMultipleForeignKeys(IReadOnlyDictionary<Type, EntityMap> metadata)
    {
        var commentMap = metadata[typeof(Comment)];

        Console.WriteLine($"Tabela: {commentMap.TableName}");
        Console.WriteLine("\nMultiple Navigation Properties:");

        foreach (var navProp in commentMap.NavigationProperties)
        {
            Console.WriteLine($"  - {navProp.PropertyInfo.Name}");
            Console.WriteLine($"    - TargetType: {navProp.TargetType?.Name}");
            Console.WriteLine($"    - ForeignKeyName: {navProp.ForeignKeyName ?? "(automatyczna detekcja)"}");
        }
    }

    private static void DemoSelfReferencing(IReadOnlyDictionary<Type, EntityMap> metadata)
    {
        var employeeMap = metadata[typeof(Employee)];

        Console.WriteLine($"Tabela: {employeeMap.TableName}");
        Console.WriteLine("\nSelf-referencing Navigation Properties:");

        foreach (var navProp in employeeMap.NavigationProperties)
        {
            Console.WriteLine($"  - {navProp.PropertyInfo.Name}");
            Console.WriteLine($"    - TargetType: {navProp.TargetType?.Name}");
            Console.WriteLine($"    - IsCollection: {navProp.IsCollection}");
            Console.WriteLine($"    - ForeignKeyName: {navProp.ForeignKeyName}");
        }
    }

    private static void DemoOptionalRelationship(IReadOnlyDictionary<Type, EntityMap> metadata)
    {
        var orderMap = metadata[typeof(DemoOrder)];

        Console.WriteLine($"Tabela: {orderMap.TableName}");
        Console.WriteLine("\nOptional Navigation Property (nullable FK):");

        var addressNav = orderMap.NavigationProperties.FirstOrDefault(p => p.PropertyInfo.Name == "ShippingAddress");
        if (addressNav != null)
        {
            Console.WriteLine($"  - {addressNav.PropertyInfo.Name}");
            Console.WriteLine($"    - TargetType: {addressNav.TargetType?.Name}");
            Console.WriteLine($"    - ForeignKeyName: {addressNav.ForeignKeyName}");

            var fkProp = orderMap.ScalarProperties.FirstOrDefault(p => p.PropertyInfo.Name == addressNav.ForeignKeyName);
            if (fkProp != null)
            {
                var isNullable = Nullable.GetUnderlyingType(fkProp.PropertyType) != null;
                Console.WriteLine($"    - FK Type: {fkProp.PropertyType.Name} (nullable: {isNullable})");
            }
        }
    }

    private static void RunDatabaseDemo()
    {
        var connectionString = "Data Source=navigation_demo.db;";
        var metadataStore = new MetadataStoreBuilder()
            .AddAssembly(typeof(NavigationPropertiesDemo).Assembly)
            .UseNamingStrategy(new PascalCaseNamingStrategy())
            .Build();

        var configuration = new DbConfiguration(connectionString, metadataStore);

        using (var context = new BlogDbContext(configuration))
        {
            // Przygotowanie bazy danych
            Console.WriteLine("Tworzenie bazy danych...");
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();
            Console.WriteLine("Baza danych utworzona\n");

            // Scenariusz 1: Blog z postami
            Console.WriteLine("═══════════════════════════════════════════════════════");
            Console.WriteLine("SCENARIUSZ 1: Tworzenie Blog z Posts");
            Console.WriteLine("═══════════════════════════════════════════════════════");

            var techBlog = new Blog
            {
                Name = "Tech Blog",
                Description = "Blog o technologii"
            };

            context.Blogs.Add(techBlog);
            context.SaveChanges();
            Console.WriteLine($"Dodano blog: {techBlog.Name} (ID: {techBlog.Id})");

            // Dodajemy posty do bloga
            var post1 = new Post
            {
                Title = "C# 12 Nowości",
                Content = "Omówienie nowych funkcji C# 12",
                BlogId = techBlog.Id
            };

            var post2 = new Post
            {
                Title = "Navigation Properties w ORM",
                Content = "Jak używać navigation properties",
                BlogId = techBlog.Id
            };

            context.Posts.Add(post1);
            context.Posts.Add(post2);
            context.SaveChanges();
            Console.WriteLine($"Dodano post: {post1.Title} (ID: {post1.Id})");
            Console.WriteLine($"Dodano post: {post2.Title} (ID: {post2.Id})\n");

            // Scenariusz 2: Komentarz z użytkownikiem i postem
            Console.WriteLine("═══════════════════════════════════════════════════════");
            Console.WriteLine("SCENARIUSZ 2: Komentarz z wieloma FK");
            Console.WriteLine("═══════════════════════════════════════════════════════");

            var user = new BlogUser
            {
                Username = "jan_kowalski",
                Email = "jan@example.pl"
            };

            context.BlogUsers.Add(user);
            context.SaveChanges();
            Console.WriteLine($"Dodano użytkownika: {user.Username} (ID: {user.Id})");

            var comment = new Comment
            {
                Text = "Świetny artykuł!",
                CreatedAt = DateTime.Now,
                PostId = post1.Id,
                AuthorId = user.Id
            };

            context.Comments.Add(comment);
            context.SaveChanges();
            Console.WriteLine($"Dodano komentarz: {comment.Text} (ID: {comment.Id})\n");

            // Scenariusz 3: Hierarchia pracowników
            Console.WriteLine("═══════════════════════════════════════════════════════");
            Console.WriteLine("SCENARIUSZ 3: Self-referencing (Employee hierarchy)");
            Console.WriteLine("═══════════════════════════════════════════════════════");

            var ceo = new Employee
            {
                FirstName = "Anna",
                LastName = "Nowak",
                ManagerId = null // CEO nie ma menedżera
            };

            context.Employees.Add(ceo);
            context.SaveChanges();
            Console.WriteLine($"Dodano CEO: {ceo.FirstName} {ceo.LastName} (ID: {ceo.Id})");

            var manager = new Employee
            {
                FirstName = "Piotr",
                LastName = "Wiśniewski",
                ManagerId = ceo.Id
            };

            var dev1 = new Employee
            {
                FirstName = "Tomasz",
                LastName = "Kowalczyk",
                ManagerId = null // Ustawimy po zapisaniu managera
            };

            context.Employees.Add(manager);
            context.SaveChanges();
            Console.WriteLine($"Dodano Manager: {manager.FirstName} {manager.LastName} (ID: {manager.Id})");

            dev1.ManagerId = manager.Id;
            context.Employees.Add(dev1);
            context.SaveChanges();
            Console.WriteLine($"Dodano Developer: {dev1.FirstName} {dev1.LastName} (ID: {dev1.Id})\n");

            // Scenariusz 4: Opcjonalna relacja
            Console.WriteLine("═══════════════════════════════════════════════════════");
            Console.WriteLine("SCENARIUSZ 4: Opcjonalna relacja (Order + Address)");
            Console.WriteLine("═══════════════════════════════════════════════════════");

            var address = new Address
            {
                Street = "ul. Główna 123",
                City = "Warszawa",
                PostalCode = "00-001",
                Country = "Polska"
            };

            context.Addresses.Add(address);
            context.SaveChanges();
            Console.WriteLine($"Dodano adres: {address.Street}, {address.City}");

            var orderWithAddress = new DemoOrder
            {
                OrderDate = DateTime.Now,
                TotalAmount = 1299.99m,
                ShippingAddressId = address.Id
            };

            var orderWithoutAddress = new DemoOrder
            {
                OrderDate = DateTime.Now,
                TotalAmount = 499.99m,
                ShippingAddressId = null // Opcjonalne
            };

            context.DemoOrders.Add(orderWithAddress);
            context.DemoOrders.Add(orderWithoutAddress);
            context.SaveChanges();
            Console.WriteLine($"Dodano zamówienie z adresem (ID: {orderWithAddress.Id})");
            Console.WriteLine($"Dodano zamówienie bez adresu (ID: {orderWithoutAddress.Id})\n");

            // CZĘŚĆ 3: Pobieranie danych
            Console.WriteLine("\n═══════════════════════════════════════════════════════");
            Console.WriteLine("CZĘŚĆ 3: POBIERANIE DANYCH I NAVIGATION PROPERTIES");
            Console.WriteLine("═══════════════════════════════════════════════════════\n");

            DemoReadingWithNavigationProperties(context, post1.Id, techBlog.Id, comment.Id, dev1.Id, orderWithAddress.Id);
        }

        Console.WriteLine("\nDemo zakończone pomyślnie!");
    }

    private static void DemoReadingWithNavigationProperties(
        BlogDbContext context,
        int postId,
        int blogId,
        int commentId,
        int employeeId,
        int orderId)
    {
        Console.WriteLine("─────────────────────────────────────────────────────");
        Console.WriteLine("1. Pobieranie Post (Many-to-One do Blog) - Eager Loading");
        Console.WriteLine("─────────────────────────────────────────────────────");

        // Użycie .Include() dla eager loading
        var postsWithBlog = context.Posts
            .Include(p => p.Blog)
            .ToList();

        var post = postsWithBlog.FirstOrDefault(p => p.Id == postId);

        if (post != null)
        {
            Console.WriteLine($"Post: {post.Title}");
            Console.WriteLine($"   BlogId (FK): {post.BlogId}");
            Console.WriteLine($"   Blog (nav): {(post.Blog == null ? "NULL " : post.Blog.Name + "")}");

            if (post.Blog != null)
            {
                Console.WriteLine($"\nSUCCESS Blog automatycznie załadowany:");
                Console.WriteLine($"   - Blog Name: {post.Blog.Name}");
                Console.WriteLine($"   - Blog Description: {post.Blog.Description}");
            }
        }

        Console.WriteLine("\n─────────────────────────────────────────────────────");
        Console.WriteLine("2. Pobieranie Blog (One-to-Many do Posts) - Eager Loading");
        Console.WriteLine("─────────────────────────────────────────────────────");

        // Użycie .Include() dla kolekcji
        var blogsWithPosts = context.Blogs
            .Include(b => b.Posts)
            .ToList();

        var blogFromDb = blogsWithPosts.FirstOrDefault(b => b.Id == blogId);

        if (blogFromDb != null)
        {
            Console.WriteLine($"Blog: {blogFromDb.Name}");
            Console.WriteLine($"   Posts (nav): Count = {blogFromDb.Posts?.Count ?? 0}");

            if (blogFromDb.Posts != null && blogFromDb.Posts.Count > 0)
            {
                Console.WriteLine($"\nSUCCESS Kolekcja Posts automatycznie załadowana:");
                foreach (var p in blogFromDb.Posts)
                {
                    Console.WriteLine($"   - {p.Title}");
                }
            }
        }

        Console.WriteLine("\n─────────────────────────────────────────────────────");
        Console.WriteLine("3. Pobieranie Comment (Multiple FK) - Multiple Includes");
        Console.WriteLine("─────────────────────────────────────────────────────");

        // Użycie wielu .Include() dla różnych navigation properties
        var commentsWithRelated = context.Comments
            .Include(c => c.Post)
            .Include(c => c.Author)
            .ToList();

        var commentFromDb = commentsWithRelated.FirstOrDefault(c => c.Id == commentId);

        if (commentFromDb != null)
        {
            Console.WriteLine($"Comment: {commentFromDb.Text}");
            Console.WriteLine($"   PostId (FK): {commentFromDb.PostId}");
            Console.WriteLine($"   AuthorId (FK): {commentFromDb.AuthorId}");
            Console.WriteLine($"   Post (nav): {(commentFromDb.Post == null ? "NULL " : commentFromDb.Post.Title + "")}");
            Console.WriteLine($"   Author (nav): {(commentFromDb.Author == null ? "NULL " : commentFromDb.Author.Username + "")}");

            if (commentFromDb.Post != null && commentFromDb.Author != null)
            {
                Console.WriteLine($"\nSUCCESS Obie navigation properties załadowane:");
                Console.WriteLine($"   - Post: {commentFromDb.Post.Title}");
                Console.WriteLine($"   - Author: {commentFromDb.Author.Username}");
            }
        }

        Console.WriteLine("\n─────────────────────────────────────────────────────");
        Console.WriteLine("4. Pobieranie Employee (Self-referencing) - Eager Loading");
        Console.WriteLine("─────────────────────────────────────────────────────");

        // Self-referencing z .Include()
        var employeesWithManager = context.Employees
            .Include(e => e.Manager)
            .ToList();

        var employee = employeesWithManager.FirstOrDefault(e => e.Id == employeeId);

        if (employee != null)
        {
            Console.WriteLine($"Employee: {employee.FirstName} {employee.LastName}");
            Console.WriteLine($"   ManagerId (FK): {employee.ManagerId?.ToString() ?? "NULL"}");

            if (employee.Manager != null)
            {
                Console.WriteLine($"   Manager (nav): {employee.Manager.FirstName} {employee.Manager.LastName} ");
                Console.WriteLine($"\nSUCCESS Manager automatycznie załadowany (self-referencing):");
                Console.WriteLine($"   - {employee.Manager.FirstName} {employee.Manager.LastName}");
            }
            else if (employee.ManagerId == null)
            {
                Console.WriteLine($"   Manager (nav): NULL (brak menedżera - to CEO)");
            }
        }

        Console.WriteLine("\n─────────────────────────────────────────────────────");
        Console.WriteLine("5. Pobieranie Order (Optional relationship) - Eager Loading");
        Console.WriteLine("─────────────────────────────────────────────────────");

        // Opcjonalna relacja z .Include()
        var ordersWithAddress = context.DemoOrders
            .Include(o => o.ShippingAddress)
            .ToList();

        var order = ordersWithAddress.FirstOrDefault(o => o.Id == orderId);

        if (order != null)
        {
            Console.WriteLine($"Order ID: {order.Id}");
            Console.WriteLine($"   Amount: {order.TotalAmount:C}");
            Console.WriteLine($"   ShippingAddressId (FK): {order.ShippingAddressId?.ToString() ?? "NULL"}");

            if (order.ShippingAddress != null)
            {
                Console.WriteLine($"   ShippingAddress (nav): {order.ShippingAddress.Street}, {order.ShippingAddress.City}");
                Console.WriteLine($"\nSUCCESS Opcjonalny adres załadowany:");
                Console.WriteLine($"   - {order.ShippingAddress.Street}");
                Console.WriteLine($"   - {order.ShippingAddress.PostalCode} {order.ShippingAddress.City}");
                Console.WriteLine($"   - {order.ShippingAddress.Country}");
            }
            else
            {
                Console.WriteLine($"   ShippingAddress (nav): NULL (relacja opcjonalna)");
            }
        }

        Console.WriteLine("\n═══════════════════════════════════════════════════════");
        Console.WriteLine("SCENARIUSZ 6: ThenInclude - Blog - Posts - Comments");
        Console.WriteLine("═══════════════════════════════════════════════════════");

        // Załaduj blog z postami i komentarzami w JEDNYM zapytaniu
        var blogsWithPostsAndComments = context.Blogs
            .Include(b => b.Posts)
                .ThenInclude(p => p.Comments)
            .ToList();

        var blogWithComments = blogsWithPostsAndComments.FirstOrDefault();

        if (blogWithComments != null)
        {
            Console.WriteLine($"Blog: {blogWithComments.Name}");
            Console.WriteLine($"   Posts: {blogWithComments.Posts?.Count ?? 0}");

            if (blogWithComments.Posts != null)
            {
                foreach (var blogPost in blogWithComments.Posts)
                {
                    Console.WriteLine($"   |- Post: {blogPost.Title}");
                    Console.WriteLine($"      Comments: {blogPost.Comments?.Count ?? 0}");

                    if (blogPost.Comments != null && blogPost.Comments.Any())
                    {
                        foreach (var postComment in blogPost.Comments)
                        {
                            Console.WriteLine($"      - Comment: {postComment.Text}");
                        }
                    }
                }

                Console.WriteLine($"\nSUCCESS ThenInclude załadował 3 poziomy hierarchii w 1 zapytaniu:");
                Console.WriteLine($"   Blog - {blogWithComments.Posts.Count} Posts - {blogWithComments.Posts.Sum(p => p.Comments?.Count ?? 0)} Comments");
            }
        }

        Console.WriteLine("\n═══════════════════════════════════════════════════════");
        Console.WriteLine("PODSUMOWANIE");
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("Navigation properties są poprawnie mapowane");
        Console.WriteLine("Klucze obce (FK) są zapisywane i odczytywane");
        Console.WriteLine("Eager loading (.Include)");
        Console.WriteLine("Multiple includes (.Include().Include())");
        Console.WriteLine("ThenInclude (zagnieżdżone includes)");
        Console.WriteLine("Collection navigation properties");
        Console.WriteLine("Self-referencing relations");
        Console.WriteLine("Optional relations");
        Console.WriteLine("\nJak używać:");
        Console.WriteLine("  context.Posts.Include(p => p.Blog).ToList()");
        Console.WriteLine("  context.Comments.Include(c => c.Post).Include(c => c.Author).ToList()");
        Console.WriteLine("  context.Blogs.Include(b => b.Posts).ToList()");
        Console.WriteLine("  context.Blogs.Include(b => b.Posts).ThenInclude(p => p.Comments).ToList()");
        Console.WriteLine("═══════════════════════════════════════════════════════");
    }
}

// DbContext dla demo navigation properties
public class BlogDbContext : DbContext
{
    public BlogDbContext(DbConfiguration configuration) : base(configuration)
    {
    }

    public DbSet<Blog> Blogs => Set<Blog>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<BlogUser> BlogUsers => Set<BlogUser>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<DemoOrder> DemoOrders => Set<DemoOrder>();
    public DbSet<Address> Addresses => Set<Address>();
}

#region Demo Models

[Table("Blogs")]
public class Blog
{
    [Key]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public List<Post> Posts { get; set; } = new();
}

[Table("Posts")]
public class Post
{
    [Key]
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public int BlogId { get; set; }

    [ForeignKey("BlogId")]
    public Blog Blog { get; set; } = null!;

    public List<Comment> Comments { get; set; } = new();
}

[Table("Comments")]
public class Comment
{
    [Key]
    public int Id { get; set; }

    public string Text { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public int AuthorId { get; set; }

    [ForeignKey("AuthorId")]
    public BlogUser Author { get; set; } = null!;

    public int PostId { get; set; }

    [ForeignKey("PostId")]
    public Post Post { get; set; } = null!;
}

[Table("BlogUsers")]
public class BlogUser
{
    [Key]
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public List<Comment> Comments { get; set; } = new();
}

[Table("Employees")]
public class Employee
{
    [Key]
    public int Id { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public int? ManagerId { get; set; }

    [ForeignKey("ManagerId")]
    public Employee? Manager { get; set; }

    public List<Employee> Subordinates { get; set; } = new();
}

[Table("DemoOrders")]
public class DemoOrder
{
    [Key]
    public int Id { get; set; }

    public DateTime OrderDate { get; set; }

    public decimal TotalAmount { get; set; }

    public int? ShippingAddressId { get; set; }

    [ForeignKey("ShippingAddressId")]
    public Address? ShippingAddress { get; set; }
}

[Table("Addresses")]
public class Address
{
    [Key]
    public int Id { get; set; }

    public string Street { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string PostalCode { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;
}

#endregion
