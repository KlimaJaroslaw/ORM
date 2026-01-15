using Xunit;
using ORM_v1.Mapping;
using ORM.Tests.TestEntities;
using System.Collections.Generic;
using ORM.Tests.Helpers;

namespace ORM.Tests.Mapping
{
    public class ObjectMaterializerTests
    {
        [Fact]
        public void Materializer_Should_Create_Object_From_DataRecord()
        {
            var naming = new PascalCaseNamingStrategy();
            var builder = new ModelBuilder(typeof(UserTestEntity).Assembly);
            var maps = builder.BuildModel(naming);
            var map = maps[typeof(UserTestEntity)];

            var materializer = new ObjectMaterializer(map, new MetadataStore(maps));

            var record = new FakeDataRecord(new Dictionary<string, object?>
            {
                ["Id"] = 5,
                ["first_name"] = "John",
                ["LastName"] = "Doe"
            });

            // var obj = (UserTestEntity)materializer.Materialize(record, map);

            int[] ordinals = new int[map.ScalarProperties.Count];
            int i = 0;

            foreach (var prop in map.ScalarProperties)
            {
                ordinals[i++] = record.GetOrdinal(prop.ColumnName!);
            }

            var obj = (UserTestEntity)materializer.Materialize(record, ordinals);


            Assert.Equal(5, obj.Id);
            Assert.Equal("John", obj.FirstName);
            Assert.Equal("Doe", obj.LastName);
        }
    }
}
