namespace ORM_v1.Mapping.Strategies
{
    public interface IInheritanceStrategy
    {
        string Name { get; }
        void Validate(EntityMap map);
    }

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

    public class TablePerTypeStrategy : IInheritanceStrategy
    {
        public string Name => "TablePerType";

        public void Validate(EntityMap map)
        {

            if (map.Discriminator != null || map.DiscriminatorColumn != null)
            {
                throw new InvalidOperationException(
                   $"TPT Error: Entity '{map.EntityType.Name}' uses TablePerType strategy " +
                   "but has Discriminator values set. TPT relies on JOINs, not discriminators.");
            }

            if (map.BaseMap != null)
            {
                if (string.Equals(map.TableName, map.BaseMap.TableName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"TPT Error: Entity '{map.EntityType.Name}' maps to '{map.TableName}', " +
                        $"and its parent '{map.BaseMap.EntityType.Name}' also maps to '{map.BaseMap.TableName}'. " +
                        "In Table Per Type (TPT), derived classes MUST have their own, distinct tables.");
                }
            }
        }
    }
}