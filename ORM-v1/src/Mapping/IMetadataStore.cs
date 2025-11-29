using System;

namespace ORM_v1.Mapping
{
    public interface IMetadataStore
    {
        EntityMap GetMap<T>();
        EntityMap GetMap(Type type);
    }
}
