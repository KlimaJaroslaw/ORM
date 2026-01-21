using ORM.Tests.TestEntities;
using ORM_v1.Mapping;
using ORM_v1.Query;

namespace ORM.Tests.SqlGeneration
{
    public class SqliteSqlGeneratorTests
    {
        private readonly SqliteSqlGenerator _generator;
        private readonly INamingStrategy _naming;

        public SqliteSqlGeneratorTests()
        {
            _generator = new SqliteSqlGenerator();
            _naming = new PascalCaseNamingStrategy();
        }

        private EntityMap CreateUserEntityMap()
        {
            INamingStrategy naming = new PascalCaseNamingStrategy();
            IModelBuilder builder = new ReflectionModelBuilder(naming);
            var director = new ModelDirector(builder);
            var maps = director.Construct(typeof(UserTestEntity).Assembly);
            return maps[typeof(UserTestEntity)];
        }

        private EntityMap CreateTestEntityMap(
            string tableName,
            string keyColumnName,
            List<(string propName, string colName, bool isKey)> properties,
            bool hasAutoIncrement = false)
        {
            var type = typeof(TestEntity);
            var props = new List<PropertyMap>();
            PropertyMap? keyProp = null;

            foreach (var (propName, colName, isKey) in properties)
            {
                var propInfo = type.GetProperty(propName);
                if (propInfo == null) continue;

                var prop = new PropertyMap(
                    propInfo,
                    colName,
                    isKey,
                    isIgnored: false,
                    isNavigation: false,
                    isCollection: false,
                    targetType: null,
                    foreignKeyName: null
                    // navigationPropertyName: null
                );

                props.Add(prop);
                if (isKey) keyProp = prop;
            }

            // var map = new EntityMap(type, tableName, keyProp!, props);

            var map = new EntityMap(
                type,
                tableName,
                isAbstract: false,
                baseMap: null,
                strategy: InheritanceStrategy.TablePerHierarchy,
                discriminator: null,
                discriminatorColumn: null,
                keyProperty: keyProp!,
                allProperties: props
            );

            if (hasAutoIncrement)
            {
                var field = typeof(EntityMap).GetField("_hasAutoIncrementKey", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                field?.SetValue(map, true);
            }

            return map;
        }

        #region Helper Methods Tests

        [Fact]
        public void QuoteIdentifier_Should_Quote_With_DoubleQuotes()
        {
            // Act
            var result = _generator.QuoteIdentifier("Users");

            // Assert
            Assert.Equal("\"Users\"", result);
        }

        [Fact]
        public void GetParameterName_Should_Return_Correct_Format()
        {
            // Act
            var result = _generator.GetParameterName("age", 1);

            // Assert
            Assert.Equal("@age1", result);
        }

        #endregion

        #region GenerateSelect Tests

        [Fact]
        public void GenerateSelect_Should_Create_Valid_SQL()
        {
            // Arrange
            var map = CreateUserEntityMap();
            var id = 1;

            // Act
            var sqlQuery = _generator.GenerateSelect(map, id);

            // Assert
            Assert.NotNull(sqlQuery);
            Assert.Contains("SELECT", sqlQuery.Sql);
            Assert.Contains("FROM Users", sqlQuery.Sql);
            Assert.Contains("WHERE Id = @id", sqlQuery.Sql);
            Assert.Single(sqlQuery.Parameters);
            Assert.Equal(id, sqlQuery.Parameters["@id"]);
        }

        [Fact]
        public void GenerateSelect_Should_Select_All_Scalar_Columns()
        {
            // Arrange
            var map = CreateUserEntityMap();

            // Act
            var sqlQuery = _generator.GenerateSelect(map, 1);

            // Assert
            Assert.Contains("Id", sqlQuery.Sql);
            Assert.Contains("first_name", sqlQuery.Sql);
            Assert.Contains("LastName", sqlQuery.Sql);
        }

        [Fact]
        public void GenerateSelect_Should_Not_Include_Ignored_Properties()
        {
            // Arrange
            var map = CreateUserEntityMap();

            // Act
            var sqlQuery = _generator.GenerateSelect(map, 1);

            // Assert
            Assert.DoesNotContain("IgnoredProp", sqlQuery.Sql);
        }

        #endregion

        #region GenerateSelectAll Tests

        [Fact]
        public void GenerateSelectAll_Should_Create_Valid_SQL()
        {
            // Arrange
            var map = CreateUserEntityMap();

            // Act
            var sqlQuery = _generator.GenerateSelectAll(map);

            // Assert
            Assert.NotNull(sqlQuery);
            Assert.Contains("SELECT", sqlQuery.Sql);
            Assert.Contains("FROM Users", sqlQuery.Sql);
            Assert.DoesNotContain("WHERE", sqlQuery.Sql);
            Assert.Empty(sqlQuery.Parameters);
        }

        [Fact]
        public void GenerateSelectAll_Should_Select_All_Scalar_Columns()
        {
            // Arrange
            var map = CreateUserEntityMap();

            // Act
            var sqlQuery = _generator.GenerateSelectAll(map);

            // Assert
            Assert.Contains("Id", sqlQuery.Sql);
            Assert.Contains("first_name", sqlQuery.Sql);
            Assert.Contains("LastName", sqlQuery.Sql);
        }

        #endregion

        #region GenerateInsert Tests

        [Fact]
        public void GenerateInsert_Should_Create_Valid_SQL()
        {
            // Arrange
            var map = CreateUserEntityMap();
            var entity = new UserTestEntity
            {
                Id = 0, // Auto-increment
                FirstName = "John",
                LastName = "Doe"
            };

            // Act
            var sqlQuery = _generator.GenerateInsert(map, entity);

            // Assert
            Assert.NotNull(sqlQuery);
            Assert.Contains("INSERT INTO Users", sqlQuery.Sql);
            Assert.Contains("first_name", sqlQuery.Sql);
            Assert.Contains("LastName", sqlQuery.Sql);
            Assert.Contains("VALUES", sqlQuery.Sql);
        }

        [Fact]
        public void GenerateInsert_Should_Include_All_Parameters()
        {
            // Arrange
            var map = CreateUserEntityMap();
            var entity = new UserTestEntity
            {
                FirstName = "Jane",
                LastName = "Smith"
            };

            // Act
            var sqlQuery = _generator.GenerateInsert(map, entity);

            // Assert
            Assert.Contains(sqlQuery.Parameters, p => p.Key == "@FirstName" && p.Value.Equals("Jane"));
            Assert.Contains(sqlQuery.Parameters, p => p.Key == "@LastName" && p.Value.Equals("Smith"));
        }

        [Fact]
        public void GenerateInsert_WithAutoIncrement_Should_Include_LastInsertRowId()
        {
            // Arrange
            var map = CreateUserEntityMap();
            var entity = new UserTestEntity
            {
                FirstName = "Auto",
                LastName = "Increment"
            };

            // Act
            var sqlQuery = _generator.GenerateInsert(map, entity);

            // Assert
            Assert.Contains("SELECT last_insert_rowid()", sqlQuery.Sql);
        }

        [Fact]
        public void GenerateInsert_Should_Handle_Null_Values()
        {
            // Arrange
            var map = CreateUserEntityMap();
            var entity = new UserTestEntity
            {
                FirstName = "Test",
                LastName = null! // Nullable in practice
            };

            // Act
            var sqlQuery = _generator.GenerateInsert(map, entity);

            Assert.NotNull(sqlQuery);
            Assert.NotEmpty(sqlQuery.Parameters);
        }

        [Fact]
        public void GenerateInsert_WithIncompatibleEntity_Should_Throw()
        {
            // Arrange
            var map = CreateUserEntityMap();
            var wrongEntity = "wrong type";

            // Act & Assert
            Assert.Throws<System.ArgumentException>(() =>
                _generator.GenerateInsert(map, wrongEntity));
        }

        #endregion

        #region GenerateUpdate Tests

        [Fact]
        public void GenerateUpdate_Should_Create_Valid_SQL()
        {
            // Arrange
            var map = CreateUserEntityMap();
            var entity = new UserTestEntity
            {
                Id = 1,
                FirstName = "Updated",
                LastName = "User"
            };

            // Act
            var sqlQuery = _generator.GenerateUpdate(map, entity);

            // Assert
            Assert.NotNull(sqlQuery);
            Assert.Contains("UPDATE Users", sqlQuery.Sql);
            Assert.Contains("SET", sqlQuery.Sql);
            Assert.Contains("first_name = @FirstName", sqlQuery.Sql);
            Assert.Contains("LastName = @LastName", sqlQuery.Sql);
            Assert.Contains("WHERE Id = @Id", sqlQuery.Sql);
        }

        [Fact]
        public void GenerateUpdate_Should_Include_All_Parameters()
        {
            // Arrange
            var map = CreateUserEntityMap();
            var entity = new UserTestEntity
            {
                Id = 5,
                FirstName = "UpdatedFirst",
                LastName = "UpdatedLast"
            };

            // Act
            var sqlQuery = _generator.GenerateUpdate(map, entity);

            // Assert
            Assert.Equal(3, sqlQuery.Parameters.Count); // FirstName, LastName, Id
            Assert.Contains(sqlQuery.Parameters, p => p.Key == "@FirstName" && p.Value.Equals("UpdatedFirst"));
            Assert.Contains(sqlQuery.Parameters, p => p.Key == "@LastName" && p.Value.Equals("UpdatedLast"));
            Assert.Contains(sqlQuery.Parameters, p => p.Key == "@Id" && p.Value.Equals(5));
        }

        [Fact]
        public void GenerateUpdate_Should_Not_Update_Key_Property()
        {
            // Arrange
            var map = CreateUserEntityMap();
            var entity = new UserTestEntity
            {
                Id = 1,
                FirstName = "Test",
                LastName = "User"
            };

            // Act
            var sqlQuery = _generator.GenerateUpdate(map, entity);

            // Assert
            Assert.DoesNotContain("SET Id =", sqlQuery.Sql);
        }

        [Fact]
        public void GenerateUpdate_WithIncompatibleEntity_Should_Throw()
        {
            // Arrange
            var map = CreateUserEntityMap();
            var wrongEntity = 123;

            // Act & Assert
            Assert.Throws<System.ArgumentException>(() =>
                _generator.GenerateUpdate(map, wrongEntity));
        }

        #endregion

        #region GenerateDelete Tests

        [Fact]
        public void GenerateDelete_Should_Create_Valid_SQL()
        {
            // Arrange
            var map = CreateUserEntityMap();
            var entity = new UserTestEntity { Id = 1 };

            // Act
            var sqlQuery = _generator.GenerateDelete(map, entity);

            // Assert
            Assert.NotNull(sqlQuery);
            Assert.Contains("DELETE FROM Users", sqlQuery.Sql);
            Assert.Contains("WHERE Id = @id", sqlQuery.Sql);
        }

        [Fact]
        public void GenerateDelete_Should_Use_Entity_Id()
        {
            // Arrange
            var map = CreateUserEntityMap();
            var entity = new UserTestEntity { Id = 42 };

            // Act
            var sqlQuery = _generator.GenerateDelete(map, entity);

            // Assert
            Assert.Single(sqlQuery.Parameters);
            Assert.Equal(42, sqlQuery.Parameters["@id"]);
        }

        [Fact]
        public void GenerateDelete_WithIncompatibleEntity_Should_Throw()
        {
            // Arrange
            var map = CreateUserEntityMap();
            var wrongEntity = new { Id = 1 }; // Anonymous type

            // Act & Assert
            Assert.Throws<System.ArgumentException>(() =>
                _generator.GenerateDelete(map, wrongEntity));
        }

        #endregion

        #region GenerateComplexSelect Tests

        [Fact]
        public void GenerateComplexSelect_WithSelectAll_Should_Create_Valid_SQL()
        {
            // Arrange
            var map = CreateUserEntityMap();
            var queryModel = new QueryModel
            {
                PrimaryEntity = map,
                SelectAllColumns = true
            };

            // Act
            var sqlQuery = _generator.GenerateComplexSelect(map, queryModel);

            // Assert
            Assert.Contains("SELECT", sqlQuery.Sql);
            Assert.Contains("FROM \"Users\"", sqlQuery.Sql);
        }

        [Fact]
        public void GenerateComplexSelect_WithDistinct_Should_Include_DISTINCT()
        {
            // Arrange
            var map = CreateUserEntityMap();
            var queryModel = new QueryModel
            {
                PrimaryEntity = map,
                Distinct = true,
                SelectAllColumns = true
            };

            // Act
            var sqlQuery = _generator.GenerateComplexSelect(map, queryModel);

            // Assert
            Assert.Contains("SELECT DISTINCT", sqlQuery.Sql);
        }

        [Fact]
        public void GenerateComplexSelect_WithTableAlias_Should_Use_Alias()
        {
            // Arrange
            var map = CreateUserEntityMap();
            var queryModel = new QueryModel
            {
                PrimaryEntity = map,
                PrimaryEntityAlias = "u",
                SelectAllColumns = true
            };

            // Act
            var sqlQuery = _generator.GenerateComplexSelect(map, queryModel);

            // Assert
            Assert.Contains("FROM \"Users\" AS \"u\"", sqlQuery.Sql);
            Assert.Contains("\"u\".", sqlQuery.Sql); // Columns should be prefixed
        }

        [Fact]
        public void GenerateComplexSelect_WithWhere_Should_Include_WHERE_Clause()
        {
            // Arrange
            var map = CreateUserEntityMap();
            var queryModel = new QueryModel
            {
                PrimaryEntity = map,
                SelectAllColumns = true,
                WhereClause = "Id > @id",
                Parameters = new Dictionary<string, object>
                {
                    { "@id", 10 }
                }
            };

            // Act
            var sqlQuery = _generator.GenerateComplexSelect(map, queryModel);

            // Assert
            Assert.Contains("WHERE Id > @id", sqlQuery.Sql);
            Assert.Equal(10, sqlQuery.Parameters["@id"]);
        }

        [Fact]
        public void GenerateComplexSelect_WithOrderBy_Should_Include_ORDER_BY()
        {
            // Arrange
            var map = CreateUserEntityMap();
            var nameProperty = map.FindPropertyByName("FirstName")!;
            
            var queryModel = new QueryModel
            {
                PrimaryEntity = map,
                SelectAllColumns = true,
                OrderBy = new List<OrderByClause>
                {
                    new OrderByClause
                    {
                        Property = nameProperty,
                        IsAscending = true
                    }
                }
            };

            // Act
            var sqlQuery = _generator.GenerateComplexSelect(map, queryModel);

            // Assert
            Assert.Contains("ORDER BY", sqlQuery.Sql);
            Assert.Contains("ASC", sqlQuery.Sql);
        }

        [Fact]
        public void GenerateComplexSelect_WithOrderByDescending_Should_Include_DESC()
        {
            // Arrange
            var map = CreateUserEntityMap();
            var idProperty = map.KeyProperty;
            
            var queryModel = new QueryModel
            {
                PrimaryEntity = map,
                SelectAllColumns = true,
                OrderBy = new List<OrderByClause>
                {
                    new OrderByClause
                    {
                        Property = idProperty,
                        IsAscending = false
                    }
                }
            };

            // Act
            var sqlQuery = _generator.GenerateComplexSelect(map, queryModel);

            // Assert
            Assert.Contains("ORDER BY", sqlQuery.Sql);
            Assert.Contains("DESC", sqlQuery.Sql);
        }

        [Fact]
        public void GenerateComplexSelect_WithTake_Should_Include_LIMIT()
        {
            // Arrange
            var map = CreateUserEntityMap();
            var queryModel = new QueryModel
            {
                PrimaryEntity = map,
                SelectAllColumns = true,
                Take = 10
            };

            // Act
            var sqlQuery = _generator.GenerateComplexSelect(map, queryModel);

            // Assert
            Assert.Contains("LIMIT 10", sqlQuery.Sql);
        }

        [Fact]
        public void GenerateComplexSelect_WithSkip_Should_Include_OFFSET()
        {
            // Arrange
            var map = CreateUserEntityMap();
            var queryModel = new QueryModel
            {
                PrimaryEntity = map,
                SelectAllColumns = true,
                Skip = 20
            };

            // Act
            var sqlQuery = _generator.GenerateComplexSelect(map, queryModel);

            // Assert
            Assert.Contains("OFFSET 20", sqlQuery.Sql);
        }

        [Fact]
        public void GenerateComplexSelect_WithPagination_Should_Include_LIMIT_And_OFFSET()
        {
            // Arrange
            var map = CreateUserEntityMap();
            var queryModel = new QueryModel
            {
                PrimaryEntity = map,
                SelectAllColumns = true,
                Take = 10,
                Skip = 20
            };

            // Act
            var sqlQuery = _generator.GenerateComplexSelect(map, queryModel);

            // Assert
            Assert.Contains("LIMIT 10", sqlQuery.Sql);
            Assert.Contains("OFFSET 20", sqlQuery.Sql);
        }

        [Fact]
        public void GenerateComplexSelect_WithGroupBy_Should_Include_GROUP_BY()
        {
            // Arrange
            var map = CreateUserEntityMap();
            var lastNameProperty = map.FindPropertyByName("LastName")!;
            
            var queryModel = new QueryModel
            {
                PrimaryEntity = map,
                GroupByColumns = new List<PropertyMap> { lastNameProperty },
                Aggregates = new List<AggregateFunction>
                {
                    new AggregateFunction
                    {
                        FunctionType = AggregateFunctionType.Count,
                        Alias = "UserCount"
                    }
                }
            };

            // Act
            var sqlQuery = _generator.GenerateComplexSelect(map, queryModel);

            // Assert
            Assert.Contains("GROUP BY", sqlQuery.Sql);
            Assert.Contains("\"LastName\"", sqlQuery.Sql);
        }

        [Fact]
        public void GenerateComplexSelect_WithHaving_Should_Include_HAVING()
        {
            // Arrange
            var map = CreateUserEntityMap();
            var queryModel = new QueryModel
            {
                PrimaryEntity = map,
                GroupByColumns = new List<PropertyMap> { map.KeyProperty },
                HavingClause = "COUNT(*) > 5",
                Aggregates = new List<AggregateFunction>
                {
                    new AggregateFunction { FunctionType = AggregateFunctionType.Count }
                }
            };

            // Act
            var sqlQuery = _generator.GenerateComplexSelect(map, queryModel);

            // Assert
            Assert.Contains("HAVING COUNT(*) > 5", sqlQuery.Sql);
        }

        [Fact]
        public void GenerateComplexSelect_WithAggregate_COUNT_Should_Generate_COUNT_Function()
        {
            // Arrange
            var map = CreateUserEntityMap();
            var queryModel = new QueryModel
            {
                PrimaryEntity = map,
                Aggregates = new List<AggregateFunction>
                {
                    new AggregateFunction
                    {
                        FunctionType = AggregateFunctionType.Count,
                        Property = null, // COUNT(*)
                        Alias = "TotalUsers"
                    }
                }
            };

            // Act
            var sqlQuery = _generator.GenerateComplexSelect(map, queryModel);

            // Assert
            Assert.Contains("COUNT(*)", sqlQuery.Sql);
            Assert.Contains("AS \"TotalUsers\"", sqlQuery.Sql);
        }

        [Fact]
        public void GenerateComplexSelect_WithMultipleAggregates_Should_Generate_All_Functions()
        {
            // Arrange
            var map = CreateUserEntityMap();
            var idProperty = map.KeyProperty;
            
            var queryModel = new QueryModel
            {
                PrimaryEntity = map,
                Aggregates = new List<AggregateFunction>
                {
                    new AggregateFunction
                    {
                        FunctionType = AggregateFunctionType.Count,
                        Alias = "Total"
                    },
                    new AggregateFunction
                    {
                        FunctionType = AggregateFunctionType.Max,
                        Property = idProperty,
                        Alias = "MaxId"
                    }
                }
            };

            // Act
            var sqlQuery = _generator.GenerateComplexSelect(map, queryModel);

            // Assert
            Assert.Contains("COUNT(*)", sqlQuery.Sql);
            Assert.Contains("MAX(", sqlQuery.Sql);
        }

        [Fact]
        public void GenerateComplexSelect_CompleteQuery_Should_Generate_All_Clauses_In_Order()
        {
            // Arrange
            var map = CreateUserEntityMap();
            var nameProperty = map.FindPropertyByName("FirstName")!;
            
            var queryModel = new QueryModel
            {
                PrimaryEntity = map,
                PrimaryEntityAlias = "u",
                SelectAllColumns = true,
                WhereClause = "u.Id > @minId",
                Parameters = new Dictionary<string, object> { { "@minId", 1 } },
                OrderBy = new List<OrderByClause>
                {
                    new OrderByClause { Property = nameProperty, IsAscending = true }
                },
                Take = 10,
                Skip = 5
            };

            // Act
            var sqlQuery = _generator.GenerateComplexSelect(map, queryModel);

            // Assert
            var sql = sqlQuery.Sql;
            var selectIndex = sql.IndexOf("SELECT");
            var fromIndex = sql.IndexOf("FROM");
            var whereIndex = sql.IndexOf("WHERE");
            var orderIndex = sql.IndexOf("ORDER BY");
            var limitIndex = sql.IndexOf("LIMIT");
            var offsetIndex = sql.IndexOf("OFFSET");

            Assert.True(selectIndex < fromIndex);
            Assert.True(fromIndex < whereIndex);
            Assert.True(whereIndex < orderIndex);
            Assert.True(orderIndex < limitIndex);
            Assert.True(limitIndex < offsetIndex);
        }

        #endregion


        private class TestEntity
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }
    }
}
