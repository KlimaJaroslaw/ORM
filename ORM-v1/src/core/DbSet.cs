using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace ORM_v1.core;

public class DbSet<T> : IQueryable<T> where T : class
{
    private readonly DbContext _context;
    private readonly Expression _expression;
    private readonly IQueryProvider _provider;

    public DbSet(DbContext context)
    {
        _context = context;
        _provider = new Query.QueryProvider(context);
        _expression = Expression.Constant(this);
    }

    public DbSet(IQueryProvider provider, Expression expression)
    {
        _provider = provider;
        _expression = expression;
    }

    public Type ElementType => typeof(T);
    public Expression Expression => _expression;
    public IQueryProvider Provider => _provider;

    public IEnumerator<T> GetEnumerator()
    {
        return Provider.Execute<IEnumerable<T>>(Expression).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void EnsureContext()
    {
        if (_context == null)
            throw new InvalidOperationException(
                "This DbSet instance is query-only and cannot be used for tracking operations");
    }

    public void Add(T entity)
    {
        EnsureContext();
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


    public T? Find(object id)
    {
        return _context.Find<T>(id);
    }

    public IEnumerable<T> All()
    {
        return _context.SetInternal<T>();
    }
}