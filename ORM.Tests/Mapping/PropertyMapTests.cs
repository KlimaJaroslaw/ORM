using ORM_v1.Mapping;
using ORM_v1.Attributes;
using ORM.Tests.TestEntities;
using Xunit;
using System.Reflection;

namespace ORM.Tests.Mapping
{
    public class PropertyMapTests
    {
        private readonly INamingStrategy _naming = new PascalCaseNamingStrategy();

        [Fact]
        public void PropertyMap_Should_Respect_ColumnAttribute()
        {
            var prop = typeof(UserTestEntity).GetProperty(nameof(UserTestEntity.FirstName))!;
            var map = PropertyMap.FromPropertyInfo(prop, _naming, t => false);

            Assert.Equal("first_name", map.ColumnName);
        }

        [Fact]
        public void PropertyMap_Should_Identify_Key_Property()
        {
            var prop = typeof(UserTestEntity).GetProperty(nameof(UserTestEntity.Id))!;
            var map = PropertyMap.FromPropertyInfo(prop, _naming, t => false);

            Assert.True(map.IsKey);
        }

        [Fact]
        public void PropertyMap_Should_Ignore_Properties_With_IgnoreAttribute()
        {
            var prop = typeof(UserTestEntity).GetProperty(nameof(UserTestEntity.IgnoredProp))!;
            var map = PropertyMap.FromPropertyInfo(prop, _naming, t => false);

            Assert.True(map.IsIgnored);
        }
    }
}