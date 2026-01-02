using ORM_v1.Query;

namespace ORM.Tests.SqlGeneration
{
    public class SqlQueryBuilderTests
    {
        [Fact]
        public void Select_Should_Build_Simple_SELECT()
        {
            // Arrange
            var builder = new SqlQueryBuilder();
            var columns = new[] { "Id", "Name", "Age" };

            // Act
            var sql = builder.Select(columns).ToString();

            // Assert
            Assert.Equal("SELECT Id, Name, Age", sql);
        }

        [Fact]
        public void SelectDistinct_Should_Build_SELECT_DISTINCT()
        {
            // Arrange
            var builder = new SqlQueryBuilder();
            var columns = new[] { "Category" };

            // Act
            var sql = builder.SelectDistinct(columns).ToString();

            // Assert
            Assert.Equal("SELECT DISTINCT Category", sql);
        }

        [Fact]
        public void From_Should_Build_FROM_Clause()
        {
            // Arrange
            var builder = new SqlQueryBuilder();

            // Act
            var sql = builder.Select(new[] { "Id" }).From("Users").ToString();

            // Assert
            Assert.Equal("SELECT Id FROM Users", sql);
        }

        [Fact]
        public void FromWithAlias_Should_Build_FROM_With_Alias()
        {
            // Arrange
            var builder = new SqlQueryBuilder();

            // Act
            var sql = builder.Select(new[] { "u.Id" })
                            .FromWithAlias("Users", "u")
                            .ToString();

            // Assert
            Assert.Contains("FROM Users AS u", sql);
        }

        [Fact]
        public void FromWithAlias_WithNullAlias_Should_Build_FROM_Without_Alias()
        {
            // Arrange
            var builder = new SqlQueryBuilder();

            // Act
            var sql = builder.Select(new[] { "Id" })
                            .FromWithAlias("Users", null)
                            .ToString();

            // Assert
            Assert.Equal("SELECT Id FROM Users", sql);
            Assert.DoesNotContain("AS", sql);
        }

        [Fact]
        public void Where_Should_Build_WHERE_Clause()
        {
            // Arrange
            var builder = new SqlQueryBuilder();

            // Act
            var sql = builder.Select(new[] { "Id" })
                            .From("Users")
                            .Where("Age > 18")
                            .ToString();

            // Assert
            Assert.Contains("WHERE Age > 18", sql);
        }

        [Fact]
        public void InnerJoin_Should_Build_INNER_JOIN()
        {
            // Arrange
            var builder = new SqlQueryBuilder();

            // Act
            var sql = builder.Select(new[] { "p.Title", "u.Name" })
                            .FromWithAlias("Posts", "p")
                            .InnerJoin("Users", "u", "p.AuthorId = u.Id")
                            .ToString();

            // Assert
            Assert.Contains("INNER JOIN Users AS u ON p.AuthorId = u.Id", sql);
        }

        [Fact]
        public void LeftJoin_Should_Build_LEFT_JOIN()
        {
            // Arrange
            var builder = new SqlQueryBuilder();

            // Act
            var sql = builder.Select(new[] { "*" })
                            .From("Posts")
                            .LeftJoin("Users", "u", "Posts.AuthorId = u.Id")
                            .ToString();

            // Assert
            Assert.Contains("LEFT JOIN Users AS u ON Posts.AuthorId = u.Id", sql);
        }

        [Fact]
        public void RightJoin_Should_Build_RIGHT_JOIN()
        {
            // Arrange
            var builder = new SqlQueryBuilder();

            // Act
            var sql = builder.Select(new[] { "*" })
                            .From("Posts")
                            .RightJoin("Users", "u", "Posts.AuthorId = u.Id")
                            .ToString();

            // Assert
            Assert.Contains("RIGHT JOIN Users AS u ON Posts.AuthorId = u.Id", sql);
        }

        [Fact]
        public void FullOuterJoin_Should_Build_FULL_OUTER_JOIN()
        {
            // Arrange
            var builder = new SqlQueryBuilder();

            // Act
            var sql = builder.Select(new[] { "*" })
                            .From("Posts")
                            .FullOuterJoin("Users", "u", "Posts.AuthorId = u.Id")
                            .ToString();

            // Assert
            Assert.Contains("FULL OUTER JOIN Users AS u ON Posts.AuthorId = u.Id", sql);
        }

