using ORM_v1.Mapping;
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
        var columns = map.ScalarProperties.Select(p => p.ColumnName);
        var columnsFiltered = FilterNullStrings(columns);
        
        if (columnsFiltered.Count() == 0)
            throw new InvalidOperationException("No columns to select.");

        builder.Select(columnsFiltered)
            .From(map.TableName)
            .Where($"{map.KeyProperty.ColumnName} = @id");

        var sqlQuery = new SqlQuery()
        {
            Sql = builder.ToString(),
            Parameters = new Dictionary<string, object>
            {
                { "@id", id }
            }
        };
        return sqlQuery;
    }

    public SqlQuery GenerateSelectAll(EntityMap map)
    {
        var builder = new SqlQueryBuilder();
                
        var columns = map.ScalarProperties.Select(p => p.ColumnName);
        var columnsFiltered = FilterNullStrings(columns);

        if (columnsFiltered.Count() == 0)
            throw new InvalidOperationException("No columns to select.");

        builder.Select(columnsFiltered)
               .From(map.TableName);

        var sqlQuery = new SqlQuery()
        {
            Sql = builder.ToString(),
            Parameters = new Dictionary<string, object>()
        };
        return sqlQuery;
    }

    public SqlQuery GenerateInsert(EntityMap map, object entity)
    {
        if (!IsEntityCompatible(map, entity))
            throw new ArgumentException("Entity type is not compatible with the provided EntityMap.", nameof(entity));

        var propsToInsert = map.ScalarProperties
            .Where(p => !(p == map.KeyProperty && map.HasAutoIncrementKey))
            .ToList();

        var columnNames = propsToInsert.Select(p => p.ColumnName);
        var paramNames = propsToInsert.Select(p => $"@{p.PropertyInfo.Name}");

        var columnsFiltered = FilterNullStrings(columnNames);

        if (columnsFiltered.Count() == 0)
            throw new InvalidOperationException("No columns to insert.");

        var builder = new SqlQueryBuilder();
        builder.InsertInto(map.TableName, columnsFiltered)
               .Values(paramNames);

        var sqlText = builder.ToString();
        
        if (map.HasAutoIncrementKey)
        {
            sqlText += "; SELECT last_insert_rowid();";
        }

        var parameters = new Dictionary<string, object>();
        foreach (var prop in propsToInsert)
        {
            var value = prop.PropertyInfo.GetValue(entity);
            parameters[$"@{prop.PropertyInfo.Name}"] = value ?? DBNull.Value;
        }

        return new SqlQuery
        {
            Sql = sqlText,
            Parameters = parameters
        };
    }

    public SqlQuery GenerateUpdate(EntityMap map, object entity)
    {
        if (!IsEntityCompatible(map, entity))
            throw new ArgumentException("Entity type is not compatible with the provided EntityMap.", nameof(entity));

        var propsToUpdate = map.ScalarProperties
            .Where(p => p != map.KeyProperty)
            .ToList();

        var assignments = propsToUpdate
            .Select(p => $"{p.ColumnName} = @{p.PropertyInfo.Name}");

        var builder = new SqlQueryBuilder();
        builder.Update(map.TableName)
               .Set(assignments)
               .Where($"{map.KeyProperty.ColumnName} = @{map.KeyProperty.PropertyInfo.Name}");

        var parameters = new Dictionary<string, object>();
        
        foreach (var prop in propsToUpdate)
        {
            var value = prop.PropertyInfo.GetValue(entity);
            parameters[$"@{prop.PropertyInfo.Name}"] = value ?? DBNull.Value;
        }

        var keyValue = map.KeyProperty.PropertyInfo.GetValue(entity);
        parameters[$"@{map.KeyProperty.PropertyInfo.Name}"] = keyValue ?? DBNull.Value;

        return new SqlQuery
        {
            Sql = builder.ToString(),
            Parameters = parameters
        };
    }

    public SqlQuery GenerateDelete(EntityMap map, object entity)
    {
        if (!IsEntityCompatible(map, entity))
            throw new ArgumentException("Entity type is not compatible with the provided EntityMap.", nameof(entity));

        var builder = new SqlQueryBuilder();
        builder.DeleteFrom(map.TableName)
               .Where($"{map.KeyProperty.ColumnName} = @id");

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
            return map.ScalarProperties.Select(p => 
                string.IsNullOrEmpty(tableAlias) 
                    ? QuoteIdentifier(p.ColumnName!) 
                    : $"{QuoteIdentifier(tableAlias)}.{QuoteIdentifier(p.ColumnName!)}");
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
        var leftCol = string.IsNullOrEmpty(primaryAlias)
            ? QuoteIdentifier(join.LeftProperty.ColumnName!)
            : $"{QuoteIdentifier(primaryAlias)}.{QuoteIdentifier(join.LeftProperty.ColumnName!)}";

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