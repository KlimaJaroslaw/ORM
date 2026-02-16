using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using ORM_v1.Mapping.Strategies;
using ORM_v1.core;

namespace ORM_v1.Mapping
{
    public sealed class ObjectMaterializer
    {
        private readonly EntityMap _rootMap;
        private readonly IMetadataStore _metadataStore;
        private readonly Func<object> _factory;
        private readonly Dictionary<PropertyMap, Action<object, object?>> _setters;
        private readonly DbContext? _context;
        private readonly ConcurrentDictionary<Type, ObjectMaterializer> _derivedMaterializers = new();

        public ObjectMaterializer(EntityMap map, IMetadataStore metadataStore, DbContext? context = null)
        {
            _rootMap = map ?? throw new ArgumentNullException(nameof(map));
            _metadataStore = metadataStore ?? throw new ArgumentNullException(nameof(metadataStore));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            if (!map.IsAbstract)
            {
                _factory = CreateFactory(map.EntityType);
            }
            else
            {
                _factory = () => throw new InvalidOperationException($"Cannot instantiate abstract class '{map.EntityType.Name}'.");
            }

            _setters = CreateSetters(map);
        }

        public object Materialize(IDataRecord record, int[] ordinals)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (ordinals == null) throw new ArgumentNullException(nameof(ordinals));

