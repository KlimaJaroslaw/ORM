using ORM_v1.Mapping;
using ORM_v1.Mapping.Strategies;
using ORM_v1.Query;
using System.Data;

public class SqliteSqlGenerator : ISqlGenerator
{
    public string GetParameterName(string name, int index)
    {
        return $"@{name}{index}";
    }

    public string QuoteIdentifier(string name)
    {
        return $"\"{name}\"";
    }

    public SqlQuery GenerateSelect(EntityMap map, object id)
    {
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

        if (map.InheritanceStrategy is TablePerHierarchyStrategy tphStrategy && !string.IsNullOrEmpty(map.Discriminator))
        {
            whereConditions.Add($"{QuoteIdentifier(tphStrategy.DiscriminatorColumn)} = @Discriminator");
        }

        builder.Where(string.Join(" AND ", whereConditions));

        var parameters = new Dictionary<string, object>
        {
            { "@id", id }
        };

        if (map.InheritanceStrategy is TablePerHierarchyStrategy tphStrat && !string.IsNullOrEmpty(map.Discriminator))
        {
            parameters["@Discriminator"] = tphStrat.DiscriminatorValue;
        }

        var sqlQuery = new SqlQuery()
        {
            Sql = builder.ToString(),
            Parameters = parameters
        };
        return sqlQuery;
    }

    public SqlQuery GenerateSelectAll(EntityMap map)
    {
        var builder = new SqlQueryBuilder();

        var columns = GetColumnsForSelect(map);
        var columnsFiltered = FilterNullStrings(columns);

        if (columnsFiltered.Count() == 0)
            throw new InvalidOperationException("No columns to select.");

        builder.Select(columnsFiltered);

        BuildFromClauseWithInheritance(builder, map);

        var parameters = new Dictionary<string, object>();

        if (map.InheritanceStrategy is TablePerHierarchyStrategy tphStrategy && !string.IsNullOrEmpty(map.Discriminator))
        {
            builder.Where($"{QuoteIdentifier(tphStrategy.DiscriminatorColumn)} = @Discriminator");
            parameters["@Discriminator"] = tphStrategy.DiscriminatorValue;
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

        var propsToUpdate = map.ScalarProperties
            .Where(p => p != map.KeyProperty)
            .ToList();

        var assignments = propsToUpdate
            .Select(p => $"{QuoteIdentifier(p.ColumnName!)} = @{p.PropertyInfo.Name}");

        var builder = new SqlQueryBuilder();
        builder.Update(QuoteIdentifier(map.TableName))
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

    public SqlQuery GenerateComplexSelect(EntityMap map, QueryModel queryModel)
    {
        var builder = new SqlQueryBuilder();
        var parameters = new Dictionary<string, object>(queryModel.Parameters);

        bool hasJoins = queryModel.Joins.Any();
        string? primaryAlias = hasJoins && string.IsNullOrEmpty(queryModel.PrimaryEntityAlias)
            ? map.TableName.ToLowerInvariant()
            : queryModel.PrimaryEntityAlias;

        var selectColumns = BuildSelectColumns(map, queryModel, primaryAlias);

        if (queryModel.Distinct)
        {
            builder.SelectDistinct(selectColumns);
        }
        else
        {
            builder.Select(selectColumns);
        }

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

        if (!string.IsNullOrEmpty(queryModel.WhereClause))
        {
            builder.Where(queryModel.WhereClause);
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
        else
        {
            foreach (var prop in map.ScalarProperties)
            {
                if (!string.IsNullOrEmpty(prop.ColumnName))
                {
                    columns.Add(QuoteIdentifier(prop.ColumnName));
                }
            }

            if (map.InheritanceStrategy is TablePerHierarchyStrategy tphStrategy)
            {
                columns.Add(QuoteIdentifier(tphStrategy.DiscriminatorColumn));
            }
        }

        return columns;
    }

    private void BuildFromClauseWithInheritance(SqlQueryBuilder builder, EntityMap map)
    {
        if (map.InheritanceStrategy is TablePerTypeStrategy && map.BaseMap != null)
        {
            var derivedAlias = $"t{map.EntityType.Name}";
            builder.From($"{QuoteIdentifier(map.TableName)} AS {QuoteIdentifier(derivedAlias)}");
            BuildTablePerTypeJoins(builder, map, derivedAlias);
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
        else if (queryModel.SelectAllColumns || !queryModel.SelectColumns.Any())
        {
            var columns = new List<string>();

            // Primary entity columns
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

            if (map.InheritanceStrategy is TablePerHierarchyStrategy tphStrategy)
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
        else
        {
            return queryModel.SelectColumns
                .Where(p => !string.IsNullOrEmpty(p.ColumnName))
                .Select(p => BuildColumnReference(p, tableAlias));
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
}