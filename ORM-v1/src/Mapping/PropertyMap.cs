using System;
using System.Collections;
using System.Reflection;
using ORM_v1.Attributes;

namespace ORM_v1.Mapping
{
    public sealed class PropertyMap
    {
        public PropertyInfo PropertyInfo { get; }
        public string? ColumnName { get; }
        public bool IsKey { get; }
        public bool IsIgnored { get; }
        public bool IsNavigation { get; }
        public bool IsCollection { get; }
        public Type? TargetType { get; }
        public string? ForeignKeyName { get; }

        public Type PropertyType { get; }
        public Type UnderlyingType { get; }

        public PropertyMap(
            PropertyInfo propertyInfo,
            string? columnName,
            bool isKey,
            bool isIgnored,
            bool isNavigation,
            bool isCollection,
            Type? targetType,
            string? foreignKeyName)
        {
            PropertyInfo = propertyInfo ?? throw new ArgumentNullException(nameof(propertyInfo));
            ColumnName = columnName;
            IsKey = isKey;
            IsIgnored = isIgnored;
            IsNavigation = isNavigation;
            IsCollection = isCollection;
            TargetType = targetType;
            ForeignKeyName = foreignKeyName;

            PropertyType = propertyInfo.PropertyType;
            UnderlyingType = Nullable.GetUnderlyingType(PropertyType) ?? PropertyType;
        }

        public static PropertyMap FromPropertyInfo(
            PropertyInfo propertyInfo,
            INamingStrategy naming,
            Func<Type, bool> isEntityType)
        {
            if (propertyInfo == null) throw new ArgumentNullException(nameof(propertyInfo));
            if (naming == null) throw new ArgumentNullException(nameof(naming));
            if (isEntityType == null) throw new ArgumentNullException(nameof(isEntityType));

            if (!propertyInfo.CanRead || !propertyInfo.CanWrite || propertyInfo.GetCustomAttribute<IgnoreAttribute>() != null)
            {
                return CreateIgnored(propertyInfo);
            }

            Type propType = propertyInfo.PropertyType;
            Type effectiveType = Nullable.GetUnderlyingType(propType) ?? propType;

            bool isCollection = false;
            Type? targetType = null;
            bool isNavigation = false;

            if (effectiveType != typeof(string) && effectiveType != typeof(byte[]))
            {
                if (effectiveType.IsGenericType && typeof(IEnumerable).IsAssignableFrom(effectiveType))
                {
                    var elementType = effectiveType.GetGenericArguments()[0];
                    if (isEntityType(elementType))
                    {
                        isNavigation = true;
                        isCollection = true;
                        targetType = elementType;
                    }
                }
                else if (isEntityType(effectiveType))
                {
                    isNavigation = true;
                    isCollection = false;
                    targetType = effectiveType;
                }
            }

            var fkAttr = propertyInfo.GetCustomAttribute<ForeignKeyAttribute>();
            var colAttr = propertyInfo.GetCustomAttribute<ColumnAttribute>();
            bool isKey = propertyInfo.GetCustomAttribute<KeyAttribute>() != null;

            string? columnName = null;
            if (!isNavigation)
            {
                columnName = colAttr != null ? colAttr.Name : naming.ConvertName(propertyInfo.Name);
            }
            else
            {
                columnName = null;
            }

            return new PropertyMap(
                propertyInfo,
                columnName,
                isKey,
                isIgnored: false,
                isNavigation,
                isCollection,
                targetType,
                foreignKeyName: fkAttr?.NavigationPropertyName
            );
        }

        private static PropertyMap CreateIgnored(PropertyInfo prop)
        {
            return new PropertyMap(prop, null, false, true, false, false, null, null);
        }
    }
}