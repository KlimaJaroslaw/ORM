using ORM_v1.Mapping;
using ORM_v1.Mapping.Strategies;
using ORM_v1.Query;
using System.Data;
using System.Text.RegularExpressions;

namespace ORM_v1.src.SQLImplementation;

/// <summary>
/// Nowa, uproszczona implementacja SQL Generator dla SQLite.
/// Zasada: Strategia dziedziczenia ZAWSZE jest analizowana PRZED budowaniem query.
/// </summary>
public class SqliteSqlGenerator : ISqlGenerator
{
    private IMetadataStore? _metadataStore;

    public string GetParameterName(string name, int index) => $"@{name}{index}";

    public string QuoteIdentifier(string name) => $"\"{name}\"";

    /// <summary>
    /// Zwraca alias tabeli dla danej encji według strategii dziedziczenia.
    /// Używane przez QueryableExtensions do budowania WHERE clause.
    /// </summary>
    public string GetTableAlias(EntityMap map, IMetadataStore metadataStore)
    {
        _metadataStore = metadataStore;
        var context = AnalyzeInheritanceStrategy(map, null);
        return context.PrimaryAlias;
    }

    // ==================== SELECT QUERIES ====================

    /// <summary>
    /// Find by ID - deleguje do GenerateComplexSelect.
    /// </summary>
    public SqlQuery GenerateSelect(EntityMap map, object id, IMetadataStore metadataStore)
    {
        _metadataStore = metadataStore;
        
        // Przeanalizuj strategię aby poznać alias
        var tempAlias = map.TableName.ToLowerInvariant();
        var inheritanceCtx = AnalyzeInheritanceStrategy(map, tempAlias);
        
        // Użyj aliasu z kontekstu dziedziczenia
        var tableAlias = inheritanceCtx.PrimaryAlias;
        var keyColumnRef = $"{QuoteIdentifier(tableAlias)}.{QuoteIdentifier(map.KeyProperty.ColumnName!)}";
        
        var queryModel = new QueryModel
        {
            PrimaryEntity = map,
            PrimaryEntityAlias = tableAlias,
            WhereClause = $"{keyColumnRef} = @id",
            Parameters = new Dictionary<string, object> { { "@id", id } }
        };

        return GenerateComplexSelect(map, queryModel, metadataStore);
    }

    /// <summary>
    /// Select all - deleguje do GenerateComplexSelect.
    /// </summary>
    public SqlQuery GenerateSelectAll(EntityMap map, IMetadataStore metadataStore)
    {
        var queryModel = new QueryModel
        {
            PrimaryEntity = map,
            Parameters = new Dictionary<string, object>()
        };

        return GenerateComplexSelect(map, queryModel, metadataStore);
    }

    /// <summary>
    /// Główna metoda generująca SELECT - obsługuje wszystkie strategie dziedziczenia.
    /// Pipeline: 1. Analiza strategii → 2. SELECT columns → 3. FROM + JOINs → 4. WHERE → 5. ORDER BY/LIMIT
    /// </summary>
    public SqlQuery GenerateComplexSelect(EntityMap map, QueryModel queryModel, IMetadataStore metadataStore)
    {
        _metadataStore = metadataStore;

        // ===== FAZA 1: ANALIZA STRATEGII DZIEDZICZENIA =====
        var inheritanceCtx = AnalyzeInheritanceStrategy(map, queryModel.PrimaryEntityAlias);
        var parameters = new Dictionary<string, object>(queryModel.Parameters);

        // Dodaj parametry discriminatora (TPH)
        foreach (var param in inheritanceCtx.DiscriminatorParameters)
            parameters[param.Key] = param.Value;

        // ===== TPC: UNION ALL =====
        if (inheritanceCtx.StrategyType == InheritanceStrategyType.TablePerConcrete &&
            inheritanceCtx.HierarchyMaps.Count > 1)
        {
            return GenerateTPCUnionQuery(inheritanceCtx, queryModel, parameters);
        }

        // ===== FAZA 2: BUILD SELECT COLUMNS =====
        var selectColumns = BuildSelectColumns(map, inheritanceCtx, queryModel);

        // ===== FAZA 3: BUILD FROM + INHERITANCE JOINS =====
        var builder = new SqlQueryBuilder();
        builder.Select(selectColumns);
        builder.FromWithAlias(QuoteIdentifier(inheritanceCtx.BaseTable), QuoteIdentifier(inheritanceCtx.PrimaryAlias));

        // INNER JOIN do rodziców (TPT - w górę hierarchii)
        foreach (var join in inheritanceCtx.ParentJoins)
            builder.InnerJoin(QuoteIdentifier(join.Table), QuoteIdentifier(join.Alias), join.Condition);

        // LEFT JOIN do dzieci (TPT abstrakcyjna - w dół hierarchii)
        foreach (var join in inheritanceCtx.ChildJoins)
            builder.LeftJoin(QuoteIdentifier(join.Table), QuoteIdentifier(join.Alias), join.Condition);

        // ===== FAZA 4: NAVIGATION PROPERTY JOINS (Include) =====
        foreach (var join in queryModel.Joins)
        {
            var joinTable = QuoteIdentifier(join.JoinedEntity.TableName);
            var joinAlias = QuoteIdentifier(join.Alias ?? join.JoinedEntity.TableName);
            var condition = BuildJoinCondition(join, inheritanceCtx.PrimaryAlias);

            builder.LeftJoin(joinTable, joinAlias, condition);

            //   Dla TPT: dodaj INNER JOIN do tabel rodziców included entity
            if (join.JoinedEntity.InheritanceStrategy is TablePerTypeStrategy && join.JoinedEntity.BaseMap != null)
            {
                AddTPTParentJoinsForInclude(builder, join.JoinedEntity, join.Alias!);
            }
        }

        // ===== FAZA 5: WHERE =====
        var whereConditions = new List<string>();

        if (!string.IsNullOrEmpty(queryModel.WhereClause))
        {
            // Dodaj aliasy do WHERE clause (jeśli używamy aliasów)
            var whereWithAlias = AddAliasesToWhereClause(queryModel.WhereClause, inheritanceCtx.PrimaryAlias, map);
            whereConditions.Add(whereWithAlias);
        }

        if (!string.IsNullOrEmpty(inheritanceCtx.DiscriminatorWhereClause))
            whereConditions.Add(inheritanceCtx.DiscriminatorWhereClause);

        if (whereConditions.Any())
            builder.Where(string.Join(" AND ", whereConditions));

        // ===== FAZA 6: ORDER BY, GROUP BY, LIMIT, OFFSET =====
        if (queryModel.GroupByColumns.Any())
        {
            var groupCols = queryModel.GroupByColumns.Select(p => BuildColumnReference(p, inheritanceCtx.PrimaryAlias));
            builder.GroupBy(groupCols);
        }

        if (!string.IsNullOrEmpty(queryModel.HavingClause))
            builder.Having(queryModel.HavingClause);

        if (queryModel.OrderBy.Any())
        {
            var orderClauses = queryModel.OrderBy.Select(o =>
            {
                var colRef = BuildColumnReference(o.Property, o.TableAlias ?? inheritanceCtx.PrimaryAlias);
                return $"{colRef} {(o.IsAscending ? "ASC" : "DESC")}";
            });
            builder.OrderBy(orderClauses);
        }

        if (queryModel.Take.HasValue)
            builder.Limit(queryModel.Take.Value);

        if (queryModel.Skip.HasValue)
            builder.Offset(queryModel.Skip.Value);

        return new SqlQuery
        {
            Sql = builder.ToString(),
            Parameters = parameters
        };
    }

