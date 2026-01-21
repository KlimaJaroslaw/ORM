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
                // --- WZORZEC BUILDER (GoF) ---
                
                // 1. Concrete Builder: Wie JAK tworzyć mapy
                IModelBuilder builder = new ReflectionModelBuilder(_naming);
                
                // 2. Director: Wie CO i W JAKIEJ KOLEJNOŚCI budować
                var director = new ModelDirector(builder);
                
                // 3. Construct: Proces budowania
                var maps = director.Construct(asm);

                // Scalanie wyników
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