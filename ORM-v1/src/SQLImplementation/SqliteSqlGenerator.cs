using ORM_v1.Mapping;
using ORM_v1.Mapping.Strategies;
using ORM_v1.Query;
using System.Data;

public class SqliteSqlGeneratorOLD : ISqlGenerator
{
    private IMetadataStore? _metadataStore;

    public string GetParameterName(string name, int index)
    {
        return $"@{name}{index}";
    }

    public string QuoteIdentifier(string name)
    {
        return $"\"{name}\"";
    }

    /// <summary>
    /// Zwraca alias tabeli dla danej encji według strategii dziedziczenia.
    /// </summary>
    public string GetTableAlias(EntityMap map, IMetadataStore metadataStore)
    {
        _metadataStore = metadataStore;
        
        if (map.InheritanceStrategy is TablePerTypeStrategy)
        {
            return $"t{map.EntityType.Name}";
        }
        
        return map.TableName.ToLowerInvariant();
    }

    public SqlQuery GenerateSelect(EntityMap map, object id, IMetadataStore metadataStore)
    {
        _metadataStore = metadataStore;
        var builder = new SqlQueryBuilder();

        var columns = GetColumnsForSelect(map);
        var columnsFiltered = FilterNullStrings(columns);

        if (columnsFiltered.Count() == 0)
            throw new InvalidOperationException("No columns to select.");

        builder.Select(columnsFiltered);

        BuildFromClauseWithInheritance(builder, map);

        var whereConditions = new List<string>();

        var keyColumnRef = map.InheritanceStrategy is TablePerTypeStrategy
            ? $"{QuoteIdentifier($"t{map.EntityType.Name}")}.{QuoteIdentifier(map.KeyProperty.ColumnName!)}"
            : QuoteIdentifier(map.KeyProperty.ColumnName!);

        whereConditions.Add($"{keyColumnRef} = @id");

        // ✅ Użyj wspólnej metody dla TPH discriminator filtering
        var tableAliasForDiscriminator = map.InheritanceStrategy is TablePerTypeStrategy ? $"t{map.EntityType.Name}" : null;
        var (discriminatorWhere, discriminatorParams) = BuildDiscriminatorFilter(map, metadataStore, tableAliasForDiscriminator);
        if (!string.IsNullOrEmpty(discriminatorWhere))
        {
            whereConditions.Add(discriminatorWhere);
        }

        builder.Where(string.Join(" AND ", whereConditions));

        var parameters = new Dictionary<string, object>
        {
            { "@id", id }
        };

        // Dodaj parametry discriminatora
        foreach (var param in discriminatorParams)
        {
            parameters[param.Key] = param.Value;
        }

        var sqlQuery = new SqlQuery()
        {
            Sql = builder.ToString(),
            Parameters = parameters
        };
        return sqlQuery;
    }

    public SqlQuery GenerateSelectAll(EntityMap map, IMetadataStore metadataStore)
    {
        _metadataStore = metadataStore;
        var builder = new SqlQueryBuilder();

        var columns = GetColumnsForSelect(map);
        var columnsFiltered = FilterNullStrings(columns);

        if (columnsFiltered.Count() == 0)
            throw new InvalidOperationException("No columns to select.");

        builder.Select(columnsFiltered);

        BuildFromClauseWithInheritance(builder, map);

        // ✅ Użyj wspólnej metody dla TPH discriminator filtering
        var (discriminatorWhere, parameters) = BuildDiscriminatorFilter(map, metadataStore, null);

        if (!string.IsNullOrEmpty(discriminatorWhere))
        {
            builder.Where(discriminatorWhere);
        }

        var sqlQuery = new SqlQuery()
        {
            Sql = builder.ToString(),
            Parameters = parameters
        };
        return sqlQuery;
    }

    public SqlQuery GenerateInsert(EntityMap map, object entity)
    {
        if (!IsEntityCompatible(map, entity))
            throw new ArgumentException("Entity type is not compatible with the provided EntityMap.", nameof(entity));

        if (map.InheritanceStrategy is TablePerTypeStrategy && map.BaseMap != null)
        {
            return GenerateInsertForTablePerType(map, entity);
        }

        var keyPropertyName = map.InheritanceStrategy is TablePerHierarchyStrategy
            ? map.RootMap.KeyProperty.ColumnName
            : map.KeyProperty.ColumnName;

        var shouldSkipKey = map.InheritanceStrategy is TablePerHierarchyStrategy
            ? map.RootMap.HasAutoIncrementKey
            : map.HasAutoIncrementKey;

        var propsToInsert = map.ScalarProperties
            .Where(p => !(string.Equals(p.ColumnName, keyPropertyName, StringComparison.OrdinalIgnoreCase) && shouldSkipKey))
            .ToList();

        var columnNames = new List<string>(propsToInsert.Select(p => p.ColumnName!));
        var paramNames = new List<string>(propsToInsert.Select(p => $"@{p.PropertyInfo.Name}"));

        if (map.InheritanceStrategy is TablePerHierarchyStrategy tphStrategy)
        {
            columnNames.Add(QuoteIdentifier(tphStrategy.DiscriminatorColumn));
            paramNames.Add($"@{tphStrategy.DiscriminatorColumn}");
        }

        var columnsFiltered = FilterNullStrings(columnNames);

        if (columnsFiltered.Count() == 0)
            throw new InvalidOperationException("No columns to insert.");

        var builder = new SqlQueryBuilder();
        builder.InsertInto(QuoteIdentifier(map.TableName), columnsFiltered)
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

        return new SqlQuery
        {
            Sql = sqlText,
            Parameters = parameters
        };
    }

