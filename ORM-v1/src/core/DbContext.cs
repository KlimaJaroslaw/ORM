using ORM_v1.Configuration;
using ORM_v1.Mapping;
using ORM_v1.Query;
using System.Collections.Concurrent;
using System.Data;


namespace ORM_v1.core;

public class DbContext : IDisposable
{
    private readonly DbConfiguration _configuration;
    private readonly ConnectionFactory _connectionFactory;
    private readonly ISqlGenerator _sqlGenerator;
    
    private IDbConnection? _connection;
    private bool _disposed;

    private static readonly ConcurrentDictionary<EntityMap, ObjectMaterializer> _materializerCache = new();
    public ChangeTracker ChangeTracker { get; } = new ChangeTracker();
    
    // Database operations API (similar to EF Core)
    public DatabaseFacade Database { get; }

    public DbContext(DbConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _connectionFactory = new ConnectionFactory(_configuration.ConnectionString);
        
        _sqlGenerator = new SqliteSqlGenerator();
        Database = new DatabaseFacade(this);
    }

    public DbSet<T> Set<T>() where T : class
    {
        return new DbSet<T>(this);
    }
    
    private ObjectMaterializer GetMaterializer(EntityMap map)
    {
        return _materializerCache.GetOrAdd(map, m => new ObjectMaterializer(m));
    }

    protected internal IDbConnection GetConnection()
    {
        if (_connection == null)
        {
            _connection = _connectionFactory.CreateConnection();
            if (_connection.State != ConnectionState.Open)
                _connection.Open();
        }
        return _connection;
    }
    
    protected internal DbConfiguration GetConfiguration() => _configuration;

    public T? Find<T>(object id) where T : class
    {
        // First check the change tracker
        foreach (var entry in ChangeTracker.Entries)
        {
            if (entry.Entity is T entity && entry.State != EntityState.Deleted)
            {
                var map = _configuration.MetadataStore.GetMap<T>();
                var keyVal = map.KeyProperty.PropertyInfo.GetValue(entity);
                if (keyVal != null && keyVal.Equals(id))
                    return entity;
            }
        }

        // Generate SQL query using the new interface
        var entityMap = _configuration.MetadataStore.GetMap<T>();
        var sqlQuery = _sqlGenerator.GenerateSelect(entityMap, id);
        
        // Create and execute command
        var conn = GetConnection();
        using var command = conn.CreateCommand();
        command.CommandText = sqlQuery.Sql;
        
        // Add parameters
        foreach (var param in sqlQuery.Parameters)
        {
            AddParameter(command, param.Key, param.Value);
        }
        
        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            var materializer = GetMaterializer(entityMap);
            int[] ordinals = new int[entityMap.ScalarProperties.Count];
            for (int i = 0; i < entityMap.ScalarProperties.Count; i++)
            {
                string colName = entityMap.ScalarProperties[i].ColumnName!;
                try { ordinals[i] = reader.GetOrdinal(colName); }
                catch { ordinals[i] = -1; }
            }

            var entity = (T)materializer.Materialize(reader, entityMap, ordinals);
            
            ChangeTracker.Track(entity, EntityState.Unchanged);
            return entity;
        }

