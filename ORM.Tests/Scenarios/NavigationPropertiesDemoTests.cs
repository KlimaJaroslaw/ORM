using ORM_v1.Mapping;
using TestApp.Scenarios;
using Xunit;

namespace ORM.Tests.Scenarios
{
    /// <summary>
    /// Testy jednostkowe sprawdzające poprawność detekcji navigation properties
    /// dla różnych wzorców relacji przedstawionych w NavigationPropertiesDemo.
    /// </summary>
    public class NavigationPropertiesDemoTests
    {
        private readonly IReadOnlyDictionary<Type, EntityMap> _metadata;

        public NavigationPropertiesDemoTests()
        {
            var naming = new PascalCaseNamingStrategy();
            var builder = new ReflectionModelBuilder(naming);
            var director = new ModelDirector(builder);

            // Buduj metadata dla assembly zawierającego modele demo
            _metadata = director.Construct(typeof(Blog).Assembly);
        }

        [Fact]
        public void Blog_Should_Have_Posts_Collection_Navigation_Property()
        {
            // Arrange
            var blogMap = _metadata[typeof(Blog)];

            // Act
            var postsNav = blogMap.NavigationProperties
                .FirstOrDefault(p => p.PropertyInfo.Name == "Posts");

            // Assert
            Assert.NotNull(postsNav);
            Assert.True(postsNav.IsNavigation);
            Assert.True(postsNav.IsCollection);
            Assert.Equal(typeof(Post), postsNav.TargetType);
            Assert.Null(postsNav.ColumnName); // Navigation properties nie mają ColumnName
        }

        [Fact]
        public void Post_Should_Have_Blog_Navigation_Property_With_ForeignKey()
        {
            // Arrange
            var postMap = _metadata[typeof(Post)];

            // Act
            var blogNav = postMap.NavigationProperties
                .FirstOrDefault(p => p.PropertyInfo.Name == "Blog");

            // Assert
            Assert.NotNull(blogNav);
            Assert.True(blogNav.IsNavigation);
            Assert.False(blogNav.IsCollection);
            Assert.Equal(typeof(Blog), blogNav.TargetType);
            Assert.Equal("BlogId", blogNav.ForeignKeyName);
            Assert.Null(blogNav.ColumnName);
        }

        [Fact]
        public void Post_Should_Have_BlogId_As_Scalar_Property()
        {
            // Arrange
            var postMap = _metadata[typeof(Post)];

            // Act
            var blogIdProp = postMap.ScalarProperties
                .FirstOrDefault(p => p.PropertyInfo.Name == "BlogId");

            // Assert
            Assert.NotNull(blogIdProp);
            Assert.False(blogIdProp.IsNavigation);
            Assert.NotNull(blogIdProp.ColumnName);
        }

        [Fact]
        public void Comment_Should_Have_Multiple_Navigation_Properties()
        {
            // Arrange
            var commentMap = _metadata[typeof(Comment)];

            // Act
            var navProps = commentMap.NavigationProperties.ToList();

            // Assert
            Assert.Equal(2, navProps.Count); // Author i Post

            var authorNav = navProps.FirstOrDefault(p => p.PropertyInfo.Name == "Author");
            Assert.NotNull(authorNav);
            Assert.Equal("AuthorId", authorNav.ForeignKeyName);
            Assert.Equal(typeof(BlogUser), authorNav.TargetType);

            var postNav = navProps.FirstOrDefault(p => p.PropertyInfo.Name == "Post");
            Assert.NotNull(postNav);
            Assert.Equal("PostId", postNav.ForeignKeyName);
            Assert.Equal(typeof(Post), postNav.TargetType);
        }

        [Fact]
        public void Employee_Should_Have_Self_Referencing_Navigation_Properties()
        {
            // Arrange
            var employeeMap = _metadata[typeof(Employee)];

            // Act
            var managerNav = employeeMap.NavigationProperties
                .FirstOrDefault(p => p.PropertyInfo.Name == "Manager");
            var subordinatesNav = employeeMap.NavigationProperties
                .FirstOrDefault(p => p.PropertyInfo.Name == "Subordinates");

            // Assert - Manager (Many-to-One)
            Assert.NotNull(managerNav);
            Assert.True(managerNav.IsNavigation);
            Assert.False(managerNav.IsCollection);
            Assert.Equal(typeof(Employee), managerNav.TargetType);
            Assert.Equal("ManagerId", managerNav.ForeignKeyName);

            // Assert - Subordinates (One-to-Many)
            Assert.NotNull(subordinatesNav);
            Assert.True(subordinatesNav.IsNavigation);
            Assert.True(subordinatesNav.IsCollection);
            Assert.Equal(typeof(Employee), subordinatesNav.TargetType);
        }

