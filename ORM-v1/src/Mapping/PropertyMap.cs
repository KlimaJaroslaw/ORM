using System;
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

        public Type PropertyType { get; }

        public Type UnderlyingType { get; }

        public string? NavigationPropertyName { get; }

        public PropertyMap(
            PropertyInfo propertyInfo,
            string? columnName,
            bool isKey,
            bool isIgnored,
            bool isNavigation,
            string? navigationPropertyName)
        {
            PropertyInfo = propertyInfo ?? throw new ArgumentNullException(nameof(propertyInfo));
            ColumnName = columnName;
            IsKey = isKey;
            IsIgnored = isIgnored;
            IsNavigation = isNavigation;
            NavigationPropertyName = navigationPropertyName;

            PropertyType = propertyInfo.PropertyType;
            UnderlyingType = Nullable.GetUnderlyingType(PropertyType) ?? PropertyType;
        }

        public static PropertyMap FromPropertyInfo(
            PropertyInfo propertyInfo,
            INamingStrategy naming,
            Func<Type, bool> isEntityType)
        {
            if (propertyInfo == null)
                throw new ArgumentNullException(nameof(propertyInfo));
            if (naming == null)
                throw new ArgumentNullException(nameof(naming));
            if (isEntityType == null)
                throw new ArgumentNullException(nameof(isEntityType));

            if (!propertyInfo.CanRead || !propertyInfo.CanWrite)
            {
                return new PropertyMap(
                    propertyInfo,
                    null,
                    isKey: false,
                    isIgnored: true,
                    isNavigation: false,
                    navigationPropertyName: null);
            }

            var ignoreAttr = propertyInfo.GetCustomAttribute<IgnoreAttribute>();
            if (ignoreAttr != null)
            {
                return new PropertyMap(
                    propertyInfo,
                    null,
                    isKey: false,
                    isIgnored: true,
                    isNavigation: false,
                    navigationPropertyName: null);
            }

            var fkAttr = propertyInfo.GetCustomAttribute<ForeignKeyAttribute>();

            bool isNavigation = IsNavigationProperty(propertyInfo, isEntityType);

            ColumnAttribute? columnAttr = propertyInfo.GetCustomAttribute<ColumnAttribute>();
            string? columnName = null;

            if (isNavigation)
            {
                columnName = null;
            }
            else
            {
                columnName = columnAttr != null
                    ? columnAttr.Name
                    : naming.ConvertName(propertyInfo.Name);
            }

            bool isKey = propertyInfo.GetCustomAttribute<KeyAttribute>() != null;

            return new PropertyMap(
                propertyInfo,
                columnName,
                isKey,
                isIgnored: false,
                isNavigation,
                navigationPropertyName: fkAttr?.NavigationPropertyName
            );
        }

        private static bool IsNavigationProperty(PropertyInfo prop, Func<Type, bool> isEntityType)
        {
            Type type = prop.PropertyType;

            if (type == typeof(string) || type == typeof(byte[]))
                return false;

            var underlying = Nullable.GetUnderlyingType(type) ?? type;

            if (underlying.IsPrimitive ||
                underlying.IsEnum ||
                underlying == typeof(decimal) ||
                underlying == typeof(Guid) ||
                underlying == typeof(DateTime) ||
                underlying == typeof(DateTimeOffset) ||
                underlying == typeof(TimeSpan))
            {
                return false;
            }

            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(type) &&
                type != typeof(string))
            {
                var elementType = type.IsGenericType ? type.GetGenericArguments()[0] : null;

                if (elementType != null && isEntityType(elementType))
                    return true;
            }

            if (isEntityType(type))
                return true;

            return false;
        }

        public static PropertyMap FromPropertyInfo(
            PropertyInfo propertyInfo,
            INamingStrategy naming)
        {
            return FromPropertyInfo(
                propertyInfo,
                naming,
                type => false
            );
        }
    }
}
