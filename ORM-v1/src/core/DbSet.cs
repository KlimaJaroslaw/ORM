using System;
using System.Collections.Generic;

namespace ORM_v1.core;

public class DbSet<T> where T : class
{
    private readonly DbContext _context;

    public DbSet(DbContext context)
    {
        _context = context;
    }

    public void Add(T entity)
    {
        _context.ChangeTracker.Track(entity, EntityState.Added);
    }

    public void Update(T entity)
    {
        _context.ChangeTracker.Track(entity, EntityState.Modified);
    }

    public void Remove(T entity)
    {
        _context.ChangeTracker.Track(entity, EntityState.Deleted);
    }
        
    // Proste Find po ID
    public T? Find(object id)
    {
        return _context.Find<T>(id);
    }
        
    // Metoda pomocnicza do pobierania wszystkich (dla testów, póki nie ma LINQ)
    public IEnumerable<T> All()
    {
        return _context.SetInternal<T>(); 
    }
}