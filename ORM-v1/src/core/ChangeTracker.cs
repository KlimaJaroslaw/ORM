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
}