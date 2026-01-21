namespace ORM_v1.Mapping.Strategies
{
    public interface IInheritanceStrategy
    {
        // Znacznik typu strategii (dla ułatwienia debugowania)
        string Name { get; }
        // Metoda walidacji mapy encji zgodnie ze strategią
        void Validate(EntityMap map);
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

        public void Validate(EntityMap map)
        {
            if (map.BaseMap != null)
            {
                if (!string.Equals(map.TableName, map.BaseMap.TableName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"TPH Error: Entity '{map.EntityType.Name}' maps to '{map.TableName}', " +
                        $"but its parent '{map.BaseMap.EntityType.Name}' maps to '{map.BaseMap.TableName}'. " +
                        "In TPH, they must share the same table.");
                }
            }
        }
    }

    // Strategia TPC (Table Per Concrete Class) - prosta, bez dodatkowych danych
    public class TablePerConcreteClassStrategy : IInheritanceStrategy
    {
        public string Name => "TablePerConcreteClass";
        public void Validate(EntityMap map)
        {   
            if (map.Discriminator != null || map.DiscriminatorColumn != null)
            {
                throw new InvalidOperationException(
                    $"TPC Error: Entity '{map.EntityType.Name}' uses TPC strategy " +
                    "but has Discriminator values set. This is not allowed.");
            }
        }
    }
}