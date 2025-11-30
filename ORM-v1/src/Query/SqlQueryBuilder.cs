using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ORM_v1.src.Query
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

        public override string ToString() => _builder.ToString();
    }
}
