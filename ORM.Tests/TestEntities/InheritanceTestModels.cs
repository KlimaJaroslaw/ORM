using System.Collections.Generic;
using ORM_v1.Attributes;

namespace ORM.Tests.Models
{
    [Table("Animals")]
    public class Animal
    {
        [Key] public int Id { get; set; }
        public string Name { get; set; } = null!;
    }

    public class Dog : Animal
    {
        public string Breed { get; set; } = null!;
    }

    [Table("Payments")]
    public abstract class Payment
    {
        [Key] public int Id { get; set; }
        public decimal Amount { get; set; }
    }

    [Table("CreditCardPayments")]
    public class CreditCardPayment : Payment
    {
        public string CardNumber { get; set; } = null!;
    }

    [Table("Users")]
    public class User
    {
        public int Id { get; set; }
        public string Login { get; set; } = null!;
        public List<Order> Orders { get; set; } = null!;
    }

    [Table("Orders")]
    public class Order
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        
        [ForeignKey("UserId")]
        public User User { get; set; } = null!;
    }
}