        [Fact]
        public void GroupBy_Should_Build_GROUP_BY()
        {
            // Arrange
            var builder = new SqlQueryBuilder();

            // Act
            var sql = builder.Select(new[] { "Category", "COUNT(*)" })
                            .From("Products")
                            .GroupBy(new[] { "Category" })
                            .ToString();

            // Assert
            Assert.Contains("GROUP BY Category", sql);
        }

        [Fact]
        public void GroupBy_WithMultipleColumns_Should_Build_Correctly()
        {
            // Arrange
            var builder = new SqlQueryBuilder();

            // Act
            var sql = builder.Select(new[] { "Country", "City", "COUNT(*)" })
                            .From("Users")
                            .GroupBy(new[] { "Country", "City" })
                            .ToString();

            // Assert
            Assert.Contains("GROUP BY Country, City", sql);
        }

        [Fact]
        public void GroupBy_WithEmptyList_Should_Not_Add_GROUP_BY()
        {
            // Arrange
            var builder = new SqlQueryBuilder();

            // Act
            var sql = builder.Select(new[] { "*" })
                            .From("Users")
                            .GroupBy(new string[] { })
                            .ToString();

            // Assert
            Assert.DoesNotContain("GROUP BY", sql);
        }

        [Fact]
        public void Having_Should_Build_HAVING_Clause()
        {
            // Arrange
            var builder = new SqlQueryBuilder();

            // Act
            var sql = builder.Select(new[] { "Category", "COUNT(*)" })
                            .From("Products")
                            .GroupBy(new[] { "Category" })
                            .Having("COUNT(*) > 5")
                            .ToString();

            // Assert
            Assert.Contains("HAVING COUNT(*) > 5", sql);
        }

        [Fact]
        public void OrderBy_Should_Build_ORDER_BY()
        {
            // Arrange
            var builder = new SqlQueryBuilder();

            // Act
            var sql = builder.Select(new[] { "Id", "Name" })
                            .From("Users")
                            .OrderBy(new[] { "Name ASC" })
                            .ToString();

            // Assert
            Assert.Contains("ORDER BY Name ASC", sql);
        }

        [Fact]
        public void OrderBy_WithMultipleColumns_Should_Build_Correctly()
        {
            // Arrange
            var builder = new SqlQueryBuilder();

            // Act
            var sql = builder.Select(new[] { "Id", "Name", "Age" })
                            .From("Users")
                            .OrderBy(new[] { "Age DESC", "Name ASC" })
                            .ToString();

            // Assert
            Assert.Contains("ORDER BY Age DESC, Name ASC", sql);
        }

        [Fact]
        public void OrderBy_WithEmptyList_Should_Not_Add_ORDER_BY()
        {
            // Arrange
            var builder = new SqlQueryBuilder();

            // Act
            var sql = builder.Select(new[] { "*" })
                            .From("Users")
                            .OrderBy(new string[] { })
                            .ToString();

            // Assert
            Assert.DoesNotContain("ORDER BY", sql);
        }

        [Fact]
        public void Limit_Should_Build_LIMIT_Clause()
        {
            // Arrange
            var builder = new SqlQueryBuilder();

            // Act
            var sql = builder.Select(new[] { "Id" })
                            .From("Users")
                            .Limit(10)
                            .ToString();

            // Assert
            Assert.Contains("LIMIT 10", sql);
        }

        [Fact]
        public void Offset_Should_Build_OFFSET_Clause()
        {
            // Arrange
            var builder = new SqlQueryBuilder();

            // Act
            var sql = builder.Select(new[] { "Id" })
                            .From("Users")
                            .Offset(20)
                            .ToString();

            // Assert
            Assert.Contains("OFFSET 20", sql);
        }

        [Fact]
        public void Limit_And_Offset_Should_Build_Pagination()
        {
            // Arrange
            var builder = new SqlQueryBuilder();

            // Act
            var sql = builder.Select(new[] { "Id" })
                            .From("Users")
                            .Limit(10)
                            .Offset(20)
                            .ToString();

            // Assert
            Assert.Contains("LIMIT 10", sql);
            Assert.Contains("OFFSET 20", sql);
        }

        [Fact]
        public void InsertInto_Should_Build_INSERT_Statement()
        {
            // Arrange
            var builder = new SqlQueryBuilder();

            // Act
            var sql = builder.InsertInto("Users", new[] { "Name", "Age" })
                            .Values(new[] { "@Name", "@Age" })
                            .ToString();

            // Assert
            Assert.Equal("INSERT INTO Users (Name, Age) VALUES (@Name, @Age)", sql);
        }

