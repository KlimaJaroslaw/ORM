using ORM_v1.core;
using ORM_v1.Configuration;
using ORM_v1.Mapping;
using TestApp.Models;

namespace TestApp;

public class AppDbContext : DbContext
{
    public AppDbContext(DbConfiguration configuration) : base(configuration)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();
}
