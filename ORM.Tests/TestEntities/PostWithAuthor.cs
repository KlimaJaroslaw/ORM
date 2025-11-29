using ORM_v1.Attributes;

namespace ORM.Tests.TestEntities
{
    public class Author
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class PostWithAuthor
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public Author Author { get; set; } = null!;
    }
}