        return null;
    }
    
    // Metoda pomocnicza dla DbSet.All() - tymczasowa, zanim wejdzie LINQ
    internal IEnumerable<T> SetInternal<T>() where T : class
    {
        var list = new List<T>();
        var entityMap = _configuration.MetadataStore.GetMap<T>();
        
        // Generate SQL query using the new interface
        var sqlQuery = _sqlGenerator.GenerateSelectAll(entityMap);
        
        // Create and execute command
        var conn = GetConnection();
        using var command = conn.CreateCommand();
        command.CommandText = sqlQuery.Sql;
        
        // Add parameters (if any)
        foreach (var param in sqlQuery.Parameters)
        {
            AddParameter(command, param.Key, param.Value);
        }
        
        using var reader = command.ExecuteReader();
        
        var materializer = GetMaterializer(entityMap);
        int[] ordinals = new int[entityMap.ScalarProperties.Count];
        for (int i = 0; i < entityMap.ScalarProperties.Count; i++)
        {
            string colName = entityMap.ScalarProperties[i].ColumnName!;
            try { ordinals[i] = reader.GetOrdinal(colName); }
            catch { ordinals[i] = -1; }
        }

        while (reader.Read())
        {
            var entity = (T)materializer.Materialize(reader, entityMap, ordinals);
            ChangeTracker.Track(entity, EntityState.Unchanged);
            list.Add(entity);
        }
        return list;
    }

    public void SaveChanges()
    {
        if (!ChangeTracker.HasChanges()) return;

        var conn = GetConnection();
        using var transaction = conn.BeginTransaction();

        try
        {
            foreach (var entry in ChangeTracker.Entries)
            {
                if (entry.State == EntityState.Unchanged || entry.State == EntityState.Detached)
                    continue;

                var map = _configuration.MetadataStore.GetMap(entry.Entity.GetType());
                SqlQuery? sqlQuery = null;

                switch (entry.State)
                {
                    case EntityState.Added:
                        sqlQuery = _sqlGenerator.GenerateInsert(map, entry.Entity);
                        break;
                    case EntityState.Modified:
                        sqlQuery = _sqlGenerator.GenerateUpdate(map, entry.Entity);
                        break;
                    case EntityState.Deleted:
                        sqlQuery = _sqlGenerator.GenerateDelete(map, entry.Entity);
                        break;
                }

                if (sqlQuery != null)
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = sqlQuery.Sql;
                    cmd.Transaction = transaction;
                    
                    // Add parameters
                    foreach (var param in sqlQuery.Parameters)
                    {
                        AddParameter(cmd, param.Key, param.Value);
                    }

                    if (entry.State == EntityState.Added && map.HasAutoIncrementKey)
                    {
                        // For Insert with AutoIncrement we need to retrieve the ID
                        // SqliteSqlGenerator adds "; SELECT last_insert_rowid();" at the end
                        var newId = cmd.ExecuteScalar();
                        
                        // Convert ID type (usually long in SQLite) to C# type
                        var targetType = map.KeyProperty.PropertyType;
                        var convertedId = Convert.ChangeType(newId, targetType);
                        
                        // Reflection - set ID in the object
                        map.KeyProperty.PropertyInfo.SetValue(entry.Entity, convertedId);
                    }
                    else
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }

            transaction.Commit();
            ChangeTracker.AcceptAllChanges();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private void AddParameter(IDbCommand command, string name, object? value)
    {
        var param = command.CreateParameter();
        param.ParameterName = name;
        param.Value = value ?? DBNull.Value;
        command.Parameters.Add(param);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _connection?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Database operations facade (similar to EF Core's Database property)
/// </summary>
public class DatabaseFacade
{
    private readonly DbContext _context;

    internal DatabaseFacade(DbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Creates the database schema for all entity types in the model.
    /// </summary>
    public void EnsureCreated()
    {
        var connection = _context.GetConnection();
        var metadataStore = _context.GetConfiguration().MetadataStore;

        foreach (var map in metadataStore.GetAllMaps())
        {
            CreateTable(connection, map);
        }
    }

    /// <summary>
    /// Deletes all tables from the database.
    /// </summary>
    public void EnsureDeleted()
    {
        var connection = _context.GetConnection();
        var metadataStore = _context.GetConfiguration().MetadataStore;

        foreach (var map in metadataStore.GetAllMaps())
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"DROP TABLE IF EXISTS \"{map.TableName}\"";
            command.ExecuteNonQuery();
        }
    }

    private void CreateTable(IDbConnection connection, EntityMap map)
    {
        var createTableSql = GenerateCreateTableSql(map);
        
        using var command = connection.CreateCommand();
        command.CommandText = createTableSql;
        command.ExecuteNonQuery();
    }

    private string GenerateCreateTableSql(EntityMap map)
    {
        var columns = new List<string>();

        foreach (var prop in map.ScalarProperties)
        {
            var columnDef = $"\"{prop.ColumnName}\" {GetSqliteType(prop.PropertyType)}";

            if (prop == map.KeyProperty)
            {
                columnDef += " PRIMARY KEY";
                if (map.HasAutoIncrementKey)
                {
                    columnDef += " AUTOINCREMENT";
                }
            }

            columns.Add(columnDef);
        }

        return $"CREATE TABLE IF NOT EXISTS \"{map.TableName}\" ({string.Join(", ", columns)})";
    }

    private string GetSqliteType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (type == typeof(int) || type == typeof(long) || type == typeof(bool) || type.IsEnum)
            return "INTEGER";
        if (type == typeof(decimal) || type == typeof(double) || type == typeof(float))
            return "REAL";
        if (type == typeof(DateTime))
            return "TEXT"; // SQLite stores dates as text
        
        return "TEXT";
    }
}