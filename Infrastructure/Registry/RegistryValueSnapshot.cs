using System;
using Microsoft.Win32;

namespace WpfApp1.Infrastructure.Registry
{
    public sealed class RegistryValueSnapshot
    {
        public bool Exists { get; set; }
        public RegistryValueKind Kind { get; set; }
        public object Value { get; set; }

        public RegistryValueSnapshot Clone()
        {
            return new RegistryValueSnapshot
            {
                Exists = Exists,
                Kind = Kind,
                Value = CloneValue(Value)
            };
        }

        private static object CloneValue(object value)
        {
            if (value is byte[] bytes) return (byte[])bytes.Clone();
            if (value is string[] strings) return (string[])strings.Clone();
            return value;
        }
    }
}
