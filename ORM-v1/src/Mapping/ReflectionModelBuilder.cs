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
            // --- KROK 1: Określenie Strategii Dziedziczenia ---
            
            IInheritanceStrategy strategy;
            string tableName;
            
            // Sprawdź czy klasa ma WŁASNY atrybut Table (co wymusza TPC w dziedziczeniu)
            var ownTableAttr = entityType.GetCustomAttribute<TableAttribute>(inherit: false);
            bool isTpcExplicit = (baseMap != null && ownTableAttr != null);

            if (baseMap != null)
            {
                if (!isTpcExplicit)
                {
                    // --- STRATEGIA: TPH (Table Per Hierarchy) ---
                    // Dziedziczymy tabelę po rodzicu
                    tableName = baseMap.TableName;

                    // Ustalamy kolumnę dyskryminatora
                    string discriminatorCol = "Discriminator"; // Default
                    
                    // Jeśli rodzic też jest TPH, bierzemy nazwę kolumny od niego
                    if (baseMap.InheritanceStrategy is TablePerHierarchyStrategy parentTph)
                    {
                        discriminatorCol = parentTph.DiscriminatorColumn;
                    }

                    // Tworzymy obiekt strategii TPH z wartością dyskryminatora = Nazwa Klasy
                    strategy = new TablePerHierarchyStrategy(discriminatorCol, entityType.Name);
                }
                else
                {
                    // --- STRATEGIA: TPC (Table Per Concrete Class) ---
                    // Mamy własną tabelę
                    tableName = ResolveTableName(entityType, naming);
                    strategy = new TablePerConcreteClassStrategy();
                }
            }
            else
            {
                // --- ROOT (Korzeń hierarchii lub klasa samodzielna) ---
                tableName = ResolveTableName(entityType, naming);

                if (hasDerivedTypes)
                {
                    // Jeśli są dzieci, startujemy hierarchię TPH
                    strategy = new TablePerHierarchyStrategy("Discriminator", entityType.Name);
                }
                else
                {
                    // Klasa samodzielna -> Traktujemy jako TPC (najbezpieczniejsza opcja domyślna)
                    strategy = new TablePerConcreteClassStrategy();
                }
            }

            // --- KROK 2: Budowanie Właściwości ---

            var properties = BuildPropertyMaps(entityType, naming);

            PropertyMap keyProperty;
            
            // W TPH klucz jest wspólny i zdefiniowany w rodzicu
            if (baseMap != null && strategy is TablePerHierarchyStrategy)
            {
                keyProperty = baseMap.KeyProperty;
            }
            else
            {
                keyProperty = ResolveKeyProperty(entityType, properties);
            }

            // --- KROK 3: Konstrukcja Mapy ---
            
            // Zauważ, że nie przekazujemy już 'discriminator' ani 'discriminatorColumn' luzem.
            // Są one zawarte w obiekcie 'strategy'.
            return new EntityMap(
                entityType,
                tableName,
                entityType.IsAbstract,
                baseMap,
                strategy,
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