            // ✅ OBSŁUGA TPH: używa Discriminatora
            if (_rootMap.InheritanceStrategy is TablePerHierarchyStrategy tphStrategy)
            {
                int discriminatorOrdinal = GetDiscriminatorOrdinal(record, tphStrategy.DiscriminatorColumn);
                
                if (discriminatorOrdinal >= 0 && !record.IsDBNull(discriminatorOrdinal))
                {
                    string discriminatorValue = record.GetString(discriminatorOrdinal);

                    if (!string.Equals(discriminatorValue, tphStrategy.DiscriminatorValue, StringComparison.OrdinalIgnoreCase))
                    {
                        return MaterializeDerived(record, discriminatorValue);
                    }
                }
            }
            // ✅ OBSŁUGA TPC: używa syntetycznego Discriminatora
            else if (_rootMap.InheritanceStrategy is TablePerConcreteClassStrategy)
            {
                int discriminatorOrdinal = GetDiscriminatorOrdinal(record, "Discriminator");
                
                if (discriminatorOrdinal >= 0 && !record.IsDBNull(discriminatorOrdinal))
                {
                    string discriminatorValue = record.GetString(discriminatorOrdinal);
                    
                    if (!string.Equals(discriminatorValue, _rootMap.EntityType.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        var candidateMap = _metadataStore.GetAllMaps()
                            .FirstOrDefault(m => string.Equals(m.EntityType.Name, discriminatorValue, StringComparison.OrdinalIgnoreCase));

                        if (candidateMap != null && _rootMap.EntityType.IsAssignableFrom(candidateMap.EntityType))
                        {
                             return MaterializeDerivedByTypeName(record, discriminatorValue);
                        }
                    }
                }
            }
            // ✅ NOWA OBSŁUGA TPT: wykryj typ na podstawie NULL-i w kolumnach dzieci
            else if (_rootMap.InheritanceStrategy is TablePerTypeStrategy)
            {
                // Wykryj najbardziej konkretny typ pochodny na podstawie niepustych kolumn
                var mostDerivedType = DetectMostDerivedTypeForTPT(record);
                
                if (mostDerivedType != null && mostDerivedType != _rootMap.EntityType)
                {
                    // Zmaterializuj jako typ pochodny
                    var derivedMap = _metadataStore.GetMap(mostDerivedType);
                    var materializer = _derivedMaterializers.GetOrAdd(mostDerivedType, _ => 
                        new ObjectMaterializer(derivedMap, _metadataStore, _context));

                    var derivedOrdinals = GetOrdinalsForMap(record, derivedMap);
                    return materializer.Materialize(record, derivedOrdinals);
                }
            }

            if (_rootMap.IsAbstract)
            {
                throw new InvalidOperationException(
                    $"Cannot instantiate abstract class '{_rootMap.EntityType.Name}'. " +
                    "Discriminator value was missing or did not match any known derived type.");
            }

            var keyProp = _rootMap.KeyProperty;
            int keyIndex = -1;
            var props = _rootMap.ScalarProperties;
            for (int i = 0; i < props.Count; i++)
            {
                if (props[i] == keyProp)
                {
                    keyIndex = i;
                    break;
                }
            }
            if (keyIndex >= 0 && keyIndex < ordinals.Length)
            {
                int keyOrdinal = ordinals[keyIndex];
                if (keyOrdinal >= 0 && !record.IsDBNull(keyOrdinal))
                {
                    object keyValue = record.GetValue(keyOrdinal);
                    keyValue = ConvertValue(keyValue, keyProp);
                    
                    var tracked = _context.ChangeTracker.FindTracked(_rootMap.EntityType, keyValue);
                    if (tracked != null)
                    {
                        // Console.WriteLine($"[DEBUG Materialize] Zwracam z CACHE {_rootMap.EntityType.Name} ID={keyValue}");
                        return tracked;
                    }
                }
            }

            var instance = _factory();
            int index = 0;

            // Console.WriteLine($"[DEBUG Materialize] Materializuję {_rootMap.EntityType.Name}, ScalarProperties.Count={_rootMap.ScalarProperties.Count}, instance.GetHashCode()={instance.GetHashCode()}");

            foreach (var prop in _rootMap.ScalarProperties)
            {
                if (index >= ordinals.Length) break;

                int ordinal = ordinals[index++];

                // Console.WriteLine($"  Property: {prop.PropertyInfo.Name}, ordinal={ordinal}");

                if (ordinal < 0)
                {
                    // Console.WriteLine($"    -> ordinal < 0, pomijam");
                    continue;
                }

                if (!_setters.TryGetValue(prop, out var setter))
                {
                    // Console.WriteLine($"    -> brak settera, pomijam");
                    continue;
                }

                object? value = record.IsDBNull(ordinal) ? null : record.GetValue(ordinal);

                // Console.WriteLine($"    -> wartość z kolumny[{ordinal}] '{record.GetName(ordinal)}' = {value ?? "NULL"}");

                if (value == null &&
                    prop.PropertyType.IsValueType &&
                    Nullable.GetUnderlyingType(prop.PropertyType) == null)
                {
                    throw new InvalidOperationException(
                        $"Column '{prop.ColumnName}' is NULL but property '{prop.PropertyInfo.Name}' " +
                        $"is non-nullable ({prop.PropertyType}).");
                }

                if (value != null)
                {
                    value = ConvertValue(value, prop);
                }

                setter(instance, value);
                // Console.WriteLine($"    -> ustawiono {prop.PropertyInfo.Name} = {value}");
            }

            // Console.WriteLine($"[DEBUG Materialize] Zwracam instancję {_rootMap.EntityType.Name}.GetHashCode()={instance.GetHashCode()}");

            return instance;
        }

        private object MaterializeDerived(IDataRecord record, string discriminatorValue)
        {
            var derivedMap = _metadataStore.GetEntityMapByDiscriminator(_rootMap.RootMap.EntityType, discriminatorValue);
            
            if (derivedMap == null)
            {
                 throw new InvalidOperationException(
                    $"Unknown discriminator value '{discriminatorValue}' for root entity '{_rootMap.EntityType.Name}'.");

            }

            var materializer = _derivedMaterializers.GetOrAdd(derivedMap.EntityType, _ => 
                new ObjectMaterializer(derivedMap, _metadataStore, _context));

            var derivedOrdinals = GetOrdinalsForMap(record, derivedMap);

