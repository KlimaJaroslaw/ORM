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
            INamingStrategy naming = new PascalCaseNamingStrategy();
            IModelBuilder builder = new ReflectionModelBuilder(naming);
            var director = new ModelDirector(builder);

            IReadOnlyDictionary<Type, EntityMap> maps = director.Construct(typeof(UserTestEntity).Assembly);
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
