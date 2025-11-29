using System.Collections.Generic;
using ORM_v1.Mapping;
using ORM.Tests.Helpers;
using ORM.Tests.TestEntities;
using Xunit;

namespace ORM.Tests.Scenarios
{
    public class SnakeCaseMappingTests
    {
        [Fact]
        public void Should_Use_SnakeCase_Convention_For_Column_Names()
        {
            var builder = new ModelBuilder(typeof(UserTestEntity).Assembly);
            var naming = new SnakeCaseNamingStrategy();

            var maps = builder.BuildModel(naming);
            var map = maps[typeof(UserTestEntity)];

            Assert.Equal("Users", map.TableName);
            Assert.Equal("id", map.KeyProperty.ColumnName);
            Assert.Equal("first_name", map.FindPropertyByName("FirstName")!.ColumnName);
            Assert.Equal("last_name", map.FindPropertyByName("LastName")!.ColumnName);
        }

        [Fact]
        public void SnakeCase_Should_Work_With_Materialization()
        {
            var builder = new ModelBuilder(typeof(UserTestEntity).Assembly);
            var naming = new SnakeCaseNamingStrategy();

            var maps = builder.BuildModel(naming);
            var map = maps[typeof(UserTestEntity)];

            var materializer = new ObjectMaterializer(map);

            var record = new FakeDataRecord(new Dictionary<string, object?>
            {
                { "id", 7 },
                { "first_name", "Bob" },
                { "last_name", "Marley" }
            });

            // var entity = (UserTestEntity)materializer.Materialize(record, map);

            int[] ordinals = new int[map.ScalarProperties.Count];
            int i = 0;

            foreach (var prop in map.ScalarProperties)
            {
                ordinals[i++] = record.GetOrdinal(prop.ColumnName!);
            }

            var entity = (UserTestEntity)materializer.Materialize(record, map, ordinals);


            Assert.Equal(7, entity.Id);
            Assert.Equal("Bob", entity.FirstName);
            Assert.Equal("Marley", entity.LastName);
        }
    }
}
