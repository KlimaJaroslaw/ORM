using ORM_v1.Attributes;

namespace TestApp.Models;

[Table("Customers")]
public class Customer
{
    [Key]
    public int Id { get; set; }

    [Column("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [Column("last_name")]
    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    [Column("registration_date")]
    public DateTime RegistrationDate { get; set; }

    public CustomerStatus Status { get; set; }

    [Ignore]
    public string FullName => $"{FirstName} {LastName}";
}

public enum CustomerStatus
{
    Inactive = 0,
    Active = 1,
    Premium = 2
}
