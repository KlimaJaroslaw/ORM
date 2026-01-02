using ORM.Tests.TestEntities;
using ORM_v1.Mapping;
using ORM_v1.Query;

namespace ORM.Tests.SqlGeneration
{
    /// <summary>
    /// Tests for handling ambiguous column names in JOINs
    /// </summary>
    public class SqliteSqlGeneratorAmbiguousColumnTests
    {
        private readonly SqliteSqlGenerator _generator;
        private readonly INamingStrategy _naming;

        public SqliteSqlGeneratorAmbiguousColumnTests()
        {
            _generator = new SqliteSqlGenerator();
            _naming = new PascalCaseNamingStrategy();
        }

        private EntityMap CreateUserEntityMap()
        {
            var builder = new ModelBuilder(typeof(UserTestEntity).Assembly);
            var maps = builder.BuildModel(_naming);
            return maps[typeof(UserTestEntity)];
        }

        [Fact]
        public void GenerateComplexSelect_WithJoin_Should_Prefix_Columns_With_Table_Aliases()
        {
            // Arrange
            var userMap = CreateUserEntityMap();
            
            // Simulate a self-join where both tables have "Id" column
            var queryModel = new QueryModel
            {
                PrimaryEntity = userMap,
                PrimaryEntityAlias = "u1",
                SelectAllColumns = true,
                Joins = new List<JoinClause>
                {
                    new JoinClause
                    {
                        JoinedEntity = userMap, // Self-join
                        LeftProperty = userMap.KeyProperty, // Id
                        RightProperty = userMap.KeyProperty, // Id
                        JoinType = JoinType.Inner,
                        Alias = "u2"
                    }
                }
            };

            // Act
            var sqlQuery = _generator.GenerateComplexSelect(userMap, queryModel);

            // Assert
            // All columns should be prefixed with table alias
            Assert.Contains("\"u1\".\"Id\"", sqlQuery.Sql);
            Assert.Contains("\"u1\".\"first_name\"", sqlQuery.Sql);
            Assert.Contains("\"u1\".\"LastName\"", sqlQuery.Sql);
            
            // JOIN condition should use aliases
            Assert.Contains("\"u1\".\"Id\" = \"u2\".\"Id\"", sqlQuery.Sql);
            
            // Should NOT have unqualified column names
            var sqlLower = sqlQuery.Sql.ToLower();
            // Check that standalone "id" doesn't appear (without table prefix)
            // This is a bit tricky to test, so we verify the structure instead
            Assert.Contains("FROM \"Users\" AS \"u1\"", sqlQuery.Sql);
            Assert.Contains("INNER JOIN \"Users\" AS \"u2\"", sqlQuery.Sql);
        }

        [Fact]
        public void GenerateComplexSelect_WithJoin_AutoGeneratesAlias_WhenNotProvided()
        {
            // Arrange
            var userMap = CreateUserEntityMap();
            
            var queryModel = new QueryModel
            {
                PrimaryEntity = userMap,
                // PrimaryEntityAlias not provided - should auto-generate
                SelectAllColumns = true,
                Joins = new List<JoinClause>
                {
                    new JoinClause
                    {
                        JoinedEntity = userMap,
                        LeftProperty = userMap.KeyProperty,
                        RightProperty = userMap.KeyProperty,
                        JoinType = JoinType.Inner,
                        Alias = "u2"
                    }
                }
            };

            // Act
            var sqlQuery = _generator.GenerateComplexSelect(userMap, queryModel);

            // Assert
            // Should auto-generate alias from table name (lowercase)
            Assert.Contains("FROM \"Users\" AS \"users\"", sqlQuery.Sql);
            Assert.Contains("\"users\".\"Id\"", sqlQuery.Sql);
            Assert.Contains("\"users\".\"first_name\"", sqlQuery.Sql);
        }

        [Fact]
        public void GenerateComplexSelect_WithoutJoin_Should_Not_Prefix_Columns()
        {
            // Arrange
            var userMap = CreateUserEntityMap();
            
            var queryModel = new QueryModel
            {
                PrimaryEntity = userMap,
                SelectAllColumns = true
                // No joins, no alias
            };

            // Act
            var sqlQuery = _generator.GenerateComplexSelect(userMap, queryModel);

            // Assert
            // Without joins, columns should NOT be prefixed
            Assert.Contains("SELECT \"Id\"", sqlQuery.Sql);
            Assert.Contains("\"first_name\"", sqlQuery.Sql);
            Assert.Contains("\"LastName\"", sqlQuery.Sql);
            Assert.DoesNotContain(".", sqlQuery.Sql); // No table.column references
        }

