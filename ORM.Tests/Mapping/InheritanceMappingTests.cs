using Xunit;
using ORM_v1.Mapping;
using ORM_v1.Mapping.Strategies;
using ORM_v1.Attributes;
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
            var model = _director.Construct(typeof(ORM.Tests.Models.Dog).Assembly);
            var dogMap = model[typeof(ORM.Tests.Models.Dog)];
            var animalMap = model[typeof(ORM.Tests.Models.Animal)];

            Assert.Equal("Animals", animalMap.TableName);
            Assert.Equal("Animals", dogMap.TableName);
            Assert.IsType<TablePerHierarchyStrategy>(dogMap.InheritanceStrategy);
            Assert.Equal("Discriminator", dogMap.DiscriminatorColumn);
            Assert.Equal(nameof(ORM.Tests.Models.Dog), dogMap.Discriminator);
            Assert.Same(animalMap, dogMap.BaseMap);
        }

        [Fact]
        public void Should_Map_TPC_To_Separate_Tables_Without_Discriminator()
        {
            var model = _director.Construct(typeof(ORM.Tests.Models.CreditCardPayment).Assembly);
            var cardMap = model[typeof(ORM.Tests.Models.CreditCardPayment)];

            Assert.Equal("CreditCardPayments", cardMap.TableName);
            Assert.IsType<TablePerConcreteClassStrategy>(cardMap.InheritanceStrategy);
            Assert.Null(cardMap.DiscriminatorColumn);
            Assert.Null(cardMap.Discriminator);
        }

        [Fact]
        public void Should_Map_TPT_To_Separate_Tables_Using_TablePerTypeStrategy()
        {

            var model = _director.Construct(this.GetType().Assembly);
            
            var vehicleMap = model[typeof(TptVehicle)];
            var carMap = model[typeof(TptCar)];

            Assert.IsType<TablePerTypeStrategy>(vehicleMap.InheritanceStrategy);
            Assert.Equal("TptVehicle", vehicleMap.TableName);
            Assert.Null(vehicleMap.Discriminator);

            Assert.IsType<TablePerTypeStrategy>(carMap.InheritanceStrategy);
            Assert.Equal("TptCars", carMap.TableName);
            Assert.Same(vehicleMap, carMap.BaseMap);

            Assert.Null(carMap.Discriminator); 
            Assert.Null(carMap.DiscriminatorColumn);
        }
    }

    [InheritanceStrategy(InheritanceStrategy.TablePerType)]
    public abstract class TptVehicle
    {
        [Key]
        public int Id { get; set; }
        public string? Manufacturer { get; set; }
    }

    [Table("TptCars")]
    public class TptCar : TptVehicle
    {
        public int HorsePower { get; set; }
    }
}