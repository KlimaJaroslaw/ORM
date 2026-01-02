using ORM_v1.Mapping;

namespace ORM_v1.Query
{
    public class QueryModel
    {
        public EntityMap PrimaryEntity { get; set; } = null!;
        public string? PrimaryEntityAlias { get; set; }
        public List<PropertyMap> SelectColumns { get; set; } = new();
        public bool SelectAllColumns { get; set; } = true;
        public bool Distinct { get; set; } = false;
        public List<JoinClause> Joins { get; set; } = new();
        public string? WhereClause { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
        public List<PropertyMap> GroupByColumns { get; set; } = new();
        public string? HavingClause { get; set; }
        public List<OrderByClause> OrderBy { get; set; } = new();
        public int? Take { get; set; }
        public int? Skip { get; set; }
        public List<AggregateFunction> Aggregates { get; set; } = new();
    }

    public class JoinClause
    {
        public EntityMap JoinedEntity { get; set; } = null!;
        public PropertyMap LeftProperty { get; set; } = null!;
        public PropertyMap RightProperty { get; set; } = null!;
        public JoinType JoinType { get; set; } = JoinType.Inner;
        public string? Alias { get; set; }
    }

    public enum JoinType
    {
        Inner,
        Left,
        Right,
        Full
    }

    public class OrderByClause
    {
        public PropertyMap Property { get; set; } = null!;
        public EntityMap? Entity { get; set; } 
        public string? TableAlias { get; set; } 
        public bool IsAscending { get; set; } = true;
    }

    public class AggregateFunction
    {
        public AggregateFunctionType FunctionType { get; set; }
        public PropertyMap? Property { get; set; } 
        public string? Alias { get; set; } 
    }

    public enum AggregateFunctionType
    {
        Count,
        Sum,
        Avg,
        Min,
        Max
    }
}
