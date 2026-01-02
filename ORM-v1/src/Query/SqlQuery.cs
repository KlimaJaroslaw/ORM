namespace ORM_v1.Query
{
    public class SqlQuery
    {
        public string Sql { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new();
    }
}
