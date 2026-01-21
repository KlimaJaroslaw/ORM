using ORM.Tests.TestEntities;
using ORM_v1.Mapping;
using ORM_v1.Query;

namespace ORM.Tests.SqlGeneration
{
    public class SqliteSqlGeneratorJoinTests
    {
        private readonly SqliteSqlGenerator _generator;
        private readonly INamingStrategy _naming;

        public SqliteSqlGeneratorJoinTests()
        {
            _generator = new SqliteSqlGenerator();
            _naming = new PascalCaseNamingStrategy();
        }

        private (EntityMap postMap, EntityMap authorMap) CreatePostAndAuthorMaps()
        {
            INamingStrategy naming = new PascalCaseNamingStrategy();
            IModelBuilder builder = new ReflectionModelBuilder(naming);
            var director = new ModelDirector(builder);
            var maps = director.Construct(typeof(PostWithAuthor).Assembly);
            
            var postMap = maps[typeof(PostWithAuthor)];
            var authorMap = maps[typeof(Author)];
            
            return (postMap, authorMap);
        }

        [Fact]
        public void GenerateComplexSelect_WithInnerJoin_Should_Generate_INNER_JOIN()
        {
            // Arrange
            var (postMap, authorMap) = CreatePostAndAuthorMaps();
            
            // Assume PostWithAuthor has an AuthorId property (you may need to adjust based on actual schema)
            var queryModel = new QueryModel
            {
                PrimaryEntity = postMap,
                PrimaryEntityAlias = "p",
                SelectAllColumns = true,
                Joins = new List<JoinClause>
                {
                    new JoinClause
                    {
                        JoinedEntity = authorMap,
                        LeftProperty = postMap.FindPropertyByName("Id")!, // Adjust as needed
                        RightProperty = authorMap.KeyProperty,
                        JoinType = JoinType.Inner,
                        Alias = "a"
                    }
                }
            };

            // Act
            var sqlQuery = _generator.GenerateComplexSelect(postMap, queryModel);

            // Assert
            Assert.Contains("INNER JOIN", sqlQuery.Sql);
            Assert.Contains("AS \"a\"", sqlQuery.Sql);
            Assert.Contains("ON", sqlQuery.Sql);
        }

        [Fact]
        public void GenerateComplexSelect_WithLeftJoin_Should_Generate_LEFT_JOIN()
        {
            // Arrange
            var (postMap, authorMap) = CreatePostAndAuthorMaps();
            
            var queryModel = new QueryModel
            {
                PrimaryEntity = postMap,
                PrimaryEntityAlias = "p",
                SelectAllColumns = true,
                Joins = new List<JoinClause>
                {
                    new JoinClause
                    {
                        JoinedEntity = authorMap,
                        LeftProperty = postMap.FindPropertyByName("Id")!,
                        RightProperty = authorMap.KeyProperty,
                        JoinType = JoinType.Left,
                        Alias = "a"
                    }
                }
            };

            // Act
            var sqlQuery = _generator.GenerateComplexSelect(postMap, queryModel);

            // Assert
            Assert.Contains("LEFT JOIN", sqlQuery.Sql);
        }

        [Fact]
        public void GenerateComplexSelect_WithRightJoin_Should_Generate_RIGHT_JOIN()
        {
            // Arrange
            var (postMap, authorMap) = CreatePostAndAuthorMaps();
            
            var queryModel = new QueryModel
            {
                PrimaryEntity = postMap,
                SelectAllColumns = true,
                Joins = new List<JoinClause>
                {
                    new JoinClause
                    {
                        JoinedEntity = authorMap,
                        LeftProperty = postMap.FindPropertyByName("Id")!,
                        RightProperty = authorMap.KeyProperty,
                        JoinType = JoinType.Right,
                        Alias = "a"
                    }
                }
            };

            // Act
            var sqlQuery = _generator.GenerateComplexSelect(postMap, queryModel);

            // Assert
            Assert.Contains("RIGHT JOIN", sqlQuery.Sql);
        }

        [Fact]
        public void GenerateComplexSelect_WithFullJoin_Should_Generate_FULL_OUTER_JOIN()
        {
            // Arrange
            var (postMap, authorMap) = CreatePostAndAuthorMaps();
            
            var queryModel = new QueryModel
            {
                PrimaryEntity = postMap,
                SelectAllColumns = true,
                Joins = new List<JoinClause>
                {
                    new JoinClause
                    {
                        JoinedEntity = authorMap,
                        LeftProperty = postMap.FindPropertyByName("Id")!,
                        RightProperty = authorMap.KeyProperty,
                        JoinType = JoinType.Full,
                        Alias = "a"
                    }
                }
            };

            // Act
            var sqlQuery = _generator.GenerateComplexSelect(postMap, queryModel);

            // Assert
            Assert.Contains("FULL OUTER JOIN", sqlQuery.Sql);
        }

