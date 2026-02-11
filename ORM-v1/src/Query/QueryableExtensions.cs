using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using ORM_v1.core;
using ORM_v1.Mapping;

namespace ORM_v1.Query;

/// <summary>
/// Extension methods dla IQueryable - implementacja eager loading (.Include) i filtering (.Where).
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// Wykonuje proste zapytanie SELECT bez eager loading.
    /// Przykład: context.Products.ToList()
    /// </summary>
    public static List<TEntity> ToList<TEntity>(this DbSet<TEntity> source)
        where TEntity : class
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var context = GetContextFromDbSet(source);
        return GetAllEntitiesFromContext<TEntity>(context);
    }

    /// <summary>
    /// Filtrowanie - WHERE clause.
    /// Przykład: context.Products.Where(p => p.Price > 100).ToList()
    /// </summary>
    public static FilterableQueryable<TEntity> Where<TEntity>(
        this DbSet<TEntity> source,
        Expression<Func<TEntity, bool>> predicate)
        where TEntity : class
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (predicate == null) throw new ArgumentNullException(nameof(predicate));

        var context = GetContextFromDbSet(source);
        return new FilterableQueryable<TEntity>(source, context, predicate);
    }

    /// <summary>
    /// Eager loading - załaduj powiązaną navigation property.
    /// Przykład: context.Posts.Include(p => p.Blog).ToList()
    /// </summary>
    public static IncludableQueryable<TEntity, TProperty> Include<TEntity, TProperty>(
        this DbSet<TEntity> source,
        Expression<Func<TEntity, TProperty>> navigationPropertyPath)
        where TEntity : class
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (navigationPropertyPath == null) throw new ArgumentNullException(nameof(navigationPropertyPath));

        var propertyName = GetPropertyName(navigationPropertyPath);
        var context = GetContextFromDbSet(source);

        var includes = new List<IncludeInfo>
        {
            new IncludeInfo(propertyName, navigationPropertyPath)
        };

        return new IncludableQueryable<TEntity, TProperty>(source, context, includes);
    }

    /// <summary>
    /// Include na wynikach Where().
    /// Przykład: context.Products.Where(p => p.Price > 100).Include(p => p.Category).ToList()
    /// </summary>
    public static IncludableQueryable<TEntity, TProperty> Include<TEntity, TProperty>(
        this FilterableQueryable<TEntity> source,
        Expression<Func<TEntity, TProperty>> navigationPropertyPath)
        where TEntity : class
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (navigationPropertyPath == null) throw new ArgumentNullException(nameof(navigationPropertyPath));

        var propertyName = GetPropertyName(navigationPropertyPath);

        var includes = new List<IncludeInfo>
        {
            new IncludeInfo(propertyName, navigationPropertyPath)
        };

        return new IncludableQueryable<TEntity, TProperty>(source, source.Context, includes, source.Predicate);
    }

    /// <summary>
    /// Kontynuacja Include - załaduj kolejną navigation property.
    /// Przykład: context.Posts.Include(p => p.Blog).Include(p => p.Comments).ToList()
    /// </summary>
    public static IncludableQueryable<TEntity, TProperty2> Include<TEntity, TProperty, TProperty2>(
        this IncludableQueryable<TEntity, TProperty> source,
        Expression<Func<TEntity, TProperty2>> navigationPropertyPath)
        where TEntity : class
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (navigationPropertyPath == null) throw new ArgumentNullException(nameof(navigationPropertyPath));

        var propertyName = GetPropertyName(navigationPropertyPath);
        var includes = new List<IncludeInfo>(source.Includes)
        {
            new IncludeInfo(propertyName, navigationPropertyPath)
        };

        return new IncludableQueryable<TEntity, TProperty2>(source, source.Context, includes);
    }

    /// <summary>
    /// ThenInclude - załaduj zagnieżdżoną navigation property z kolekcji.
    /// Przykład: context.Blogs.Include(b => b.Posts).ThenInclude(p => p.Comments).ToList()
    /// </summary>
    public static IncludableQueryable<TEntity, TProperty> ThenInclude<TEntity, TPreviousProperty, TProperty>(
        this IncludableQueryable<TEntity, List<TPreviousProperty>> source,
        Expression<Func<TPreviousProperty, TProperty>> navigationPropertyPath)
        where TEntity : class
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (navigationPropertyPath == null) throw new ArgumentNullException(nameof(navigationPropertyPath));

        var propertyName = GetPropertyName(navigationPropertyPath);

        // Znajdź poprzednią ścieżkę (ostatni element w Includes)
        var previousInclude = source.Includes.LastOrDefault();
        var previousPath = previousInclude?.FullPath ?? "";

        // Zbuduj pełną ścieżkę: "Posts.Comments"
        var fullPath = string.IsNullOrEmpty(previousPath) ? propertyName : $"{previousPath}.{propertyName}";

        var includes = new List<IncludeInfo>(source.Includes)
        {
            new IncludeInfo(propertyName, navigationPropertyPath, fullPath)
        };

        return new IncludableQueryable<TEntity, TProperty>(source, source.Context, includes);
    }

    /// <summary>
    /// ThenInclude - załaduj zagnieżdżoną navigation property z pojedynczej referencji.
    /// Przykład: context.Posts.Include(p => p.Blog).ThenInclude(b => b.Owner).ToList()
    /// </summary>
    public static IncludableQueryable<TEntity, TProperty> ThenInclude<TEntity, TPreviousProperty, TProperty>(
        this IncludableQueryable<TEntity, TPreviousProperty> source,
        Expression<Func<TPreviousProperty, TProperty>> navigationPropertyPath)
        where TEntity : class
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (navigationPropertyPath == null) throw new ArgumentNullException(nameof(navigationPropertyPath));

        var propertyName = GetPropertyName(navigationPropertyPath);

        // Znajdź poprzednią ścieżkę
        var previousInclude = source.Includes.LastOrDefault();
        var previousPath = previousInclude?.FullPath ?? "";

        // Zbuduj pełną ścieżkę
        var fullPath = string.IsNullOrEmpty(previousPath) ? propertyName : $"{previousPath}.{propertyName}";

        var includes = new List<IncludeInfo>(source.Includes)
        {
            new IncludeInfo(propertyName, navigationPropertyPath, fullPath)
        };

        return new IncludableQueryable<TEntity, TProperty>(source, source.Context, includes);
    }

    /// <summary>
    /// Wykonuje zapytanie z filtrowaniem WHERE.
    /// Przykład: context.Products.Where(p => p.Price > 100).ToList()
    /// </summary>
    public static List<TEntity> ToList<TEntity>(
        this FilterableQueryable<TEntity> source)
        where TEntity : class
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var context = source.Context;
        var metadataStore = context.GetConfiguration().MetadataStore;
        var entityMap = metadataStore.GetMap<TEntity>();

        // Buduj QueryModel z WHERE clause
        var queryModel = new QueryModel
        {
            PrimaryEntity = entityMap,
            SelectAllColumns = true
        };

        // Konwertuj Expression<Func<T, bool>> na SQL WHERE
        var whereTranslator = new ExpressionToSqlConverter(entityMap);
        var (whereClause, parameters) = whereTranslator.Translate(source.Predicate);

        queryModel.WhereClause = whereClause;
        queryModel.Parameters = parameters;

        // Generuj SQL
        var sqlGenerator = new SqliteSqlGenerator();
        var sqlQuery = sqlGenerator.GenerateComplexSelect(entityMap, queryModel);

        // Wykonaj zapytanie
        var connection = GetConnectionFromContext(context);
        using var command = connection.CreateCommand();
        command.CommandText = sqlQuery.Sql;

        foreach (var param in sqlQuery.Parameters)
        {
            var dbParam = command.CreateParameter();
            dbParam.ParameterName = param.Key;
            dbParam.Value = param.Value ?? DBNull.Value;
            command.Parameters.Add(dbParam);
        }

        // Materializuj encje
        using var reader = command.ExecuteReader();
        var materializer = new ObjectMaterializer(entityMap, metadataStore);
        var ordinals = GetOrdinals(reader, entityMap, null); // null tableAlias dla prostego SELECT
        var entities = new List<TEntity>();

        while (reader.Read())
        {
            var entity = (TEntity)materializer.Materialize(reader, ordinals);
            context.ChangeTracker.Track(entity, EntityState.Unchanged);
            entities.Add(entity);
        }

        return entities;
    }

    /// <summary>
    /// Wykonuje zapytanie i ładuje wszystkie includes UŻYWAJĄC SQL JOIN (optymalizacja).
    /// Generuje jedno zapytanie SQL z LEFT JOIN dla każdej navigation property.
    /// </summary>
    public static List<TEntity> ToList<TEntity, TProperty>(
        this IncludableQueryable<TEntity, TProperty> source)
        where TEntity : class
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var context = source.Context;
        var metadataStore = context.GetConfiguration().MetadataStore;
        var entityMap = metadataStore.GetMap<TEntity>();

        if (source.Includes.Count == 0 && source.Predicate == null)
        {
            // Brak includes ani filtrowania - użyj standardowego SetInternal
            return GetAllEntitiesFromContext<TEntity>(context);
        }

        // Buduj QueryModel z JOINami dla includes i opcjonalnie WHERE
        var queryModel = BuildQueryModelWithIncludes(entityMap, source.Includes, metadataStore);

        // Dodaj WHERE clause jeśli istnieje
        if (source.Predicate != null)
        {
            var whereTranslator = new ExpressionToSqlConverter(entityMap);
            var (whereClause, parameters) = whereTranslator.Translate(source.Predicate);
            queryModel.WhereClause = whereClause;
            queryModel.Parameters = parameters;
        }

        // Generuj SQL z JOINami (używamy nowej instancji SqliteSqlGenerator)
        var sqlGenerator = new SqliteSqlGenerator();
        var sqlQuery = sqlGenerator.GenerateComplexSelect(entityMap, queryModel);

        // Wykonaj zapytanie
        var connection = GetConnectionFromContext(context);
        using var command = connection.CreateCommand();
        command.CommandText = sqlQuery.Sql;

        foreach (var param in sqlQuery.Parameters)
        {
            var dbParam = command.CreateParameter();
            dbParam.ParameterName = param.Key;
            dbParam.Value = param.Value ?? DBNull.Value;
            command.Parameters.Add(dbParam);
        }

        // Materializuj encje z wyników JOIN
        using var reader = command.ExecuteReader();

        // Materializuj encje z wyników JOIN (z auto-tracking)
        var entities = MaterializeEntitiesWithJoins<TEntity>(
            reader,
            entityMap,
            queryModel.PrimaryEntityAlias,
            queryModel.IncludeJoins,
            metadataStore,
            context);

        return entities;
    }

    /// <summary>
    /// Pobiera wszystkie encje z context używając SetInternal (bez includes).
    /// </summary>
    private static List<TEntity> GetAllEntitiesFromContext<TEntity>(DbContext context)
        where TEntity : class
    {
        var setInternalMethod = typeof(DbContext).GetMethod("SetInternal",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?.MakeGenericMethod(typeof(TEntity));

        if (setInternalMethod == null)
            throw new InvalidOperationException("Cannot access SetInternal method");

        var allEntitiesEnumerable = setInternalMethod.Invoke(context, null) as IEnumerable<TEntity>;
        if (allEntitiesEnumerable == null)
            throw new InvalidOperationException("Failed to retrieve entities");

        return allEntitiesEnumerable.ToList();
    }

    /// <summary>
    /// Buduje QueryModel z LEFT JOIN dla każdej navigation property w includes.
    /// Obsługuje zarówno proste Include jak i zagnieżdżone ThenInclude.
    /// </summary>
    private static QueryModel BuildQueryModelWithIncludes(
        EntityMap entityMap,
        List<IncludeInfo> includes,
        IMetadataStore metadataStore)
    {
        var queryModel = new QueryModel
        {
            PrimaryEntity = entityMap,
            PrimaryEntityAlias = entityMap.TableName.ToLowerInvariant() + "_main",
            SelectAllColumns = true
        };

        int aliasIndex = 0;

        // ✅ Obsługa zagnieżdżonych includes: dla każdego include przetwarzamy tylko pierwszy segment
        // Zagnieżdżone ThenInclude są grupowane razem
        var processedPaths = new HashSet<string>();

        foreach (var include in includes)
        {
            // Dla prostych includes - przetwarzamy normalnie
            // Dla zagnieżdżonych - pomijamy, bo zostaną przetworzone rekurencyjnie
            if (include.IsNested)
            {
                // Zagnieżdżony include zostanie przetworzony jako część swojego rodzica
                continue;
            }

            ProcessIncludeRecursively(
                entityMap,
                include,
                includes,
                queryModel,
                metadataStore,
                ref aliasIndex,
                queryModel.PrimaryEntityAlias,
                processedPaths);
        }

        return queryModel;
    }

    /// <summary>
    /// Przetwarza pojedynczy include wraz z jego zagnieżdżonymi dziećmi (ThenInclude).
    /// </summary>
    private static void ProcessIncludeRecursively(
        EntityMap currentEntityMap,
        IncludeInfo currentInclude,
        List<IncludeInfo> allIncludes,
        QueryModel queryModel,
        IMetadataStore metadataStore,
        ref int aliasIndex,
        string parentAlias,
        HashSet<string> processedPaths)
    {
        var currentPath = currentInclude.FullPath;

        // Sprawdź czy już przetworzyliśmy tę ścieżkę
        if (!processedPaths.Add(currentPath))
        {
            return; // Już przetworzona
        }

        var navProp = currentEntityMap.NavigationProperties
            .FirstOrDefault(p => p.PropertyInfo.Name == currentInclude.NavigationPropertyName);

        if (navProp == null)
        {
            throw new InvalidOperationException(
                $"Navigation property '{currentInclude.NavigationPropertyName}' not found on type '{currentEntityMap.EntityType.Name}'");
        }

        if (navProp.TargetType == null)
            return;

        var targetMap = metadataStore.GetMap(navProp.TargetType);
        var joinAlias = $"{targetMap.TableName.ToLowerInvariant()}_j{aliasIndex++}";

        PropertyMap leftProperty;
        PropertyMap rightProperty;

        if (navProp.IsCollection)
        {
            // ONE-TO-MANY: Primary.Id = Target.FK
            leftProperty = currentEntityMap.KeyProperty;

            // Znajdź FK w target entity
            var inverseFk = targetMap.NavigationProperties
                .FirstOrDefault(np => np.TargetType == currentEntityMap.EntityType && !np.IsCollection);

            if (inverseFk?.ForeignKeyName == null)
                return;

            var fkProp = targetMap.ScalarProperties
                .FirstOrDefault(p => p.PropertyInfo.Name == inverseFk.ForeignKeyName);

            if (fkProp == null)
                return;

            rightProperty = fkProp;
        }
        else
        {
            // MANY-TO-ONE: Primary.FK = Target.Id
            if (navProp.ForeignKeyName == null)
                return;

            var fkProp = currentEntityMap.ScalarProperties
                .FirstOrDefault(p => p.PropertyInfo.Name == navProp.ForeignKeyName);

            if (fkProp == null)
                return;

            leftProperty = fkProp;
            rightProperty = targetMap.KeyProperty;
        }

        var joinClause = new JoinClause
        {
            JoinedEntity = targetMap,
            LeftProperty = leftProperty,
            RightProperty = rightProperty,
            JoinType = JoinType.Left,
            Alias = joinAlias
        };

        // Ustaw poprawny alias rodzica dla JOIN
        // Dla pierwszego poziomu używamy primaryAlias, dla zagnieżdżonych używamy alias rodzica
        joinClause.ParentAlias = parentAlias;

        queryModel.Joins.Add(joinClause);
        queryModel.IncludeJoins.Add(new IncludeJoinInfo
        {
            NavigationProperty = navProp,
            Join = joinClause,
            TableAlias = joinAlias,
            IncludeInfo = currentInclude  // ✅ Dodaj IncludeInfo dla wsparcia ThenInclude
        });

        // ✅ Rekurencyjnie przetwarzaj zagnieżdżone ThenInclude
        // Znajdź wszystkie includes które są dziećmi obecnego (zaczynają się od currentPath + ".")
        var childIncludes = allIncludes.Where(inc =>
            inc.IsNested &&
            inc.FullPath.StartsWith(currentPath + ".") &&
            inc.PathSegments.Length == currentInclude.PathSegments.Length + 1 // Tylko bezpośrednie dzieci
        ).ToList();

        foreach (var childInclude in childIncludes)
        {
            ProcessIncludeRecursively(
                targetMap,  // Teraz target jest rodzicem
                childInclude,
                allIncludes,
                queryModel,
                metadataStore,
                ref aliasIndex,
                joinAlias,  // Obecny alias staje się rodzicem dla dziecka
                processedPaths);
        }
    }

    /// <summary>
    /// Materializuje encje z wyników SQL JOIN.
    /// Grupuje wiersze według głównej encji i przypisuje navigation properties.
    /// Auto-tracking: wszystkie encje są automatycznie śledzone przez ChangeTracker.
    /// </summary>
    private static List<TEntity> MaterializeEntitiesWithJoins<TEntity>(
        IDataReader reader,
        EntityMap entityMap,
        string? primaryEntityAlias,
        List<IncludeJoinInfo> includeJoins,
        IMetadataStore metadataStore,
        DbContext context)
        where TEntity : class
    {
        var materializer = new ObjectMaterializer(entityMap, metadataStore);
        var primaryOrdinals = GetOrdinals(reader, entityMap, primaryEntityAlias);

        // Przygotuj materializers dla joined entities
        var joinMaterializers = new Dictionary<string, (ObjectMaterializer materializer, int[] ordinals, PropertyMap navProp, EntityMap entityMap)>();

        foreach (var includeJoin in includeJoins)
        {
            var targetMap = includeJoin.Join.JoinedEntity;
            var targetMaterializer = new ObjectMaterializer(targetMap, metadataStore);
            var targetOrdinals = GetOrdinals(reader, targetMap, includeJoin.TableAlias);

            joinMaterializers[includeJoin.TableAlias] = (targetMaterializer, targetOrdinals, includeJoin.NavigationProperty, targetMap);
        }

        // Słownik głównych encji według primary key
        var entitiesDict = new Dictionary<object, TEntity>();

        // Słownik kolekcji per entity instance (nie per primary key!) dla zagnieżdżonych includes
        // Dictionary<entity object reference, Dictionary<propertyName, IList>>
        var collectionsByEntity = new Dictionary<object, Dictionary<string, IList>>();

        while (reader.Read())
        {
            // 1. MATERIALIZUJ ROOT ENTITY
            var tempEntity = (TEntity)materializer.Materialize(reader, primaryOrdinals);
            var primaryKey = entityMap.KeyProperty.PropertyInfo.GetValue(tempEntity)!;

            TEntity entity;
            if (!entitiesDict.ContainsKey(primaryKey))
            {
                // Sprawdź czy już śledzona w ChangeTracker (Identity Map)
                var trackedEntity = context.ChangeTracker.FindTracked(typeof(TEntity), primaryKey) as TEntity;

                if (trackedEntity != null)
                {
                    entity = trackedEntity;
                }
                else
                {
                    entity = tempEntity;
                    context.ChangeTracker.Track(entity, EntityState.Unchanged);
                }

                entitiesDict[primaryKey] = entity;
                collectionsByEntity[entity] = new Dictionary<string, IList>();
            }
            else
            {
                entity = entitiesDict[primaryKey];
            }

            // 2. MATERIALIZUJ ZAGNIEŻDŻONE ENCJE W HIERARCHII
            // Słownik zmaterializowanych encji w tym wierszu: alias → entity instance
            var currentRowEntities = new Dictionary<string, object>
            {
                [primaryEntityAlias ?? "primary"] = entity
            };

            // Sortuj includeJoins według poziomu zagnieżdżenia (depth-first order)
            var sortedJoins = includeJoins
                .Select(ij => new { IncludeJoin = ij, Depth = ij.IncludeInfo?.PathSegments.Length ?? 1 })
                .OrderBy(x => x.Depth)
                .Select(x => x.IncludeJoin)
                .ToList();

            foreach (var includeJoin in sortedJoins)
            {
                var (joinMaterializer, joinOrdinals, navProp, relatedMap) = joinMaterializers[includeJoin.TableAlias];

                // Sprawdź czy joined row ma dane (LEFT JOIN może zwrócić NULL)
                if (IsJoinedRowNull(reader, joinOrdinals))
                    continue;

                // Materializuj powiązaną encję
                var tempRelatedEntity = joinMaterializer.Materialize(reader, joinOrdinals);
                var relatedKeyValue = relatedMap.KeyProperty.PropertyInfo.GetValue(tempRelatedEntity)!;

                // Identity Map check
                var relatedEntity = context.ChangeTracker.FindTracked(navProp.TargetType!, relatedKeyValue);
                if (relatedEntity == null)
                {
                    relatedEntity = tempRelatedEntity;
                    context.ChangeTracker.Track(relatedEntity, EntityState.Unchanged);
                }

                // 3. ZNAJDŹ RODZICA DLA TEGO JOINA (wsparcie dla ThenInclude!)
                var parentAlias = includeJoin.Join.ParentAlias ?? primaryEntityAlias ?? "primary";

                if (!currentRowEntities.TryGetValue(parentAlias, out var parentEntity))
                {
                    // Rodzic nie zmaterializowany w tym wierszu (LEFT JOIN null parent) - pomiń
                    continue;
                }

                // 4. PRZYPISZ DO WŁAŚCIWEGO RODZICA
                if (navProp.IsCollection)
                {
                    // ONE-TO-MANY: dodaj do kolekcji RODZICA (nie root!)
                    if (!collectionsByEntity.ContainsKey(parentEntity))
                    {
                        collectionsByEntity[parentEntity] = new Dictionary<string, IList>();
                    }

                    var collections = collectionsByEntity[parentEntity];

                    if (!collections.ContainsKey(navProp.PropertyInfo.Name))
                    {
                        var listType = typeof(List<>).MakeGenericType(navProp.TargetType!);
                        collections[navProp.PropertyInfo.Name] = (IList)Activator.CreateInstance(listType)!;
                    }

                    var collection = collections[navProp.PropertyInfo.Name];

                    // Unikaj duplikatów
                    var relatedKey = relatedMap.KeyProperty.PropertyInfo.GetValue(relatedEntity)!;
                    var alreadyExists = false;
                    foreach (var item in collection)
                    {
                        var itemKey = relatedMap.KeyProperty.PropertyInfo.GetValue(item)!;
                        if (itemKey.Equals(relatedKey))
                        {
                            alreadyExists = true;
                            break;
                        }
                    }

                    if (!alreadyExists)
                    {
                        collection.Add(relatedEntity);
                    }
                }
                else
                {
                    // MANY-TO-ONE lub ONE-TO-ONE: przypisz bezpośrednio do rodzica
                    navProp.PropertyInfo.SetValue(parentEntity, relatedEntity);
                }

                // Zapisz zmaterializowaną encję do currentRowEntities dla dalszych ThenInclude
                currentRowEntities[includeJoin.TableAlias] = relatedEntity;
            }
        }

        // 5. PRZYPISZ KOLEKCJE DO WSZYSTKICH ENCJI (nie tylko root!)
        foreach (var kvp in collectionsByEntity)
        {
            var parentEntity = kvp.Key;
            var collections = kvp.Value;

            foreach (var collection in collections)
            {
                var navPropName = collection.Key;
                var navPropList = collection.Value;

                // Znajdź PropertyInfo na typie rodzica
                var parentType = parentEntity.GetType();
                var navPropInfo = parentType.GetProperty(navPropName);

                if (navPropInfo != null)
                {
                    navPropInfo.SetValue(parentEntity, navPropList);
                }
            }
        }

        return entitiesDict.Values.ToList();
    }

    /// <summary>
    /// Pobiera ordinals (indeksy kolumn) dla danego EntityMap z DataReader.
    /// Używa aliasów kolumn (tableAlias_columnName) aby uniknąć konfliktów w JOIN.
    /// </summary>
    private static int[] GetOrdinals(IDataReader reader, EntityMap map, string? tableAlias)
    {
        var ordinals = new int[map.ScalarProperties.Count];

        for (int i = 0; i < map.ScalarProperties.Count; i++)
        {
            var prop = map.ScalarProperties[i];
            ordinals[i] = -1;

            var columnName = prop.ColumnName;
            if (columnName == null)
                continue;

            // W przypadku JOIN z aliasem, szukaj "tableAlias_columnName"
            string searchName;
            if (!string.IsNullOrEmpty(tableAlias))
            {
                searchName = $"{tableAlias}_{columnName}";
            }
            else
            {
                searchName = columnName;
            }

            for (int j = 0; j < reader.FieldCount; j++)
            {
                var fieldName = reader.GetName(j);

                if (string.Equals(fieldName, searchName, StringComparison.OrdinalIgnoreCase))
                {
                    ordinals[i] = j;
                    break;
                }
            }
        }

        return ordinals;
    }

    /// <summary>
    /// Sprawdza czy joined row zawiera NULL (LEFT JOIN bez match).
    /// </summary>
    private static bool IsJoinedRowNull(IDataReader reader, int[] ordinals)
    {
        // Jeśli wszystkie kolumny joined entity są NULL, to nie ma match
        foreach (var ordinal in ordinals)
        {
            if (ordinal >= 0 && !reader.IsDBNull(ordinal))
            {
                return false; // Przynajmniej jedna kolumna ma wartość
            }
        }

        return true; // Wszystkie NULL lub brak kolumn
    }

    /// <summary>
    /// Pobiera connection z DbContext używając reflection.
    /// </summary>
    private static IDbConnection GetConnectionFromContext(DbContext context)
    {
        var getConnectionMethod = typeof(DbContext)
            .GetMethod("GetConnection", BindingFlags.NonPublic | BindingFlags.Instance);

        if (getConnectionMethod == null)
        {
            throw new InvalidOperationException("Cannot access GetConnection from DbContext");
        }

        var connection = getConnectionMethod.Invoke(context, null) as IDbConnection;
        if (connection == null)
        {
            throw new InvalidOperationException("DbContext.GetConnection() returned null");
        }

        return connection;
    }

    /// <summary>
    /// Ekstraktuje nazwę właściwości z expression.
    /// </summary>
    private static string GetPropertyName<TEntity, TProperty>(
        Expression<Func<TEntity, TProperty>> expression)
    {
        if (expression.Body is MemberExpression memberExpr)
        {
            return memberExpr.Member.Name;
        }

        throw new ArgumentException(
            $"Expression '{expression}' refers to a method, not a property.");
    }

    /// <summary>
    /// Pobiera DbContext z DbSet używając reflection.
    /// </summary>
    private static DbContext GetContextFromDbSet<TEntity>(DbSet<TEntity> dbSet)
        where TEntity : class
    {
        var contextField = typeof(DbSet<TEntity>)
            .GetField("_context", BindingFlags.NonPublic | BindingFlags.Instance);

        if (contextField == null)
        {
            throw new InvalidOperationException("Cannot access DbContext from DbSet");
        }

        var context = contextField.GetValue(dbSet) as DbContext;
        if (context == null)
        {
            throw new InvalidOperationException("DbSet does not have an associated DbContext");
        }

        return context;
    }
}
