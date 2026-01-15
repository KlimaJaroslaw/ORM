using System;
using System.Collections.Generic;
using Xunit;
using ORM_v1.Mapping;
using ORM_v1.Attributes;
using ORM.Tests.Models;

namespace ORM.Tests
{
    public class InheritanceValidationTests
    {
        [Fact]
        public void Should_Throw_When_TPC_Strategy_Has_Discriminator_Column()
        {
            
            Assert.Throws<InvalidOperationException>(() =>
            {
                new EntityMap(
                    entityType: typeof(CreditCardPayment),
                    tableName: "CCPayments",
                    isAbstract: false,
                    baseMap: null,
                    strategy: InheritanceStrategy.TablePerConcreteClass,
                    discriminator: "CC",
                    discriminatorColumn: "Disc",
                    keyProperty: null!,
                    allProperties: new List<PropertyMap>()
                );
            });
        }
    }
}