using System;

namespace ORM_v1.Mapping
{
    public sealed class LowerCaseNamingStrategy : INamingStrategy
    {
        public string ConvertName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be null or whitespace.", nameof(name));

            return name.ToLowerInvariant();
        }
    }
}