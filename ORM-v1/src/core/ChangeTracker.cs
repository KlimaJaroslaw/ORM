namespace ORM_v1.core;

public class ChangeTracker
{
    private readonly Dictionary<object, EntityEntry> _entries = new();

    public IEnumerable<EntityEntry> Entries => _entries.Values;

    public void Track(object entity, EntityState state)
    {
        if (_entries.TryGetValue(entity, out var entry))
        {
            if (state == EntityState.Detached)
                _entries.Remove(entity);
            else
                entry.State = state;
        }
        else
        {
            _entries[entity] = new EntityEntry(entity, state);
        }
    }

    public EntityEntry? GetEntry(object entity)
    {
        _entries.TryGetValue(entity, out var entry);
        return entry;
    }

    public bool HasChanges()
    {
        return _entries.Values.Any(e => e.State != EntityState.Unchanged && e.State != EntityState.Detached);
    }

    public void AcceptAllChanges()
    {
        var deleted = _entries.Values.Where(e => e.State == EntityState.Deleted).Select(e => e.Entity).ToList();
        foreach (var d in deleted) _entries.Remove(d);

        foreach (var entry in _entries.Values)
        {
            entry.State = EntityState.Unchanged;
        }
    }

    /// <summary>
    /// Sprawdza czy encja jest już śledzona (po referencji).
    /// </summary>
    public bool IsTracked(object entity)
    {
        return _entries.ContainsKey(entity);
    }

    /// <summary>
    /// Pobiera śledzoną encję po kluczu głównym lub null jeśli nie znaleziono.
    /// Używane dla Identity Map pattern w Include().
    ///   POPRAWKA TPC: Sprawdza DOKŁADNY typ encji (nie używa IsAssignableFrom).
    /// Dla TPC Student (ID=1) i StudentPart (ID=1) to RÓŻNE encje!
    /// </summary>
    public object? FindTracked(Type entityType, object keyValue)
    {
        foreach (var entry in _entries.Values)
        {
            //   ZMIANA: Porównaj DOKŁADNY typ encji
            // Dla TPC: Student != StudentPart (nawet jeśli mają ten sam ID!)
            if (entry.Entity.GetType() == entityType)  // Porównaj dokładny typ, NIE IsAssignableFrom
            {
                //   POPRAWKA: Znajdź property z atrybutem [Key]
                var keyProp = entityType.GetProperties()
                    .FirstOrDefault(p => p.GetCustomAttributes(typeof(ORM_v1.Attributes.KeyAttribute), true).Any());

                if (keyProp != null)
                {
                    var trackedKeyValue = keyProp.GetValue(entry.Entity);
                    if (trackedKeyValue != null && trackedKeyValue.Equals(keyValue))
                    {
                        return entry.Entity;
                    }
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Generyczna wersja FindTracked - zwraca poprawny typ.
    /// </summary>
    public T? FindTracked<T>(object keyValue) where T : class
    {
        return FindTracked(typeof(T), keyValue) as T;
    }
}