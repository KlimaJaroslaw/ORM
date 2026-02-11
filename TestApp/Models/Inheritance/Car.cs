using ORM_v1.Attributes;

namespace TestApp.Models.Inheritance;

[Table("Cars")]
public class Car : Vehicle
{
    public int NumberOfDoors { get; set; }

    public string EngineType { get; set; } = string.Empty;
}
