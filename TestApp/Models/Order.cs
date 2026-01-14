using ORM_v1.Attributes;

namespace TestApp.Models;

[Table("Orders")]
public class Order
{
    [Key]
    public int Id { get; set; }

    [Column("customer_id")]
    public int CustomerId { get; set; }

    [Column("order_date")]
    public DateTime OrderDate { get; set; }

    [Column("total_amount")]
    public decimal TotalAmount { get; set; }

    [Ignore]
    public Customer? Customer { get; set; }
}
