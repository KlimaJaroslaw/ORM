using ORM.Tests.TestEntities;
using ORM_v1.Mapping;
using ORM_v1.Query;
using Xunit;
using System.Collections.Generic;

namespace ORM.Tests.Linq
{
    public class QueryModelPipelineTests
    {
        [Fact]
        public void QueryModel_Should_Generate_SQL_From_LinqLike_Scenario()
        {
            var builder = new ModelBuilder(typeof(UserTestEntity).Assembly);
            var maps = builder.BuildModel(new PascalCaseNamingStrategy());
            var map = maps[typeof(UserTestEntity)];

            var query = new QueryModel
            {
                PrimaryEntity = map,
                PrimaryEntityAlias = "u",
                SelectAllColumns = true,
                WhereClause = "u.first_name = @name",
                Parameters = new Dictionary<string, object>
                {
                    { "@name", "John" }
                },
                OrderBy = new List<OrderByClause>
                {
                    new OrderByClause
                    {
                        Property = map.FindPropertyByName("FirstName")!,
                        TableAlias = "u",
                        IsAscending = true
                    }
                },
                Take = 5
            };

            var generator = new SqliteSqlGenerator();
            var sql = generator.GenerateComplexSelect(map, query);

            Assert.Contains("WHERE u.first_name = @name", sql.Sql);
            Assert.Contains("ORDER BY \"u\".\"first_name\"", sql.Sql);
            Assert.Contains("LIMIT 5", sql.Sql);
        }
    }
}