    private SqlQuery GenerateInsertForTablePerType(EntityMap map, object entity)
    {
        var sqlStatements = new List<string>();
        var parameters = new Dictionary<string, object>();

        var currentMap = map;
        var hierarchy = new List<EntityMap>();

        while (currentMap != null)
        {
            hierarchy.Add(currentMap);
            currentMap = currentMap.BaseMap;
        }

        hierarchy.Reverse();

        var rootMap = hierarchy[0];
        var rootKeyParamName = $"@{rootMap.KeyProperty.PropertyInfo.Name}";

        for (int i = 0; i < hierarchy.Count; i++)
        {
            var hierarchyMap = hierarchy[i];
            var propsToInsert = new List<PropertyMap>();

            if (hierarchyMap.BaseMap == null)
            {
                propsToInsert = hierarchyMap.ScalarProperties
                    .Where(p => !(string.Equals(p.ColumnName, hierarchyMap.KeyProperty.ColumnName, StringComparison.OrdinalIgnoreCase)
                        && hierarchyMap.HasAutoIncrementKey))
                    .ToList();
            }
            else
            {
                var keyProp = hierarchyMap.ScalarProperties.FirstOrDefault(p =>
                    string.Equals(p.ColumnName, hierarchyMap.KeyProperty.ColumnName, StringComparison.OrdinalIgnoreCase));

                if (keyProp != null)
                {
                    propsToInsert.Add(keyProp);
                }

                foreach (var prop in hierarchyMap.ScalarProperties)
                {
                    var isInheritedColumn = hierarchyMap.BaseMap.ScalarProperties.Any(bp =>
                        string.Equals(bp.ColumnName, prop.ColumnName, StringComparison.OrdinalIgnoreCase));

                    if (!isInheritedColumn && prop != keyProp)
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
                        paramNames.Add("(SELECT last_insert_rowid())");
                    }
                    else
                    {
                        paramNames.Add($"@{prop.PropertyInfo.Name}");
                    }
                }

                var insertSql = $"INSERT INTO {QuoteIdentifier(hierarchyMap.TableName)} ({string.Join(", ", columnNames)}) VALUES ({string.Join(", ", paramNames)})";

                sqlStatements.Add(insertSql);

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

    public SqlQuery GenerateUpdate(EntityMap map, object entity)
    {
        if (!IsEntityCompatible(map, entity))
            throw new ArgumentException("Entity type is not compatible with the provided EntityMap.", nameof(entity));

        if (map.InheritanceStrategy is TablePerTypeStrategy && map.BaseMap != null)
        {
            return GenerateUpdateForTablePerType(map, entity);
        }

        var tableName = map.TableName;
        if (map.InheritanceStrategy is TablePerHierarchyStrategy)
        {
            tableName = map.RootMap.TableName;
        }

        var propsToUpdate = map.ScalarProperties
            .Where(p => p != map.KeyProperty)
            .ToList();

        var assignments = propsToUpdate
            .Select(p => $"{QuoteIdentifier(p.ColumnName!)} = @{p.PropertyInfo.Name}");

        var builder = new SqlQueryBuilder();
        builder.Update(QuoteIdentifier(tableName))
            .Set(assignments);

        if (map.InheritanceStrategy is TablePerHierarchyStrategy tphStrategy && !string.IsNullOrEmpty(map.Discriminator))
        {
            builder.Where($"{QuoteIdentifier(map.KeyProperty.ColumnName!)} = @{map.KeyProperty.PropertyInfo.Name} AND {QuoteIdentifier(tphStrategy.DiscriminatorColumn)} = @Discriminator");
        }
        else
        {
            builder.Where($"{QuoteIdentifier(map.KeyProperty.ColumnName!)} = @{map.KeyProperty.PropertyInfo.Name}");
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

        return new SqlQuery
        {
            Sql = builder.ToString(),
            Parameters = parameters
        };
    }

    private SqlQuery GenerateUpdateForTablePerType(EntityMap map, object entity)
    {
        var sqlStatements = new List<string>();
        var parameters = new Dictionary<string, object>();

        var currentMap = map;
        var hierarchy = new List<EntityMap>();

        while (currentMap != null)
        {
            hierarchy.Add(currentMap);
            currentMap = currentMap.BaseMap;
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
                propsToUpdate = hierarchyMap.ScalarProperties
                    .Where(p => !string.Equals(p.ColumnName, hierarchyMap.KeyProperty.ColumnName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            else
            {
                foreach (var prop in hierarchyMap.ScalarProperties)
                {
                    var isInheritedColumn = hierarchyMap.BaseMap.ScalarProperties.Any(bp =>
                        string.Equals(bp.ColumnName, prop.ColumnName, StringComparison.OrdinalIgnoreCase));

                    if (!isInheritedColumn)
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

    public SqlQuery GenerateDelete(EntityMap map, object entity)
    {
        if (!IsEntityCompatible(map, entity))
            throw new ArgumentException("Entity type is not compatible with the provided EntityMap.", nameof(entity));

        if (map.InheritanceStrategy is TablePerTypeStrategy && map.BaseMap != null)
        {
            return GenerateDeleteForTablePerType(map, entity);
        }

        var builder = new SqlQueryBuilder();
        builder.DeleteFrom(QuoteIdentifier(map.TableName));

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

        return new SqlQuery
        {
            Sql = builder.ToString(),
            Parameters = parameters
        };
    }

    private SqlQuery GenerateDeleteForTablePerType(EntityMap map, object entity)
    {
        var sqlStatements = new List<string>();

        var currentMap = map;
        var hierarchy = new List<EntityMap>();

        while (currentMap != null)
        {
            hierarchy.Add(currentMap);
            currentMap = currentMap.BaseMap;
        }

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

    public SqlQuery GenerateComplexSelect(EntityMap map, QueryModel queryModel, IMetadataStore metadataStore)
    {
        _metadataStore = metadataStore;

        var builder = new SqlQueryBuilder();
        var parameters = new Dictionary<string, object>(queryModel.Parameters);

        bool hasJoins = queryModel.Joins.Any();
        // ✅ Zawsze używaj aliasu (nawszen bez JOIN-ów)
        string? primaryAlias = string.IsNullOrEmpty(queryModel.PrimaryEntityAlias)
            ? map.TableName.ToLowerInvariant()
            : queryModel.PrimaryEntityAlias;

        var selectColumns = BuildSelectColumns(map, queryModel, primaryAlias);

        builder.Select(selectColumns);

        builder.FromWithAlias(QuoteIdentifier(map.TableName),
            string.IsNullOrEmpty(primaryAlias)
                ? null
                : QuoteIdentifier(primaryAlias));

        if (map.InheritanceStrategy is TablePerTypeStrategy && map.BaseMap != null)
        {
            BuildTablePerTypeJoins(builder, map, primaryAlias);
        }

        foreach (var join in queryModel.Joins)
        {
            var joinTable = QuoteIdentifier(join.JoinedEntity.TableName);
            var joinAlias = string.IsNullOrEmpty(join.Alias) ? null : QuoteIdentifier(join.Alias);
            var onCondition = BuildJoinCondition(join, primaryAlias);

            switch (join.JoinType)
            {
                case JoinType.Inner:
                    builder.InnerJoin(joinTable, joinAlias, onCondition);
                    break;
                case JoinType.Left:
                    builder.LeftJoin(joinTable, joinAlias, onCondition);
                    break;
                case JoinType.Right:
                    builder.RightJoin(joinTable, joinAlias, onCondition);
                    break;
                case JoinType.Full:
                    builder.FullOuterJoin(joinTable, joinAlias, onCondition);
                    break;
            }
        }

        // ✅ Dodaj discriminator filtering dla TPH (jeśli nie ma własnego WHERE)
        var whereConditions = new List<string>();

        if (!string.IsNullOrEmpty(queryModel.WhereClause))
        {
            // ✅ Dodaj aliasy do nazw kolumn w WHERE clause
            var whereWithAlias = AddTableAliasToWhereClause(queryModel.WhereClause, primaryAlias, map);
            whereConditions.Add(whereWithAlias);
        }

        // Dodaj filtrowanie po discriminator (ale tylko jeśli user nie podał własnego warunku na discriminator)
        if (!queryModel.WhereClause?.Contains("Discriminator") ?? true)
        {
            var (discriminatorWhere, discriminatorParams) = BuildDiscriminatorFilter(map, metadataStore, primaryAlias);
            if (!string.IsNullOrEmpty(discriminatorWhere))
            {
                whereConditions.Add(discriminatorWhere);

                // Dodaj parametry discriminatora
                foreach (var param in discriminatorParams)
                {
                    parameters[param.Key] = param.Value;
                }
            }
        }

        if (whereConditions.Any())
        {
            builder.Where(string.Join(" AND ", whereConditions));
        }

        if (queryModel.GroupByColumns.Any())
        {
            var groupColumns = queryModel.GroupByColumns
                .Select(p => BuildColumnReference(p, primaryAlias));
            builder.GroupBy(groupColumns);
        }

        if (!string.IsNullOrEmpty(queryModel.HavingClause))
        {
            builder.Having(queryModel.HavingClause);
        }

        if (queryModel.OrderBy.Any())
        {
            var orderClauses = queryModel.OrderBy.Select(o =>
            {
                var colRef = BuildColumnReference(o.Property, o.TableAlias ?? primaryAlias);
                return $"{colRef} {(o.IsAscending ? "ASC" : "DESC")}";
            });
            builder.OrderBy(orderClauses);
        }

        if (queryModel.Take.HasValue)
        {
            builder.Limit(queryModel.Take.Value);
        }

        if (queryModel.Skip.HasValue)
        {
            builder.Offset(queryModel.Skip.Value);
        }

        return new SqlQuery
        {
            Sql = builder.ToString(),
            Parameters = parameters
        };
    }

    private IEnumerable<string> GetColumnsForSelect(EntityMap map)
    {
        var columns = new List<string>();

        if (map.InheritanceStrategy is TablePerTypeStrategy && map.BaseMap != null)
        {
            var derivedAlias = $"t{map.EntityType.Name}";

            foreach (var prop in map.ScalarProperties)
            {
                if (!string.IsNullOrEmpty(prop.ColumnName))
                {
                    var isDerivedColumn = !map.BaseMap.ScalarProperties.Any(bp =>
                        string.Equals(bp.ColumnName, prop.ColumnName, StringComparison.OrdinalIgnoreCase));

                    if (isDerivedColumn)
                    {
                        columns.Add($"{QuoteIdentifier(derivedAlias)}.{QuoteIdentifier(prop.ColumnName)}");
                    }
                }
            }

            var current = map.BaseMap;
            while (current != null)
            {
                var baseAlias = $"t{current.EntityType.Name}";

                foreach (var prop in current.ScalarProperties)
                {
                    if (!string.IsNullOrEmpty(prop.ColumnName))
                    {
                        var isInherited = current.BaseMap == null ||
                            !current.BaseMap.ScalarProperties.Any(bp =>
                                string.Equals(bp.ColumnName, prop.ColumnName, StringComparison.OrdinalIgnoreCase));

                        if (isInherited)
                        {
                            columns.Add($"{QuoteIdentifier(baseAlias)}.{QuoteIdentifier(prop.ColumnName)}");
                        }
                    }
                }

                current = current.BaseMap;
            }
        }
        else if (map.InheritanceStrategy is TablePerTypeStrategy && map.IsAbstract && _metadataStore != null)
        {
            // ✅ Dla TPT + abstrakcyjna bazowa: dodaj kolumny z klasy bazowej i wszystkich klas pochodnych
            var baseAlias = $"t{map.EntityType.Name}";

            // Kolumny z klasy bazowej
            foreach (var prop in map.ScalarProperties)
            {
                if (!string.IsNullOrEmpty(prop.ColumnName))
                {
                    columns.Add($"{QuoteIdentifier(baseAlias)}.{QuoteIdentifier(prop.ColumnName)}");
                }
            }

            // Kolumny ze wszystkich klas pochodnych (rekurencyjnie)
            AddColumnsFromDerivedTypes(columns, map);
        }
        else if (map.InheritanceStrategy is TablePerHierarchyStrategy tphStrategy)
        {
            // ✅ Dla TPH: zwróć WSZYSTKIE kolumny z hierarchii!
            var rootMap = map.RootMap;
            var processedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Zbierz wszystkie mapy w hierarchii TPH
            var allMaps = new List<EntityMap>();
            CollectTPHHierarchy(rootMap, _metadataStore, allMaps);

            // Dodaj kolumny ze wszystkich klas w hierarchii
            foreach (var hierarchyMap in allMaps)
            {
                foreach (var prop in hierarchyMap.ScalarProperties)
                {
                    if (!string.IsNullOrEmpty(prop.ColumnName) && processedColumns.Add(prop.ColumnName))
                    {
                        columns.Add(QuoteIdentifier(prop.ColumnName));
                    }
                }

                // Dodaj Discriminator
                if (!string.IsNullOrEmpty(tphStrategy.DiscriminatorColumn) &&
                    processedColumns.Add(tphStrategy.DiscriminatorColumn))
                {
                    columns.Add(QuoteIdentifier(tphStrategy.DiscriminatorColumn));
                }
            }
        }
        else
        {
            foreach (var prop in map.ScalarProperties)
            {
                if (!string.IsNullOrEmpty(prop.ColumnName))
                {
                    columns.Add(QuoteIdentifier(prop.ColumnName));
                }
            }

            if (map.InheritanceStrategy is TablePerHierarchyStrategy tphStrategy2)
            {
                columns.Add(QuoteIdentifier(tphStrategy2.DiscriminatorColumn));
            }
        }

        return columns;
    }

    private void CollectTPHHierarchy(EntityMap rootMap, IMetadataStore? metadataStore, List<EntityMap> result)
    {
        result.Add(rootMap);

        if (metadataStore != null)
        {
            foreach (var map in metadataStore.GetAllMaps())
            {
                if (map != rootMap &&
                    map.InheritanceStrategy is TablePerHierarchyStrategy &&
                    map.RootMap == rootMap)
                {
                    result.Add(map);
                }
            }
        }
    }

    private void BuildFromClauseWithInheritance(SqlQueryBuilder builder, EntityMap map)
    {
        if (map.InheritanceStrategy is TablePerTypeStrategy && map.BaseMap != null)
        {
            var derivedAlias = $"t{map.EntityType.Name}";
            builder.From($"{QuoteIdentifier(map.TableName)} AS {QuoteIdentifier(derivedAlias)}");
            BuildTablePerTypeJoins(builder, map, derivedAlias);
        }
        else if (map.InheritanceStrategy is TablePerTypeStrategy && map.IsAbstract && _metadataStore != null)
        {
            // ✅ Dla TPT + abstrakcyjna klasa bazowa: dodaj LEFT JOIN do wszystkich klas pochodnych
            var baseAlias = $"t{map.EntityType.Name}";
            builder.From($"{QuoteIdentifier(map.TableName)} AS {QuoteIdentifier(baseAlias)}");
            BuildLeftJoinsToDerivedTypes(builder, map, baseAlias);
        }
        else
        {
            builder.From(QuoteIdentifier(map.TableName));
        }
    }

    private void BuildTablePerTypeJoins(SqlQueryBuilder builder, EntityMap map, string? primaryAlias)
    {
        var current = map.BaseMap;
        int level = 0;

        while (current != null)
        {
            var baseAlias = $"t{current.EntityType.Name}";

            var leftKey = string.IsNullOrEmpty(primaryAlias)
                ? QuoteIdentifier(map.KeyProperty.ColumnName!)
                : $"{QuoteIdentifier(primaryAlias)}.{QuoteIdentifier(map.KeyProperty.ColumnName!)}";

            var rightKey = $"{QuoteIdentifier(baseAlias)}.{QuoteIdentifier(current.KeyProperty.ColumnName!)}";

            var onCondition = $"{leftKey} = {rightKey}";

            builder.InnerJoin(QuoteIdentifier(current.TableName), QuoteIdentifier(baseAlias), onCondition);

            primaryAlias = baseAlias;
            current = current.BaseMap;
            level++;
        }
    }

    /// <summary>
    /// Dla TPT + abstrakcyjna klasa bazowa: dodaje LEFT JOIN do wszystkich konkretnych klas pochodnych.
    /// To pozwala na wykrycie typu pochodnego w ObjectMaterializer.
    /// </summary>
    private void BuildLeftJoinsToDerivedTypes(SqlQueryBuilder builder, EntityMap baseMap, string baseAlias)
    {
        if (_metadataStore == null) return;

        // Znajdź wszystkie bezpośrednie typy pochodne (tylko pierwszy poziom)
        var directDerivedMaps = _metadataStore.GetAllMaps()
            .Where(m => m.InheritanceStrategy is TablePerTypeStrategy &&
                        m.BaseMap == baseMap)
            .ToList();

        foreach (var derivedMap in directDerivedMaps)
        {
            var derivedAlias = $"t{derivedMap.EntityType.Name}";
            var onCondition = $"{QuoteIdentifier(baseAlias)}.{QuoteIdentifier(baseMap.KeyProperty.ColumnName!)} = {QuoteIdentifier(derivedAlias)}.{QuoteIdentifier(derivedMap.KeyProperty.ColumnName!)}";
            
            builder.LeftJoin(QuoteIdentifier(derivedMap.TableName), QuoteIdentifier(derivedAlias), onCondition);

            // Rekurencyjnie dodaj JOIN-y dla kolejnych poziomów (jeśli klasa pochodna też jest abstrakcyjna)
            if (derivedMap.IsAbstract)
            {
                BuildLeftJoinsToDerivedTypes(builder, derivedMap, derivedAlias);
            }
        }
    }

    /// <summary>
    /// Rekurencyjnie dodaje kolumny ze wszystkich klas pochodnych (dla TPT + abstrakcyjna bazowa).
    /// </summary>
    private void AddColumnsFromDerivedTypes(List<string> columns, EntityMap baseMap)
    {
        if (_metadataStore == null) return;

        // Znajdź wszystkie bezpośrednie typy pochodne
        var directDerivedMaps = _metadataStore.GetAllMaps()
            .Where(m => m.InheritanceStrategy is TablePerTypeStrategy &&
                        m.BaseMap == baseMap)
            .ToList();

        foreach (var derivedMap in directDerivedMaps)
        {
            var derivedAlias = $"t{derivedMap.EntityType.Name}";

            // Dodaj kolumny specyficzne dla tego typu (nie dziedziczone z rodzica)
            foreach (var prop in derivedMap.ScalarProperties)
            {
                if (!string.IsNullOrEmpty(prop.ColumnName))
                {
                    var isInheritedFromParent = baseMap.ScalarProperties.Any(bp =>
                        string.Equals(bp.ColumnName, prop.ColumnName, StringComparison.OrdinalIgnoreCase));

                    if (!isInheritedFromParent)
                    {
                        columns.Add($"{QuoteIdentifier(derivedAlias)}.{QuoteIdentifier(prop.ColumnName)}");
                    }
                }
            }

            // Rekurencyjnie dla dalszych poziomów
            if (derivedMap.IsAbstract)
            {
                AddColumnsFromDerivedTypes(columns, derivedMap);
            }
        }
    }

    private IEnumerable<string> BuildSelectColumns(EntityMap map, QueryModel queryModel, string? tableAlias)
    {
        if (queryModel.Aggregates.Any())
        {
            var aggregateColumns = new List<string>();

            foreach (var agg in queryModel.Aggregates)
            {
                var aggSql = BuildAggregateFunction(agg, tableAlias);
                aggregateColumns.Add(aggSql);
            }

            foreach (var groupCol in queryModel.GroupByColumns)
            {
                var colName = BuildColumnReference(groupCol, tableAlias);
                aggregateColumns.Add(colName);
            }

            return aggregateColumns;
        }
        else
        {
            // Zawsze zwracaj wszystkie kolumny (SELECT ALL)
            var columns = new List<string>();

            // ✅ Dla TPH: użyj tej samej logiki co GetColumnsForSelect!
            if (map.InheritanceStrategy is TablePerHierarchyStrategy tphStrategy)
            {
                var rootMap = map.RootMap;
                var processedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Zbierz wszystkie mapy w hierarchii TPH
                var allMaps = new List<EntityMap>();
                CollectTPHHierarchy(rootMap, _metadataStore, allMaps);

                // Dodaj kolumny ze wszystkich klas w hierarchii
                foreach (var hierarchyMap in allMaps)
                {
                    foreach (var prop in hierarchyMap.ScalarProperties)
                    {
                        if (!string.IsNullOrEmpty(prop.ColumnName) && processedColumns.Add(prop.ColumnName))
                        {
                            if (string.IsNullOrEmpty(tableAlias))
                            {
                                columns.Add(QuoteIdentifier(prop.ColumnName));
                            }
                            else if (queryModel.IncludeJoins.Any())
                            {
                                var columnWithAlias = $"{QuoteIdentifier(tableAlias)}.{QuoteIdentifier(prop.ColumnName)} AS {QuoteIdentifier(tableAlias + "_" + prop.ColumnName)}";
                                columns.Add(columnWithAlias);
                            }
                            else
                            {
                                var column = $"{QuoteIdentifier(tableAlias)}.{QuoteIdentifier(prop.ColumnName)}";
                                columns.Add(column);
                            }
                        }
                    }

                    // Discriminator
                    if (!string.IsNullOrEmpty(tphStrategy.DiscriminatorColumn) &&
                        processedColumns.Add(tphStrategy.DiscriminatorColumn))
                    {
                        if (string.IsNullOrEmpty(tableAlias))
                        {
                            columns.Add(QuoteIdentifier(tphStrategy.DiscriminatorColumn));
                        }
                        else if (queryModel.IncludeJoins.Any())
                        {
                            var discColumnWithAlias = $"{QuoteIdentifier(tableAlias)}.{QuoteIdentifier(tphStrategy.DiscriminatorColumn)} AS {QuoteIdentifier(tableAlias + "_" + tphStrategy.DiscriminatorColumn)}";
                            columns.Add(discColumnWithAlias);
                        }
                        else
                        {
                            var discColumn = $"{QuoteIdentifier(tableAlias)}.{QuoteIdentifier(tphStrategy.DiscriminatorColumn)}";
                            columns.Add(discColumn);
                        }
                    }
                }
            }
            else
            {
                // Primary entity columns (non-TPH)
                foreach (var prop in map.ScalarProperties)
                {
                    if (!string.IsNullOrEmpty(prop.ColumnName))
                    {
                        if (string.IsNullOrEmpty(tableAlias))
                        {
                            columns.Add(QuoteIdentifier(prop.ColumnName));
                        }
                        else if (queryModel.IncludeJoins.Any())
                        {
                            // Aliasuj kolumny aby uniknąć konfliktów nazw w JOIN
                            var columnWithAlias = $"{QuoteIdentifier(tableAlias)}.{QuoteIdentifier(prop.ColumnName)} AS {QuoteIdentifier(tableAlias + "_" + prop.ColumnName)}";
                            columns.Add(columnWithAlias);
                        }
                        else
                        {
                            var column = $"{QuoteIdentifier(tableAlias)}.{QuoteIdentifier(prop.ColumnName)}";
                            columns.Add(column);
                        }
                    }
                }

                if (map.InheritanceStrategy is TablePerHierarchyStrategy tphStrategy2)
                {
                    if (string.IsNullOrEmpty(tableAlias))
                    {
                        columns.Add(QuoteIdentifier(tphStrategy2.DiscriminatorColumn));
                    }
                    else if (queryModel.IncludeJoins.Any())
                    {
                        var discColumnWithAlias = $"{QuoteIdentifier(tableAlias)}.{QuoteIdentifier(tphStrategy2.DiscriminatorColumn)} AS {QuoteIdentifier(tableAlias + "_" + tphStrategy2.DiscriminatorColumn)}";
                        columns.Add(discColumnWithAlias);
                    }
                    else
                    {
                        var discColumn = $"{QuoteIdentifier(tableAlias)}.{QuoteIdentifier(tphStrategy2.DiscriminatorColumn)}";
                        columns.Add(discColumn);
                    }
                }
            }

            // Include joined entity columns (eager loading)
            foreach (var includeJoin in queryModel.IncludeJoins)
            {
                var joinedMap = includeJoin.Join.JoinedEntity;
                var alias = includeJoin.TableAlias;

                foreach (var prop in joinedMap.ScalarProperties)
                {
                    if (!string.IsNullOrEmpty(prop.ColumnName))
                    {
                        // Aliasuj kolumny aby uniknąć kolizji nazw
                        var columnWithAlias = $"{QuoteIdentifier(alias)}.{QuoteIdentifier(prop.ColumnName)} AS {QuoteIdentifier(alias + "_" + prop.ColumnName)}";
                        columns.Add(columnWithAlias);
                    }
                }

                if (joinedMap.InheritanceStrategy is TablePerHierarchyStrategy joinedTph)
                {
                    var discColumnWithAlias = $"{QuoteIdentifier(alias)}.{QuoteIdentifier(joinedTph.DiscriminatorColumn)} AS {QuoteIdentifier(alias + "_" + joinedTph.DiscriminatorColumn)}";
                    columns.Add(discColumnWithAlias);
                }
            }

            return columns;
        }
    }

    private string BuildJoinCondition(JoinClause join, string? primaryAlias)
    {
        // ✅ Użyj ParentAlias jeśli jest ustawiony (dla ThenInclude)
        // W przeciwnym razie użyj primaryAlias (dla prostego Include)
        var leftAlias = join.ParentAlias ?? primaryAlias;

        var leftCol = string.IsNullOrEmpty(leftAlias)
            ? QuoteIdentifier(join.LeftProperty.ColumnName!)
            : $"{QuoteIdentifier(leftAlias)}.{QuoteIdentifier(join.LeftProperty.ColumnName!)}";

        var rightCol = string.IsNullOrEmpty(join.Alias)
            ? QuoteIdentifier(join.RightProperty.ColumnName!)
            : $"{QuoteIdentifier(join.Alias)}.{QuoteIdentifier(join.RightProperty.ColumnName!)}";

        return $"{leftCol} = {rightCol}";
    }

    private string BuildColumnReference(PropertyMap property, string? tableAlias)
    {
        if (string.IsNullOrEmpty(property.ColumnName))
            throw new InvalidOperationException($"Property {property.PropertyInfo.Name} does not have a column mapping.");

        if (string.IsNullOrEmpty(tableAlias))
        {
            return QuoteIdentifier(property.ColumnName);
        }
        else
        {
            return $"{QuoteIdentifier(tableAlias)}.{QuoteIdentifier(property.ColumnName)}";
        }
    }

    private string BuildAggregateFunction(AggregateFunction agg, string? tableAlias)
    {
        var functionName = agg.FunctionType switch
        {
            AggregateFunctionType.Count => "COUNT",
            AggregateFunctionType.Sum => "SUM",
            AggregateFunctionType.Avg => "AVG",
            AggregateFunctionType.Min => "MIN",
            AggregateFunctionType.Max => "MAX",
            _ => throw new NotSupportedException($"Aggregate function {agg.FunctionType} is not supported.")
        };

        string column;
        if (agg.Property == null)
        {
            column = "*";
        }
        else
        {
            column = BuildColumnReference(agg.Property, tableAlias);
        }

        var result = $"{functionName}({column})";

        if (!string.IsNullOrEmpty(agg.Alias))
        {
            result += $" AS {QuoteIdentifier(agg.Alias)}";
        }

        return result;
    }

    /// <summary>
    /// Buduje warunek WHERE dla filtrowania po discriminator (TPH) - polimorficzny.
    /// Zwraca null jeśli nie trzeba filtrować (klasa bazowa bez discriminator).
    /// </summary>
    private (string? whereClause, Dictionary<string, object> parameters) BuildDiscriminatorFilter(
        EntityMap map,
        IMetadataStore? metadataStore,
        string? tableAlias = null)
    {
        var parameters = new Dictionary<string, object>();

        if (map.InheritanceStrategy is not TablePerHierarchyStrategy tphStrategy)
        {
            return (null, parameters); // Nie TPH - brak filtrowania
        }

        if (string.IsNullOrEmpty(map.Discriminator))
        {
            return (null, parameters); // Klasa bazowa - zwróć wszystko z hierarchii
        }

        // Konkretna klasa - znajdź wszystkie podklasy (polimorficzny)
        var allDiscriminators = new List<string> { map.Discriminator };

        if (metadataStore != null)
        {
            // Znajdź wszystkie typy dziedziczące
            foreach (var otherMap in metadataStore.GetAllMaps())
            {
                if (otherMap.EntityType != map.EntityType &&
                    map.EntityType.IsAssignableFrom(otherMap.EntityType) &&
                    !string.IsNullOrEmpty(otherMap.Discriminator))
                {
                    allDiscriminators.Add(otherMap.Discriminator);
                }
            }
        }

        // Buduj WHERE z aliasem
        var columnRef = string.IsNullOrEmpty(tableAlias)
            ? QuoteIdentifier(tphStrategy.DiscriminatorColumn)
            : $"{QuoteIdentifier(tableAlias)}.{QuoteIdentifier(tphStrategy.DiscriminatorColumn)}";

        if (allDiscriminators.Count == 1)
        {
            // Tylko jedna klasa - prosty warunek
            parameters["@Discriminator"] = allDiscriminators[0];
            return ($"{columnRef} = @Discriminator", parameters);
        }
        else
        {
            // Wiele klas - użyj IN (...)
            var paramNames = new List<string>();
            for (int i = 0; i < allDiscriminators.Count; i++)
            {
                var paramName = $"@Discriminator{i}";
                paramNames.Add(paramName);
                parameters[paramName] = allDiscriminators[i];
            }

            return ($"{columnRef} IN ({string.Join(", ", paramNames)})", parameters);
        }
    }

    /// <summary>
    /// Dodaje alias tabeli do wszystkich nazw kolumn w klauzuli WHERE.
    /// Zamienia "column_name" na "alias"."column_name".
    /// </summary>
    private string AddTableAliasToWhereClause(string whereClause, string? tableAlias, EntityMap map)
    {
        if (string.IsNullOrEmpty(tableAlias) || string.IsNullOrEmpty(whereClause))
            return whereClause;

        // Zbierz wszystkie nazwy kolumn z mapy
        var columnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Dla TPH: zbierz kolumny ze wszystkich klas w hierarchii
        if (map.InheritanceStrategy is TablePerHierarchyStrategy tphStrategy)
        {
            var allMaps = new List<EntityMap>();
            CollectTPHHierarchy(map.RootMap, _metadataStore, allMaps);

            foreach (var hierarchyMap in allMaps)
            {
                foreach (var prop in hierarchyMap.ScalarProperties)
                {
                    if (!string.IsNullOrEmpty(prop.ColumnName))
                    {
                        columnNames.Add(prop.ColumnName);
                    }
                }
            }

            if (!string.IsNullOrEmpty(tphStrategy.DiscriminatorColumn))
            {
                columnNames.Add(tphStrategy.DiscriminatorColumn);
            }
        }
        else
        {
            // Dla innych strategii: tylko kolumny z bieżącej mapy
            foreach (var prop in map.ScalarProperties)
            {
                if (!string.IsNullOrEmpty(prop.ColumnName))
                {
                    columnNames.Add(prop.ColumnName);
                }
            }
        }

        var result = whereClause;

        // Dla każdej kolumny, zamień "column" na "alias"."column"
        // ale tylko jeśli nie jest już poprzedzona aliasem
        foreach (var columnName in columnNames)
        {
            // Pattern: "column_name" które NIE jest poprzedzone przez "." (czyli nie jest już aliasowane)
            // Używamy negative lookbehind: (?<!\.)
            var pattern = $@"(?<!\.)""({System.Text.RegularExpressions.Regex.Escape(columnName)})""";
            var replacement = $"{QuoteIdentifier(tableAlias)}.{QuoteIdentifier(columnName)}";

            result = System.Text.RegularExpressions.Regex.Replace(
                result,
                pattern,
                replacement,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return result;
    }

    private static IEnumerable<string> FilterNullStrings(IEnumerable<string?>? source)
    {
        if (source == null)
            return Enumerable.Empty<string>();
        return source.Where(s => s != null).Select(s => s!);
    }

    private static bool IsEntityCompatible(EntityMap map, object entity)
    {
        if (entity == null)
            return false;
        Type actualType = entity.GetType();
        return actualType == map.EntityType;
    }

    // ==================== NOWA SEKCJA: INHERITANCE STRATEGY ANALYSIS ====================

    /// <summary>
    /// Analiza strategii dziedziczenia - zwraca kontekst z wszystkimi informacjami potrzebnymi do budowania SQL.
    /// </summary>
    private InheritanceContext AnalyzeInheritanceStrategy(EntityMap map, string? requestedAlias)
    {
        var context = new InheritanceContext
        {
            PrimaryAlias = requestedAlias ?? map.TableName.ToLowerInvariant()
        };

        // Określ typ strategii
        if (map.InheritanceStrategy is TablePerHierarchyStrategy tphStrategy)
        {
            context.StrategyType = InheritanceStrategyType.TablePerHierarchy;
            context.BaseTable = map.RootMap.TableName;
            
            // TPH: zbierz wszystkie mapy w hierarchii
            CollectTPHHierarchy(map.RootMap, _metadataStore, context.HierarchyMaps);
            
            // TPH: zbuduj WHERE dla discriminatora (jeśli konkretna klasa)
            if (!string.IsNullOrEmpty(map.Discriminator))
            {
                var (whereClause, parameters) = BuildDiscriminatorFilterInternal(map, _metadataStore, context.PrimaryAlias);
                context.DiscriminatorWhereClause = whereClause;
                context.DiscriminatorParameters = parameters;
            }
        }
        else if (map.InheritanceStrategy is TablePerTypeStrategy)
        {
            context.StrategyType = InheritanceStrategyType.TablePerType;
            
            if (map.BaseMap != null)
            {
                // TPT - konkretna klasa pochodna
                context.BaseTable = map.TableName;
                context.PrimaryAlias = $"t{map.EntityType.Name}";
                context.TableAliases[map.TableName] = context.PrimaryAlias;
                
                // Zbuduj INNER JOIN do wszystkich rodziców (w górę hierarchii)
                BuildTPTParentJoins(map, context);
            }
            else if (map.IsAbstract)
            {
                // TPT - abstrakcyjna klasa bazowa
                context.BaseTable = map.TableName;
                context.PrimaryAlias = $"t{map.EntityType.Name}";
                context.TableAliases[map.TableName] = context.PrimaryAlias;
                
                // Zbuduj LEFT JOIN do wszystkich dzieci (w dół hierarchii)
                BuildTPTChildJoins(map, context);
            }
            else
            {
                // TPT - konkretna klasa bez dziedziczenia
                context.BaseTable = map.TableName;
                context.TableAliases[map.TableName] = context.PrimaryAlias;
            }
        }
        else if (map.InheritanceStrategy is TablePerConcreteClassStrategy)
        {
            context.StrategyType = InheritanceStrategyType.TablePerConcrete;
            context.BaseTable = map.TableName;
            // TPC: UNION ALL będzie obsługiwane osobno
        }
        else
        {
            // Brak dziedziczenia
            context.StrategyType = InheritanceStrategyType.None;
            context.BaseTable = map.TableName;
        }

        return context;
    }

    /// <summary>
    /// Buduje INNER JOIN do tabel rodzica (TPT - w górę hierarchii).
    /// </summary>
    private void BuildTPTParentJoins(EntityMap map, InheritanceContext context)
    {
        var current = map.BaseMap;
        var childAlias = context.PrimaryAlias;

        while (current != null)
        {
            var parentAlias = $"t{current.EntityType.Name}";
            context.TableAliases[current.TableName] = parentAlias;

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

    /// <summary>
    /// Buduje LEFT JOIN do tabel dzieci (TPT - w dół hierarchii, rekurencyjnie).
    /// </summary>
    private void BuildTPTChildJoins(EntityMap baseMap, InheritanceContext context)
    {
        if (_metadataStore == null) return;

        // Znajdź bezpośrednie typy pochodne
        var directDerivedMaps = _metadataStore.GetAllMaps()
            .Where(m => m.InheritanceStrategy is TablePerTypeStrategy && m.BaseMap == baseMap)
            .ToList();

        foreach (var derivedMap in directDerivedMaps)
        {
            var parentAlias = context.TableAliases.ContainsKey(baseMap.TableName) 
                ? context.TableAliases[baseMap.TableName] 
                : $"t{baseMap.EntityType.Name}";
                
            var childAlias = $"t{derivedMap.EntityType.Name}";
            context.TableAliases[derivedMap.TableName] = childAlias;

            var condition = $"{QuoteIdentifier(parentAlias)}.{QuoteIdentifier(baseMap.KeyProperty.ColumnName!)} = {QuoteIdentifier(childAlias)}.{QuoteIdentifier(derivedMap.KeyProperty.ColumnName!)}";

            context.ChildJoins.Add(new InheritanceJoinClause
            {
                Table = derivedMap.TableName,
                Alias = childAlias,
                Condition = condition,
                JoinType = JoinType.Left
            });

            // Rekurencyjnie dla dalszych poziomów
            if (derivedMap.IsAbstract)
            {
                BuildTPTChildJoins(derivedMap, context);
            }
        }
    }

    /// <summary>
    /// Wersja BuildDiscriminatorFilter która nie modyfikuje context.Parameters (używana wewnętrznie).
    /// </summary>
    private (string? whereClause, Dictionary<string, object> parameters) BuildDiscriminatorFilterInternal(
        EntityMap map,
        IMetadataStore? metadataStore,
        string? tableAlias = null)
    {
        var parameters = new Dictionary<string, object>();

        if (map.InheritanceStrategy is not TablePerHierarchyStrategy tphStrategy)
        {
            return (null, parameters);
        }

        if (string.IsNullOrEmpty(map.Discriminator))
        {
            return (null, parameters); // Klasa bazowa - zwróć wszystko
        }

        // Konkretna klasa - znajdź wszystkie podklasy (polimorficzny)
        var allDiscriminators = new List<string> { map.Discriminator };

        if (metadataStore != null)
        {
            foreach (var otherMap in metadataStore.GetAllMaps())
            {
                if (otherMap.EntityType != map.EntityType &&
                    map.EntityType.IsAssignableFrom(otherMap.EntityType) &&
                    !string.IsNullOrEmpty(otherMap.Discriminator))
                {
                    allDiscriminators.Add(otherMap.Discriminator);
                }
            }
        }

        var columnRef = string.IsNullOrEmpty(tableAlias)
            ? QuoteIdentifier(tphStrategy.DiscriminatorColumn)
            : $"{QuoteIdentifier(tableAlias)}.{QuoteIdentifier(tphStrategy.DiscriminatorColumn)}";

        if (allDiscriminators.Count == 1)
        {
            parameters["@Discriminator"] = allDiscriminators[0];
            return ($"{columnRef} = @Discriminator", parameters);
        }
        else
        {
            var paramNames = new List<string>();
            for (int i = 0; i < allDiscriminators.Count; i++)
            {
                var paramName = $"@Discriminator{i}";
                paramNames.Add(paramName);
                parameters[paramName] = allDiscriminators[i];
            }

            return ($"{columnRef} IN ({string.Join(", ", paramNames)})", parameters);
        }
    }
}