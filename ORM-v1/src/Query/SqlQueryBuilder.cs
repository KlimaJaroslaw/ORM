using System.Text;

namespace ORM_v1.Query
{
    public class SqlQueryBuilder
    {
        private readonly StringBuilder _builder = new();      
        
        public SqlQueryBuilder Select(IEnumerable<string> columns)
        {
            _builder.Append("SELECT ");
            _builder.Append(string.Join(", ", columns));
            return this;
        }

        public SqlQueryBuilder From(string table)
        {
            _builder.Append($" FROM {table}");
            return this;
        }

        public SqlQueryBuilder Where(string condition)
        {
            _builder.Append($" WHERE {condition}");
            return this;
        }

        public SqlQueryBuilder InsertInto(string table, IEnumerable<string> columns)
        {
            _builder.Append($"INSERT INTO {table} (");
            _builder.Append(string.Join(", ", columns));
            _builder.Append(") VALUES (");
            return this;
        }

        public SqlQueryBuilder Values(IEnumerable<string> paramNames)
        {
            _builder.Append(string.Join(", ", paramNames));
            _builder.Append(')');
            return this;
        }

        public SqlQueryBuilder Update(string table)
        {
            _builder.Append($"UPDATE {table} SET ");
            return this;
        }

        public SqlQueryBuilder Set(IEnumerable<string> assignments)
        {
            _builder.Append(string.Join(", ", assignments));
            return this;
        }

        public SqlQueryBuilder DeleteFrom(string table)
        {
            _builder.Append($"DELETE FROM {table}");
            return this;
        }

        public SqlQueryBuilder SelectDistinct(IEnumerable<string> columns)
        {
            _builder.Append("SELECT DISTINCT ");
            _builder.Append(string.Join(", ", columns));
            return this;
        }

        public SqlQueryBuilder FromWithAlias(string table, string? alias)
        {
            _builder.Append($" FROM {table}");
            if (!string.IsNullOrEmpty(alias))
            {
                _builder.Append($" AS {alias}");
            }
            return this;
        }

        public SqlQueryBuilder InnerJoin(string table, string? alias, string onCondition)
        {
            _builder.Append($" INNER JOIN {table}");
            if (!string.IsNullOrEmpty(alias))
            {
                _builder.Append($" AS {alias}");
            }
            _builder.Append($" ON {onCondition}");
            return this;
        }

        public SqlQueryBuilder LeftJoin(string table, string? alias, string onCondition)
        {
            _builder.Append($" LEFT JOIN {table}");
            if (!string.IsNullOrEmpty(alias))
            {
                _builder.Append($" AS {alias}");
            }
            _builder.Append($" ON {onCondition}");
            return this;
        }

        public SqlQueryBuilder RightJoin(string table, string? alias, string onCondition)
        {
            _builder.Append($" RIGHT JOIN {table}");
            if (!string.IsNullOrEmpty(alias))
            {
                _builder.Append($" AS {alias}");
            }
            _builder.Append($" ON {onCondition}");
            return this;
        }

        public SqlQueryBuilder FullOuterJoin(string table, string? alias, string onCondition)
        {
            _builder.Append($" FULL OUTER JOIN {table}");
            if (!string.IsNullOrEmpty(alias))
            {
                _builder.Append($" AS {alias}");
            }
            _builder.Append($" ON {onCondition}");
            return this;
        }

        public SqlQueryBuilder GroupBy(IEnumerable<string> columns)
        {
            if (columns?.Any() == true)
            {
                _builder.Append(" GROUP BY ");
                _builder.Append(string.Join(", ", columns));
            }
            return this;
        }

        public SqlQueryBuilder Having(string condition)
        {
            _builder.Append($" HAVING {condition}");
            return this;
        }

        public SqlQueryBuilder OrderBy(IEnumerable<string> orderClauses)
        {
            if (orderClauses?.Any() == true)
            {
                _builder.Append(" ORDER BY ");
                _builder.Append(string.Join(", ", orderClauses));
            }
            return this;
        }

        public SqlQueryBuilder Limit(int limit)
        {
            _builder.Append($" LIMIT {limit}");
            return this;
        }

        public SqlQueryBuilder Offset(int offset)
        {
            _builder.Append($" OFFSET {offset}");
            return this;
        }

        public override string ToString() => _builder.ToString();
    }
}
