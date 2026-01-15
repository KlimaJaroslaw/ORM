using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ORM_v1.Attributes;

namespace ORM_v1.Mapping
{
    public sealed class ModelBuilder
    {
        private readonly Assembly _assembly;

        public ModelBuilder(Assembly assembly)
        {
            _assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
        }

        public IReadOnlyDictionary<Type, EntityMap> BuildModel(INamingStrategy namingStrategy)
        {
            if (namingStrategy == null) throw new ArgumentNullException(nameof(namingStrategy));

            var result = new Dictionary<Type, EntityMap>();

            var entityTypes = _assembly
                .GetTypes()
                .Where(t => IsEntityCandidate(t))
                .ToList();

            var sortedEntityTypes = entityTypes.OrderBy(t => GetInheritanceDepth(t));

            foreach (var type in sortedEntityTypes)
            {
                EntityMap? baseMap = null;
                if (type.BaseType != null && result.TryGetValue(type.BaseType, out var parentMap))
                {
                    baseMap = parentMap;
                }

                bool hasDerivedTypes = entityTypes.Any(t => t.BaseType == type);

                var entityMap = BuildEntityMap(type, namingStrategy, baseMap, hasDerivedTypes);
                result[type] = entityMap;
            }

            return result;
        }

        private static int GetInheritanceDepth(Type t)
        {
            int depth = 0;
            while (t.BaseType != null)
            {
                depth++;
                t = t.BaseType;
            }
            return depth;
        }

        private static bool IsEntityCandidate(Type type)
        {
            if (!type.IsClass || !type.IsPublic)
                return false;

            if (type.GetCustomAttribute<IgnoreAttribute>() != null) 
                return false;

            if (type.GetCustomAttribute<TableAttribute>() != null)
                return true;

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var p in properties)
            {
                if (p.GetCustomAttribute<KeyAttribute>() != null)
                    return true;

                if (string.Equals(p.Name, "Id", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (string.Equals(p.Name, type.Name + "Id", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            if (type.BaseType != null && IsEntityCandidate(type.BaseType))
                return true;

            return false;
        }

        private EntityMap BuildEntityMap(Type entityType, INamingStrategy naming, EntityMap? baseMap, bool hasDerivedTypes)
        {
            InheritanceStrategy strategy = InheritanceStrategy.TablePerHierarchy;
            var ownTableAttr = entityType.GetCustomAttribute<TableAttribute>(inherit: false);

            if (baseMap != null && ownTableAttr != null)
            {
                strategy = InheritanceStrategy.TablePerConcreteClass;
            }

            string tableName;
            string? discriminatorColumn = null;
            string? discriminator = null;

            if (baseMap != null)
            {
                if (strategy == InheritanceStrategy.TablePerHierarchy)
                {
                    tableName = baseMap.TableName;
                    discriminatorColumn = baseMap.DiscriminatorColumn;
                    discriminator = entityType.Name;
                }
                else
                {
                    tableName = ResolveTableName(entityType, naming);
                    discriminatorColumn = null; 
                    discriminator = null;
                }
            }
            else
            {
                tableName = ResolveTableName(entityType, naming);

                if (hasDerivedTypes)
                {
                    discriminatorColumn = "Discriminator";
                    discriminator = entityType.Name;
                }
                else
                {
                    discriminatorColumn = null;
                    discriminator = null;
                }
            }

            var properties = BuildPropertyMaps(entityType, naming);

            PropertyMap keyProperty;
            if (baseMap != null && strategy == InheritanceStrategy.TablePerHierarchy)
            {
                keyProperty = baseMap.KeyProperty;
            }
            else
            {
                keyProperty = ResolveKeyProperty(entityType, properties);
            }

            return new EntityMap(
                entityType, 
                tableName, 
                entityType.IsAbstract,
                baseMap,
                strategy,
                discriminator,
                discriminatorColumn,
                keyProperty, 
                properties);
        }

        private static string ResolveTableName(Type type, INamingStrategy naming)
        {
            var tableAttr = type.GetCustomAttribute<TableAttribute>();
            if (tableAttr != null)
                return tableAttr.Name;

            return naming.ConvertName(type.Name);
        }

        private static List<PropertyMap> BuildPropertyMaps(Type type, INamingStrategy naming)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            var list = new List<PropertyMap>();

            foreach (var prop in properties)
            {
                var map = PropertyMap.FromPropertyInfo(
                    prop,
                    naming,
                    t => IsEntityCandidate(t)
                );

                if (map.IsIgnored)
                    continue;

                list.Add(map);
            }

            return list;
        }

        private static PropertyMap ResolveKeyProperty(Type type, List<PropertyMap> properties)
        {
            var explicitKey = properties.FirstOrDefault(p => p.IsKey);
            if (explicitKey != null)
                return explicitKey;

            var idProp = properties.FirstOrDefault(p =>
                string.Equals(p.PropertyInfo.Name, "Id", StringComparison.OrdinalIgnoreCase));

            if (idProp != null)
                return idProp;

            string expected = type.Name + "Id";
            var typeIdProp = properties.FirstOrDefault(p =>
                string.Equals(p.PropertyInfo.Name, expected, StringComparison.OrdinalIgnoreCase));

            if (typeIdProp != null)
                return typeIdProp;

            throw new InvalidOperationException(
                $"Entity '{type.Name}' does not define a primary key. " +
                "Add [Key] or property named 'Id' or '{TypeName}Id'.");
        }
    }
}