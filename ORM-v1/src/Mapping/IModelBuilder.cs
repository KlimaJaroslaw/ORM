using System;
using System.Collections.Generic;

namespace ORM_v1.Mapping
{
    public interface IModelBuilder
    {
        void Reset();
        void BuildEntity(Type type, bool hasDerivedTypes);
        IReadOnlyDictionary<Type, EntityMap> GetResult();
    }
}