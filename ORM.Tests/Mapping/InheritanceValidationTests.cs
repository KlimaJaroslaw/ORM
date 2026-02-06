using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using ORM_v1.Mapping;
using ORM_v1.Attributes;
using ORM_v1.Mapping.Strategies;
using ORM.Tests.Models;

namespace ORM.Tests
{
    public class InheritanceValidationTests
    {
        [Fact]
        public void Should_Set_Discriminator_To_Null_When_TPC_Strategy_Is_Used()
        {
            // Arrange
            // Musimy stworzyć atrapę klucza, bo EntityMap nie pozwala na null w konstruktorze
            var dummyPropInfo = typeof(CreditCardPayment).GetProperty("Id") 
                                ?? typeof(CreditCardPayment).GetProperties()[0];
            
            var dummyKey = new PropertyMap(
                dummyPropInfo, 
                "Id", 
                isKey: true, 
                isIgnored: false, 
                isNavigation: false, 
                isCollection: false, 
                targetType: null, 
                foreignKeyName: null
            );

            // Act
            var map = new EntityMap(
                entityType: typeof(CreditCardPayment),
                tableName: "CCPayments",
                isAbstract: false,
                baseMap: null,
                strategy: new TablePerConcreteClassStrategy(), // TPC
                keyProperty: dummyKey, // Przekazujemy atrapę zamiast null!
                allProperties: new List<PropertyMap>()
            );

            // Assert
            Assert.Null(map.Discriminator);
            Assert.Null(map.DiscriminatorColumn);
            Assert.IsType<TablePerConcreteClassStrategy>(map.InheritanceStrategy);
        }
    }
}