using ORM_v1.Attributes;

namespace TestApp.Models.Inheritance;

[Table("Trucks")]
public class Truck : Vehicle
{
    public decimal PayloadCapacity { get; set; }

    public int NumberOfAxles { get; set; }
}
