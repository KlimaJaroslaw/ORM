namespace ORM.Tests.TestEntities
{
    public enum Role
    {
        User = 0,
        Admin = 1
    }

    public class UserWithEnums
    {
        public int Id { get; set; }

        public Role Role { get; set; }

        public Role? OptionalRole { get; set; }
    }
}
