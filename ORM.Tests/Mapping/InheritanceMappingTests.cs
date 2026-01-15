using Xunit;
using ORM_v1.Mapping;
using ORM.Tests.Models;
using System.Linq;

namespace ORM.Tests
{
    public class InheritanceMappingTests
    {
        private readonly ModelBuilder _modelBuilder;
        private readonly INamingStrategy _naming;

        public InheritanceMappingTests()
        {
            _modelBuilder = new ModelBuilder(this.GetType().Assembly);
            _naming = new PascalCaseNamingStrategy();
        }

        [Fact]
        public void Should_Map_TPH_To_Single_Table_With_Discriminator()
        {
            var model = _modelBuilder.BuildModel(_naming);
            var dogMap = model[typeof(Dog)];
            var animalMap = model[typeof(Animal)];

            Assert.Equal("Animals", animalMap.TableName);
            Assert.Equal("Animals", dogMap.TableName);

            Assert.Equal(InheritanceStrategy.TablePerHierarchy, dogMap.Strategy);

            Assert.Equal("Discriminator", dogMap.DiscriminatorColumn);
            Assert.Equal(nameof(Dog), dogMap.Discriminator);

            Assert.Same(animalMap, dogMap.BaseMap);
        }

        [Fact]
        public void Should_Map_TPC_To_Separate_Tables_Without_Discriminator()
        {
            var model = _modelBuilder.BuildModel(_naming);
            var cardMap = model[typeof(CreditCardPayment)];

            Assert.Equal("CreditCardPayments", cardMap.TableName);

            Assert.Equal(InheritanceStrategy.TablePerConcreteClass, cardMap.Strategy);

            Assert.Null(cardMap.DiscriminatorColumn);
            Assert.Null(cardMap.Discriminator);

            Assert.Contains(cardMap.ScalarProperties, p => p.PropertyInfo.Name == "Amount");
            Assert.Contains(cardMap.ScalarProperties, p => p.PropertyInfo.Name == "CardNumber");
        }
    }
}