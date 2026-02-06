using ORM_v1.Attributes;

namespace TestApp.Models.Inheritance;

[Table("Circles")]
public class Circle : Shape
{
    public double Radius { get; set; }
}