            return materializer.Materialize(record, derivedOrdinals);
        }

        /// <summary>
        /// Materializuje klasę pochodną na podstawie nazwy typu (dla TPC).
        /// </summary>
        private object MaterializeDerivedByTypeName(IDataRecord record, string typeName)
        {
            // Znajdź EntityMap po nazwie typu
            var derivedMap = _metadataStore.GetAllMaps()
                .FirstOrDefault(m => string.Equals(m.EntityType.Name, typeName, StringComparison.OrdinalIgnoreCase));
            
            if (derivedMap == null)
            {
                throw new InvalidOperationException(
                    $"Unknown type name '{typeName}' in TPC discriminator for root entity '{_rootMap.EntityType.Name}'.");
            }

            // ✅ POPRAWKA: Przelicz ordinals dla derived map i zmaterializuj BEZPOŚREDNIO
            var derivedOrdinals = GetOrdinalsForMap(record, derivedMap);
            
            // NIE wywołuj Materialize() bo to prowadzi do nieskończonej rekurencji!
            // Zamiast tego zmaterializuj BEZPOŚREDNIO:
            return MaterializeDirect(record, derivedOrdinals, derivedMap);
        }

        /// <summary>
        /// Bezpośrednia materializacja bez sprawdzania discriminatora (dla TPC).
        /// </summary>
        private object MaterializeDirect(IDataRecord record, int[] ordinals, EntityMap map)
        {
            if (map.IsAbstract)
            {
                throw new InvalidOperationException(
                    $"Cannot instantiate abstract class '{map.EntityType.Name}'.");
            }

            var keyProp = map.KeyProperty;
            int keyIndex = -1;
            
            var props = map.ScalarProperties;
            for (int i = 0; i < props.Count; i++)
            {
                if (props[i] == keyProp) 
                {
                    keyIndex = i; 
                    break;
                }
            }
            if (keyIndex >= 0 && keyIndex < ordinals.Length)
            {
                int keyOrdinal = ordinals[keyIndex];
                if (keyOrdinal >= 0 && !record.IsDBNull(keyOrdinal))
                {
                    object keyValue = record.GetValue(keyOrdinal);
                    keyValue = ConvertValue(keyValue, keyProp);
                    
                    var tracked = _context.ChangeTracker.FindTracked(map.EntityType, keyValue);
                    if (tracked != null) return tracked;
                }
            }

            var factory = CreateFactory(map.EntityType);
            var setters = CreateSetters(map);
            var instance = factory();

            int index = 0;

            foreach (var prop in map.ScalarProperties)
            {
                if (index >= ordinals.Length) break;

                int ordinal = ordinals[index++];

                if (ordinal < 0)
                    continue;

                if (!setters.TryGetValue(prop, out var setter))
                    continue;

                object? value = record.IsDBNull(ordinal) ? null : record.GetValue(ordinal);

                if (value == null &&
                    prop.PropertyType.IsValueType &&
                    Nullable.GetUnderlyingType(prop.PropertyType) == null)
                {
                    throw new InvalidOperationException(
                        $"Column '{prop.ColumnName}' is NULL but property '{prop.PropertyInfo.Name}' " +
                        $"is non-nullable ({prop.PropertyType}).");
                }

                if (value != null)
                {
                    value = ConvertValue(value, prop);
                }

                setter(instance, value);
            }

            return instance;
        }

