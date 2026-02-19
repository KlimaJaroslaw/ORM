using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ORM_v1.Attributes;

namespace ORM_v1.Mapping
{
    public sealed class ModelDirector
    {
        private readonly IModelBuilder _builder;

        public ModelDirector(IModelBuilder builder)
        {
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        }

        public IReadOnlyDictionary<Type, EntityMap> Construct(Assembly assembly)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));

            // Reset stanu Buildera przed nowym budowaniem
            _builder.Reset();

            // 1. Znajdź kandydatów na encje
            var entityTypes = assembly.GetTypes()
                .Where(IsEntityCandidate)
                .ToList();

            // 2. Posortuj (Rodzic -> Dziecko), aby Builder miał już gotowe mapy bazowe
            var sortedTypes = entityTypes.OrderBy(GetInheritanceDepth);

            // 3. Zarządzaj procesem budowy
            foreach (var type in sortedTypes)
            {
                // Sprawdź, czy inne encje dziedziczą po tym typie
                bool hasDerived = entityTypes.Any(t => t.BaseType == type);
                
                // Zleć budowę
                _builder.BuildEntity(type, hasDerived);
            }

            // 4. Zwróć wynik
            return _builder.GetResult();
        }

        private static int GetInheritanceDepth(Type t)
        {
            int depth = 0;
            while (t.BaseType != null)
            {
                depth++;
                t = t.BaseType;
            }
            return depth;
        }

        private static bool IsEntityCandidate(Type type)
        {
            if (!type.IsClass || !type.IsPublic || (type.IsAbstract && type.IsSealed)) 
                return false;

            if (type.GetCustomAttribute<IgnoreAttribute>() != null) 
                return false;

            if (type.GetCustomAttribute<TableAttribute>() != null)
                return true;

            // Heurystyka: szukamy klucza
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var p in properties)
            {
                if (p.GetCustomAttribute<KeyAttribute>() != null) return true;
                if (string.Equals(p.Name, "Id", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(p.Name, type.Name + "Id", StringComparison.OrdinalIgnoreCase)) return true;
            }

            // Jeśli baza jest encją, to pochodna też może być (TPH)
            if (type.BaseType != null && IsEntityCandidate(type.BaseType))
                return true;

            return false;
        }
    }
}