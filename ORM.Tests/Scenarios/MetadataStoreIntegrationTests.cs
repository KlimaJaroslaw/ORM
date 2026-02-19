using System.Collections.Generic;
using ORM_v1.Mapping;
using ORM.Tests.Helpers;
using ORM.Tests.TestEntities;
using Xunit;

namespace ORM.Tests.Scenarios
{
    public class MetadataStoreIntegrationTests
    {
        private IMetadataStore CreateStore()
        {
            return new MetadataStoreBuilder()
                .AddAssembly(typeof(UserTestEntity).Assembly)
                .UseNamingStrategy(new PascalCaseNamingStrategy())
                .Build();
        }

        [Fact]
        public void Should_Initialize_Store_And_Materialize_Entity_EndToEnd()
        {
            var store = CreateStore();

            var map = store.GetMap<UserTestEntity>();

            var record = new FakeDataRecord(new Dictionary<string, object?>
            {
                { "Id", 42 },
                { "first_name", "Linus" },
                { "LastName", "Torvalds" }
            });

            var materializer = new ObjectMaterializer(map, store);
            // var entity = (UserTestEntity)materializer.Materialize(record, map);

            int[] ordinals = new int[map.ScalarProperties.Count];
            int i = 0;

            foreach (var prop in map.ScalarProperties)
            {
                ordinals[i++] = record.GetOrdinal(prop.ColumnName!);
            }

            var entity = (UserTestEntity)materializer.Materialize(record, ordinals);


            Assert.Equal(42, entity.Id);
            Assert.Equal("Linus", entity.FirstName);
            Assert.Equal("Torvalds", entity.LastName);
        }
    }
}
