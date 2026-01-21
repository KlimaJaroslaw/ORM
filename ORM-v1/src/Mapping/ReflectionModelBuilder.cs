using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ORM_v1.Attributes;

namespace ORM_v1.Mapping
{
    public sealed class ReflectionModelBuilder : IModelBuilder
    {
        private readonly INamingStrategy _naming;
        private readonly Dictionary<Type, EntityMap> _builtMaps = new();

        public ReflectionModelBuilder(INamingStrategy naming)
        {
            _naming = naming ?? throw new ArgumentNullException(nameof(naming));
        }

        public void BuildEntity(Type type, bool hasDerivedTypes)
        {
            // Próba pobrania mapy rodzica (jeśli istnieje i została już zbudowana)
            EntityMap? baseMap = null;
            if (type.BaseType != null && _builtMaps.TryGetValue(type.BaseType, out var parentMap))
            {
                baseMap = parentMap;
            }

            var map = BuildEntityMap(type, _naming, baseMap, hasDerivedTypes);
            _builtMaps[type] = map;
        }

        public IReadOnlyDictionary<Type, EntityMap> GetResult()
        {
            return _builtMaps;
        }

        // --- Logika prywatna (szczegóły budowania) ---

        private EntityMap BuildEntityMap(Type entityType, INamingStrategy naming, EntityMap? baseMap, bool hasDerivedTypes)
        {
            InheritanceStrategy strategy = InheritanceStrategy.TablePerHierarchy;

            // Sprawdź atrybut [Table] dla własnej tabeli (nie dziedziczonej)
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
                    // TPH: Tabela rodzica
                    tableName = baseMap.TableName; // w TPH trzymamy nazwę tabeli rodzica
                    discriminatorColumn = baseMap.DiscriminatorColumn;
                    discriminator = entityType.Name;
                }
                else
                {
                    // TPC: własna tabela – konwertujemy przez strategię
                    tableName = naming.ConvertName(ResolveTableName(entityType, naming));
                }
            }
            else
            {
                // Root
                tableName = naming.ConvertName(ResolveTableName(entityType, naming));

                if (hasDerivedTypes)
                {
                    discriminatorColumn = "Discriminator";
                    discriminator = entityType.Name;
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
            if (tableAttr != null) return tableAttr.Name;
            return naming.ConvertName(type.Name);
        }

        private static List<PropertyMap> BuildPropertyMaps(Type type, INamingStrategy naming)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var list = new List<PropertyMap>();

            foreach (var prop in properties)
            {
                // IsEntityCandidate helper - uproszczony check na potrzeby relacji
                var map = PropertyMap.FromPropertyInfo(
                    prop,
                    naming,
                    t => IsTypeEntityCandidate(t) 
                );

                if (!map.IsIgnored) list.Add(map);
            }
            return list;
        }

        private static PropertyMap ResolveKeyProperty(Type type, List<PropertyMap> properties)
        {
            var explicitKey = properties.FirstOrDefault(p => p.IsKey);
            if (explicitKey != null) return explicitKey;

            var idProp = properties.FirstOrDefault(p =>
                string.Equals(p.PropertyInfo.Name, "Id", StringComparison.OrdinalIgnoreCase));
            if (idProp != null) return idProp;

            string expected = type.Name + "Id";
            var typeIdProp = properties.FirstOrDefault(p =>
                string.Equals(p.PropertyInfo.Name, expected, StringComparison.OrdinalIgnoreCase));
            if (typeIdProp != null) return typeIdProp;

            // Fallback: Jeśli encja nie ma klucza, rzucamy wyjątek (chyba że to klasa abstrakcyjna bez tabeli)
            if (!type.IsAbstract)
            {
                 throw new InvalidOperationException(
                    $"Entity '{type.Name}' does not define a primary key. Add [Key] or named 'Id'.");
            }
            
            // Dla bezpieczeństwa, żeby kompilator nie krzyczał (w TPH klucz jest w BaseMap)
            return properties.FirstOrDefault()!;
        }

        // Kopia helpera do wykrywania encji (potrzebna dla PropertyMap do wykrywania relacji)
        private static bool IsTypeEntityCandidate(Type type)
        {
            // Odsiewamy typy proste
            if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime) || type.IsEnum) 
                return false;
                
            return type.IsClass && !type.IsAbstract; 
        }

        // Builder może być użyty wielokrotnie
        public void Reset() => _builtMaps.Clear();
    }
}