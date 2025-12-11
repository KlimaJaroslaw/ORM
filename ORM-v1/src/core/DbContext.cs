using System;
using System.Collections.Generic;
using System.Data;
using ORM_v1.Configuration;
using ORM_v1.Mapping;


namespace ORM_v1.core;

public class DbContext : IDisposable
    {
        private readonly DbConfiguration _configuration;
        private readonly ConnectionFactory _connectionFactory;
        private readonly ISqlGenerator _sqlGenerator;
        
        private IDbConnection? _connection;
        private bool _disposed;

        public ChangeTracker ChangeTracker { get; } = new ChangeTracker();

        public DbContext(DbConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _connectionFactory = new ConnectionFactory(_configuration.ConnectionString);
            
            _sqlGenerator = new SqliteSqlGenerator();
        }

        public DbSet<T> Set<T>() where T : class
        {
            return new DbSet<T>(this);
        }

        protected IDbConnection GetConnection()
        {
            if (_connection == null)
            {
                _connection = _connectionFactory.CreateConnection();
                if (_connection.State != ConnectionState.Open)
                    _connection.Open();
            }
            return _connection;
        }

        public T? Find<T>(object id) where T : class
        {
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

            var entityMap = _configuration.MetadataStore.GetMap<T>();
            using var command = _sqlGenerator.GenerateSelect(GetConnection(), entityMap, id);
            
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                var materializer = new ObjectMaterializer(entityMap);
                int[] ordinals = new int[entityMap.ScalarProperties.Count];
                for (int i = 0; i < entityMap.ScalarProperties.Count; i++)
                {
                    string colName = entityMap.ScalarProperties[i].ColumnName!;
                    try { ordinals[i] = reader.GetOrdinal(colName); }
                    catch { ordinals[i] = -1; }
                }

                var entity = (T)materializer.Materialize(reader, entityMap, ordinals);
                
                // Śledzimy jako Unchanged
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
            using var command = _sqlGenerator.GenerateSelectAll(GetConnection(), entityMap);
            
            using var reader = command.ExecuteReader();
            
            var materializer = new ObjectMaterializer(entityMap);
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
                    IDbCommand? cmd = null;

                    switch (entry.State)
                    {
                        case EntityState.Added:
                            cmd = _sqlGenerator.GenerateInsert(conn, map, entry.Entity);
                            break;
                        case EntityState.Modified:
                            cmd = _sqlGenerator.GenerateUpdate(conn, map, entry.Entity);
                            break;
                        case EntityState.Deleted:
                            cmd = _sqlGenerator.GenerateDelete(conn, map, entry.Entity);
                            break;
                    }

                    if (cmd != null)
                    {
                        cmd.Transaction = transaction;

                        if (entry.State == EntityState.Added && map.HasAutoIncrementKey)
                        {
                            // Dla Insert z AutoIncrement musimy pobrać ID
                            // SqliteSqlGenerator dodaje "; SELECT last_insert_rowid();" na końcu
                            var newId = cmd.ExecuteScalar();
                            
                            // Konwersja typu ID (zazwyczaj long w SQLite) na typ w C#
                            var targetType = map.KeyProperty.PropertyType;
                            var convertedId = Convert.ChangeType(newId, targetType);
                            
                            // Refleksja - ustawienie ID w obiekcie
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