using System;
using System.Linq;
using System.Linq.Expressions;

namespace ORM_v1.Query;

public class SqlExpressionVisitor : ExpressionVisitor
{
    private readonly SqlQueryBuilder _builder = new();

    public string Sql => _builder.ToString();

    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (node.Value is IQueryable q)
        {
            _builder.Select(new[] { "*" })
                    .From(q.ElementType.Name);
        }
        return node;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.Name == "Where")
        {
            Visit(node.Arguments[0]);

            var lambda = (LambdaExpression)((UnaryExpression)node.Arguments[1]).Operand;
            var condition = ParseCondition(lambda.Body);

            _builder.Where(condition);
            return node;
        }

        if (node.Method.Name == "OrderBy")
        {
            Visit(node.Arguments[0]);

            var lambda = (LambdaExpression)((UnaryExpression)node.Arguments[1]).Operand;
            var member = (MemberExpression)lambda.Body;

            _builder.OrderBy(new[] { member.Member.Name });
            return node;
        }

        if (node.Method.Name == "Take")
        {
            Visit(node.Arguments[0]);
            var constant = (ConstantExpression)node.Arguments[1];

            _builder.Limit((int)constant.Value!);
            return node;
        }

        if (node.Method.Name == "Skip")
        {
            Visit(node.Arguments[0]);
            var constant = (ConstantExpression)node.Arguments[1];

            _builder.Offset((int)constant.Value!);
            return node;
        }

        return base.VisitMethodCall(node);
    }

    private string ParseCondition(Expression expr)
    {
        if (expr is BinaryExpression bin)
        {
            var left = ParseCondition(bin.Left);
            var right = ParseCondition(bin.Right);

            var op = bin.NodeType switch
            {
                ExpressionType.Equal => "=",
                ExpressionType.GreaterThan => ">",
                ExpressionType.LessThan => "<",
                _ => throw new NotSupportedException()
            };

            return $"{left} {op} {right}";
        }

        if (expr is MemberExpression member)
            return member.Member.Name;

        if (expr is ConstantExpression constant)
            return constant.Value?.ToString() ?? "NULL";

        throw new NotSupportedException(expr.NodeType.ToString());
    }
}
