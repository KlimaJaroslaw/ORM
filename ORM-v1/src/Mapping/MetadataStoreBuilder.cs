using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace ORM_v1.Mapping
{
    public sealed class MetadataStoreBuilder
    {
        private readonly List<Assembly> _assemblies = new();
        private readonly List<Type> _explicitTypes = new();
        private INamingStrategy? _naming;

        public MetadataStoreBuilder AddAssembly(Assembly assembly)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            _assemblies.Add(assembly);
            return this;
        }

        public MetadataStoreBuilder AddAssemblies(IEnumerable<Assembly> assemblies)
        {
            if (assemblies == null) throw new ArgumentNullException(nameof(assemblies));
            _assemblies.AddRange(assemblies);
            return this;
        }

        public MetadataStoreBuilder AddEntity(Type entityType)
        {
            if (entityType == null) throw new ArgumentNullException(nameof(entityType));
            if (!_explicitTypes.Contains(entityType))
            {
                _explicitTypes.Add(entityType);
            }
            return this;
        }

        public MetadataStoreBuilder UseNamingStrategy(INamingStrategy strategy)
        {
            _naming = strategy ?? throw new ArgumentNullException(nameof(strategy));
            return this;
        }

        public IMetadataStore Build()
        {
            if (_assemblies.Count == 0 && _explicitTypes.Count == 0)
                throw new InvalidOperationException("No assemblies or types provided for MetadataStore.");

            if (_naming == null)
                throw new InvalidOperationException("Naming strategy was not provided.");

            var allMaps = new Dictionary<Type, EntityMap>();

            // Przetwórz assembly
            foreach (var asm in _assemblies)
            {
                IModelBuilder builder = new ReflectionModelBuilder(_naming);
                var director = new ModelDirector(builder);
                var maps = director.Construct(asm);
                
                foreach (var kv in maps)
                {
                    if (!allMaps.ContainsKey(kv.Key))
                        allMaps.Add(kv.Key, kv.Value);
                }
            }

            // Przetwórz explicite dodane typy
            if (_explicitTypes.Count > 0)
            {
                IModelBuilder builder = new ReflectionModelBuilder(_naming);
                var maps = ConstructFromTypes(builder, _explicitTypes);
                
                foreach (var kv in maps)
                {
                    if (!allMaps.ContainsKey(kv.Key))
                        allMaps.Add(kv.Key, kv.Value);
                }
            }

            return new MetadataStore(
                new ReadOnlyDictionary<Type, EntityMap>(allMaps)
            );
        }

        private IReadOnlyDictionary<Type, EntityMap> ConstructFromTypes(IModelBuilder builder, List<Type> types)
        {
            builder.Reset();

            // Posortuj typy według głębokości dziedziczenia (rodzic -> dziecko)
            var sortedTypes = types.OrderBy(GetInheritanceDepth).ToList();

            foreach (var type in sortedTypes)
            {
                // Sprawdź czy są typy pochodne
                bool hasDerived = types.Any(t => t.BaseType == type);
                builder.BuildEntity(type, hasDerived);
            }

            return builder.GetResult();
        }

        private static int GetInheritanceDepth(Type t)
        {
            int depth = 0;
            var current = t;
            while (current.BaseType != null && current.BaseType != typeof(object))
            {
                depth++;
                current = current.BaseType;
            }
            return depth;
        }
    }
}