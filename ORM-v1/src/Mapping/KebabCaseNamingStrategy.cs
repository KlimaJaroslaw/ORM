using System;
using System.Text.RegularExpressions;

namespace ORM_v1.Mapping
{
    public sealed class KebabCaseNamingStrategy : INamingStrategy
    {
        private static readonly Regex _regex = new Regex("([a-z0-9])([A-Z])", RegexOptions.Compiled);

        public string ConvertName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be null or whitespace.", nameof(name));

            string withDashes = _regex.Replace(name, "$1-$2");

            return withDashes.ToLowerInvariant();
        }
    }
}