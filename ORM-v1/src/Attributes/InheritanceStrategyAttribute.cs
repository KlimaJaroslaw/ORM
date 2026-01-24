using System;
using ORM_v1.Mapping.Strategies;

namespace ORM_v1.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class InheritanceStrategyAttribute : Attribute
    {
        public InheritanceStrategy Strategy { get; }
        public InheritanceStrategyAttribute(InheritanceStrategy strategy) 
            => Strategy = strategy;
    }
}