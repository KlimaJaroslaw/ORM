using Xunit;
using ORM_v1.Mapping;
using ORM_v1.Query;
using System.Linq;

namespace ORM.Tests.Scenarios
{
    public class SnakeCaseMappingTests
    {
        private class SimpleUser
        {
            public int Id { get; set; }
            public string? FirstName { get; set; }
            public int UserAge { get; set; }
        }

        [Fact]
        public void Should_Use_SnakeCase_Convention_For_Table_And_Column_Names()
        {
            var naming = new SnakeCaseNamingStrategy();
            var builder = new ReflectionModelBuilder(naming);
            var director = new ModelDirector(builder);

            builder.BuildEntity(typeof(SimpleUser), hasDerivedTypes: false);
            var maps = builder.GetResult();
            var map = maps[typeof(SimpleUser)];

            Assert.Equal("simple_user", map.TableName);

            var firstNameProp = map.ScalarProperties.First(p => p.PropertyInfo.Name == "FirstName");
            var userAgeProp = map.ScalarProperties.First(p => p.PropertyInfo.Name == "UserAge");

            Assert.Equal("first_name", firstNameProp.ColumnName);

            Assert.Equal("user_age", userAgeProp.ColumnName);
        }
    }
}