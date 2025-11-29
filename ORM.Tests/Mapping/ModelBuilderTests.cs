using System.Reflection;
using ORM_v1.Mapping;
using ORM.Tests.TestEntities;
using Xunit;

namespace ORM.Tests.Mapping
{
    public class ModelBuilderTests
    {
        [Fact]
        public void ModelBuilder_Should_Find_Entity_And_Create_Map()
        {
            var builder = new ModelBuilder(typeof(UserTestEntity).Assembly);
            var maps = builder.BuildModel(new PascalCaseNamingStrategy());

            Assert.True(maps.ContainsKey(typeof(UserTestEntity)));

            var map = maps[typeof(UserTestEntity)];

            Assert.Equal("Users", map.TableName);
            Assert.Equal("first_name", map.FindPropertyByName("FirstName")!.ColumnName);
        }

        [Fact]
        public void ModelBuilder_Should_Detect_Primary_Key()
        {
            var builder = new ModelBuilder(typeof(UserTestEntity).Assembly);
            var maps = builder.BuildModel(new PascalCaseNamingStrategy());

            var map = maps[typeof(UserTestEntity)];

            Assert.Equal("Id", map.KeyProperty.PropertyInfo.Name);
        }
    }
}
