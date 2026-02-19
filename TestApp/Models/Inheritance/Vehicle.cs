using ORM_v1.Attributes;
using ORM_v1.Mapping;
using ORM_v1.Mapping.Strategies;

namespace TestApp.Models.Inheritance;

[Table("Vehicles")]
[InheritanceStrategy(InheritanceStrategy.TablePerType)]
public abstract class Vehicle
{
    [Key]
    public int Id { get; set; }

    public string Brand { get; set; } = string.Empty;

    public int Year { get; set; }

    public decimal Price { get; set; }
}