        [Fact]
        public void GenerateComplexSelect_WithMultipleJoins_Should_Generate_All_Joins()
        {
            // Arrange
            INamingStrategy naming = new PascalCaseNamingStrategy();
            IModelBuilder builder = new ReflectionModelBuilder(naming);
            var director = new ModelDirector(builder);
            var maps = director.Construct(typeof(UserTestEntity).Assembly);
            var userMap = maps[typeof(UserTestEntity)];
            
            // Create mock entity maps for testing multiple joins
            var queryModel = new QueryModel
            {
                PrimaryEntity = userMap,
                PrimaryEntityAlias = "u",
                SelectAllColumns = true,
                Joins = new List<JoinClause>
                {
                    new JoinClause
                    {
                        JoinedEntity = userMap, // Self-join for demonstration
                        LeftProperty = userMap.KeyProperty,
                        RightProperty = userMap.KeyProperty,
                        JoinType = JoinType.Inner,
                        Alias = "u2"
                    },
                    new JoinClause
                    {
                        JoinedEntity = userMap,
                        LeftProperty = userMap.KeyProperty,
                        RightProperty = userMap.KeyProperty,
                        JoinType = JoinType.Left,
                        Alias = "u3"
                    }
                }
            };

            // Act
            var sqlQuery = _generator.GenerateComplexSelect(userMap, queryModel);

            // Assert
            var joinCount = CountOccurrences(sqlQuery.Sql, "JOIN");
            Assert.Equal(2, joinCount);
            Assert.Contains("AS \"u2\"", sqlQuery.Sql);
            Assert.Contains("AS \"u3\"", sqlQuery.Sql);
        }

        [Fact]
        public void GenerateComplexSelect_JoinWithWhereClause_Should_Combine_Properly()
        {
            // Arrange
            var (postMap, authorMap) = CreatePostAndAuthorMaps();
            
            var queryModel = new QueryModel
            {
                PrimaryEntity = postMap,
                PrimaryEntityAlias = "p",
                SelectAllColumns = true,
                Joins = new List<JoinClause>
                {
                    new JoinClause
                    {
                        JoinedEntity = authorMap,
                        LeftProperty = postMap.FindPropertyByName("Id")!,
                        RightProperty = authorMap.KeyProperty,
                        JoinType = JoinType.Inner,
                        Alias = "a"
                    }
                },
                WhereClause = "p.Title LIKE @title",
                Parameters = new Dictionary<string, object>
                {
                    { "@title", "%test%" }
                }
            };

            // Act
            var sqlQuery = _generator.GenerateComplexSelect(postMap, queryModel);

            // Assert
            var sql = sqlQuery.Sql;
            var joinIndex = sql.IndexOf("INNER JOIN");
            var whereIndex = sql.IndexOf("WHERE");
            
            Assert.True(joinIndex < whereIndex, "JOIN should come before WHERE");
            Assert.Contains("WHERE p.Title LIKE @title", sqlQuery.Sql);
        }

        [Fact]
        public void GenerateComplexSelect_JoinWithOrderBy_Should_Support_Aliased_Columns()
        {
            // Arrange
            var (postMap, authorMap) = CreatePostAndAuthorMaps();
            var titleProperty = postMap.FindPropertyByName("Title")!;
            
            var queryModel = new QueryModel
            {
                PrimaryEntity = postMap,
                PrimaryEntityAlias = "p",
                SelectAllColumns = true,
                Joins = new List<JoinClause>
                {
                    new JoinClause
                    {
                        JoinedEntity = authorMap,
                        LeftProperty = postMap.FindPropertyByName("Id")!,
                        RightProperty = authorMap.KeyProperty,
                        JoinType = JoinType.Inner,
                        Alias = "a"
                    }
                },
                OrderBy = new List<OrderByClause>
                {
                    new OrderByClause
                    {
                        Property = titleProperty,
                        TableAlias = "p",
                        IsAscending = true
                    }
                }
            };

            // Act
            var sqlQuery = _generator.GenerateComplexSelect(postMap, queryModel);

            // Assert
            Assert.Contains("ORDER BY", sqlQuery.Sql);
            Assert.Contains("\"p\".", sqlQuery.Sql);
        }

        private int CountOccurrences(string text, string pattern)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(pattern, index)) != -1)
            {
                count++;
                index += pattern.Length;
            }
            return count;
        }
    }
}
