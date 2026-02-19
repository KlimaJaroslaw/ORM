using ORM_v1.Attributes;

namespace TestApp.Models.Inheritance;

[InheritanceStrategy(ORM_v1.Mapping.Strategies.InheritanceStrategy.TablePerConcreteClass)]
public abstract class Shape
{
    public int Id { get; set; }

    public string Color { get; set; } = string.Empty;
}
