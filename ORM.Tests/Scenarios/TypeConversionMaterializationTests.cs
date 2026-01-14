using ORM_v1.Mapping;
using ORM.Tests.TestEntities;
using ORM.Tests.Helpers;
using Xunit;
using ORM_v1.Attributes;

namespace ORM.Tests.Scenarios
{
    /// <summary>
    /// Test weryfikuj¹cy konwersjê typów podczas materializacji obiektów.
    /// Szczególnie wa¿ne dla SQLite, które zwraca Int64 zamiast Int32.
    /// </summary>
    public class TypeConversionMaterializationTests
    {
        [Fact]
        public void Should_Convert_Int64_To_Int32_From_SQLite()
        {
            // Arrange
            var builder = new ModelBuilder(typeof(UserTestEntity).Assembly);
            var maps = builder.BuildModel(new PascalCaseNamingStrategy());
            var map = maps[typeof(UserTestEntity)];

            // Symulujemy dane z SQLite - zwraca Int64 zamiast Int32
            var record = new FakeDataRecord(new Dictionary<string, object?>
            {
                { "Id", 42L },  // Int64 (long) - jak w SQLite
                { "first_name", "John" },
                { "LastName", "Doe" }
            });

            var materializer = new ObjectMaterializer(map);
            int[] ordinals = new int[map.ScalarProperties.Count];
            int i = 0;

            foreach (var prop in map.ScalarProperties)
            {
                ordinals[i++] = record.GetOrdinal(prop.ColumnName!);
            }

            // Act
            var entity = (UserTestEntity)materializer.Materialize(record, map, ordinals);

            // Assert
            Assert.Equal(42, entity.Id);  // Int32
            Assert.Equal("John", entity.FirstName);
            Assert.Equal("Doe", entity.LastName);
        }

        [Fact]
        public void Should_Handle_Various_Numeric_Conversions()
        {
            // Arrange
            var builder = new ModelBuilder(typeof(UserTestEntity).Assembly);
            var maps = builder.BuildModel(new PascalCaseNamingStrategy());
            var map = maps[typeof(UserTestEntity)];

            // Test z ró¿nymi wartoœciami numerycznymi
            var testCases = new[]
            {
                42L,      // Int64
                100L,     // Int64
                1L        // Int64
            };

            foreach (var idValue in testCases)
            {
                var record = new FakeDataRecord(new Dictionary<string, object?>
                {
                    { "Id", idValue },  // Int64 -> Int32
                    { "first_name", "Test" },
                    { "LastName", "User" }
                });

                var materializer = new ObjectMaterializer(map);
                int[] ordinals = new int[map.ScalarProperties.Count];
                int i = 0;

                foreach (var prop in map.ScalarProperties)
                {
                    ordinals[i++] = record.GetOrdinal(prop.ColumnName!);
                }

                // Act
                var entity = (UserTestEntity)materializer.Materialize(record, map, ordinals);

                // Assert
                Assert.Equal((int)idValue, entity.Id);
            }
        }

        [Fact]
        public void Should_Throw_InvalidOperationException_On_Invalid_Conversion()
        {
            // Arrange
            var builder = new ModelBuilder(typeof(UserTestEntity).Assembly);
            var maps = builder.BuildModel(new PascalCaseNamingStrategy());
            var map = maps[typeof(UserTestEntity)];

            // Próbujemy wstawiæ string do int
            var record = new FakeDataRecord(new Dictionary<string, object?>
            {
                { "Id", "not a number" },  // String -> Int32 (b³¹d!)
                { "first_name", "John" },
                { "LastName", "Doe" }
            });

            var materializer = new ObjectMaterializer(map);
            int[] ordinals = new int[map.ScalarProperties.Count];
            int i = 0;

            foreach (var prop in map.ScalarProperties)
            {
                ordinals[i++] = record.GetOrdinal(prop.ColumnName!);
            }

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                materializer.Materialize(record, map, ordinals);
            });

            Assert.Contains("Cannot convert", ex.Message);
            Assert.Contains("Id", ex.Message);
            Assert.Contains("System.String", ex.Message);
        }

        [Fact]
        public void Should_Handle_Enum_With_Int64_Value()
        {
            // Arrange
            var builder = new ModelBuilder(typeof(UserWithEnums).Assembly);
            var maps = builder.BuildModel(new PascalCaseNamingStrategy());
            var map = maps[typeof(UserWithEnums)];

            // SQLite zwraca enum jako Int64
            var record = new FakeDataRecord(new Dictionary<string, object?>
            {
                { "Id", 1L },
                { "Role", 1L },  // Int64 -> Enum
                { "OptionalRole", 0L }
            });

            var materializer = new ObjectMaterializer(map);
            int[] ordinals = new int[map.ScalarProperties.Count];
            int i = 0;

            foreach (var prop in map.ScalarProperties)
            {
                ordinals[i++] = record.GetOrdinal(prop.ColumnName!);
            }

            // Act
            var entity = (UserWithEnums)materializer.Materialize(record, map, ordinals);

            // Assert
            Assert.Equal(1, entity.Id);
            Assert.Equal(Role.Admin, entity.Role);
            Assert.Equal(Role.User, entity.OptionalRole);
        }
    }
}
