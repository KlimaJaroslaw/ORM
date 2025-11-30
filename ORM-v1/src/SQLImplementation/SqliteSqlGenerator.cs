using Microsoft.Data.Sqlite;
using ORM_v1.Mapping;
using ORM_v1.src.Query;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

public class SqliteSqlGenerator : ISqlGenerator
{
    public IDbCommand GenerateSelect(IDbConnection connection, EntityMap map, object id)
    {       
        var builder = new SqlQueryBuilder();
        var columns = map.ScalarProperties.Select(p => p.ColumnName);

        var columnsFiltered = FilterNullStrings(columns);

        if (columnsFiltered.Count() == 0)
            throw new InvalidOperationException("No columns to select.");

        builder.Select(columnsFiltered)
               .From(map.TableName)
               .Where($"{map.KeyProperty.ColumnName} = @id");

        var command = connection.CreateCommand();
        command.CommandText = builder.ToString();

        AddParameter(command, "@id", id);

        return command;
    }

    public IDbCommand GenerateSelectAll(IDbConnection connection, EntityMap map)
    {
        var builder = new SqlQueryBuilder();
        var columns = map.ScalarProperties.Select(p => p.ColumnName);
        var columnsFiltered = FilterNullStrings(columns);

        if(columnsFiltered.Count() == 0)        
            throw new InvalidOperationException("No columns to select.");        

        builder.Select(columnsFiltered)
               .From(map.TableName);

        var command = connection.CreateCommand();
        command.CommandText = builder.ToString();

        return command;
    }

    public IDbCommand GenerateInsert(IDbConnection connection, EntityMap map, object entity)
    {
        if(!IsEntityCompatible(map, entity))        
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

        var command = connection.CreateCommand();
        command.CommandText = builder.ToString();

        foreach (var prop in propsToInsert)
        {
            var value = prop.PropertyInfo.GetValue(entity);
            AddParameter(command, $"@{prop.PropertyInfo.Name}", value);
        }

        if (map.HasAutoIncrementKey)
        {
            command.CommandText += "; SELECT last_insert_rowid();";
        }

        return command;
    }

    public IDbCommand GenerateUpdate(IDbConnection connection, EntityMap map, object entity)
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

        var command = connection.CreateCommand();
        command.CommandText = builder.ToString();
        
        foreach (var prop in propsToUpdate)
        {
            var value = prop.PropertyInfo.GetValue(entity);
            AddParameter(command, $"@{prop.PropertyInfo.Name}", value);
        }
        
        var keyValue = map.KeyProperty.PropertyInfo.GetValue(entity);
        AddParameter(command, $"@{map.KeyProperty.PropertyInfo.Name}", keyValue);

        return command;
    }

    public IDbCommand GenerateDelete(IDbConnection connection, EntityMap map, object entity)
    {
        if (!IsEntityCompatible(map, entity))
            throw new ArgumentException("Entity type is not compatible with the provided EntityMap.", nameof(entity));

        var builder = new SqlQueryBuilder();
        builder.DeleteFrom(map.TableName)
               .Where($"{map.KeyProperty.ColumnName} = @id");

        var command = connection.CreateCommand();
        command.CommandText = builder.ToString();

        var idValue = map.KeyProperty.PropertyInfo.GetValue(entity);
        AddParameter(command, "@id", idValue);

        return command;
    }

 
    private void AddParameter(IDbCommand command, string name, object? value)
    {
        var param = command.CreateParameter();
        param.ParameterName = name;
        param.Value = value ?? DBNull.Value;
        command.Parameters.Add(param);
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