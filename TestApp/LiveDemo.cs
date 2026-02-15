using ORM_v1.Configuration;
using ORM_v1.Mapping;
using System;
using System.Collections.Generic;
using ORM_v1.Attributes;
using System.Linq;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Threading.Tasks;

namespace TestApp
{
    public class LiveDemo
    {
        public void Main()
        {
            string connectionstring = "Data Source=live_demo2.db;";
            var metadataStore = new MetadataStoreBuilder()
                .AddAssembly(typeof(Person).Assembly)
                .UseNamingStrategy(new PascalCaseNamingStrategy())
                .Build();
            var config = new DbConfiguration(connectionstring, metadataStore);
            using var context = new AppDbContext(config);
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();
        }
        
            
    }

    [Table("Persons")]
    public abstract class Person
    {
        [Key]
        public int Id { get; set; }
        public string FirstName { get; set; }
    }

    [Table("Employees")]
    public class Employee : Person
    {
        [Key]
        public int Id { get; set; }
        public string Position { get; set; }
    }

    [Table("Students")]
    public class Student : Person
    {
        [Key]
        public int Id { get; set; }
        public string Email { get; set; }
        public List<Subject> Subjects { get; set; }
    }

    [Table("Subjects")]
    public class Subject
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public List<Student> Studens{ get; set; }
    }
}
