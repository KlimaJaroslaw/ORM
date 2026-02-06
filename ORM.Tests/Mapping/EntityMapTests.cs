using ORM_v1.Mapping;
using ORM_v1.Mapping.Strategies;
using ORM.Tests.TestEntities;
using Xunit;
using System.Linq;
using System.Collections.Generic;

namespace ORM.Tests.Mapping
{
    public class EntityMapTests
    {
        [Fact]
        public void EntityMap_Should_Have_Correct_Table_Name()
        {
            var naming = new PascalCaseNamingStrategy();

            var props = typeof(UserTestEntity)
                .GetProperties()
                .Select(p => PropertyMap.FromPropertyInfo(p, naming, t => false))
                .Where(p => !p.IsIgnored);

            var key = props.First(p => p.IsKey);

            var map = new EntityMap(
                entityType: typeof(UserTestEntity), 
                tableName: "Users", 
                isAbstract: false,
                baseMap: null,
                strategy: new TablePerConcreteClassStrategy(),
                keyProperty: key, 
                allProperties: props);

            Assert.Equal("Users", map.TableName);
        }

        [Fact]
        public void EntityMap_Should_Contain_ScalarProperties()
        {
            var naming = new PascalCaseNamingStrategy();

            var props = typeof(UserTestEntity)
                .GetProperties()
                .Select(p => PropertyMap.FromPropertyInfo(p, naming, t => false))
                .Where(p => !p.IsIgnored);

            var key = props.First(p => p.IsKey);

            var map = new EntityMap(
                entityType: typeof(UserTestEntity), 
                tableName: "Users", 
                isAbstract: false,
                baseMap: null,
                strategy: new TablePerConcreteClassStrategy(),
                keyProperty: key, 
                allProperties: props);

            Assert.Contains(map.ScalarProperties, p => p.ColumnName == "FirstName" || p.ColumnName == "first_name");
        }
    }
}