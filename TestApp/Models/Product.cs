using ORM_v1.Attributes;

namespace TestApp.Models;

[Table("Products")]
public class Product
{
    [Key]
    public int Id { get; set; }

    [Column("product_name")]
    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int Stock { get; set; }

    [Column("category_id")]
    public int CategoryId { get; set; }

    [Ignore]
    public Category? Category { get; set; }
}
