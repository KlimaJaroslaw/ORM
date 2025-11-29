using System.Reflection;
using ORM_v1.Mapping;
using ORM.Tests.TestEntities;
using Xunit;

namespace ORM.Tests.Mapping
{
    public class MetadataStoreTests
    {
        private IMetadataStore CreateStore()
        {
            return new MetadataStoreBuilder()
                .AddAssembly(typeof(UserTestEntity).Assembly)
                .UseNamingStrategy(new PascalCaseNamingStrategy())
                .Build();
        }

        [Fact]
        public void Build_Should_Create_MetadataStore()
        {
            var store = CreateStore();
            Assert.NotNull(store);
        }

        [Fact]
        public void GetMap_Should_Return_Correct_Map()
        {
            var store = CreateStore();

            var map = store.GetMap<UserTestEntity>();

            Assert.Equal("Users", map.TableName);
            Assert.Equal("Id", map.KeyProperty.ColumnName);
        }

        [Fact]
        public void GetMap_UnknownType_Should_Throw()
        {
            var store = CreateStore();

            Assert.Throws<KeyNotFoundException>(() =>
            {
                store.GetMap(typeof(string));
            });
        }
    }
}
