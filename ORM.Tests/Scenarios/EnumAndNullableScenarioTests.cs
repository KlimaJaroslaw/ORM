using ORM_v1.Mapping;
using ORM.Tests.Helpers;
using ORM.Tests.TestEntities;
using Xunit;
using System.Collections.Generic;

namespace ORM.Tests.Scenarios
{
    public class EnumAndNullableScenarioTests
    {
        [Fact]
        public void Should_Map_Enum_And_Nullable_Enum_Correctly()
        {
            var builder = new ModelBuilder(typeof(UserWithEnums).Assembly);
            var naming = new PascalCaseNamingStrategy();

            var maps = builder.BuildModel(naming);
            var map = maps[typeof(UserWithEnums)];

            var materializer = new ObjectMaterializer(map, new MetadataStore(maps));

            var record = new FakeDataRecord(new Dictionary<string, object?>
            {
                { "Id", 1 },
                { "Role", 1 },
                { "OptionalRole", null }
            });

            // var entity = (UserWithEnums)materializer.Materialize(record, map);

            int[] ordinals = new int[map.ScalarProperties.Count];
            int i = 0;

            foreach (var prop in map.ScalarProperties)
            {
                ordinals[i++] = record.GetOrdinal(prop.ColumnName!);
            }

            var entity = (UserWithEnums)materializer.Materialize(record, ordinals);


            Assert.Equal(Role.Admin, entity.Role);
            Assert.Null(entity.OptionalRole);
        }
    }
}