        [Fact]
        public void DemoOrder_Should_Have_Optional_Navigation_Property()
        {
            // Arrange
            var orderMap = _metadata[typeof(DemoOrder)];

            // Act
            var addressNav = orderMap.NavigationProperties
                .FirstOrDefault(p => p.PropertyInfo.Name == "ShippingAddress");
            var addressIdProp = orderMap.ScalarProperties
                .FirstOrDefault(p => p.PropertyInfo.Name == "ShippingAddressId");

            // Assert - Navigation property
            Assert.NotNull(addressNav);
            Assert.True(addressNav.IsNavigation);
            Assert.False(addressNav.IsCollection);
            Assert.Equal(typeof(Address), addressNav.TargetType);
            Assert.Equal("ShippingAddressId", addressNav.ForeignKeyName);

            // Assert - FK is nullable
            Assert.NotNull(addressIdProp);
            var isNullable = Nullable.GetUnderlyingType(addressIdProp.PropertyType) != null;
            Assert.True(isNullable);
        }

        [Fact]
        public void BlogUser_Should_Have_Comments_Collection()
        {
            // Arrange
            var blogUserMap = _metadata[typeof(BlogUser)];

            // Act
            var commentsNav = blogUserMap.NavigationProperties
                .FirstOrDefault(p => p.PropertyInfo.Name == "Comments");

            // Assert
            Assert.NotNull(commentsNav);
            Assert.True(commentsNav.IsNavigation);
            Assert.True(commentsNav.IsCollection);
            Assert.Equal(typeof(Comment), commentsNav.TargetType);
            Assert.Null(commentsNav.ForeignKeyName); // Kolekcje nie mają FK na tej stronie relacji
        }

        [Fact]
        public void All_Navigation_Properties_Should_Have_Null_ColumnName()
        {
            // Act & Assert
            foreach (var entityMap in _metadata.Values)
            {
                foreach (var navProp in entityMap.NavigationProperties)
                {
                    Assert.Null(navProp.ColumnName);
                }
            }
        }

        [Fact]
        public void Navigation_Properties_Should_Not_Appear_In_ScalarProperties()
        {
            // Act & Assert
            foreach (var entityMap in _metadata.Values)
            {
                var navPropNames = entityMap.NavigationProperties
                    .Select(p => p.PropertyInfo.Name)
                    .ToHashSet();

                var scalarPropNames = entityMap.ScalarProperties
                    .Select(p => p.PropertyInfo.Name)
                    .ToHashSet();

                // Navigation properties i scalar properties nie powinny się pokrywać
                Assert.Empty(navPropNames.Intersect(scalarPropNames));
            }
        }

        [Fact]
        public void ForeignKey_Properties_Should_Be_In_ScalarProperties()
        {
            // Arrange
            var postMap = _metadata[typeof(Post)];

            // Act
            var blogNav = postMap.NavigationProperties
                .First(p => p.PropertyInfo.Name == "Blog");
            var fkName = blogNav.ForeignKeyName;

            var fkProp = postMap.ScalarProperties
                .FirstOrDefault(p => p.PropertyInfo.Name == fkName);

            // Assert
            Assert.NotNull(fkProp);
            Assert.Equal("BlogId", fkProp.PropertyInfo.Name);
            Assert.False(fkProp.IsNavigation);
        }

        [Fact]
        public void Post_Should_Have_Both_Blog_And_Comments_Navigation()
        {
            // Arrange
            var postMap = _metadata[typeof(Post)];

            // Act
            var blogNav = postMap.NavigationProperties
                .FirstOrDefault(p => p.PropertyInfo.Name == "Blog");
            var commentsNav = postMap.NavigationProperties
                .FirstOrDefault(p => p.PropertyInfo.Name == "Comments");

            // Assert
            Assert.NotNull(blogNav);
            Assert.False(blogNav.IsCollection); // Many-to-One

            Assert.NotNull(commentsNav);
            Assert.True(commentsNav.IsCollection); // One-to-Many
        }
    }
}