        private int GetDiscriminatorOrdinal(IDataRecord record, string discriminatorColumn)
        {
            // Znajdź ordinal kolumny Discriminator (dla TPH)
            for (int i = 0; i < record.FieldCount; i++)
            {
                if (string.Equals(record.GetName(i), discriminatorColumn, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private int[] GetOrdinalsForMap(IDataRecord record, EntityMap map)
        {
            int[] ordinals = new int[map.ScalarProperties.Count];

            for (int i = 0; i < map.ScalarProperties.Count; i++)
            {
                var prop = map.ScalarProperties[i];
                ordinals[i] = GetOrdinal(record, prop.ColumnName);
            }

            return ordinals;
        }

        private int GetOrdinal(IDataRecord record, string columnName)
        {
            // Znajdź ordinal kolumny według nazwy
            for (int i = 0; i < record.FieldCount; i++)
            {
                if (string.Equals(record.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private Func<object> CreateFactory(Type type)
        {
            var constructor = type.GetConstructor(Type.EmptyTypes);

            if (constructor == null)
            {
                throw new InvalidOperationException($"Type '{type.Name}' does not have a parameterless constructor.");
            }

            return Expression.Lambda<Func<object>>(Expression.New(constructor)).Compile();
        }

        private Dictionary<PropertyMap, Action<object, object?>> CreateSetters(EntityMap map)
        {
            var setters = new Dictionary<PropertyMap, Action<object, object?>>();

            foreach (var prop in map.ScalarProperties)
            {
                var entityParam = Expression.Parameter(typeof(object), "entity");
                var valueParam = Expression.Parameter(typeof(object), "value");

                var body = Expression.Assign(
                    Expression.Property(
                        Expression.Convert(entityParam, map.EntityType),
                        prop.PropertyInfo
                    ),
                    Expression.Convert(valueParam, prop.PropertyType)
                );

                var setter = Expression.Lambda<Action<object, object?>>(body, entityParam, valueParam).Compile();
                setters.Add(prop, setter);
            }

            return setters;
        }

        private object ConvertValue(object value, PropertyMap prop)
        {
            var targetType = prop.PropertyType;
            
            // ✅ Obsługa Nullable<T>
            var underlyingType = Nullable.GetUnderlyingType(targetType);
            if (underlyingType != null)
            {
                // Property jest Nullable<T> (np. int?)
                targetType = underlyingType;
            }

            // ✅ Obsługa SQLite Int64 → Int32 (i innych konwersji numerycznych)
            if (value is long longValue && (targetType == typeof(int) || targetType == typeof(int?)))
            {
                return (int)longValue;
            }

            if (value is long longValue2 && (targetType == typeof(short) || targetType == typeof(short?)))
            {
                return (short)longValue2;
            }

            if (value is long longValue3 && (targetType == typeof(byte) || targetType == typeof(byte?)))
            {
                return (byte)longValue3;
            }

            // ✅ Obsługa Enum
            if (targetType.IsEnum)
            {
                return Enum.ToObject(targetType, value);
            }

            // ✅ Standardowa konwersja (bez Nullable wrapper)
            try
            {
                return Convert.ChangeType(value, targetType);
            }
            catch (InvalidCastException ex)
            {
                throw new InvalidOperationException(
                    $"Cannot convert value '{value}' (type: {value.GetType().Name}) " +
                    $"to property '{prop.PropertyInfo.Name}' (type: {prop.PropertyType.Name})", ex);
            }
        }

        /// <summary>
        /// Wykrywa najbardziej konkretny typ pochodny dla TPT na podstawie niepustych kolumn.
        /// Hierarchia Student → StudentPart: jeśli kolumny StudentPart nie są NULL, to zwróć StudentPart.
        /// </summary>
        private Type? DetectMostDerivedTypeForTPT(IDataRecord record)
        {
            if (_rootMap.InheritanceStrategy is not TablePerTypeStrategy)
                return null;

            // Znajdź wszystkie typy pochodne (rekurencyjnie w dół hierarchii)
            var allDerivedTypes = GetAllDerivedTypesRecursive(_rootMap);
            
            // Sortuj od najbardziej pochodnych do bazowych (depth-first)
            var sortedTypes = allDerivedTypes
                .OrderByDescending(t => GetInheritanceDepth(t))
                .ToList();

            // Console.WriteLine($"[DEBUG TPT] Wykrywanie typu dla {_rootMap.EntityType.Name}, candidates: {string.Join(", ", sortedTypes.Select(t => t.EntityType.Name))}");

            // Sprawdź każdy typ od najbardziej pochodnego
            foreach (var candidateMap in sortedTypes)
            {
                // Znajdź kolumny specyficzne dla tego typu (nie dziedziczone z rodzica)
                var ownColumns = GetOwnColumnsForType(candidateMap);

                if (!ownColumns.Any())
                {
                    // Console.WriteLine($"[DEBUG TPT]   {candidateMap.EntityType.Name} - brak własnych kolumn, pomijam");
                    continue;
                }

                // Sprawdź czy jakakolwiek kolumna tego typu ma wartość (nie NULL)
                bool hasAnyValue = false;
                foreach (var columnName in ownColumns)
                {
                    int ordinal = GetOrdinal(record, columnName);
                    if (ordinal >= 0 && !record.IsDBNull(ordinal))
                    {
                        hasAnyValue = true;
                        // Console.WriteLine($"[DEBUG TPT]   {candidateMap.EntityType.Name} - kolumna '{columnName}' ma wartość → MATCH!");
                        break;
                    }
                }

                if (hasAnyValue)
                {
                    return candidateMap.EntityType; // Znaleziono najbardziej konkretny typ
                }

                // Console.WriteLine($"[DEBUG TPT]   {candidateMap.EntityType.Name} - wszystkie własne kolumny NULL");
            }

            // Nie znaleziono typu pochodnego - zwróć typ bazowy
            // Console.WriteLine($"[DEBUG TPT]   Brak typu pochodnego, zwracam {_rootMap.EntityType.Name}");
            return _rootMap.EntityType;
        }

        /// <summary>
        /// Zwraca wszystkie typy pochodne rekurencyjnie (dzieci, wnuki, etc.).
        /// </summary>
        private List<EntityMap> GetAllDerivedTypesRecursive(EntityMap baseMap)
        {
            var result = new List<EntityMap>();

            var directDerived = _metadataStore.GetAllMaps()
                .Where(m => m.InheritanceStrategy is TablePerTypeStrategy && m.BaseMap == baseMap)
                .ToList();

            foreach (var derived in directDerived)
            {
                result.Add(derived);
                
                // Rekurencyjnie dodaj dalsze dzieci
                result.AddRange(GetAllDerivedTypesRecursive(derived));
            }

            return result;
        }

        /// <summary>
        /// Zwraca głębokość dziedziczenia (0 = bazowa, 1 = bezpośrednie dziecko, etc.).
        /// </summary>
        private int GetInheritanceDepth(EntityMap map)
        {
            int depth = 0;
            var current = map.BaseMap;
            
            while (current != null)
            {
                depth++;
                current = current.BaseMap;
            }
            
            return depth;
        }

        /// <summary>
        /// Zwraca nazwy kolumn które są specyficzne dla danego typu (nie dziedziczone z rodzica).
        /// </summary>
        private List<string> GetOwnColumnsForType(EntityMap map)
        {
            var ownColumns = new List<string>();

            if (map.BaseMap == null)
            {
                // Klasa bazowa - wszystkie kolumny są własne
                return map.ScalarProperties
                    .Where(p => !string.IsNullOrEmpty(p.ColumnName))
                    .Select(p => p.ColumnName!)
                    .ToList();
            }

            // Kolumny które nie istnieją w rodzicu
            foreach (var prop in map.ScalarProperties)
            {
                if (string.IsNullOrEmpty(prop.ColumnName))
                    continue;

                var isInherited = map.BaseMap.ScalarProperties.Any(bp =>
                    string.Equals(bp.ColumnName, prop.ColumnName, StringComparison.OrdinalIgnoreCase));

                if (!isInherited)
                {
                    ownColumns.Add(prop.ColumnName);
                }
            }

            return ownColumns;
        }
    }
}