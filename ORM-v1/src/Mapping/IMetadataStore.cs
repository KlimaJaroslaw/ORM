using System;
using System.Collections.Generic;

namespace ORM_v1.Mapping
{
    public interface IMetadataStore
    {
        EntityMap GetMap<T>();
        EntityMap GetMap(Type type);
        IEnumerable<EntityMap> GetAllMaps();
    }
}
