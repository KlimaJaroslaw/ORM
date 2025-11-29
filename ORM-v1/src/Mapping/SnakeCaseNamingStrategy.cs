using System;
using System.Text.RegularExpressions;

namespace ORM_v1.Mapping
{
    public sealed class SnakeCaseNamingStrategy : INamingStrategy
    {
        private static readonly Regex _regex = new Regex("([a-z0-9])([A-Z])", RegexOptions.Compiled);

        public string ConvertName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be null or whitespace.", nameof(name));

            string withUnderscores = _regex.Replace(name, "$1_$2");
            return withUnderscores.ToLowerInvariant();
        }
    }
}