        [Fact]
        public void GenerateComplexSelect_WithJoin_And_WHERE_Should_Use_Aliases()
        {
            // Arrange
            var userMap = CreateUserEntityMap();
            
            var queryModel = new QueryModel
            {
                PrimaryEntity = userMap,
                PrimaryEntityAlias = "u1",
                SelectAllColumns = true,
                Joins = new List<JoinClause>
                {
                    new JoinClause
                    {
                        JoinedEntity = userMap,
                        LeftProperty = userMap.KeyProperty,
                        RightProperty = userMap.KeyProperty,
                        JoinType = JoinType.Left,
                        Alias = "u2"
                    }
                },
                WhereClause = "u1.Id > @minId AND u2.Id < @maxId",
                Parameters = new Dictionary<string, object>
                {
                    { "@minId", 1 },
                    { "@maxId", 100 }
                }
            };

            // Act
            var sqlQuery = _generator.GenerateComplexSelect(userMap, queryModel);

            // Assert
            Assert.Contains("WHERE u1.Id > @minId AND u2.Id < @maxId", sqlQuery.Sql);
            Assert.Contains("\"u1\".\"Id\"", sqlQuery.Sql); // In SELECT
            Assert.Contains("\"u1\".\"Id\" = \"u2\".\"Id\"", sqlQuery.Sql); // In JOIN ON
        }

        [Fact]
        public void GenerateComplexSelect_WithJoin_And_OrderBy_Should_Use_Table_Aliases()
        {
            // Arrange
            var userMap = CreateUserEntityMap();
            var nameProperty = userMap.FindPropertyByName("FirstName")!;
            
            var queryModel = new QueryModel
            {
                PrimaryEntity = userMap,
                PrimaryEntityAlias = "u",
                SelectAllColumns = true,
                Joins = new List<JoinClause>
                {
                    new JoinClause
                    {
                        JoinedEntity = userMap,
                        LeftProperty = userMap.KeyProperty,
                        RightProperty = userMap.KeyProperty,
                        JoinType = JoinType.Inner,
                        Alias = "manager"
                    }
                },
                OrderBy = new List<OrderByClause>
                {
                    new OrderByClause
                    {
                        Property = nameProperty,
                        TableAlias = "u",
                        IsAscending = true
                    }
                }
            };

            // Act
            var sqlQuery = _generator.GenerateComplexSelect(userMap, queryModel);

            // Assert
            Assert.Contains("ORDER BY \"u\".\"first_name\" ASC", sqlQuery.Sql);
        }

        [Fact]
        public void GenerateComplexSelect_MultipleJoins_Should_Handle_All_Aliases()
        {
            // Arrange
            var userMap = CreateUserEntityMap();
            
            var queryModel = new QueryModel
            {
                PrimaryEntity = userMap,
                PrimaryEntityAlias = "employee",
                SelectAllColumns = true,
                Joins = new List<JoinClause>
                {
                    new JoinClause
                    {
                        JoinedEntity = userMap,
                        LeftProperty = userMap.KeyProperty,
                        RightProperty = userMap.KeyProperty,
                        JoinType = JoinType.Left,
                        Alias = "manager"
                    },
                    new JoinClause
                    {
                        JoinedEntity = userMap,
                        LeftProperty = userMap.KeyProperty,
                        RightProperty = userMap.KeyProperty,
                        JoinType = JoinType.Left,
                        Alias = "department_head"
                    }
                }
            };

            // Act
            var sqlQuery = _generator.GenerateComplexSelect(userMap, queryModel);

            // Assert
            // All SELECT columns should use "employee" alias
            Assert.Contains("\"employee\".\"Id\"", sqlQuery.Sql);
            Assert.Contains("\"employee\".\"first_name\"", sqlQuery.Sql);
            
            // JOIN conditions should use proper aliases
            Assert.Contains("\"employee\".\"Id\" = \"manager\".\"Id\"", sqlQuery.Sql);
            Assert.Contains("\"employee\".\"Id\" = \"department_head\".\"Id\"", sqlQuery.Sql);
        }

        [Fact]
        public void GenerateComplexSelect_WithJoin_And_Aggregates_Should_Use_Aliases()
        {
            // Arrange
            var userMap = CreateUserEntityMap();
            var idProperty = userMap.KeyProperty;
            
            var queryModel = new QueryModel
            {
                PrimaryEntity = userMap,
                PrimaryEntityAlias = "u",
                Joins = new List<JoinClause>
                {
                    new JoinClause
                    {
                        JoinedEntity = userMap,
                        LeftProperty = idProperty,
                        RightProperty = idProperty,
                        JoinType = JoinType.Inner,
                        Alias = "related"
                    }
                },
                GroupByColumns = new List<PropertyMap> { idProperty },
                Aggregates = new List<AggregateFunction>
                {
                    new AggregateFunction
                    {
                        FunctionType = AggregateFunctionType.Count,
                        Property = idProperty,
                        Alias = "RelatedCount"
                    }
                }
            };

            // Act
            var sqlQuery = _generator.GenerateComplexSelect(userMap, queryModel);

            // Assert
            // Aggregate function should use table alias
            Assert.Contains("COUNT(\"u\".\"Id\")", sqlQuery.Sql);
            // GROUP BY should use table alias
            Assert.Contains("GROUP BY \"u\".\"Id\"", sqlQuery.Sql);
        }

