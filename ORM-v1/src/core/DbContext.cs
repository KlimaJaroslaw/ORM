using ORM_v1.Configuration;
using ORM_v1.Mapping;
using ORM_v1.Mapping.Strategies;
using ORM_v1.Query;
using ORM_v1.src.SQLImplementation;
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
    private static bool _sqliteInitialized = false;
    private static readonly object _initLock = new();
    
    public ChangeTracker ChangeTracker { get; } = new ChangeTracker();

    // Database operations API (similar to EF Core)
    public DatabaseFacade Database { get; }

    public DbContext(DbConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        
        // Inicjalizacja SQLite (raz dla całej aplikacji)
        if (!_sqliteInitialized)
        {
            lock (_initLock)
            {
                if (!_sqliteInitialized)
                {
                    SQLitePCL.Batteries.Init();
                    _sqliteInitialized = true;
                }
            }
        }
        
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
        return _materializerCache.GetOrAdd(map, m => new ObjectMaterializer(m, _configuration.MetadataStore));
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
        var sqlQuery = _sqlGenerator.GenerateSelect(entityMap, id, _configuration.MetadataStore);

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

            var entity = (T)materializer.Materialize(reader, ordinals);

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

        Console.WriteLine($"\n[DEBUG SetInternal] EntityMap for {typeof(T).Name}:");
        Console.WriteLine($"  ScalarProperties.Count = {entityMap.ScalarProperties.Count}");
        foreach (var prop in entityMap.ScalarProperties)
        {
            Console.WriteLine($"    - {prop.PropertyInfo.Name} -> {prop.ColumnName}");
        }

        // Generate SQL query using the new interface
        var sqlQuery = _sqlGenerator.GenerateSelectAll(entityMap, _configuration.MetadataStore);

        Console.WriteLine($"  SQL: {sqlQuery.Sql}");

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

        Console.WriteLine($"  Reader fields ({reader.FieldCount}):");
        for (int i = 0; i < reader.FieldCount; i++)
        {
            Console.WriteLine($"    [{i}] {reader.GetName(i)}");
        }

        var materializer = GetMaterializer(entityMap);
        
        // ✅ Dla TPH: użyj GetOrdinalsForMapWithAlias aby obsłużyć hierarchię dziedziczenia
        // Ordinals będą odczytywane dynamicznie dla każdej konkretnej klasy podczas materializacji
        int[] ordinals = GetOrdinalsForReader(reader, entityMap);

        Console.WriteLine($"  Ordinals:");
        for (int i = 0; i < ordinals.Length; i++)
        {
            Console.WriteLine($"    [{i}] {entityMap.ScalarProperties[i].PropertyInfo.Name} -> ordinal={ordinals[i]}");
        }

        while (reader.Read())
        {
            var entity = (T)materializer.Materialize(reader, ordinals);

            Console.WriteLine($"[DEBUG SetInternal] Po Materialize: entity.GetHashCode()={entity.GetHashCode()}");

            // Identity Map: sprawdź czy encja o tym kluczu już istnieje
            var keyValue = entityMap.KeyProperty.PropertyInfo.GetValue(entity);
            if (keyValue != null)
            {
                // ✅ POPRAWKA TPC: Użyj FAKTYCZNEGO typu encji (entity.GetType()), nie T!
                // Dla TPC Student (ID=1) i StudentPart (ID=1) to RÓŻNE encje!
                var actualType = entity.GetType();
                var tracked = ChangeTracker.FindTracked(actualType, keyValue) as T;
                
                if (tracked != null)
                {
                    Console.WriteLine($"[DEBUG SetInternal] Znaleziono w ChangeTracker: tracked.GetHashCode()={tracked.GetHashCode()}, Type={tracked.GetType().Name}");
                    list.Add(tracked); // Użyj istniejącej instancji
                    continue;
                }
            }

            ChangeTracker.Track(entity, EntityState.Unchanged);
            Console.WriteLine($"[DEBUG SetInternal] Dodaję do listy: entity.GetHashCode()={entity.GetHashCode()}, Type={entity.GetType().Name}");
            list.Add(entity);
        }
        return list;
    }

    private int[] GetOrdinalsForReader(IDataReader reader, EntityMap map)
    {
        var ordinals = new int[map.ScalarProperties.Count];
        for (int i = 0; i < map.ScalarProperties.Count; i++)
        {
            var prop = map.ScalarProperties[i];
            ordinals[i] = -1;
            
            var columnName = prop.ColumnName;
            if (columnName == null)
                continue;

            // Spróbuj znaleźć kolumnę (bez aliasu dla SetInternal)
            for (int j = 0; j < reader.FieldCount; j++)
            {
                if (string.Equals(reader.GetName(j), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    ordinals[i] = j;
                    break;
                }
            }
        }
        return ordinals;
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

                    foreach (var param in sqlQuery.Parameters)
                    {
                        AddParameter(cmd, param.Key, param.Value);
                    }

                    var hasAutoIncrement = map.InheritanceStrategy is TablePerHierarchyStrategy
                        ? map.RootMap.HasAutoIncrementKey
                        : map.HasAutoIncrementKey;

                    var isRootAutoIncrement = map.InheritanceStrategy is TablePerTypeStrategy && map.BaseMap != null
                        ? GetRootMap(map).HasAutoIncrementKey
                        : hasAutoIncrement;

                    if (entry.State == EntityState.Added && isRootAutoIncrement)
                    {
                        var newId = cmd.ExecuteScalar();

                        var targetType = map.KeyProperty.PropertyType;
                        var convertedId = Convert.ChangeType(newId, targetType);

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

    private EntityMap GetRootMap(EntityMap map)
    {
        var current = map;
        while (current.BaseMap != null)
        {
            current = current.BaseMap;
        }
        return current;
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

        var processedTables = new HashSet<string>();

        foreach (var map in metadataStore.GetAllMaps())
        {
            if (ShouldCreateTable(map))
            {
                var tableName = GetTableNameForCreation(map);

                if (processedTables.Add(tableName))
                {
                    CreateTable(connection, map, metadataStore);
                }
            }
        }
    }

    /// <summary>
    /// Deletes all tables from the database.
    /// </summary>
    public void EnsureDeleted()
    {
        var connection = _context.GetConnection();
        var metadataStore = _context.GetConfiguration().MetadataStore;

        var tablesToDrop = new HashSet<string>();

        foreach (var map in metadataStore.GetAllMaps())
        {
            tablesToDrop.Add(map.TableName);
        }

        using var disableFk = connection.CreateCommand();
        disableFk.CommandText = "PRAGMA foreign_keys = OFF";
        disableFk.ExecuteNonQuery();

        foreach (var tableName in tablesToDrop)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"DROP TABLE IF EXISTS \"{tableName}\"";
            command.ExecuteNonQuery();
        }

        using var enableFk = connection.CreateCommand();
        enableFk.CommandText = "PRAGMA foreign_keys = ON";
        enableFk.ExecuteNonQuery();
    }

    private bool ShouldCreateTable(EntityMap map)
    {
        if (map.InheritanceStrategy is TablePerHierarchyStrategy)
        {
            return map.IsHierarchyRoot;
        }
        else if (map.InheritanceStrategy is TablePerTypeStrategy)
        {
            return true;
        }
        else if (map.InheritanceStrategy is TablePerConcreteClassStrategy)
        {
            return !map.IsAbstract;
        }

        return true;
    }

    private string GetTableNameForCreation(EntityMap map)
    {
        if (map.InheritanceStrategy is TablePerHierarchyStrategy)
        {
            return map.RootMap.TableName;
        }
        return map.TableName;
    }

    private void CreateTable(IDbConnection connection, EntityMap map, IMetadataStore metadataStore)
    {
        var createTableSql = GenerateCreateTableSql(map, metadataStore);

        using var command = connection.CreateCommand();
        command.CommandText = createTableSql;
        command.ExecuteNonQuery();
    }

    private string GenerateCreateTableSql(EntityMap map, IMetadataStore metadataStore)
    {
        var columns = new List<string>();
        var processedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (map.InheritanceStrategy is TablePerHierarchyStrategy)
        {
            var rootMap = map.RootMap;
            var allMapsInHierarchy = GetAllMapsInTPHHierarchy(rootMap, metadataStore);

            foreach (var hierarchyMap in allMapsInHierarchy)
            {
                foreach (var prop in hierarchyMap.ScalarProperties)
                {
                    if (processedColumns.Add(prop.ColumnName!))
                    {
                        var columnDef = $"\"{prop.ColumnName}\" {GetSqliteType(prop.PropertyType)}";

                        if (string.Equals(prop.ColumnName, rootMap.KeyProperty.ColumnName, StringComparison.OrdinalIgnoreCase))
                        {
                            columnDef += " PRIMARY KEY";
                            if (rootMap.HasAutoIncrementKey)
                            {
                                columnDef += " AUTOINCREMENT";
                            }
                        }

                        columns.Add(columnDef);
                    }
                }
            }

                if (map.InheritanceStrategy is TablePerHierarchyStrategy tphStrategy)
                {
                    if (processedColumns.Add(tphStrategy.DiscriminatorColumn))
                    {
                        columns.Add($"\"{tphStrategy.DiscriminatorColumn}\" TEXT NOT NULL");
                    }
                }

                return $"CREATE TABLE IF NOT EXISTS \"{rootMap.TableName}\" ({string.Join(", ", columns)})";
            }
            else if (map.InheritanceStrategy is TablePerTypeStrategy)
            {
                if (map.BaseMap == null)
                {
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
                }
                else
                {
                    foreach (var prop in map.ScalarProperties)
                    {
                        var isInheritedColumn = map.BaseMap.ScalarProperties.Any(bp =>
                            string.Equals(bp.ColumnName, prop.ColumnName, StringComparison.OrdinalIgnoreCase));

                        if (!isInheritedColumn)
                        {
                            columns.Add($"\"{prop.ColumnName}\" {GetSqliteType(prop.PropertyType)}");
                        }
                        else if (string.Equals(prop.ColumnName, map.KeyProperty.ColumnName, StringComparison.OrdinalIgnoreCase))
                        {
                            columns.Add($"\"{prop.ColumnName}\" INTEGER PRIMARY KEY");
                        }
                    }

                    columns.Add($"FOREIGN KEY (\"{map.KeyProperty.ColumnName}\") REFERENCES \"{map.BaseMap.TableName}\"(\"{map.BaseMap.KeyProperty.ColumnName}\")");
                }

                return $"CREATE TABLE IF NOT EXISTS \"{map.TableName}\" ({string.Join(", ", columns)})";
            }
            else
            {
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

                if (map.InheritanceStrategy is TablePerHierarchyStrategy tphStrategy)
                {
                    columns.Add($"\"{tphStrategy.DiscriminatorColumn}\" TEXT NOT NULL");
                }

                return $"CREATE TABLE IF NOT EXISTS \"{map.TableName}\" ({string.Join(", ", columns)})";
            }
        }

        private List<EntityMap> GetAllMapsInTPHHierarchy(EntityMap rootMap, IMetadataStore metadataStore)
        {
            var result = new List<EntityMap> { rootMap };

            foreach (var map in metadataStore.GetAllMaps())
            {
                if (map != rootMap &&
                    map.InheritanceStrategy is TablePerHierarchyStrategy &&
                    map.RootMap == rootMap)
                {
                    result.Add(map);
                }
            }

            return result;
        }

        private string GetSqliteType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;

            if (type == typeof(int) || type == typeof(long) || type == typeof(bool) || type.IsEnum)
                return "INTEGER";
            if (type == typeof(decimal) || type == typeof(double) || type == typeof(float))
                return "REAL";
            if (type == typeof(DateTime))
                return "TEXT";

            return "TEXT";
        }
    }

