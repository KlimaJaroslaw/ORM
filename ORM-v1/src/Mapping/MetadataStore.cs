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

        public IEnumerable<EntityMap> GetAllMaps()
        {
            return _maps.Values;
        }

        public EntityMap? GetEntityMapByDiscriminator(Type rootType, string discriminator)
        {
            if (rootType == null) throw new ArgumentNullException(nameof(rootType));
            if (string.IsNullOrWhiteSpace(discriminator)) return null;
            foreach (var map in _maps.Values)
            {
                if (string.Equals(map.Discriminator, discriminator, StringComparison.OrdinalIgnoreCase))
                {
                    if (rootType.IsAssignableFrom(map.EntityType))
                    {
                        return map;
                    }
                }
            }

            return null;
        }
    }
}