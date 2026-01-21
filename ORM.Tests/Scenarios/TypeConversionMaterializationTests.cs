using ORM_v1.Mapping;
using ORM.Tests.TestEntities;
using ORM.Tests.Helpers;
using Xunit;
using ORM_v1.Attributes;
using System.Collections.Generic;
using System.Reflection;

namespace ORM.Tests.Scenarios
{
    /// <summary>
    /// Test weryfikujący konwersję typów podczas materializacji obiektów.
    /// Szczególnie ważne dla SQLite, które zwraca Int64 zamiast Int32.
    /// </summary>
    public class TypeConversionMaterializationTests
    {
        private static IReadOnlyDictionary<Type, EntityMap> BuildModel(Assembly asm)
        {
            INamingStrategy naming = new PascalCaseNamingStrategy();
            IModelBuilder builder = new ReflectionModelBuilder(naming);
            var director = new ModelDirector(builder);
            return director.Construct(asm);
        }

        [Fact]
        public void Should_Convert_Int64_To_Int32_From_SQLite()
        {
            var maps = BuildModel(typeof(UserTestEntity).Assembly);
            var map = maps[typeof(UserTestEntity)];

            var record = new FakeDataRecord(new Dictionary<string, object?>
            {
                { "Id", 42L },  // Int64 (SQLite)
                { "first_name", "John" },
                { "LastName", "Doe" }
            });

            var materializer = new ObjectMaterializer(map, new MetadataStore(maps));

            int[] ordinals = new int[map.ScalarProperties.Count];
            int i = 0;
            foreach (var prop in map.ScalarProperties)
            {
                ordinals[i++] = record.GetOrdinal(prop.ColumnName!);
            }

            var entity = (UserTestEntity)materializer.Materialize(record, ordinals);

            Assert.Equal(42, entity.Id);  // Int32
            Assert.Equal("John", entity.FirstName);
            Assert.Equal("Doe", entity.LastName);
        }

        [Fact]
        public void Should_Handle_Various_Numeric_Conversions()
        {
            var maps = BuildModel(typeof(UserTestEntity).Assembly);
            var map = maps[typeof(UserTestEntity)];

            var testCases = new[] { 42L, 100L, 1L };

            foreach (var idValue in testCases)
            {
                var record = new FakeDataRecord(new Dictionary<string, object?>
                {
                    { "Id", idValue },
                    { "first_name", "Test" },
                    { "LastName", "User" }
                });

                var materializer = new ObjectMaterializer(map, new MetadataStore(maps));

                int[] ordinals = new int[map.ScalarProperties.Count];
                int i = 0;
                foreach (var prop in map.ScalarProperties)
                {
                    ordinals[i++] = record.GetOrdinal(prop.ColumnName!);
                }

                var entity = (UserTestEntity)materializer.Materialize(record, ordinals);

                Assert.Equal((int)idValue, entity.Id);
            }
        }

        [Fact]
        public void Should_Throw_InvalidOperationException_On_Invalid_Conversion()
        {
            var maps = BuildModel(typeof(UserTestEntity).Assembly);
            var map = maps[typeof(UserTestEntity)];

            var record = new FakeDataRecord(new Dictionary<string, object?>
            {
                { "Id", "not a number" }, // String -> int
                { "first_name", "John" },
                { "LastName", "Doe" }
            });

            var materializer = new ObjectMaterializer(map, new MetadataStore(maps));

            int[] ordinals = new int[map.ScalarProperties.Count];
            int i = 0;
            foreach (var prop in map.ScalarProperties)
            {
                ordinals[i++] = record.GetOrdinal(prop.ColumnName!);
            }

            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                materializer.Materialize(record, ordinals);
            });

            Assert.Contains("Cannot convert", ex.Message);
            Assert.Contains("Id", ex.Message);
            Assert.Contains("System.String", ex.Message);
        }

        [Fact]
        public void Should_Handle_Enum_With_Int64_Value()
        {
            var maps = BuildModel(typeof(UserWithEnums).Assembly);
            var map = maps[typeof(UserWithEnums)];

            var record = new FakeDataRecord(new Dictionary<string, object?>
            {
                { "Id", 1L },
                { "Role", 1L },  // Int64 -> Enum
                { "OptionalRole", 0L }
            });

            var materializer = new ObjectMaterializer(map, new MetadataStore(maps));

            int[] ordinals = new int[map.ScalarProperties.Count];
            int i = 0;
            foreach (var prop in map.ScalarProperties)
            {
                ordinals[i++] = record.GetOrdinal(prop.ColumnName!);
            }

            var entity = (UserWithEnums)materializer.Materialize(record, ordinals);

            Assert.Equal(1, entity.Id);
            Assert.Equal(Role.Admin, entity.Role);
            Assert.Equal(Role.User, entity.OptionalRole);
        }
    }
}
