using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ORM_v1.core;

namespace ORM_v1.Query;

/// <summary>
/// Wrapper dla IQueryable z filtrowaniem (WHERE clause).
/// </summary>
public class FilterableQueryable<TEntity> : IQueryable<TEntity> where TEntity : class
{
    private readonly IQueryable<TEntity> _source;
    // internal readonly DbContext Context;
    public DbContext Context { get; }
    internal readonly Expression<Func<TEntity, bool>> Predicate;

    internal FilterableQueryable(
        IQueryable<TEntity> source,
        DbContext context,
        Expression<Func<TEntity, bool>> predicate)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }

    public Type ElementType => _source.ElementType;
    public Expression Expression => _source.Expression;
    public IQueryProvider Provider => _source.Provider;

    public IEnumerator<TEntity> GetEnumerator() => _source.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
