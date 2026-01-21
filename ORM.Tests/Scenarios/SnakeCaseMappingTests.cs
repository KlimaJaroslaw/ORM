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
            INamingStrategy naming = new SnakeCaseNamingStrategy(); // ← zmiana
            IModelBuilder builder = new ReflectionModelBuilder(naming);
            var director = new ModelDirector(builder);

            IReadOnlyDictionary<System.Type, EntityMap> maps = director.Construct(typeof(UserTestEntity).Assembly);
            var map = maps[typeof(UserTestEntity)];

            Assert.Equal("users", map.TableName); // teraz zgodne z SnakeCaseNamingStrategy
            Assert.Equal("id", map.KeyProperty.ColumnName);
            Assert.Equal("first_name", map.FindPropertyByName("FirstName")!.ColumnName);
            Assert.Equal("last_name", map.FindPropertyByName("LastName")!.ColumnName);
        }

        [Fact]
        public void SnakeCase_Should_Work_With_Materialization()
        {
            INamingStrategy naming = new SnakeCaseNamingStrategy(); // ← zmiana
            IModelBuilder builder = new ReflectionModelBuilder(naming);
            var director = new ModelDirector(builder);

            IReadOnlyDictionary<System.Type, EntityMap> maps = director.Construct(typeof(UserTestEntity).Assembly);
            var map = maps[typeof(UserTestEntity)];

            var materializer = new ObjectMaterializer(map, new MetadataStore(maps));

            var record = new FakeDataRecord(new Dictionary<string, object?>
            {
                { "id", 7 },
                { "first_name", "Bob" },
                { "last_name", "Marley" }
            });

            int[] ordinals = new int[map.ScalarProperties.Count];
            int i = 0;

            foreach (var prop in map.ScalarProperties)
            {
                ordinals[i++] = record.GetOrdinal(prop.ColumnName!);
            }

            var entity = (UserTestEntity)materializer.Materialize(record, ordinals);

            Assert.Equal(7, entity.Id);
            Assert.Equal("Bob", entity.FirstName);
            Assert.Equal("Marley", entity.LastName);
        }
    }
}
