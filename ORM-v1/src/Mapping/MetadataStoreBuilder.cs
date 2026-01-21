using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;

namespace ORM_v1.Mapping
{
    public sealed class MetadataStoreBuilder
    {
        private readonly List<Assembly> _assemblies = new();
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

        public MetadataStoreBuilder UseNamingStrategy(INamingStrategy strategy)
        {
            _naming = strategy ?? throw new ArgumentNullException(nameof(strategy));
            return this;
        }

        public IMetadataStore Build()
        {
            if (_assemblies.Count == 0)
                throw new InvalidOperationException("No assemblies provided for MetadataStore.");

            if (_naming == null)
                throw new InvalidOperationException("Naming strategy was not provided.");

            var allMaps = new Dictionary<Type, EntityMap>();

            foreach (var asm in _assemblies)
            {
                // Użycie refleksji do budowy modelu
                IModelBuilder builder = new ReflectionModelBuilder(_naming);
                // Użycie dyrektora do skonstruowania map
                var director = new ModelDirector(builder);
                // Konstruowanie map dla danej assembly
                var maps = director.Construct(asm);
                // Dodawanie map do zbioru wszystkich map
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
    }
}