        [Fact]
        public void Update_Should_Build_UPDATE_Statement()
        {
            // Arrange
            var builder = new SqlQueryBuilder();

            // Act
            var sql = builder.Update("Users")
                            .Set(new[] { "Name = @Name", "Age = @Age" })
                            .Where("Id = @Id")
                            .ToString();

            // Assert
            Assert.Contains("UPDATE Users SET Name = @Name, Age = @Age", sql);
            Assert.Contains("WHERE Id = @Id", sql);
        }

        [Fact]
        public void DeleteFrom_Should_Build_DELETE_Statement()
        {
            // Arrange
            var builder = new SqlQueryBuilder();

            // Act
            var sql = builder.DeleteFrom("Users")
                            .Where("Id = @Id")
                            .ToString();

            // Assert
            Assert.Equal("DELETE FROM Users WHERE Id = @Id", sql);
        }

        [Fact]
        public void ComplexQuery_Should_Build_All_Clauses_In_Correct_Order()
        {
            // Arrange
            var builder = new SqlQueryBuilder();

            // Act
            var sql = builder.Select(new[] { "u.Id", "u.Name", "COUNT(p.Id) AS PostCount" })
                            .FromWithAlias("Users", "u")
                            .LeftJoin("Posts", "p", "u.Id = p.AuthorId")
                            .Where("u.Age > 18")
                            .GroupBy(new[] { "u.Id", "u.Name" })
                            .Having("COUNT(p.Id) > 5")
                            .OrderBy(new[] { "PostCount DESC" })
                            .Limit(10)
                            .Offset(5)
                            .ToString();

            // Assert
            Assert.Contains("SELECT u.Id, u.Name, COUNT(p.Id) AS PostCount", sql);
            Assert.Contains("FROM Users AS u", sql);
            Assert.Contains("LEFT JOIN Posts AS p ON u.Id = p.AuthorId", sql);
            Assert.Contains("WHERE u.Age > 18", sql);
            Assert.Contains("GROUP BY u.Id, u.Name", sql);
            Assert.Contains("HAVING COUNT(p.Id) > 5", sql);
            Assert.Contains("ORDER BY PostCount DESC", sql);
            Assert.Contains("LIMIT 10", sql);
            Assert.Contains("OFFSET 5", sql);

            // Verify order
            var selectIdx = sql.IndexOf("SELECT");
            var fromIdx = sql.IndexOf("FROM");
            var joinIdx = sql.IndexOf("LEFT JOIN");
            var whereIdx = sql.IndexOf("WHERE");
            var groupIdx = sql.IndexOf("GROUP BY");
            var havingIdx = sql.IndexOf("HAVING");
            var orderIdx = sql.IndexOf("ORDER BY");
            var limitIdx = sql.IndexOf("LIMIT");
            var offsetIdx = sql.IndexOf("OFFSET");

            Assert.True(selectIdx < fromIdx);
            Assert.True(fromIdx < joinIdx);
            Assert.True(joinIdx < whereIdx);
            Assert.True(whereIdx < groupIdx);
            Assert.True(groupIdx < havingIdx);
            Assert.True(havingIdx < orderIdx);
            Assert.True(orderIdx < limitIdx);
            Assert.True(limitIdx < offsetIdx);
        }

        [Fact]
        public void FluentAPI_Should_Be_Chainable()
        {
            // Arrange & Act
            var sql = new SqlQueryBuilder()
                .Select(new[] { "Id", "Name" })
                .From("Users")
                .Where("Age > 18")
                .OrderBy(new[] { "Name ASC" })
                .ToString();

            // Assert
            Assert.NotEmpty(sql);
            Assert.Contains("SELECT", sql);
            Assert.Contains("FROM", sql);
            Assert.Contains("WHERE", sql);
            Assert.Contains("ORDER BY", sql);
        }

        [Fact]
        public void Multiple_Joins_Should_Build_Correctly()
        {
            // Arrange
            var builder = new SqlQueryBuilder();

            // Act
            var sql = builder.Select(new[] { "o.Id", "u.Name", "p.Title" })
                            .FromWithAlias("Orders", "o")
                            .InnerJoin("Users", "u", "o.UserId = u.Id")
                            .LeftJoin("Products", "p", "o.ProductId = p.Id")
                            .ToString();

            // Assert
            Assert.Contains("INNER JOIN Users AS u ON o.UserId = u.Id", sql);
            Assert.Contains("LEFT JOIN Products AS p ON o.ProductId = p.Id", sql);
        }
    }
}