        [Fact]
        public void GenerateComplexSelect_WithJoin_SpecificColumns_Should_Prefix_All()
        {
            // Arrange
            var userMap = CreateUserEntityMap();
            var idProperty = userMap.KeyProperty;
            var nameProperty = userMap.FindPropertyByName("FirstName")!;
            
            var queryModel = new QueryModel
            {
                PrimaryEntity = userMap,
                PrimaryEntityAlias = "u1",
                SelectAllColumns = false,
                SelectColumns = new List<PropertyMap> { idProperty, nameProperty },
                Joins = new List<JoinClause>
                {
                    new JoinClause
                    {
                        JoinedEntity = userMap,
                        LeftProperty = idProperty,
                        RightProperty = idProperty,
                        JoinType = JoinType.Inner,
                        Alias = "u2"
                    }
                }
            };

            // Act
            var sqlQuery = _generator.GenerateComplexSelect(userMap, queryModel);

            // Assert
            // Specific columns should be prefixed
            Assert.Contains("\"u1\".\"Id\"", sqlQuery.Sql);
            Assert.Contains("\"u1\".\"first_name\"", sqlQuery.Sql);
            
            // Should NOT contain other columns
            Assert.DoesNotContain("\"LastName\"", sqlQuery.Sql);
        }

        [Fact]
        public void GenerateComplexSelect_WithoutJoin_WithAlias_Should_Not_AutoPrefix()
        {
            // Arrange
            var userMap = CreateUserEntityMap();
            
            var queryModel = new QueryModel
            {
                PrimaryEntity = userMap,
                PrimaryEntityAlias = "u",
                SelectAllColumns = true
                // No joins - alias provided but shouldn't auto-prefix
            };

            // Act
            var sqlQuery = _generator.GenerateComplexSelect(userMap, queryModel);

            // Assert
            // With explicit alias but no joins, should use the alias
            Assert.Contains("FROM \"Users\" AS \"u\"", sqlQuery.Sql);
            Assert.Contains("\"u\".\"Id\"", sqlQuery.Sql);
            Assert.Contains("\"u\".\"first_name\"", sqlQuery.Sql);
        }

        [Fact]
        public void GenerateComplexSelect_ComplexJoinScenario_Should_Generate_Valid_SQL()
        {
            // Arrange
            var userMap = CreateUserEntityMap();
            var idProperty = userMap.KeyProperty;
            var nameProperty = userMap.FindPropertyByName("FirstName")!;
            
            // Simulate: SELECT employee.*, manager.FirstName
            // FROM Users AS employee
            // INNER JOIN Users AS manager ON employee.Id = manager.Id
            // WHERE employee.Id > @id
            // ORDER BY employee.FirstName
            var queryModel = new QueryModel
            {
                PrimaryEntity = userMap,
                PrimaryEntityAlias = "employee",
                SelectAllColumns = true,
                Joins = new List<JoinClause>
                {
                    new JoinClause
                    {
                        JoinedEntity = userMap,
                        LeftProperty = idProperty,
                        RightProperty = idProperty,
                        JoinType = JoinType.Inner,
                        Alias = "manager"
                    }
                },
                WhereClause = "employee.Id > @id",
                Parameters = new Dictionary<string, object> { { "@id", 5 } },
                OrderBy = new List<OrderByClause>
                {
                    new OrderByClause
                    {
                        Property = nameProperty,
                        TableAlias = "employee",
                        IsAscending = true
                    }
                }
            };

            // Act
            var sqlQuery = _generator.GenerateComplexSelect(userMap, queryModel);

            // Assert
            var sql = sqlQuery.Sql;
            
            // Verify structure and order
            Assert.Contains("SELECT \"employee\".\"Id\"", sql);
            Assert.Contains("\"employee\".\"first_name\"", sql);
            Assert.Contains("FROM \"Users\" AS \"employee\"", sql);
            Assert.Contains("INNER JOIN \"Users\" AS \"manager\"", sql);
            Assert.Contains("ON \"employee\".\"Id\" = \"manager\".\"Id\"", sql);
            Assert.Contains("WHERE employee.Id > @id", sql);
            Assert.Contains("ORDER BY \"employee\".\"first_name\" ASC", sql);
            
            // Verify no ambiguous column references
            var selectPart = sql.Substring(0, sql.IndexOf("FROM"));
            // All columns in SELECT should have table prefix
            Assert.DoesNotContain("SELECT \"Id\"", selectPart); // Should be "employee"."Id"
        }
    }
}
