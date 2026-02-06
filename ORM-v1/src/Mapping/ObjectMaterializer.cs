using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using ORM_v1.Mapping.Strategies;

namespace ORM_v1.Mapping
{
    public sealed class ObjectMaterializer
    {
        private readonly EntityMap _rootMap;
        private readonly IMetadataStore _metadataStore;
        private readonly Func<object> _factory;
        private readonly Dictionary<PropertyMap, Action<object, object?>> _setters;

        private readonly ConcurrentDictionary<Type, ObjectMaterializer> _derivedMaterializers = new();

        public ObjectMaterializer(EntityMap map, IMetadataStore metadataStore)
        {
            _rootMap = map ?? throw new ArgumentNullException(nameof(map));
            _metadataStore = metadataStore ?? throw new ArgumentNullException(nameof(metadataStore));

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

            if (_rootMap.IsAbstract)
            {
                throw new InvalidOperationException(
                    $"Cannot instantiate abstract class '{_rootMap.EntityType.Name}'. " +
                    "Discriminator value was missing or did not match any known derived type.");
            }

            var instance = _factory();
            int index = 0;

            foreach (var prop in _rootMap.ScalarProperties)
            {
                if (index >= ordinals.Length) break;

                int ordinal = ordinals[index++];

                if (ordinal < 0)
                    continue;

                if (!_setters.TryGetValue(prop, out var setter))
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

        private object MaterializeDerived(IDataRecord record, string discriminatorValue)
        {
            var derivedMap = _metadataStore.GetEntityMapByDiscriminator(_rootMap.RootMap.EntityType, discriminatorValue);
            
            if (derivedMap == null)
            {
                 throw new InvalidOperationException(
                    $"Unknown discriminator value '{discriminatorValue}' for root entity '{_rootMap.EntityType.Name}'.");
            }

            var materializer = _derivedMaterializers.GetOrAdd(derivedMap.EntityType, _ => 
                new ObjectMaterializer(derivedMap, _metadataStore));

            var derivedOrdinals = GetOrdinalsForMap(record, derivedMap);

            return materializer.Materialize(record, derivedOrdinals);
        }

        private static int GetDiscriminatorOrdinal(IDataRecord record, string columnName)
        {
            for (int i = 0; i < record.FieldCount; i++)
            {
                if (string.Equals(record.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        private static int[] GetOrdinalsForMap(IDataRecord record, EntityMap map)
        {
            var ordinals = new int[map.ScalarProperties.Count];
            for (int i = 0; i < map.ScalarProperties.Count; i++)
            {
                var prop = map.ScalarProperties[i];
                ordinals[i] = -1;
                for (int j = 0; j < record.FieldCount; j++)
                {
                    if (string.Equals(record.GetName(j), prop.ColumnName, StringComparison.OrdinalIgnoreCase))
                    {
                        ordinals[i] = j;
                        break;
                    }
                }
            }
            return ordinals;
        }

        private static object ConvertValue(object value, PropertyMap prop)
        {
            var targetType = prop.UnderlyingType;

            if (targetType.IsEnum)
            {
                return Enum.ToObject(targetType, value);
            }

            if (targetType == typeof(Guid))
            {
                if (value is string s) return Guid.Parse(s);
                if (value is byte[] b) return new Guid(b);
            }

            var valueType = value.GetType();
            if (valueType != targetType)
            {
                try
                {
                    return Convert.ChangeType(value, targetType);
                }
                catch (Exception ex) when (ex is InvalidCastException || ex is FormatException || ex is OverflowException)
                {
                    throw new InvalidOperationException(
                        $"Cannot convert value of type '{valueType}' to property '{prop.PropertyInfo.Name}' " +
                        $"of type '{prop.PropertyType}'. Column: '{prop.ColumnName}', Value: {value}", ex);
                }
            }

            return value;
        }

        private static Func<object> CreateFactory(Type type)
        {
            var ctor = Expression.New(type);
            var convert = Expression.Convert(ctor, typeof(object));
            return Expression.Lambda<Func<object>>(convert).Compile();
        }

        private static Dictionary<PropertyMap, Action<object, object?>> CreateSetters(EntityMap map)
        {
            var dict = new Dictionary<PropertyMap, Action<object, object?>>();

            foreach (var prop in map.ScalarProperties)
            {
                var setter = prop.PropertyInfo.GetSetMethod();
                if (setter == null)
                    continue;

                var instanceParam = Expression.Parameter(typeof(object));
                var valueParam = Expression.Parameter(typeof(object));

                var typedInstance = Expression.Convert(instanceParam, map.EntityType);
                var typedValue = Expression.Convert(valueParam, prop.PropertyType);

                var call = Expression.Call(typedInstance, setter, typedValue);
                var lambda = Expression.Lambda<Action<object, object?>>(
                    call, instanceParam, valueParam);

                dict[prop] = lambda.Compile();
            }

            return dict;
        }
    }
}