using ORM_v1.Mapping;
using ORM.Tests.TestEntities;
using Xunit;

namespace ORM.Tests.Scenarios
{
    public class NavigationScenarioTests
    {
        [Fact]
        public void Should_Identify_Navigation_Properties_And_Not_Assign_ColumnName()
        {
            var builder = new ModelBuilder(typeof(PostWithAuthor).Assembly);
            var naming = new PascalCaseNamingStrategy();

            var maps = builder.BuildModel(naming);
            var map = maps[typeof(PostWithAuthor)];

            var navProp = map.NavigationProperties.First();

            Assert.Equal("Author", navProp.PropertyInfo.Name);
            Assert.True(navProp.IsNavigation);
            Assert.Null(navProp.ColumnName);
        }
    }
}
