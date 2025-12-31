using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Net.Http.Headers;
using System.Text;

namespace ORM_v1.Query;

public class SqlExpressionVisitor : ExpressionVisitor
{
    private readonly StringBuilder _sql = new();
    private readonly List<object?> _parameters = new();
    public string Sql => _sql.ToString();
    public IReadOnlyList<object?> Parameters => _parameters;

    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (node.Value is IQueryable)
        {
            _sql.Append("SELECT * FROM ");
        }
        else
        {
            var param = $"@p{_parameters.Count}";
            _sql.Append(param);
            _parameters.Add(node.Value);
        }

        return node;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.Name == "Where")
        {
            Visit(node.Arguments[0]);
            _sql.Append(" WHERE ");
            Visit(node.Arguments[1]);
            return node;
        }

        if (node.Method.Name == "OrderBy")
        {
            Visit(node.Arguments[0]);
            _sql.Append(" ORDER BY ");
            Visit(node.Arguments[1]);
            return node;
        }

        if (node.Method.Name == "Take")
        {
            Visit(node.Arguments[0]);
            _sql.Append(" LIMIT ");
            Visit(node.Arguments[1]);
            return node;
        }

        if (node.Method.Name == "Skip")
        {
            Visit(node.Arguments[0]);
            _sql.Append(" OFFSET ");
            Visit(node.Arguments[1]);
            return node;
        }
        return base.VisitMethodCall(node);
    }

    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        Visit(node.Body);
        return node;
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        Visit(node.Left);
        _sql.Append(node.NodeType switch
        {
            ExpressionType.Equal => " = ",
            ExpressionType.GreaterThan => " > ",
            ExpressionType.LessThan => " < ",
            _ => throw new NotSupportedException()
        });
        Visit(node.Right);
        return node;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        _sql.Append(node.Member.Name);
        return node;
    }
}