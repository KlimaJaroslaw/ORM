using Xunit;
using ORM_v1.Mapping;
using ORM.Tests.Models;
using System.Linq;
using System.Collections.Generic;

namespace ORM.Tests
{
    public class RelationMappingTests
    {
        private readonly IModelBuilder _builder;
        private readonly ModelDirector _director;
        private readonly INamingStrategy _naming;

        public RelationMappingTests()
        {
            _naming = new PascalCaseNamingStrategy();
            _builder = new ReflectionModelBuilder(_naming);
            _director = new ModelDirector(_builder);
        }

        [Fact]
        public void Should_Detect_OneToMany_Collection()
        {
            var model = _director.Construct(this.GetType().Assembly);
            var userMap = model[typeof(User)];
            
            var ordersProp = userMap.NavigationProperties.First(p => p.PropertyInfo.Name == "Orders");

            Assert.True(ordersProp.IsNavigation);
            Assert.True(ordersProp.IsCollection);
            Assert.Equal(typeof(Order), ordersProp.TargetType);
            Assert.Null(ordersProp.ColumnName);
        }

        [Fact]
        public void Should_Detect_ManyToOne_With_ForeignKey()
        {
            var model = _director.Construct(this.GetType().Assembly);
            var orderMap = model[typeof(Order)];

            var userProp = orderMap.NavigationProperties.First(p => p.PropertyInfo.Name == "User");

            Assert.True(userProp.IsNavigation);
            Assert.False(userProp.IsCollection);
            Assert.Equal(typeof(User), userProp.TargetType);

            Assert.Equal("UserId", userProp.ForeignKeyName);
        }
    }

}