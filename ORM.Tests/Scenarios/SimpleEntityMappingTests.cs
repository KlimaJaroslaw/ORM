using ORM_v1.Mapping;
using ORM.Tests.TestEntities;
using Xunit;

namespace ORM.Tests.Scenarios
{
    public class SimpleEntityMappingTests
    {
        [Fact]
        public void Should_Map_Entity_With_Table_And_Columns()
        {
            var builder = new ModelBuilder(typeof(UserTestEntity).Assembly);
            var naming = new PascalCaseNamingStrategy();

            var maps = builder.BuildModel(naming);
            var map = maps[typeof(UserTestEntity)];

            Assert.Equal("Users", map.TableName);

            Assert.Equal("Id", map.KeyProperty.ColumnName);
            Assert.Equal("first_name", map.FindPropertyByName("FirstName")!.ColumnName);

            Assert.Contains(map.ScalarProperties, p => p.ColumnName == "Id");
            Assert.Contains(map.ScalarProperties, p => p.ColumnName == "first_name");
            Assert.DoesNotContain(map.ScalarProperties, p => p.PropertyInfo.Name == "IgnoredProp");
        }
    }
}
