using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ORM_v1.Attributes;
using ORM_v1.Mapping.Strategies;

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

        public void Reset() => _builtMaps.Clear();

        // --- Logika prywatna (szczegóły budowania) ---

        private EntityMap BuildEntityMap(Type entityType, INamingStrategy naming, EntityMap? baseMap, bool hasDerivedTypes)
        {
            // KROK 1: Decyzja o typie strategii (Heurystyka na Enumach)
            InheritanceStrategy strategyType = InheritanceStrategy.TablePerHierarchy;

            var ownTableAttr = entityType.GetCustomAttribute<TableAttribute>(inherit: false);
            var rootType = baseMap?.RootMap.EntityType ?? entityType;
            var strategyAttr = rootType.GetCustomAttribute<InheritanceStrategyAttribute>();

            if (baseMap != null)
            {
                // --- Jesteśmy DZIECKIEM ---
                // ✅ Strategia dziedziczenia MUSI być zgodna z root/parent!
                
                if (strategyAttr != null)
                {
                    // Rodzic ma jawnie zdefiniowaną strategię - używamy jej
                    strategyType = strategyAttr.Strategy;
                }
                else
                {
                    // Brak strategii na root - heurystyka
                    if (ownTableAttr != null)
                    {
                        strategyType = InheritanceStrategy.TablePerConcreteClass;
                    }
                    else
                    {
                        strategyType = InheritanceStrategy.TablePerHierarchy;
                    }
                }
            }
            else 
            {
                // --- Jesteśmy KORZENIEM (ROOT) ---
                if (strategyAttr != null)
                {
                    strategyType = strategyAttr.Strategy;
                }
                else if (ownTableAttr != null && !hasDerivedTypes)
                {
                    strategyType = InheritanceStrategy.TablePerConcreteClass;
                }
                else
                {
                    strategyType = InheritanceStrategy.TablePerHierarchy;
                }
            }

            // KROK 2: Instancjonowanie Obiektu Strategii (Interface)
            // --- ZMIANA TUTAJ ---
            
            IInheritanceStrategy strategyImpl;
            string tableName;

            if (strategyType == InheritanceStrategy.TablePerHierarchy)
            {
                // --- TPH ---
                if (baseMap != null)
                {
                    tableName = baseMap.TableName;
                    string discriminatorCol = "Discriminator";
                    if (baseMap.InheritanceStrategy is TablePerHierarchyStrategy parentTph)
                    {
                        discriminatorCol = parentTph.DiscriminatorColumn;
                    }
                    strategyImpl = new TablePerHierarchyStrategy(discriminatorCol, entityType.Name);
                }
                else
                {
                    tableName = ResolveTableName(entityType, naming);
                    strategyImpl = new TablePerHierarchyStrategy("Discriminator", entityType.Name);
                }
            }
            else if (strategyType == InheritanceStrategy.TablePerType)
            {
                // --- TPT (Nowa klasa!) ---
                tableName = ResolveTableName(entityType, naming);
                strategyImpl = new TablePerTypeStrategy();
            }
            else
            {
                // --- TPC ---
                tableName = ResolveTableName(entityType, naming);
                strategyImpl = new TablePerConcreteClassStrategy();
            }

            // KROK 3: Budowanie Właściwości

            var properties = BuildPropertyMaps(entityType, naming);

            PropertyMap keyProperty;
            
            if (baseMap != null && strategyImpl is TablePerHierarchyStrategy)
            {
                keyProperty = baseMap.KeyProperty;
            }
            else
            {
                keyProperty = ResolveKeyProperty(entityType, properties);
            }

            // KROK 4: Konstrukcja Mapy
            
            return new EntityMap(
                entityType,
                tableName,
                entityType.IsAbstract,
                baseMap,
                strategyImpl,
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
                // Helper lambda do wykrywania encji
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

            if (!type.IsAbstract)
            {
                 throw new InvalidOperationException(
                    $"Entity '{type.Name}' does not define a primary key. Add [Key] or named 'Id'.");
            }
            
            return properties.FirstOrDefault()!;
        }

        private static bool IsTypeEntityCandidate(Type type)
        {
            if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime) || type.IsEnum) 
                return false;
                
            return type.IsClass && !type.IsAbstract; 
        }
    }
}