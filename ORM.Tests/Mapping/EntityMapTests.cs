using ORM_v1.Mapping;
using ORM.Tests.TestEntities;
using Xunit;

namespace ORM.Tests.Mapping
{
    public class EntityMapTests
    {
        [Fact]
        public void EntityMap_Should_Have_Correct_Table_Name()
        {
            var naming = new PascalCaseNamingStrategy();

            var props = typeof(UserTestEntity)
                .GetProperties()
                .Select(p => PropertyMap.FromPropertyInfo(p, naming))
                .Where(p => !p.IsIgnored);

            var key = props.First(p => p.IsKey);

            var map = new EntityMap(typeof(UserTestEntity), "Users", key, props);

            Assert.Equal("Users", map.TableName);
        }

        [Fact]
        public void EntityMap_Should_Contain_ScalarProperties()
        {
            var naming = new PascalCaseNamingStrategy();

            var props = typeof(UserTestEntity)
                .GetProperties()
                .Select(p => PropertyMap.FromPropertyInfo(p, naming))
                .Where(p => !p.IsIgnored);

            var key = props.First(p => p.IsKey);

            var map = new EntityMap(typeof(UserTestEntity), "Users", key, props);

            Assert.Contains(map.ScalarProperties, p => p.ColumnName == "first_name");
        }
    }
}
