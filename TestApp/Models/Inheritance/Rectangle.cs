using ORM_v1.Attributes;

namespace TestApp.Models.Inheritance;

[Table("Rectangles")]
public class Rectangle : Shape
{
    public double Width { get; set; }

    public double Height { get; set; }
}
