using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;

namespace ORM_v1.Mapping
{
    public sealed class ObjectMaterializer
    {
        private readonly Func<object> _factory;
        private readonly Dictionary<PropertyMap, Action<object, object?>> _setters;

        public ObjectMaterializer(EntityMap map)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));

            _factory = CreateFactory(map.EntityType);
            _setters = CreateSetters(map);
        }

        public object Materialize(IDataRecord record, EntityMap map, int[] ordinals)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            if (map == null)
                throw new ArgumentNullException(nameof(map));
            if (ordinals == null)
                throw new ArgumentNullException(nameof(ordinals));
            if (ordinals.Length != map.ScalarProperties.Count)
                throw new InvalidOperationException(
                    "Ordinal array length does not match number of scalar properties.");

            var instance = _factory();

            int index = 0;

            foreach (var prop in map.ScalarProperties)
            {
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

                if (prop.UnderlyingType.IsEnum && value != null)
                {
                    value = Enum.ToObject(prop.UnderlyingType, value);
                }

                setter(instance, value);
            }

            return instance;
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