    // ==================== TPC UNION ALL ====================

    /// <summary>
    /// Generuje zapytanie UNION ALL dla TPC (Table Per Concrete Class).
    /// Każda konkretna klasa ma własną tabelę, łączymy je przez UNION ALL.
    /// </summary>
    private SqlQuery GenerateTPCUnionQuery(
        InheritanceContext context,
        QueryModel queryModel,
        Dictionary<string, object> parameters)
    {
        var unionQueries = new List<string>();
        var allColumns = GetAllColumnsForTPC(context.HierarchyMaps);

        foreach (var hierarchyMap in context.HierarchyMaps)
        {
            // 1. Alias dla konkretnej tabeli (np. "employees" lub "teachers")
            var alias = hierarchyMap.TableName.ToLowerInvariant();

            // 2. Budowanie kolumn (dodaje NULL dla brakujących)
            var selectColumns = BuildTPCSelectColumns(hierarchyMap, allColumns).ToList();
            if (queryModel.IncludeJoins != null)
            {
                foreach (var includeJoin in queryModel.IncludeJoins)
                {
                    var joinedMap = includeJoin.Join.JoinedEntity;
                    var joinAlias = includeJoin.TableAlias;

                    foreach (var prop in joinedMap.ScalarProperties)
                    {
                        if (!string.IsNullOrEmpty(prop.ColumnName))
                        {
                             // Generujemy: "joinAlias"."col" AS "joinAlias_col"
                             selectColumns.Add($"{QuoteIdentifier(joinAlias)}.{QuoteIdentifier(prop.ColumnName)} AS {QuoteIdentifier($"{joinAlias}_{prop.ColumnName}")}");
                        }
                    }
                }
            }

            var builder = new SqlQueryBuilder();
            builder.Select(selectColumns);
            
            // Używamy aliasu w FROM, żeby JOIN-y miały się do czego odwołać
            builder.FromWithAlias(QuoteIdentifier(hierarchyMap.TableName), QuoteIdentifier(alias));

            // Dodajemy obsługę JOIN-ów (Include/ThenInclude)
            if (queryModel.IncludeJoins != null)
            {
                foreach (var includeJoin in queryModel.IncludeJoins)
                {
                    var joinTable = includeJoin.Join.JoinedEntity.TableName;
                    var joinAlias = includeJoin.TableAlias; // Alias zdefiniowany w Include

                    // ✅ POPRAWKA TPC: Budujemy warunek JOIN używając LOKALNEGO aliasu bieżącej tabeli
                    // Nie używamy includeJoin.Join.ParentAlias (który jest ustawiony globalnie),
                    // tylko lokalny alias tej konkretnej tabeli w UNION (np. "employees" lub "teachers")
                    var localJoinCondition = BuildJoinConditionForTPC(includeJoin.Join, alias, joinAlias);

                    builder.LeftJoin(QuoteIdentifier(joinTable), QuoteIdentifier(joinAlias), localJoinCondition);
                    
                    // Uwaga: Jeśli dołączana tabela też jest w hierarchii TPT/TPC, 
                    // tutaj można by dodać rekurencyjne łączenie, ale dla 1:1 wystarczy prosty LEFT JOIN.
                }
            }

            // 3. WHERE clause
            if (!string.IsNullOrEmpty(queryModel.WhereClause))
            {
                //   ZMIANA 3: Zamiast usuwać aliasy, podmieniamy alias "główny" na alias aktualnej tabeli
                // Np. "person"."Key" -> "employees"."Key"
                var fixedWhere = queryModel.WhereClause.Replace(
                    $"{QuoteIdentifier(queryModel.PrimaryEntityAlias)}.", 
                    $"{QuoteIdentifier(alias)}.");
                
                builder.Where(fixedWhere);
            }

            unionQueries.Add(builder.ToString());
        }

        // Połącz wszystkie SELECT-y przez UNION ALL
        var unionSql = string.Join("\nUNION ALL\n", unionQueries);

        // ORDER BY / LIMIT / OFFSET muszą być na końcu całego UNION
        if (queryModel.OrderBy.Any() || queryModel.Take.HasValue || queryModel.Skip.HasValue)
        {
            var finalBuilder = new System.Text.StringBuilder();
            finalBuilder.Append("SELECT * FROM (");
            finalBuilder.Append(unionSql);
            finalBuilder.Append(") AS _u"); // Alias dla całego wyniku UNION

            if (queryModel.OrderBy.Any())
            {
                var orderClauses = queryModel.OrderBy.Select(o =>
                    // Tutaj używamy aliasu kolumny, który jest taki sam jak nazwa property
                    $"{QuoteIdentifier(o.Property.ColumnName!)} {(o.IsAscending ? "ASC" : "DESC")}");
                finalBuilder.Append(" ORDER BY ");
                finalBuilder.Append(string.Join(", ", orderClauses));
            }

            if (queryModel.Take.HasValue)
            {
                finalBuilder.Append($" LIMIT {queryModel.Take.Value}");
            }

            if (queryModel.Skip.HasValue)
            {
                finalBuilder.Append($" OFFSET {queryModel.Skip.Value}");
            }

            return new SqlQuery
            {
                Sql = finalBuilder.ToString(),
                Parameters = parameters
            };
        }

        return new SqlQuery
        {
            Sql = unionSql,
            Parameters = parameters
        };
    }

    /// <summary>
    /// Zbiera wszystkie unikalne kolumny ze wszystkich klas w hierarchii TPC.
    /// </summary>
    private HashSet<string> GetAllColumnsForTPC(List<EntityMap> hierarchyMaps)
    {
        var allColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var map in hierarchyMaps)
        {
            foreach (var prop in map.ScalarProperties)
            {
                if (!string.IsNullOrEmpty(prop.ColumnName))
                {
                    allColumns.Add(prop.ColumnName);
                }
            }
        }

