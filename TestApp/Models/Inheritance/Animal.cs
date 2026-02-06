using ORM_v1.Attributes;

namespace TestApp.Models.Inheritance;

[Table("Animals")]
public abstract class Animal
{
    [Key]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Age { get; set; }
}
