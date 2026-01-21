using Xunit;
using ORM_v1.Mapping;
using ORM_v1.Mapping.Strategies; // WAŻNE: Dodaj ten using
using ORM.Tests.Models;
using System.Linq;

namespace ORM.Tests
{
    public class InheritanceMappingTests
    {
        private readonly IModelBuilder _builder;
        private readonly ModelDirector _director;
        private readonly INamingStrategy _naming;

        public InheritanceMappingTests()
        {
            _naming = new PascalCaseNamingStrategy();
            _builder = new ReflectionModelBuilder(_naming);
            _director = new ModelDirector(_builder);
        }

        [Fact]
        public void Should_Map_TPH_To_Single_Table_With_Discriminator()
        {
            var model = _director.Construct(this.GetType().Assembly);
            var dogMap = model[typeof(Dog)];
            var animalMap = model[typeof(Animal)];

            Assert.Equal("Animals", animalMap.TableName);
            Assert.Equal("Animals", dogMap.TableName);

            // ZMIANA: Sprawdzamy typ strategii zamiast Enuma
            Assert.IsType<TablePerHierarchyStrategy>(dogMap.InheritanceStrategy);

            // Te pola nadal działają, bo EntityMap je wystawia (pobierając ze strategii)
            Assert.Equal("Discriminator", dogMap.DiscriminatorColumn);
            Assert.Equal(nameof(Dog), dogMap.Discriminator);

            Assert.Same(animalMap, dogMap.BaseMap);
        }

        [Fact]
        public void Should_Map_TPC_To_Separate_Tables_Without_Discriminator()
        {
            var model = _director.Construct(this.GetType().Assembly);
            var cardMap = model[typeof(CreditCardPayment)];

            Assert.Equal("CreditCardPayments", cardMap.TableName);

            // ZMIANA: Sprawdzamy typ strategii zamiast Enuma
            Assert.IsType<TablePerConcreteClassStrategy>(cardMap.InheritanceStrategy);

            Assert.Null(cardMap.DiscriminatorColumn);
            Assert.Null(cardMap.Discriminator);

            Assert.Contains(cardMap.ScalarProperties, p => p.PropertyInfo.Name == "Amount");
            Assert.Contains(cardMap.ScalarProperties, p => p.PropertyInfo.Name == "CardNumber");
        }
    }
}