using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ORM_v1.Attributes;

namespace ORM_v1.Mapping
{
    public sealed class EntityMap
    {
        public Type EntityType { get; }
        public string TableName { get; }
        public bool IsAbstract { get; }
        public InheritanceStrategy Strategy { get; }
        public EntityMap? BaseMap { get; }
        public string? Discriminator { get; }
        public string? DiscriminatorColumn { get; }

        public PropertyMap KeyProperty { get; }
        public IReadOnlyList<PropertyMap> Properties { get; }
        public IReadOnlyList<PropertyMap> ScalarProperties { get; }
        public IReadOnlyList<PropertyMap> NavigationProperties { get; }

        private readonly IReadOnlyDictionary<string, PropertyMap> _columnMapping;
        private readonly IReadOnlyDictionary<string, PropertyMap> _propertyMapping;

        public EntityMap(
            Type entityType,
            string tableName,
            bool isAbstract,
            EntityMap? baseMap,
            InheritanceStrategy strategy,
            string? discriminator,
            string? discriminatorColumn,
            PropertyMap keyProperty,
            IEnumerable<PropertyMap> allProperties)
        {
            EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));

            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Table name cannot be null or whitespace.", nameof(tableName));

            TableName = tableName;
            IsAbstract = isAbstract;
            BaseMap = baseMap;
            Strategy = strategy;

            if (Strategy == InheritanceStrategy.TablePerHierarchy)
            {
                DiscriminatorColumn = discriminatorColumn ?? baseMap?.DiscriminatorColumn;
            }
            else
            {
                DiscriminatorColumn = null;
            }

            if (Strategy == InheritanceStrategy.TablePerConcreteClass)
            {
                Discriminator = null;
            }
            else
            {
                Discriminator = discriminator;
            }

            if (BaseMap != null && 
                Strategy == InheritanceStrategy.TablePerHierarchy && 
                !IsAbstract && 
                string.IsNullOrWhiteSpace(Discriminator))
            {
                Discriminator = entityType.Name;
            }

            if (BaseMap != null && Strategy == InheritanceStrategy.TablePerHierarchy)
            {
                if (!string.Equals(TableName, BaseMap.TableName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"TPH Error: Entity '{EntityType.Name}' is derived from '{BaseMap.EntityType.Name}' " +
                        $"but maps to a different table ('{TableName}' vs '{BaseMap.TableName}'). " +
                        "In Table-Per-Hierarchy, all classes must map to the same table.");
                }
            }

            if (Strategy == InheritanceStrategy.TablePerConcreteClass)
            {
                if (Strategy == InheritanceStrategy.TablePerConcreteClass)
                {
                    if (!string.IsNullOrWhiteSpace(discriminator) || discriminatorColumn != null)
                    {
                        throw new InvalidOperationException(
                            $"TPC Error: Entity '{EntityType.Name}' uses Table-Per-Concrete-Class strategy " +
                            "and cannot define discriminator or discriminator column.");
                    }
                }
            }

            KeyProperty = keyProperty ?? throw new ArgumentNullException(nameof(keyProperty));

            var propsList = allProperties.ToList();
            Properties = propsList.AsReadOnly();

            ScalarProperties = propsList
                .Where(p => !p.IsNavigation && !p.IsIgnored)
                .ToList()
                .AsReadOnly();

            NavigationProperties = propsList
                .Where(p => p.IsNavigation && !p.IsIgnored)
                .ToList()
                .AsReadOnly();

            ValidateKey(keyProperty);

            var colMap = new Dictionary<string, PropertyMap>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in ScalarProperties)
            {
                if (!string.IsNullOrEmpty(prop.ColumnName))
                {
                    if (!colMap.ContainsKey(prop.ColumnName))
                        colMap[prop.ColumnName] = prop;
                }
            }
            _columnMapping = new ReadOnlyDictionary<string, PropertyMap>(colMap);

            var propMap = new Dictionary<string, PropertyMap>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in Properties)
            {
                if (!propMap.ContainsKey(prop.PropertyInfo.Name))
                    propMap[prop.PropertyInfo.Name] = prop;
            }
            _propertyMapping = new ReadOnlyDictionary<string, PropertyMap>(propMap);
        }

        public PropertyMap? FindPropertyByColumn(string columnName)
        {
            if (string.IsNullOrEmpty(columnName)) return null;
            _columnMapping.TryGetValue(columnName, out var map);
            return map;
        }

        public PropertyMap? FindPropertyByName(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName)) return null;
            _propertyMapping.TryGetValue(propertyName, out var map);
            return map;
        }

        public EntityMap RootMap
        {
            get
            {
                var current = this;
                while (current.BaseMap != null)
                {
                    current = current.BaseMap;
                }
                return current;
            }
        }

        public bool IsHierarchyRoot => BaseMap == null;

        public bool UsesDiscriminator => DiscriminatorColumn != null;

        public bool HasAutoIncrementKey =>
            KeyProperty.UnderlyingType == typeof(int) ||
            KeyProperty.UnderlyingType == typeof(long);

        private static void ValidateKey(PropertyMap key)
        {
            if (key.IsIgnored)
                throw new InvalidOperationException($"Property '{key.PropertyInfo.Name}' cannot be marked as [Ignore] and [Key].");
            if (key.IsNavigation)
                throw new InvalidOperationException($"Navigation property '{key.PropertyInfo.Name}' cannot be used as a primary key.");
        }
    }
}