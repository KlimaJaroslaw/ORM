using System;

namespace ORM_v1.Attributes
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class KeyAttribute : Attribute
    {
    }
}
