using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using ORM_v1.Mapping;

namespace ORM_v1.Query;

/// <summary>
/// Konwertuje Expression<Func<T, bool>> na SQL WHERE clause.
/// Obsługuje podstawowe operatory: ==, !=, <, >, <=, >=, &&, ||, Contains, StartsWith, EndsWith.
/// </summary>
internal class ExpressionToSqlConverter
{
    private readonly EntityMap _entityMap;
    private readonly Dictionary<string, object> _parameters = new();
    private int _parameterIndex = 0;

    public ExpressionToSqlConverter(EntityMap entityMap)
    {
        _entityMap = entityMap ?? throw new ArgumentNullException(nameof(entityMap));
    }

    public (string WhereClause, Dictionary<string, object> Parameters) Translate<T>(Expression<Func<T, bool>> predicate)
    {
        var body = predicate.Body;

        // Usuń Convert() node jeśli istnieje (np. x => (object)x.Id == 5)
        if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
        {
            body = unary.Operand;
        }

        var sql = Visit(body);
        return (sql, _parameters);
    }

    private string Visit(Expression node)
    {
        return node switch
        {
            BinaryExpression binary => VisitBinary(binary),
            MemberExpression member => VisitMember(member),
            ConstantExpression constant => VisitConstant(constant),
            MethodCallExpression methodCall => VisitMethodCall(methodCall),
            UnaryExpression unary => VisitUnary(unary),
            _ => throw new NotSupportedException($"Expression type {node.NodeType} is not supported in WHERE clause")
        };
    }

    private string VisitBinary(BinaryExpression node)
    {
        var left = Visit(node.Left);
        var right = Visit(node.Right);

        var op = node.NodeType switch
        {
            ExpressionType.Equal => "=",
            ExpressionType.NotEqual => "!=",
            ExpressionType.GreaterThan => ">",
            ExpressionType.GreaterThanOrEqual => ">=",
            ExpressionType.LessThan => "<",
            ExpressionType.LessThanOrEqual => "<=",
            ExpressionType.AndAlso => "AND",
            ExpressionType.OrElse => "OR",
            _ => throw new NotSupportedException($"Binary operator {node.NodeType} not supported")
        };

        // Dla AND/OR dodaj nawiasy
        if (op == "AND" || op == "OR")
        {
            return $"({left} {op} {right})";
        }

        return $"{left} {op} {right}";
    }

    private string VisitMember(MemberExpression node)
    {
        // x.PropertyName → "ColumnName"
        var propertyName = node.Member.Name;

        // Znajdź PropertyMap dla tej property
        var propertyMap = _entityMap.ScalarProperties
            .FirstOrDefault(p => p.PropertyInfo.Name == propertyName);

        if (propertyMap == null)
        {
            // Może to jest navigation property lub klucz
            propertyMap = _entityMap.KeyProperty.PropertyInfo.Name == propertyName
                ? _entityMap.KeyProperty
                : null;
        }

        if (propertyMap == null)
        {
            throw new InvalidOperationException($"Property '{propertyName}' not found in entity map for {_entityMap.EntityType.Name}");
        }

        return $"\"{propertyMap.ColumnName}\"";
    }

    private string VisitConstant(ConstantExpression node)
    {
        // Wartości stałe zamieniamy na parametry SQL aby uniknąć SQL injection
        var paramName = $"@p{_parameterIndex++}";
        _parameters[paramName] = node.Value ?? DBNull.Value;
        return paramName;
    }

    private string VisitMethodCall(MethodCallExpression node)
    {
        // String methods: Contains, StartsWith, EndsWith
        if (node.Object != null && node.Object.Type == typeof(string))
        {
            var property = Visit(node.Object);

            if (node.Arguments.Count == 1)
            {
                var argument = node.Arguments[0];
                var value = GetConstantValue(argument);

                switch (node.Method.Name)
                {
                    case "Contains":
                        var containsParam = $"@p{_parameterIndex++}";
                        _parameters[containsParam] = $"%{value}%";
                        return $"{property} LIKE {containsParam}";

                    case "StartsWith":
                        var startsParam = $"@p{_parameterIndex++}";
                        _parameters[startsParam] = $"{value}%";
                        return $"{property} LIKE {startsParam}";

                    case "EndsWith":
                        var endsParam = $"@p{_parameterIndex++}";
                        _parameters[endsParam] = $"%{value}";
                        return $"{property} LIKE {endsParam}";
                }
            }
        }

        throw new NotSupportedException($"Method call '{node.Method.Name}' not supported in WHERE clause");
    }

    private string VisitUnary(UnaryExpression node)
    {
        if (node.NodeType == ExpressionType.Not)
        {
            return $"NOT ({Visit(node.Operand)})";
        }

        if (node.NodeType == ExpressionType.Convert)
        {
            return Visit(node.Operand);
        }

        throw new NotSupportedException($"Unary operator {node.NodeType} not supported");
    }

    private object? GetConstantValue(Expression node)
    {
        if (node is ConstantExpression constant)
        {
            return constant.Value;
        }

        // Dla bardziej złożonych wyrażeń (np. zmienne lokalne) kompilujemy i wykonujemy
        var lambda = Expression.Lambda<Func<object>>(Expression.Convert(node, typeof(object)));
        var compiled = lambda.Compile();
        return compiled();
    }
}
