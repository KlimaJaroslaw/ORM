using System;
using System.Linq;
using System.Linq.Expressions;
using ORM_v1.core;

namespace ORM_v1.Query;

public class QueryProvider : IQueryProvider
{
    private readonly DbContext _context;

    public QueryProvider(DbContext context)
    {
        _context = context;
    }

    public IQueryable CreateQuery(Expression expression)
    {
        var elementType = expression.Type.GetGenericArguments()[0];
        var dbSetType = typeof(DbSet<>).MakeGenericType(elementType);
        return (IQueryable)Activator.CreateInstance(dbSetType, this,  _context)!;
    }
    
    IQueryable<TElement> IQueryProvider.CreateQuery<TElement>(Expression expression)
    {
        var dbSetType = typeof(DbSet<>).MakeGenericType(typeof(TElement));
        return (IQueryable<TElement>)Activator.CreateInstance(dbSetType, this, expression)!;
    }
    public object Execute(Expression expression)
    {
        return Execute<object>(expression);
    }

    public TResult Execute<TResult>(Expression expression)
    {
        var visitor = new SqlExpressionVisitor();
        visitor.Visit(expression);

        // TODO
        // var sql = visitor.Sql;
        // var parameters = visitor.Parameters;
        
        // return _context.ExecuteQuery<TResult>(sql, parameters);
        throw new NotImplementedException("Linq execution pipeline not implemented yet");
    }
}