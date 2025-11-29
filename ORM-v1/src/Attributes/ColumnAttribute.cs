using System;

namespace ORM_v1.Attributes
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class ColumnAttribute : Attribute
    {
        public string Name { get; }
        public ColumnAttribute(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Column name cannot be null or whitespace.", nameof(name));
            }

            Name = name;
        }
    }
}
