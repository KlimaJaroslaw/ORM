namespace ORM_v1.Mapping.Strategies
{
    public interface IInheritanceStrategy
    {
        // Znacznik typu strategii (dla ułatwienia debugowania)
        string Name { get; }
    }

    // Strategia TPH (Table Per Hierarchy) - wymaga dyskryminatora
    public class TablePerHierarchyStrategy : IInheritanceStrategy
    {
        public string Name => "TablePerHierarchy";

        public string DiscriminatorColumn { get; }
        public string DiscriminatorValue { get; }

        public TablePerHierarchyStrategy(string column, string value)
        {
            DiscriminatorColumn = column;
            DiscriminatorValue = value;
        }
    }

    // Strategia TPC (Table Per Concrete Class) - prosta, bez dodatkowych danych
    public class TablePerConcreteClassStrategy : IInheritanceStrategy
    {
        public string Name => "TablePerConcreteClass";
    }
}