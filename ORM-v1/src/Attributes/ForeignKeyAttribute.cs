using System;

namespace ORM_v1.Attributes
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class ForeignKeyAttribute : Attribute
    {
        public string NavigationPropertyName { get; }

        public ForeignKeyAttribute(string navigationPropertyName)
        {
            if (string.IsNullOrWhiteSpace(navigationPropertyName))
            {
                throw new ArgumentException(
                    "Navigation property name cannot be null or whitespace.",
                    nameof(navigationPropertyName));
            }

            NavigationPropertyName = navigationPropertyName;
        }
    }
}
