using ORM_v1.Attributes;

namespace TestApp.Models;

[Table("Categories")]
public class Category
{
    [Key]
    public int Id { get; set; }

    [Column("category_name")]
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    // Navigation property - kolekcja produktów w tej kategorii
    public List<Product> Products { get; set; } = new();
}
