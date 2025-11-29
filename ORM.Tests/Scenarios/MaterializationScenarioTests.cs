using System.Collections.Generic;
using ORM_v1.Mapping;
using ORM.Tests.Helpers;
using ORM.Tests.TestEntities;
using Xunit;

namespace ORM.Tests.Scenarios
{
    public class MaterializationScenarioTests
    {
        [Fact]
        public void Should_Materialize_UserTestEntity_From_FakeDataRecord()
        {
            var builder = new ModelBuilder(typeof(UserTestEntity).Assembly);
            var maps = builder.BuildModel(new PascalCaseNamingStrategy());
            var map = maps[typeof(UserTestEntity)];

            var materializer = new ObjectMaterializer(map);

            var record = new FakeDataRecord(new Dictionary<string, object?>
            {
                { "Id", 10 },
                { "first_name", "Alice" },
                { "LastName", "Smith" }
            });

            // var entity = (UserTestEntity)materializer.Materialize(record, map);

            int[] ordinals = new int[map.ScalarProperties.Count];
            int i = 0;

            foreach (var prop in map.ScalarProperties)
            {
                ordinals[i++] = record.GetOrdinal(prop.ColumnName!);
            }

            var entity = (UserTestEntity)materializer.Materialize(record, map, ordinals);


            Assert.Equal(10, entity.Id);
            Assert.Equal("Alice", entity.FirstName);
            Assert.Equal("Smith", entity.LastName);
        }
    }
}
