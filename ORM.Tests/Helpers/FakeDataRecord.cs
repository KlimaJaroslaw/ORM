using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ORM.Tests.Helpers
{
    public class FakeDataRecord : IDataRecord
    {
        private readonly Dictionary<string, object?> _values;

        public FakeDataRecord(Dictionary<string, object?> values)
        {
            _values = values;
        }

        public object this[string name] => _values[name] ?? DBNull.Value;
        public object this[int i] => throw new NotImplementedException();

        public int FieldCount => _values.Count;

        public string GetName(int i) => _values.Keys.ElementAt(i);

        public string GetDataTypeName(int i) =>
            _values.ElementAt(i).Value?.GetType()?.Name ?? "null";

        public Type GetFieldType(int i) =>
            _values.ElementAt(i).Value?.GetType() ?? typeof(object);

        public object GetValue(int i) => _values.ElementAt(i).Value ?? DBNull.Value;

        public int GetValues(object[] values) => throw new NotImplementedException();

        public int GetOrdinal(string name) =>
            _values.Keys.ToList().IndexOf(name);

        public bool GetBoolean(int i) => (bool)GetValue(i)!;
        public byte GetByte(int i) => (byte)GetValue(i)!;
        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) =>
            throw new NotImplementedException();
        public char GetChar(int i) => (char)GetValue(i)!;
        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) =>
            throw new NotImplementedException();
        public IDataReader GetData(int i) => throw new NotImplementedException();
        public DateTime GetDateTime(int i) => (DateTime)GetValue(i)!;
        public decimal GetDecimal(int i) => (decimal)GetValue(i)!;
        public double GetDouble(int i) => (double)GetValue(i)!;
        public float GetFloat(int i) => (float)GetValue(i)!;
        public Guid GetGuid(int i) => (Guid)GetValue(i)!;
        public short GetInt16(int i) => (short)GetValue(i)!;
        public int GetInt32(int i) => (int)GetValue(i)!;
        public long GetInt64(int i) => (long)GetValue(i)!;
        public string GetString(int i) => (string)GetValue(i)!;

        public bool IsDBNull(int i) => GetValue(i) is null || GetValue(i) is DBNull;
    }
}