        return allColumns;
    }

    /// <summary>
    /// Buduje SELECT columns dla pojedynczej tabeli w TPC.
    /// Dodaje NULL dla kolumn które nie istnieją w tej tabeli.
    /// WAŻNE: Dodaje syntetyczny discriminator aby ObjectMaterializer mógł rozpoznać typ.
    /// </summary>
    private IEnumerable<string> BuildTPCSelectColumns(EntityMap map, HashSet<string> allColumns)
    {
        var columns = new List<string>();
        var existingColumns = new HashSet<string>(
            map.ScalarProperties.Select(p => p.ColumnName!),
            StringComparer.OrdinalIgnoreCase);

        var alias = map.TableName.ToLowerInvariant();

        foreach (var columnName in allColumns)
        {
            if (existingColumns.Contains(columnName))
            {
                columns.Add($"{QuoteIdentifier(alias)}.{QuoteIdentifier(columnName)} AS {QuoteIdentifier(columnName)}");
            }
            else
            {
                // Kolumna nie istnieje w tej tabeli - użyj NULL
                columns.Add($"NULL AS {QuoteIdentifier(columnName)}");
            }
        }

        // TPC nie ma fizycznej kolumny Discriminator, ale potrzebujemy jej dla ObjectMaterializer
        // Dodajemy literał z nazwą typu jako "Discriminator"
        columns.Add($"'{map.EntityType.Name}' AS \"Discriminator\"");

        return columns;
    }

    // ==================== INHERITANCE STRATEGY ANALYSIS ====================

    /// <summary>
    /// Analizuje strategię dziedziczenia i przygotowuje kontekst z aliasami, JOIN-ami i WHERE dla discriminatora.
    /// </summary>
    private InheritanceContext AnalyzeInheritanceStrategy(EntityMap map, string? requestedAlias)
    {
        var context = new InheritanceContext
        {
            PrimaryAlias = requestedAlias ?? map.TableName.ToLowerInvariant()
        };

        if (map.InheritanceStrategy is TablePerHierarchyStrategy tphStrategy)
        {
            // TPH: jedna tabela, filtruj po Discriminator
            context.StrategyType = InheritanceStrategyType.TablePerHierarchy;
            context.BaseTable = map.RootMap.TableName;

            // Zbierz wszystkie mapy w hierarchii
            CollectTPHHierarchy(map.RootMap, context.HierarchyMaps);

            // Zbuduj WHERE dla discriminatora (jeśli konkretna klasa)
            if (!string.IsNullOrEmpty(map.Discriminator))
            {
                var (whereClause, parameters) = BuildDiscriminatorFilter(map, context.PrimaryAlias);
                context.DiscriminatorWhereClause = whereClause;
                context.DiscriminatorParameters = parameters;
            }
        }
        else if (map.InheritanceStrategy is TablePerTypeStrategy)
        {
            // TPT: wiele tabel, JOIN w górę/dół hierarchii
            context.StrategyType = InheritanceStrategyType.TablePerType;

            if (map.BaseMap != null)
            {
                // TPT - konkretna klasa pochodna: INNER JOIN do rodziców
                context.BaseTable = map.TableName;
                context.PrimaryAlias = $"t{map.EntityType.Name}";
                BuildTPTParentJoins(map, context);
                
                //   DODAJ LEFT JOIN DO DZIECI (jeśli są klasy pochodne)
                // Nawet konkretna klasa może mieć dzieci w TPT!
                if (HasDerivedTypes(map))
                {
                    BuildTPTChildJoins(map, context);
                }
            }
            else if (map.IsAbstract)
            {
                // TPT - abstrakcyjna bazowa: LEFT JOIN do dzieci
                context.BaseTable = map.TableName;
                context.PrimaryAlias = $"t{map.EntityType.Name}";
                BuildTPTChildJoins(map, context);
            }
            else
            {
                // TPT - konkretna klasa bez BaseMap (root lub standalone)
                context.BaseTable = map.TableName;
                
                //   SPRAWDŹ CZY MA DZIECI (Student → StudentPart)
                if (HasDerivedTypes(map))
                {
                    context.PrimaryAlias = $"t{map.EntityType.Name}";
                    BuildTPTChildJoins(map, context);
                }
            }
        }
        else if (map.InheritanceStrategy is TablePerConcreteClassStrategy)
        {
            // TPC: każda konkretna klasa ma swoją tabelę, UNION ALL
            context.StrategyType = InheritanceStrategyType.TablePerConcrete;
            context.BaseTable = map.TableName;

            // Zbierz wszystkie konkretne klasy w hierarchii
            CollectTPCHierarchy(map, context.HierarchyMaps);
        }
        else
        {
            // Brak dziedziczenia
            context.StrategyType = InheritanceStrategyType.None;
            context.BaseTable = map.TableName;
        }

        return context;
    }

    private void CollectTPHHierarchy(EntityMap rootMap, List<EntityMap> result)
    {
        result.Add(rootMap);

        if (_metadataStore != null)
        {
            foreach (var otherMap in _metadataStore.GetAllMaps())
            {
                if (otherMap != rootMap &&
                    otherMap.InheritanceStrategy is TablePerHierarchyStrategy &&
                    otherMap.RootMap == rootMap)
                {
                    result.Add(otherMap);
                }
            }
        }
    }

    private void CollectTPCHierarchy(EntityMap map, List<EntityMap> result)
    {
        // Dla TPC: zbierz wszystkie konkretne klasy (nie abstrakcyjne)
        if (!map.IsAbstract)
        {
            result.Add(map);
        }

        if (_metadataStore != null)
        {
            // Znajdź wszystkie klasy pochodne
            foreach (var otherMap in _metadataStore.GetAllMaps())
            {
                if (otherMap != map &&
                    !otherMap.IsAbstract &&
                    otherMap.InheritanceStrategy is TablePerConcreteClassStrategy &&
                    map.EntityType.IsAssignableFrom(otherMap.EntityType))
                {
                    result.Add(otherMap);
                }
            }
        }
    }

    private void BuildTPTParentJoins(EntityMap map, InheritanceContext context)
    {
        var current = map.BaseMap;
        var childAlias = context.PrimaryAlias;

        while (current != null)
        {
            var parentAlias = $"t{current.EntityType.Name}";
            var condition = $"{QuoteIdentifier(childAlias)}.{QuoteIdentifier(map.KeyProperty.ColumnName!)} = {QuoteIdentifier(parentAlias)}.{QuoteIdentifier(current.KeyProperty.ColumnName!)}";

            context.ParentJoins.Add(new InheritanceJoinClause
            {
                Table = current.TableName,
                Alias = parentAlias,
                Condition = condition,
                JoinType = JoinType.Inner
            });

            childAlias = parentAlias;
            current = current.BaseMap;
        }
    }

    private void BuildTPTChildJoins(EntityMap baseMap, InheritanceContext context)
    {
        if (_metadataStore == null) return;

        var directDerived = _metadataStore.GetAllMaps()
            .Where(m => m.InheritanceStrategy is TablePerTypeStrategy && m.BaseMap == baseMap)
            .ToList();

        foreach (var derivedMap in directDerived)
        {
            var childAlias = $"t{derivedMap.EntityType.Name}";
            var condition = $"{QuoteIdentifier(context.PrimaryAlias)}.{QuoteIdentifier(baseMap.KeyProperty.ColumnName!)} = {QuoteIdentifier(childAlias)}.{QuoteIdentifier(derivedMap.KeyProperty.ColumnName!)}";

            context.ChildJoins.Add(new InheritanceJoinClause
            {
                Table = derivedMap.TableName,
                Alias = childAlias,
                Condition = condition,
                JoinType = JoinType.Left
            });

            //   Rekurencyjnie dla kolejnych poziomów (jeśli klasa ma dalsze dzieci)
            if (HasDerivedTypes(derivedMap))
            {
                BuildTPTChildJoins(derivedMap, context);
            }
        }
    }

    /// <summary>
    /// Sprawdza czy EntityMap ma jakiekolwiek klasy pochodne (dzieci) w hierarchii TPT.
    /// </summary>
    private bool HasDerivedTypes(EntityMap map)
    {
        if (_metadataStore == null) return false;

        return _metadataStore.GetAllMaps()
            .Any(m => m.InheritanceStrategy is TablePerTypeStrategy && m.BaseMap == map);
    }

    private (string? whereClause, Dictionary<string, object> parameters) BuildDiscriminatorFilter(
        EntityMap map, string tableAlias)
    {
        var parameters = new Dictionary<string, object>();

        if (map.InheritanceStrategy is not TablePerHierarchyStrategy tphStrategy)
            return (null, parameters);

        if (string.IsNullOrEmpty(map.Discriminator))
            return (null, parameters); // Klasa bazowa - bez filtrowania

        // Znajdź wszystkie podklasy (polimorficzny)
        var allDiscriminators = new List<string> { map.Discriminator };

        if (_metadataStore != null)
        {
            foreach (var otherMap in _metadataStore.GetAllMaps())
            {
                if (otherMap.EntityType != map.EntityType &&
                    map.EntityType.IsAssignableFrom(otherMap.EntityType) &&
                    !string.IsNullOrEmpty(otherMap.Discriminator))
                {
                    allDiscriminators.Add(otherMap.Discriminator);
                }
            }
        }

        var columnRef = $"{QuoteIdentifier(tableAlias)}.{QuoteIdentifier(tphStrategy.DiscriminatorColumn)}";

        if (allDiscriminators.Count == 1)
        {
            parameters["@Discriminator"] = allDiscriminators[0];
            return ($"{columnRef} = @Discriminator", parameters);
        }
        else
        {
            var paramNames = allDiscriminators.Select((d, i) =>
            {
                var paramName = $"@Discriminator{i}";
                parameters[paramName] = d;
                return paramName;
            }).ToList();

            return ($"{columnRef} IN ({string.Join(", ", paramNames)})", parameters);
        }
    }

    // ==================== SELECT COLUMNS BUILDING ====================

    private IEnumerable<string> BuildSelectColumns(EntityMap map, InheritanceContext context, QueryModel queryModel)
    {
        var columns = new List<string>();
        bool hasIncludeJoins = queryModel.IncludeJoins.Any();

        if (context.StrategyType == InheritanceStrategyType.TablePerHierarchy)
        {
            // TPH: wszystkie kolumny z hierarchii + Discriminator
            var processedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var hierarchyMap in context.HierarchyMaps)
            {
                foreach (var prop in hierarchyMap.ScalarProperties)
                {
                    if (!string.IsNullOrEmpty(prop.ColumnName) && processedColumns.Add(prop.ColumnName))
                    {
                        columns.Add(BuildColumnWithAlias(prop.ColumnName, context.PrimaryAlias, hasIncludeJoins));
                    }
                }

                // Discriminator
                if (map.InheritanceStrategy is TablePerHierarchyStrategy tph &&
                    processedColumns.Add(tph.DiscriminatorColumn))
                {
                    columns.Add(BuildColumnWithAlias(tph.DiscriminatorColumn, context.PrimaryAlias, hasIncludeJoins));
                }
            }
        }
        else if (context.StrategyType == InheritanceStrategyType.TablePerType)
        {
            // TPT: kolumny z wszystkich tabel w hierarchii
            AddTPTColumns(map, context, columns, hasIncludeJoins);
        }
        else
        {
            // Brak dziedziczenia: tylko kolumny z tej encji
            foreach (var prop in map.ScalarProperties)
            {
                if (!string.IsNullOrEmpty(prop.ColumnName))
                {
                    columns.Add(BuildColumnWithAlias(prop.ColumnName, context.PrimaryAlias, hasIncludeJoins));
                }
            }
        }

        // Dodaj kolumny z Include JOINs
        foreach (var includeJoin in queryModel.IncludeJoins)
        {
            var joinedMap = includeJoin.Join.JoinedEntity;
            var alias = includeJoin.TableAlias;

            //   Sprawdź czy joined entity używa TPT (hierarchia)
            if (joinedMap.InheritanceStrategy is TablePerTypeStrategy)
            {
                // TPT: dodaj kolumny z hierarchii (tak jak w głównym SELECT)
                AddTPTColumnsForInclude(joinedMap, alias, columns);
            }
            else
            {
                // Proste: wszystkie kolumny z jednej tabeli
                foreach (var prop in joinedMap.ScalarProperties)
                {
                    if (!string.IsNullOrEmpty(prop.ColumnName))
                    {
                        var colAlias = $"{alias}_{prop.ColumnName}";
                        columns.Add($"{QuoteIdentifier(alias)}.{QuoteIdentifier(prop.ColumnName)} AS {QuoteIdentifier(colAlias)}");
                    }
                }
            }
        }

        return columns;
    }

    private void AddTPTColumns(EntityMap map, InheritanceContext context, List<string> columns, bool hasIncludeJoins)
    {
        if (map.BaseMap != null)
        {
            // Konkretna klasa: dodaj kolumny z tej klasy + rekurencyjnie z rodziców
            var derivedAlias = $"t{map.EntityType.Name}";

            foreach (var prop in map.ScalarProperties)
            {
                if (!string.IsNullOrEmpty(prop.ColumnName))
                {
                    var isDerived = map.BaseMap.ScalarProperties.All(bp =>
                        !string.Equals(bp.ColumnName, prop.ColumnName, StringComparison.OrdinalIgnoreCase));

                    if (isDerived)
                    {
                        columns.Add(BuildColumnWithAlias(prop.ColumnName, derivedAlias, hasIncludeJoins));
                    }
                }
            }

            // Rekurencyjnie dodaj kolumny rodziców
            var current = map.BaseMap;
            while (current != null)
            {
                var baseAlias = $"t{current.EntityType.Name}";

                foreach (var prop in current.ScalarProperties)
                {
                    if (!string.IsNullOrEmpty(prop.ColumnName))
                    {
                        var isOwned = current.BaseMap == null ||
                            current.BaseMap.ScalarProperties.All(bp =>
                                !string.Equals(bp.ColumnName, prop.ColumnName, StringComparison.OrdinalIgnoreCase));

                        if (isOwned)
                        {
                            columns.Add(BuildColumnWithAlias(prop.ColumnName, baseAlias, hasIncludeJoins));
                        }
                    }
                }

                current = current.BaseMap;
            }
            
            //   DODAJ KOLUMNY DZIECI (jeśli są)
            if (HasDerivedTypes(map))
            {
                AddTPTChildColumns(map, context, columns, hasIncludeJoins);
            }
        }
        else
        {
            // Abstrakcyjna bazowa lub konkretna bez BaseMap: dodaj kolumny z tej klasy + wszystkie z dzieci
            var baseAlias = $"t{map.EntityType.Name}";

            foreach (var prop in map.ScalarProperties)
            {
                if (!string.IsNullOrEmpty(prop.ColumnName))
                {
                    columns.Add(BuildColumnWithAlias(prop.ColumnName, baseAlias, hasIncludeJoins));
                }
            }

            // Dodaj kolumny dzieci (rekurencyjnie)
            AddTPTChildColumns(map, context, columns, hasIncludeJoins);
        }
    }

    private void AddTPTChildColumns(EntityMap baseMap, InheritanceContext context, List<string> columns, bool hasIncludeJoins)
    {
        if (_metadataStore == null) return;

        var directDerived = _metadataStore.GetAllMaps()
            .Where(m => m.InheritanceStrategy is TablePerTypeStrategy && m.BaseMap == baseMap)
            .ToList();

        foreach (var derivedMap in directDerived)
        {
            var childAlias = $"t{derivedMap.EntityType.Name}";

            foreach (var prop in derivedMap.ScalarProperties)
            {
                if (!string.IsNullOrEmpty(prop.ColumnName))
                {
                    var isOwned = baseMap.ScalarProperties.All(bp =>
                        !string.Equals(bp.ColumnName, prop.ColumnName, StringComparison.OrdinalIgnoreCase));

                    if (isOwned)
                    {
                        columns.Add(BuildColumnWithAlias(prop.ColumnName, childAlias, hasIncludeJoins));
                    }
                }
            }

            //   Rekurencyjnie dla kolejnych poziomów (jeśli klasa ma dalsze dzieci)
            if (HasDerivedTypes(derivedMap))
            {
                AddTPTChildColumns(derivedMap, context, columns, hasIncludeJoins);
            }
        }
    }

    private string BuildColumnWithAlias(string columnName, string tableAlias, bool useAlias)
    {
        if (useAlias)
            return $"{QuoteIdentifier(tableAlias)}.{QuoteIdentifier(columnName)} AS {QuoteIdentifier($"{tableAlias}_{columnName}")}";
        else
            return $"{QuoteIdentifier(tableAlias)}.{QuoteIdentifier(columnName)}";
    }

    /// <summary>
    /// Dodaje kolumny dla TPT entity w Include (z hierarchii tabel).
    /// </summary>
    private void AddTPTColumnsForInclude(EntityMap map, string baseAlias, List<string> columns)
    {
        // Zbierz hierarchię (od derived do root)
        var hierarchy = new List<EntityMap>();
        var current = map;
        while (current != null)
        {
            hierarchy.Add(current);
            current = current.BaseMap;
        }

        // Dla każdej tabeli w hierarchii dodaj jej własne kolumny
        foreach (var hierarchyMap in hierarchy)
        {
            // Alias dla tej tabeli: dla root używamy baseAlias, dla rodziców dodajemy "_parent"
            string tableAlias;
            if (hierarchyMap == map)
            {
                tableAlias = baseAlias;  // np. "teacher_j1"
            }
            else
            {
                tableAlias = $"{baseAlias}_p{hierarchyMap.EntityType.Name}";  // np. "teacher_j1_pPerson"
            }

            foreach (var prop in hierarchyMap.ScalarProperties)
            {
                if (!string.IsNullOrEmpty(prop.ColumnName))
                {
                    // Sprawdź czy kolumna jest dziedziczona
                    bool isInherited = false;
                    if (hierarchyMap.BaseMap != null)
                    {
                        isInherited = hierarchyMap.BaseMap.ScalarProperties.Any(bp =>
                            string.Equals(bp.ColumnName, prop.ColumnName, StringComparison.OrdinalIgnoreCase));
                    }

                    if (!isInherited)
                    {
                        var colAlias = $"{baseAlias}_{prop.ColumnName}";
                        columns.Add($"{QuoteIdentifier(tableAlias)}.{QuoteIdentifier(prop.ColumnName)} AS {QuoteIdentifier(colAlias)}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Dodaje INNER JOIN do tabel rodziców dla TPT entity w Include.
    /// </summary>
    private void AddTPTParentJoinsForInclude(SqlQueryBuilder builder, EntityMap map, string baseAlias)
    {
        var current = map.BaseMap;
        var childAlias = baseAlias;

        while (current != null)
        {
            var parentAlias = $"{baseAlias}_p{current.EntityType.Name}";
            var condition = $"{QuoteIdentifier(childAlias)}.{QuoteIdentifier(map.KeyProperty.ColumnName!)} = {QuoteIdentifier(parentAlias)}.{QuoteIdentifier(current.KeyProperty.ColumnName!)}";

            builder.InnerJoin(QuoteIdentifier(current.TableName), QuoteIdentifier(parentAlias), condition);

            childAlias = parentAlias;
            current = current.BaseMap;
        }
    }

    // ==================== HELPER METHODS ====================

    private string BuildJoinCondition(JoinClause join, string primaryAlias)
    {
        var leftAlias = join.ParentAlias ?? primaryAlias;
        var leftCol = $"{QuoteIdentifier(leftAlias)}.{QuoteIdentifier(join.LeftProperty.ColumnName!)}";
        var rightCol = $"{QuoteIdentifier(join.Alias!)}.{QuoteIdentifier(join.RightProperty.ColumnName!)}";
        return $"{leftCol} = {rightCol}";
    }

    /// <summary>
    /// Buduje warunek złączenia (ON ...) dla IncludeJoinInfo.
    /// Obsługuje relacje FK zarówno po stronie dziecka, jak i rodzica.
    /// </summary>
    private string BuildJoinCondition(IncludeJoinInfo info, string parentAlias)
    {
        // 1. Pobieramy wewnętrzny obiekt JoinClause
        var joinClause = info.Join; 
        
        // 2. Alias tabeli dołączanej (target/child)
        var childAlias = info.TableAlias;

        // 3. Budujemy warunek JOIN
        // ParentAlias jest już poprawnie ustawiony w ProcessIncludeRecursively
        var leftAlias = joinClause.ParentAlias ?? parentAlias;

        var leftCol = $"{QuoteIdentifier(leftAlias)}.{QuoteIdentifier(joinClause.LeftProperty.ColumnName!)}";
        var rightCol = $"{QuoteIdentifier(childAlias)}.{QuoteIdentifier(joinClause.RightProperty.ColumnName!)}";
        
        return $"{leftCol} = {rightCol}";
    }

    /// <summary>
    /// Buduje warunek JOIN dla TPC UNION - używa lokalnego aliasu tabeli zamiast globalnego ParentAlias.
    /// </summary>
    private string BuildJoinConditionForTPC(JoinClause joinClause, string localParentAlias, string childAlias)
    {
        // Dla TPC w UNION każda tabela ma swój własny alias (np. "employees", "teachers")
        // Ignorujemy joinClause.ParentAlias (który jest globalny) i używamy localParentAlias
        var leftCol = $"{QuoteIdentifier(localParentAlias)}.{QuoteIdentifier(joinClause.LeftProperty.ColumnName!)}";
        var rightCol = $"{QuoteIdentifier(childAlias)}.{QuoteIdentifier(joinClause.RightProperty.ColumnName!)}";
        
        return $"{leftCol} = {rightCol}";
    }

    private string BuildColumnReference(PropertyMap property, string tableAlias)
    {
        return $"{QuoteIdentifier(tableAlias)}.{QuoteIdentifier(property.ColumnName!)}";
    }

    private string AddAliasesToWhereClause(string whereClause, string tableAlias, EntityMap map)
    {
        if (string.IsNullOrEmpty(whereClause) || string.IsNullOrEmpty(tableAlias))
            return whereClause;

        // WHERE clause z ExpressionToSqlConverter już ma aliasy (np. "animals"."Name" = @Name0)
        // lub jest w prostej formie z GenerateSelect (już ma alias)
        // Więc tylko zwracamy bez zmian
        
        // UWAGA: ExpressionToSqlConverter generuje quoted identifiers z aliasem już wbudowanym
        // Jeśli w przyszłości będzie problem, można dodać regex replacement tutaj
        
        return whereClause;
    }

    /// <summary>
    /// Usuwa aliasy tabel z WHERE clause dla TPC (tabele nie mają aliasów w FROM).
    /// Zamienia "tableAlias"."columnName" na "columnName".
    /// </summary>
    private string RemoveAliasesFromWhereClause(string whereClause, EntityMap map)
    {
        if (string.IsNullOrEmpty(whereClause))
            return whereClause;

        // WHERE clause ma format: "tableAlias"."columnName" OPERATOR value
        // Musimy zamienić na: "columnName" OPERATOR value
        
        // Regex pattern: "alias"."column" → "column"
        // Przykład: "student"."semester" > @p0 → "semester" > @p0
        
        var result = whereClause;
        
        // Znajdź wszystkie wystąpienia quoted identifier przed kropką
        // Pattern: "anyAlias"."columnName" → "columnName"
        result = System.Text.RegularExpressions.Regex.Replace(result, @"""[^""]+""\.", "", RegexOptions.IgnoreCase);
        
        return result;
    }

    // ==================== NOT IMPLEMENTED (INSERT/UPDATE/DELETE) ====================
    // ==================== INSERT/UPDATE/DELETE OPERATIONS ====================

    public SqlQuery GenerateInsert(EntityMap map, object entity)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        if (!IsEntityCompatible(map, entity))
            throw new ArgumentException($"Entity type {entity.GetType().Name} is not compatible with EntityMap for {map.EntityType.Name}");

        // Deleguj do odpowiedniej metody według strategii
        if (map.InheritanceStrategy is TablePerTypeStrategy && map.BaseMap != null)
        {
            return GenerateInsertTPT(map, entity);
        }
        else if (map.InheritanceStrategy is TablePerConcreteClassStrategy)
        {
            return GenerateInsertTPC(map, entity);
        }
        else
        {
            // TPH lub brak dziedziczenia
            return GenerateInsertSimple(map, entity);
        }
    }

    public SqlQuery GenerateUpdate(EntityMap map, object entity)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        if (!IsEntityCompatible(map, entity))
            throw new ArgumentException($"Entity type {entity.GetType().Name} is not compatible with EntityMap for {map.EntityType.Name}");

        // Deleguj do odpowiedniej metody według strategii
        if (map.InheritanceStrategy is TablePerTypeStrategy && map.BaseMap != null)
        {
            return GenerateUpdateTPT(map, entity);
        }
        else if (map.InheritanceStrategy is TablePerConcreteClassStrategy)
        {
            return GenerateUpdateTPC(map, entity);
        }
        else
        {
            // TPH lub brak dziedziczenia
            return GenerateUpdateSimple(map, entity);
        }
    }

    public SqlQuery GenerateDelete(EntityMap map, object entity)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        if (!IsEntityCompatible(map, entity))
            throw new ArgumentException($"Entity type {entity.GetType().Name} is not compatible with EntityMap for {map.EntityType.Name}");

        // Deleguj do odpowiedniej metody według strategii
        if (map.InheritanceStrategy is TablePerTypeStrategy && map.BaseMap != null)
        {
            return GenerateDeleteTPT(map, entity);
        }
        else if (map.InheritanceStrategy is TablePerConcreteClassStrategy)
        {
            return GenerateDeleteTPC(map, entity);
        }
        else
        {
            // TPH lub brak dziedziczenia
            return GenerateDeleteSimple(map, entity);
        }
    }

    // ==================== INSERT IMPLEMENTATIONS ====================

    /// <summary>
    /// INSERT dla TPH lub brak dziedziczenia.
    /// Wstawia do jednej tabeli, dla TPH dodaje Discriminator.
    /// </summary>
    private SqlQuery GenerateInsertSimple(EntityMap map, object entity)
    {
        var keyPropertyName = map.InheritanceStrategy is TablePerHierarchyStrategy
            ? map.RootMap.KeyProperty.ColumnName
            : map.KeyProperty.ColumnName;

        var shouldSkipKey = map.InheritanceStrategy is TablePerHierarchyStrategy
            ? map.RootMap.HasAutoIncrementKey
            : map.HasAutoIncrementKey;

        var propsToInsert = map.ScalarProperties
            .Where(p => !(string.Equals(p.ColumnName, keyPropertyName, StringComparison.OrdinalIgnoreCase) && shouldSkipKey))
            .ToList();

        var columnNames = propsToInsert.Select(p => QuoteIdentifier(p.ColumnName!)).ToList();
        var paramNames = propsToInsert.Select(p => $"@{p.PropertyInfo.Name}").ToList();

        // Dodaj Discriminator dla TPH
        if (map.InheritanceStrategy is TablePerHierarchyStrategy tphStrategy)
        {
            columnNames.Add(QuoteIdentifier(tphStrategy.DiscriminatorColumn));
            paramNames.Add($"@{tphStrategy.DiscriminatorColumn}");
        }

        var builder = new SqlQueryBuilder();
        builder.InsertInto(QuoteIdentifier(map.TableName), columnNames)
               .Values(paramNames);

        var sqlText = builder.ToString();

        if (shouldSkipKey)
        {
            sqlText += "; SELECT last_insert_rowid();";
        }

        var parameters = new Dictionary<string, object>();
        foreach (var prop in propsToInsert)
        {
            var value = prop.PropertyInfo.GetValue(entity);
            parameters[$"@{prop.PropertyInfo.Name}"] = value ?? DBNull.Value;
        }

        if (map.InheritanceStrategy is TablePerHierarchyStrategy tphStrat)
        {
            parameters[$"@{tphStrat.DiscriminatorColumn}"] = tphStrat.DiscriminatorValue;
        }

        return new SqlQuery { Sql = sqlText, Parameters = parameters };
    }

    /// <summary>
    /// INSERT dla TPT - wstawia do wszystkich tabel w hierarchii (od roota w dół).
    /// </summary>
    private SqlQuery GenerateInsertTPT(EntityMap map, object entity)
    {
        var sqlStatements = new List<string>();
        var parameters = new Dictionary<string, object>();

        // Zbierz hierarchię (od roota do derived)
        var hierarchy = new List<EntityMap>();
        var current = map;
        while (current != null)
        {
            hierarchy.Add(current);
            current = current.BaseMap;
        }
        hierarchy.Reverse(); // Root → Derived

        var rootMap = hierarchy[0];

        foreach (var hierarchyMap in hierarchy)
        {
            var propsToInsert = new List<PropertyMap>();

            if (hierarchyMap.BaseMap == null)
            {
                // Root: wszystkie właściwości oprócz auto-increment key
                propsToInsert = hierarchyMap.ScalarProperties
                    .Where(p => !(string.Equals(p.ColumnName, hierarchyMap.KeyProperty.ColumnName, StringComparison.OrdinalIgnoreCase)
                        && hierarchyMap.HasAutoIncrementKey))
                    .ToList();
            }
            else
            {
                // Derived: dodaj key + tylko nowe właściwości (nie dziedziczone)
                var keyProp = hierarchyMap.ScalarProperties.FirstOrDefault(p =>
                    string.Equals(p.ColumnName, hierarchyMap.KeyProperty.ColumnName, StringComparison.OrdinalIgnoreCase));

                if (keyProp != null)
                {
                    propsToInsert.Add(keyProp);
                }

                foreach (var prop in hierarchyMap.ScalarProperties)
                {
                    var isInherited = hierarchyMap.BaseMap!.ScalarProperties.Any(bp =>
                        string.Equals(bp.ColumnName, prop.ColumnName, StringComparison.OrdinalIgnoreCase));

                    if (!isInherited && prop != keyProp)
                    {
                        propsToInsert.Add(prop);
                    }
                }
            }

            if (propsToInsert.Any())
            {
                var columnNames = propsToInsert.Select(p => QuoteIdentifier(p.ColumnName!)).ToList();
                var paramNames = new List<string>();

                foreach (var prop in propsToInsert)
                {
                    if (string.Equals(prop.ColumnName, hierarchyMap.KeyProperty.ColumnName, StringComparison.OrdinalIgnoreCase)
                        && hierarchyMap.BaseMap != null)
                    {
                        // Dla derived używamy last_insert_rowid() z roota
                        paramNames.Add("(SELECT last_insert_rowid())");
                    }
                    else
                    {
                        paramNames.Add($"@{prop.PropertyInfo.Name}");
                    }
                }

                var insertSql = $"INSERT INTO {QuoteIdentifier(hierarchyMap.TableName)} ({string.Join(", ", columnNames)}) VALUES ({string.Join(", ", paramNames)})";
                sqlStatements.Add(insertSql);

                // Parametry
                foreach (var prop in propsToInsert)
                {
                    var paramName = $"@{prop.PropertyInfo.Name}";
                    if (!parameters.ContainsKey(paramName))
                    {
                        var value = prop.PropertyInfo.GetValue(entity);
                        parameters[paramName] = value ?? DBNull.Value;
                    }
                }
            }
        }

        if (rootMap.HasAutoIncrementKey)
        {
            sqlStatements.Add("SELECT last_insert_rowid()");
        }

        return new SqlQuery
        {
            Sql = string.Join("; ", sqlStatements),
            Parameters = parameters
        };
    }

    /// <summary>
    /// INSERT dla TPC - wstawia tylko do tabeli konkretnej klasy.
    /// </summary>
    private SqlQuery GenerateInsertTPC(EntityMap map, object entity)
    {
        // TPC: każda klasa ma kompletny zestaw kolumn (włącznie z dziedziczonymi)
        var shouldSkipKey = map.HasAutoIncrementKey;

        var propsToInsert = map.ScalarProperties
            .Where(p => !(string.Equals(p.ColumnName, map.KeyProperty.ColumnName, StringComparison.OrdinalIgnoreCase) && shouldSkipKey))
            .ToList();

        var columnNames = propsToInsert.Select(p => QuoteIdentifier(p.ColumnName!)).ToList();
        var paramNames = propsToInsert.Select(p => $"@{p.PropertyInfo.Name}").ToList();

        var builder = new SqlQueryBuilder();
        builder.InsertInto(QuoteIdentifier(map.TableName), columnNames)
               .Values(paramNames);

        var sqlText = builder.ToString();

        if (shouldSkipKey)
        {
            sqlText += "; SELECT last_insert_rowid();";
        }

        var parameters = new Dictionary<string, object>();
        foreach (var prop in propsToInsert)
        {
            var value = prop.PropertyInfo.GetValue(entity);
            parameters[$"@{prop.PropertyInfo.Name}"] = value ?? DBNull.Value;
        }

        return new SqlQuery { Sql = sqlText, Parameters = parameters };
    }

    // ==================== UPDATE IMPLEMENTATIONS ====================

    /// <summary>
    /// UPDATE dla TPH lub brak dziedziczenia.
    /// Dla TPH dodaje warunek na Discriminator.
    /// </summary>
    private SqlQuery GenerateUpdateSimple(EntityMap map, object entity)
    {

        var tableName = map.TableName;
        var keyColName = map.KeyProperty.ColumnName!;

        if (map.InheritanceStrategy is TablePerHierarchyStrategy)
        {
            tableName = map.RootMap.TableName;
            keyColName = map.RootMap.KeyProperty.ColumnName!;
        }
        var propsToUpdate = map.ScalarProperties
            .Where(p => p != map.KeyProperty)
            .ToList();

        var assignments = propsToUpdate
            .Select(p => $"{QuoteIdentifier(p.ColumnName!)} = @{p.PropertyInfo.Name}");

        var builder = new SqlQueryBuilder();
        builder.Update(QuoteIdentifier(tableName))
               .Set(assignments);

        // WHERE: Key + opcjonalnie Discriminator (TPH)
        if (map.InheritanceStrategy is TablePerHierarchyStrategy tphStrategy && !string.IsNullOrEmpty(map.Discriminator))
        {
            builder.Where($"{QuoteIdentifier(keyColName)} = @{map.KeyProperty.PropertyInfo.Name} AND {QuoteIdentifier(tphStrategy.DiscriminatorColumn)} = @Discriminator");
        }
        else
        {
            builder.Where($"{QuoteIdentifier(keyColName)} = @{map.KeyProperty.PropertyInfo.Name}");
        }

        var parameters = new Dictionary<string, object>();

        foreach (var prop in propsToUpdate)
        {
            var value = prop.PropertyInfo.GetValue(entity);
            parameters[$"@{prop.PropertyInfo.Name}"] = value ?? DBNull.Value;
        }

        var keyValue = map.KeyProperty.PropertyInfo.GetValue(entity);
        parameters[$"@{map.KeyProperty.PropertyInfo.Name}"] = keyValue ?? DBNull.Value;

        if (map.InheritanceStrategy is TablePerHierarchyStrategy tphStrat && !string.IsNullOrEmpty(map.Discriminator))
        {
            parameters["@Discriminator"] = tphStrat.DiscriminatorValue;
        }

        return new SqlQuery { Sql = builder.ToString(), Parameters = parameters };
    }

    /// <summary>
    /// UPDATE dla TPT - aktualizuje wszystkie tabele w hierarchii.
    /// </summary>
    private SqlQuery GenerateUpdateTPT(EntityMap map, object entity)
    {
        var sqlStatements = new List<string>();
        var parameters = new Dictionary<string, object>();

        // Zbierz hierarchię (od roota do derived)
        var hierarchy = new List<EntityMap>();
        var current = map;
        while (current != null)
        {
            hierarchy.Add(current);
            current = current.BaseMap;
        }
        hierarchy.Reverse();

        var keyValue = map.KeyProperty.PropertyInfo.GetValue(entity);
        var keyParamName = $"@{map.KeyProperty.PropertyInfo.Name}";
        parameters[keyParamName] = keyValue ?? DBNull.Value;

        foreach (var hierarchyMap in hierarchy)
        {
            var propsToUpdate = new List<PropertyMap>();

            if (hierarchyMap.BaseMap == null)
            {
                // Root: wszystkie właściwości oprócz klucza
                propsToUpdate = hierarchyMap.ScalarProperties
                    .Where(p => !string.Equals(p.ColumnName, hierarchyMap.KeyProperty.ColumnName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            else
            {
                // Derived: tylko nowe właściwości (nie dziedziczone)
                foreach (var prop in hierarchyMap.ScalarProperties)
                {
                    var isInherited = hierarchyMap.BaseMap!.ScalarProperties.Any(bp =>
                        string.Equals(bp.ColumnName, prop.ColumnName, StringComparison.OrdinalIgnoreCase));

                    if (!isInherited)
                    {
                        propsToUpdate.Add(prop);
                    }
                }
            }

            if (propsToUpdate.Any())
            {
                var assignments = propsToUpdate.Select(p =>
                    $"{QuoteIdentifier(p.ColumnName!)} = @{p.PropertyInfo.Name}");

                var updateSql = $"UPDATE {QuoteIdentifier(hierarchyMap.TableName)} SET {string.Join(", ", assignments)} WHERE {QuoteIdentifier(hierarchyMap.KeyProperty.ColumnName!)} = {keyParamName}";
                sqlStatements.Add(updateSql);

                // Parametry
                foreach (var prop in propsToUpdate)
                {
                    var paramName = $"@{prop.PropertyInfo.Name}";
                    if (!parameters.ContainsKey(paramName))
                    {
                        var value = prop.PropertyInfo.GetValue(entity);
                        parameters[paramName] = value ?? DBNull.Value;
                    }
                }
            }
        }

        return new SqlQuery
        {
            Sql = string.Join("; ", sqlStatements),
            Parameters = parameters
        };
    }

    /// <summary>
    /// UPDATE dla TPC - aktualizuje tylko tabelę konkretnej klasy.
    /// </summary>
    private SqlQuery GenerateUpdateTPC(EntityMap map, object entity)
    {
        var propsToUpdate = map.ScalarProperties
            .Where(p => p != map.KeyProperty)
            .ToList();

        var assignments = propsToUpdate
            .Select(p => $"{QuoteIdentifier(p.ColumnName!)} = @{p.PropertyInfo.Name}");

        var builder = new SqlQueryBuilder();
        builder.Update(QuoteIdentifier(map.TableName))
               .Set(assignments)
               .Where($"{QuoteIdentifier(map.KeyProperty.ColumnName!)} = @{map.KeyProperty.PropertyInfo.Name}");

        var parameters = new Dictionary<string, object>();

        foreach (var prop in propsToUpdate)
        {
            var value = prop.PropertyInfo.GetValue(entity);
            parameters[$"@{prop.PropertyInfo.Name}"] = value ?? DBNull.Value;
        }

        var keyValue = map.KeyProperty.PropertyInfo.GetValue(entity);
        parameters[$"@{map.KeyProperty.PropertyInfo.Name}"] = keyValue ?? DBNull.Value;

        return new SqlQuery { Sql = builder.ToString(), Parameters = parameters };
    }

    // ==================== DELETE IMPLEMENTATIONS ====================

    /// <summary>
    /// DELETE dla TPH lub brak dziedziczenia.
    /// Dla TPH dodaje warunek na Discriminator.
    /// </summary>
    private SqlQuery GenerateDeleteSimple(EntityMap map, object entity)
    {
        var builder = new SqlQueryBuilder();
        builder.DeleteFrom(QuoteIdentifier(map.TableName));

        // WHERE: Key + opcjonalnie Discriminator (TPH)
        if (map.InheritanceStrategy is TablePerHierarchyStrategy tphStrategy && !string.IsNullOrEmpty(map.Discriminator))
        {
            builder.Where($"{QuoteIdentifier(map.KeyProperty.ColumnName!)} = @id AND {QuoteIdentifier(tphStrategy.DiscriminatorColumn)} = @Discriminator");
        }
        else
        {
            builder.Where($"{QuoteIdentifier(map.KeyProperty.ColumnName!)} = @id");
        }

        var idValue = map.KeyProperty.PropertyInfo.GetValue(entity);
        var parameters = new Dictionary<string, object>
        {
            { "@id", idValue ?? DBNull.Value }
        };

        if (map.InheritanceStrategy is TablePerHierarchyStrategy tphStrat && !string.IsNullOrEmpty(map.Discriminator))
        {
            parameters["@Discriminator"] = tphStrat.DiscriminatorValue;
        }

        return new SqlQuery { Sql = builder.ToString(), Parameters = parameters };
    }

    /// <summary>
    /// DELETE dla TPT - usuwa z wszystkich tabel w hierarchy (od derived do roota).
    /// </summary>
    private SqlQuery GenerateDeleteTPT(EntityMap map, object entity)
    {
        var sqlStatements = new List<string>();

        // Zbierz hierarchię (od derived do roota - odwrotna kolejność)
        var hierarchy = new List<EntityMap>();
        var current = map;
        while (current != null)
        {
            hierarchy.Add(current);
            current = current.BaseMap;
        }
        // NIE reverse - chcemy usuwać od derived do roota (FK constraints)

        var idValue = map.KeyProperty.PropertyInfo.GetValue(entity);

        foreach (var hierarchyMap in hierarchy)
        {
            var deleteSql = $"DELETE FROM {QuoteIdentifier(hierarchyMap.TableName)} WHERE {QuoteIdentifier(hierarchyMap.KeyProperty.ColumnName!)} = @id";
            sqlStatements.Add(deleteSql);
        }

        return new SqlQuery
        {
            Sql = string.Join("; ", sqlStatements),
            Parameters = new Dictionary<string, object>
            {
                { "@id", idValue ?? DBNull.Value }
            }
        };
    }

    /// <summary>
    /// DELETE dla TPC - usuwa tylko z tabeli konkretnej klasy.
    /// </summary>
    private SqlQuery GenerateDeleteTPC(EntityMap map, object entity)
    {
        var builder = new SqlQueryBuilder();
        builder.DeleteFrom(QuoteIdentifier(map.TableName))
               .Where($"{QuoteIdentifier(map.KeyProperty.ColumnName!)} = @id");

        var idValue = map.KeyProperty.PropertyInfo.GetValue(entity);

        return new SqlQuery
        {
            Sql = builder.ToString(),
            Parameters = new Dictionary<string, object>
            {
                { "@id", idValue ?? DBNull.Value }
            }
        };
    }

    private static bool IsEntityCompatible(EntityMap map, object entity)
    {
        if (entity == null) return false;
        var actualType = entity.GetType();
        return actualType == map.EntityType;
    }
}
