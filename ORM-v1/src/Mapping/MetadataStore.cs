using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ORM_v1.Mapping
{
    public sealed class MetadataStore : IMetadataStore
    {
        private readonly IReadOnlyDictionary<Type, EntityMap> _maps;

        internal MetadataStore(IReadOnlyDictionary<Type, EntityMap> maps)
        {
            _maps = maps ?? throw new ArgumentNullException(nameof(maps));
        }

        public EntityMap GetMap<T>() => GetMap(typeof(T));

        public EntityMap GetMap(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (_maps.TryGetValue(type, out var map))
                return map;

            throw new KeyNotFoundException(
                $"Type '{type.FullName}' is not registered in MetadataStore.");
        }
    }
}
