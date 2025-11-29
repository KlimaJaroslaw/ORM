using ORM_v1.Attributes;

namespace ORM.Tests.TestEntities
{
    [Table("Users")]
    public class UserTestEntity
    {
        [Key]
        public int Id { get; set; }

        [Column("first_name")]
        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        [Ignore]
        public string IgnoredProp { get; set; } = "ignore me";
    }
}
