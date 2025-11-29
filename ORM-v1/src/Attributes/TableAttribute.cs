using System;

namespace ORM_v1.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class TableAttribute : Attribute
    {
        public string Name { get; }

        public TableAttribute(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Table name cannot be null or whitespace.", nameof(name));
            }

            Name = name;
        }
    }
}