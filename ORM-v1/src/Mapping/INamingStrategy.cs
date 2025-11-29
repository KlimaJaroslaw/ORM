using System;

namespace ORM_v1.Mapping
{
    public interface INamingStrategy
    {
        string ConvertName(string name);
    }
}
