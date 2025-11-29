using ORM_v1.Mapping;
using Xunit;

namespace ORM.Tests.Mapping
{
    public class NamingStrategyTests
    {
        [Fact]
        public void PascalCaseStrategy_Should_Not_Change_Name()
        {
            var strategy = new PascalCaseNamingStrategy();
            Assert.Equal("FirstName", strategy.ConvertName("FirstName"));
        }

        [Fact]
        public void SnakeCaseStrategy_Should_Convert_To_Snake_Case()
        {
            var strategy = new SnakeCaseNamingStrategy();
            Assert.Equal("first_name", strategy.ConvertName("FirstName"));
        }
    }
}
