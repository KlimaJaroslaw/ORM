using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ORM_v1.core;

namespace ORM_v1.Query;

/// <summary>
/// Wrapper dla IQueryable który przechowuje informacje o navigation properties do załadowania.
/// Implementuje wzorzec Decorator.
/// </summary>
public class IncludableQueryable<TEntity, TProperty> : IQueryable<TEntity> where TEntity : class
{
    private readonly IQueryable<TEntity> _source;
    internal readonly List<IncludeInfo> Includes;
    internal readonly DbContext Context;
    internal readonly Expression<Func<TEntity, bool>>? Predicate;

    internal IncludableQueryable(
        IQueryable<TEntity> source,
        DbContext context,
        List<IncludeInfo> includes,
        Expression<Func<TEntity, bool>>? predicate = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Includes = includes ?? new List<IncludeInfo>();
        Predicate = predicate;
    }

    public Type ElementType => _source.ElementType;
    public Expression Expression => _source.Expression;
    public IQueryProvider Provider => _source.Provider;

    public IEnumerator<TEntity> GetEnumerator() => _source.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
