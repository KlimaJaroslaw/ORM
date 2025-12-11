namespace ORM_v1.core;

public class EntityEntry
{
    public object Entity { get; }
    public EntityState State { get; set; }

    public EntityEntry(object entity, EntityState state)
    {
        Entity = entity ?? throw new ArgumentNullException(nameof(entity));
        State = state;
    } 
}